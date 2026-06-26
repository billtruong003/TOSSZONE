using BillGameCore;
using TossZone.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace TossZone.Throwing
{
    /// <summary>
    /// Local throw input + hand state machine (see <c>Docs/Throw_Mechanic_Spec.md</c>). One throwing hand
    /// (right by default). Press grab → a ball loads in the hand; HOLD grab and the throw fires on the SWING
    /// gesture — the hand must wind BACK behind a body-level plane (Armed) then sweep FORWARD across it above a
    /// speed threshold (FIRE). On fire it spawns a pooled <see cref="ThrowProjectile"/> in the swing direction,
    /// punches haptics, fires <see cref="BallThrownEvent"/>, and (still holding grab) auto-refills after a
    /// cooldown for continuous throwing. Release grab = cancel.
    ///
    /// Grab/swing are read with no AutoHand coupling: grip from XR <see cref="InputDevices"/>, swing velocity
    /// from the wrist transform delta. A <b>debug throw key (T)</b> fires straight ahead so the projectile +
    /// juice can be validated even before the gesture is dialed in. Place on any scene object; it finds the
    /// local <see cref="PlayerRig"/> at runtime.
    /// </summary>
    public class ThrowController : MonoBehaviour
    {
        private enum ThrowState { Empty, Loaded, Armed }

        [Header("Config + projectile")]
        [SerializeField] private ThrowConfig _config;
        [Tooltip("Pooled flying-ball prefab (ThrowProjectile + TrailRenderer).")]
        [SerializeField] private GameObject _projectilePrefab;
        [Tooltip("Held-ball visual prefab (parented into the hand) — tune this one. Empty = runtime sphere fallback.")]
        [SerializeField] private GameObject _heldBallPrefab;
        [SerializeField] private bool _rightHand = true;
        [Tooltip("Editor/dev: this key fires a throw straight ahead from the head (validate juice without XR swing).")]
        [SerializeField] private Key _debugThrowKey = Key.T;
        [Tooltip("Editor/dev: hold this key to simulate grip when no XR controller is present.")]
        [SerializeField] private Key _editorGripKey = Key.G;

        private const string PoolKey = "throwprojectile";

        private PlayerRig _rig;
        private Transform _wrist, _head, _root, _heldBall;
        private float _heldBaseScale = 1f;
        private System.Action _onRefillCb;                  // cached → no per-throw delegate alloc
        private System.Action<BallLandedEvent> _onBallLandedCb;
        private Vector3 _lastWristPos;
        private bool _hasLastPos;
        private float _prevFwdDist;
        private bool _hasPrevDist;
        private ThrowState _state;
        private bool _onCooldown;
        private bool _ready;

        private void OnDisable()
        {
            if (_ready && Bill.IsReady && _onBallLandedCb != null)
                Bill.Events.Unsubscribe<BallLandedEvent>(_onBallLandedCb);
        }

        private void Update()
        {
            if (!_ready)
            {
                TryInit();
                return;
            }
            if (_wrist == null || PlayerRig.Local == null) // rig lost — re-resolve next frames
            {
                _ready = false;
                return;
            }
            Tick();
        }

        private void TryInit()
        {
            if (!Bill.IsReady || _config == null) return;
            _rig = PlayerRig.Local;
            if (_rig == null || _rig.WristR == null || _rig.Head == null) return;

            _wrist = _rightHand ? _rig.WristR : _rig.WristL;
            _head = _rig.Head;
            _root = _rig.Root;
            if (_wrist == null) return;

            if (_projectilePrefab != null) Bill.Pool.Register(PoolKey, _projectilePrefab, 8);
            CreateHeldBall();
            _onRefillCb = OnRefill;
            _onBallLandedCb = OnBallLanded;
            Bill.Events.Subscribe<BallLandedEvent>(_onBallLandedCb);

            _hasLastPos = false;
            _hasPrevDist = false;
            _state = ThrowState.Empty;
            _ready = true;
            Debug.Log("[Throw] ThrowController ready (hand=" + (_rightHand ? "R" : "L") + "). Debug throw key = " + _debugThrowKey);
        }

        private void Tick()
        {
            float dt = Time.deltaTime;
            Vector3 wp = _wrist.position;
            Vector3 wvel = (_hasLastPos && dt > 1e-5f) ? (wp - _lastWristPos) / dt : Vector3.zero;
            _lastWristPos = wp;
            _hasLastPos = true;

            if (DebugKeyPressed())
            {
                DebugThrow();
                return;
            }

            bool grip = ReadGrip();

            Vector3 chest = new Vector3(_head.position.x, _root.position.y + _config.planeHeight, _head.position.z);
            Vector3 fwd = FlatForward();
            float fwdDist = Vector3.Dot(wp - chest, fwd);
            float fwdVel = Vector3.Dot(wvel, fwd);

            switch (_state)
            {
                case ThrowState.Empty:
                    if (grip) Load();
                    break;

                case ThrowState.Loaded:
                    if (!grip) { Cancel(); break; }
                    if (fwdDist < -_config.windBackDepth) { _state = ThrowState.Armed; PulseHeld(); }
                    break;

                case ThrowState.Armed:
                    if (!grip) { Cancel(); break; }
                    bool crossedForward = _hasPrevDist && _prevFwdDist < 0f && fwdDist >= 0f;
                    if (!_onCooldown && crossedForward && fwdVel > _config.vMinFire)
                        Fire(wp, wvel);
                    break;
            }

            _prevFwdDist = fwdDist;
            _hasPrevDist = true;
        }

        private void Load()
        {
            _state = ThrowState.Loaded;
            ShowHeld(true);
        }

        private void Cancel()
        {
            _state = ThrowState.Empty;
            ShowHeld(false);
        }

        private void Fire(Vector3 origin, Vector3 wvel)
        {
            // Ballistic: launch with the REAL hand velocity (no aim cone) → goes exactly where you threw.
            Vector3 dir = wvel.sqrMagnitude > 1e-4f ? wvel.normalized : FlatForward();
            float speed = Mathf.Clamp(wvel.magnitude * _config.velocityScale, _config.minLaunchSpeed, _config.maxLaunchSpeed);
            Vector3 v0 = dir * speed;
            float power = Mathf.Clamp01(speed / Mathf.Max(_config.maxLaunchSpeed, 0.01f));
            SpawnProjectile(origin, v0, power);

            Haptic(_config.hapticRelease, 0.06f);
            if (!string.IsNullOrEmpty(_config.throwSfx)) Bill.Audio.Play(_config.throwSfx);
            Bill.Events.Fire(new BallThrownEvent { Origin = origin, Direction = dir, Power = power });

            ShowHeld(false);
            _onCooldown = true;
            _state = ThrowState.Loaded;
            Bill.Timer.Delay(_config.cooldown, _onRefillCb);
        }

        private void OnRefill()
        {
            _onCooldown = false;
            if (ReadGrip()) ShowHeld(true);
            else Cancel();
        }

        private void DebugThrow()
        {
            // Throw straight where the head is LOOKING, at a moderate speed (aim with your gaze for the dev test).
            Vector3 origin = _head.position + _head.forward * 0.25f;
            Vector3 v0 = _head.forward * (_config.maxLaunchSpeed * 0.6f);
            SpawnProjectile(origin, v0, 0.6f);
            Haptic(_config.hapticRelease, 0.06f);
            if (!string.IsNullOrEmpty(_config.throwSfx)) Bill.Audio.Play(_config.throwSfx);
            Bill.Events.Fire(new BallThrownEvent { Origin = origin, Direction = _head.forward, Power = 0.6f });
            Debug.Log("[Throw] DEBUG throw fired.");
        }

        private void SpawnProjectile(Vector3 pos, Vector3 velocity, float power)
        {
            if (_projectilePrefab == null) return;
            Quaternion rot = velocity.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
            GameObject go = Bill.Pool.Spawn(PoolKey, pos, rot);
            if (go == null) return;
            ThrowProjectile proj = go.GetComponent<ThrowProjectile>();
            if (proj != null) proj.Launch(pos, velocity, power, _config);
        }

        private void OnBallLanded(BallLandedEvent e) => Haptic(_config.hapticImpact, 0.05f);

        // ── helpers ──────────────────────────────────────────────────────────────

        private Vector3 FlatForward()
        {
            Vector3 f = _head.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        private void CreateHeldBall()
        {
            if (_heldBall != null) return;
            GameObject ball;
            if (_heldBallPrefab != null)
            {
                ball = Instantiate(_heldBallPrefab);                 // prefab path — keep its own scale/material (you tune it)
                ball.name = "HeldBall(throw)";
                _heldBaseScale = ball.transform.localScale.x;
            }
            else
            {
                ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // fallback so it works with no prefab wired
                ball.name = "HeldBall(throw, runtime)";
                Collider col = ball.GetComponent<Collider>();
                if (col != null) Destroy(col);
                Renderer r = ball.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = ThrowProjectile.BallMaterial(_config.ballColor);
                _heldBaseScale = _config.heldBallScale;
                ball.transform.localScale = Vector3.one * _heldBaseScale;
            }
            _heldBall = ball.transform;
            _heldBall.SetParent(_wrist, false);
            _heldBall.localPosition = Vector3.zero;
            ball.SetActive(false);
        }

        private void ShowHeld(bool on)
        {
            if (_heldBall == null) return;
            if (on)
            {
                BillTween.KillTarget(_heldBall);
                _heldBall.localScale = Vector3.one * _heldBaseScale;
            }
            _heldBall.gameObject.SetActive(on);
        }

        private void PulseHeld()
        {
            if (_heldBall == null) return;
            BillTween.Scale(_heldBall, _heldBaseScale * 1.3f, 0.12f)
                ?.SetEase(EaseType.OutQuad).SetLoops(1, LoopType.Yoyo).SetTarget(_heldBall);
        }

        private bool ReadGrip()
        {
            UnityEngine.XR.InputDevice dev = InputDevices.GetDeviceAtXRNode(_rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float v) && v > 0.6f) return true;
            Keyboard kb = Keyboard.current;
            return kb != null && kb[_editorGripKey].isPressed;
        }

        private bool DebugKeyPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb[_debugThrowKey].wasPressedThisFrame;
        }

        private void Haptic(float amplitude, float duration)
        {
            if (amplitude <= 0f) return;
            UnityEngine.XR.InputDevice dev = InputDevices.GetDeviceAtXRNode(_rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid && dev.TryGetHapticCapabilities(out UnityEngine.XR.HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
