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
    /// gesture — a trigger plane sits a short distance IN FRONT of you; the hand pulls back behind it (Armed) then
    /// pushes/sweeps FORWARD through the plane above a speed threshold (FIRE). No reaching behind the head. On fire
    /// it spawns a pooled <see cref="ThrowProjectile"/> in the swing direction,
    /// punches haptics, fires <see cref="BallThrownEvent"/>, and (still holding grab) auto-refills after a
    /// cooldown for continuous throwing. Release grab = cancel.
    ///
    /// Grab/swing are read with no AutoHand coupling: grip from XR <see cref="InputDevices"/>, swing velocity
    /// from the wrist transform delta MINUS the rig-root delta (so joystick locomotion can't fake a throw).
    /// A <b>debug throw key (T)</b> fires straight ahead so the projectile +
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
        [Tooltip("OFF when a ThrowBallHolder provides the real AutoHand grabbable as the in-hand visual (proper finger pose). ON = use the simple parented sphere here.")]
        [SerializeField] private bool _showVisualHeldBall = true;
        [SerializeField] private bool _rightHand = true;
        [Tooltip("Editor/dev: this key fires a throw straight ahead from the head (validate juice without XR swing).")]
        [SerializeField] private Key _debugThrowKey = Key.T;
        [Tooltip("Editor/dev: hold this key to simulate grip when no XR controller is present.")]
        [SerializeField] private Key _editorGripKey = Key.G;
        [Tooltip("In-headset HUD with the live throw state + swing speed (debug/tuning). Turn off when dialed in.")]
        [SerializeField] private bool _debugHud = true;
        [Tooltip("DebugHud: a PRE-PLACED instance in the scene (used as-is) OR the DebugHud prefab (auto-instantiated + attached to the head). Empty = no HUD.")]
        [SerializeField] private TossZone.UI.DebugHud _hudRef;

        private const string PoolKey = "throwprojectile";

        private PlayerRig _rig;
        private Transform _wrist, _head, _root, _heldBall;
        private float _heldBaseScale = 1f;
        private System.Action _onRefillCb;                  // cached → no per-throw delegate alloc
        private System.Action<BallLandedEvent> _onBallLandedCb;
        private Vector3 _lastWristPos;
        private Vector3 _lastRootPos;
        private bool _hasLastPos;
        private float _peakFwdVel;          // peak forward swing speed since the last wind-up / fire
        private Vector3 _peakArmVel;        // smoothed hand velocity at that peak → the launch velocity
        private const int VelSamples = 4;
        private Vector3[] _velBuf;          // moving-average ring buffer → kills 1-frame tracking jitter
        private int _velCount;
        private TossZone.UI.DebugHud _hud;
        private float _lastLaunchSpeed;
        private int _fireCount;
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
            _velBuf = new Vector3[VelSamples];
            _velCount = 0;
            _peakFwdVel = 0f;
            if (_debugHud) CreateHud();
            _state = ThrowState.Empty;
            _ready = true;
            Debug.Log("[Throw] ThrowController ready (hand=" + (_rightHand ? "R" : "L") + "). Debug throw key = " + _debugThrowKey);
        }

        private void Tick()
        {
            float dt = Time.deltaTime;
            Vector3 wp = _wrist.position;
            Vector3 rp = _root.position;
            bool hadLast = _hasLastPos && dt > 1e-5f;
            Vector3 wvel = hadLast ? (wp - _lastWristPos) / dt : Vector3.zero;
            Vector3 rootVel = hadLast ? (rp - _lastRootPos) / dt : Vector3.zero;
            _lastWristPos = wp;
            _lastRootPos = rp;
            _hasLastPos = true;

            // Body-relative (strips joystick locomotion), then a moving average to kill 1-frame tracking jitter.
            Vector3 smoothVel = PushSmooth(wvel - rootVel);
            float fwdVel = Vector3.Dot(smoothVel, FlatForward());

            if (DebugKeyPressed())
            {
                DebugThrow();
                return;
            }

            bool grip = ReadGrip();

            switch (_state)
            {
                case ThrowState.Empty:
                    if (grip) Load();
                    break;

                case ThrowState.Loaded:
                    if (!grip) { Cancel(); break; }
                    // A backward flick re-arms: reset the peak so the next forward swing is its own throw.
                    if (fwdVel < -_config.windBackSpeed) _peakFwdVel = 0f;
                    // Track the forward-swing peak (speed + the velocity vector captured at that instant).
                    if (fwdVel > _peakFwdVel) { _peakFwdVel = fwdVel; _peakArmVel = smoothVel; }
                    // FIRE at the natural release point: once a real swing has peaked and started to slow down.
                    if (!_onCooldown && _peakFwdVel >= _config.vMinFire && fwdVel < _peakFwdVel * _config.releaseDrop)
                    {
                        Fire(wp, _peakArmVel);
                        _peakFwdVel = 0f;
                    }
                    break;
            }

            if (_hud != null) UpdateHud(fwdVel, grip);
        }

        private void Load()
        {
            _state = ThrowState.Loaded;
            _peakFwdVel = 0f;
            ShowHeld(true);
        }

        private void Cancel()
        {
            _state = ThrowState.Empty;
            ShowHeld(false);
        }

        private void Fire(Vector3 origin, Vector3 swingVel)
        {
            // Ballistic: launch with the body-relative swing velocity (no aim cone) → goes exactly where you threw.
            Vector3 dir = swingVel.sqrMagnitude > 1e-4f ? swingVel.normalized : FlatForward();
            float speed = Mathf.Clamp(swingVel.magnitude * _config.velocityScale, _config.minLaunchSpeed, _config.maxLaunchSpeed);
            Vector3 v0 = dir * speed;
            float power = Mathf.Clamp01(speed / Mathf.Max(_config.maxLaunchSpeed, 0.01f));
            _lastLaunchSpeed = speed;
            _fireCount++;
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

        private Vector3 PushSmooth(Vector3 v)
        {
            if (_velBuf == null) return v;
            _velBuf[_velCount % VelSamples] = v;
            _velCount++;
            int n = Mathf.Min(_velCount, VelSamples);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < n; i++) sum += _velBuf[i];
            return sum / n;
        }

        private void CreateHud()
        {
            if (_hud != null || _hudRef == null) return;
            if (_hudRef.gameObject.scene.IsValid())   // a scene instance → pre-placed, use as-is
            {
                _hud = _hudRef;
                return;
            }
            if (_head == null) return;
            GameObject go = Instantiate(_hudRef.gameObject);
            go.name = "ThrowDebugHUD";
            _hud = go.GetComponent<TossZone.UI.DebugHud>();
            if (_hud != null) _hud.AttachTo(_head);
        }

        private void UpdateHud(float fwdVel, bool grip)
        {
            _hud.SetText("THROW " + _state + (grip ? " [grip]" : "")
                + "\nfwdVel " + fwdVel.ToString("0.0") + "  peak " + _peakFwdVel.ToString("0.0")
                + "\nvMin " + _config.vMinFire.ToString("0.0") + "  fires " + _fireCount
                + "\nlast launch " + _lastLaunchSpeed.ToString("0.0") + " m/s");
        }

        private void CreateHeldBall()
        {
            if (!_showVisualHeldBall) return;   // a ThrowBallHolder provides the real grabbable visual instead
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
