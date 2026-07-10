using BillGameCore;
using TossZone.Combat;
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

        private const string PoolKey = "throwprojectile";

        /// <summary>NetworkAvatar reads this in FixedUpdateNetwork to sync the held-ball visual to other players.</summary>
        public static bool LocalHoldingBall { get; private set; }

#if PHOTON_FUSION
        [Header("Networking (Fusion)")]
        [Tooltip("NetworkProjectile prefab (NetworkObject + NetworkTransform + NetworkProjectile). Assign to replicate projectile flight to remote clients.")]
        [SerializeField] private Fusion.NetworkObject _netProjectilePrefab;
        private readonly System.Collections.Generic.Dictionary<Transform, Fusion.NetworkObject> _twins
            = new System.Collections.Generic.Dictionary<Transform, Fusion.NetworkObject>();
        private Fusion.NetworkRunner _runner;
#endif

        private PlayerRig _rig;
        private Transform _wrist, _head, _heldBall;
        private float _heldBaseScale = 1f;
        private int _lastEquippedIndex = -999;
        private System.Action _onRefillCb;                  // cached → no per-throw delegate alloc
        private System.Action<BallLandedEvent> _onBallLandedCb;
        private Vector3 _lastWristPos;
        private Vector3 _lastHeadPos;
        private bool _hasLastPos;
        private float _peakFwdVel;          // peak forward swing speed since the last wind-up / fire
        private Vector3 _peakArmVel;        // smoothed hand velocity at that peak → the launch velocity
        private const int VelSamples = 4;
        private const float MinSwingDistance = 0.25f;
        private Vector3[] _velBuf;          // moving-average ring buffer → kills 1-frame tracking jitter
        private int _velCount;
        private ThrowState _state;
        private bool _onCooldown;
        private bool _ready;
        private bool _windBackTriggered;
        private float _swingFwdDist;

        private void OnDisable()
        {
            // HandWeapon disables this controller when a non-ballistic weapon (Gun/Bazooka/Sword) is equipped —
            // without hiding the held ball here, the rock ball stayed visible in the hand NEXT TO the new
            // weapon's model (two visuals at once).
            _state = ThrowState.Empty;
            ShowHeld(false);
            if (_ready && Bill.IsReady && _onBallLandedCb != null)
                Bill.Events.Unsubscribe<BallLandedEvent>(_onBallLandedCb);
        }

        private void OnEnable()
        {
            // Pairs with OnDisable's unsubscribe: switching Gun → back to Rock re-enables this controller, but
            // TryInit only subscribes once (_ready guard) — without this, landing haptics + the networked
            // projectile despawn silently stopped working after the first weapon switch.
            if (_ready && Bill.IsReady && _onBallLandedCb != null)
                Bill.Events.Subscribe<BallLandedEvent>(_onBallLandedCb);
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
                // Leak fix (Session 17.13): dropping _ready without unsubscribing left the old BallLanded
                // subscription alive — TryInit then subscribed AGAIN, so every landing fired the callback
                // twice (double haptics/despawn) and OnDisable's _ready gate skipped the orphan forever.
                if (Bill.IsReady && _onBallLandedCb != null)
                    Bill.Events.Unsubscribe<BallLandedEvent>(_onBallLandedCb);
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
            if (_wrist == null) return;

            if (_projectilePrefab != null) Bill.Pool.Register(PoolKey, _projectilePrefab, 8);
            RefreshHeldModel();
            _onRefillCb = OnRefill;
            _onBallLandedCb = OnBallLanded;
            Bill.Events.Subscribe<BallLandedEvent>(_onBallLandedCb);

            _hasLastPos = false;
            _velBuf = new Vector3[VelSamples];
            _velCount = 0;
            _peakFwdVel = 0f;
            _state = ThrowState.Empty;
            _ready = true;
            Debug.Log("[Throw] ThrowController ready (hand=" + (_rightHand ? "R" : "L") + "). Debug throw key = " + _debugThrowKey);
        }

        private void Tick()
        {
            // T17: swap the held visual to match whichever ThrowBallistic weapon is equipped (Rock/Grenade/
            // BigBoom/LandMine) — was always the same generic ball regardless of equip. Rock resolves to a
            // null WeaponConfig by design (ResolveEquippedConfig) and falls back to _heldBallPrefab, same as
            // before this change.
            int equippedIdx = PlayerCombat.Local != null ? PlayerCombat.Local.EquippedIndex : -999;
            if (equippedIdx != _lastEquippedIndex)
            {
                _lastEquippedIndex = equippedIdx;
                RefreshHeldModel();
            }

#if PHOTON_FUSION
            if (PlayerCombat.Local != null && PlayerCombat.Local.IsFrozen)
            {
                if (_state != ThrowState.Empty) Cancel();
                return;
            }
#endif
            float dt = Time.deltaTime;
            Vector3 wp = _wrist.position;
            Vector3 hp = _head.position;
            bool hadLast = _hasLastPos && dt > 1e-5f;
            Vector3 wvel = hadLast ? (wp - _lastWristPos) / dt : Vector3.zero;
            Vector3 headVel = hadLast ? (hp - _lastHeadPos) / dt : Vector3.zero;
            _lastWristPos = wp;
            _lastHeadPos = hp;
            _hasLastPos = true;

            Vector3 smoothVel = PushSmooth(wvel - headVel);
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
                    if (fwdVel < -_config.windBackSpeed)
                    {
                        if (!_windBackTriggered) { Haptic(_config.hapticWind, 0.05f); PulseHeld(); _windBackTriggered = true; }
                        _peakFwdVel = 0f;
                        _swingFwdDist = 0f;
                    }
                    else
                    {
                        _windBackTriggered = false;
                    }
                    if (fwdVel > 0f) _swingFwdDist += fwdVel * dt;
                    if (fwdVel > _peakFwdVel) { _peakFwdVel = fwdVel; _peakArmVel = smoothVel; }
                    if (!_onCooldown && _swingFwdDist >= MinSwingDistance
                        && _peakFwdVel >= _config.vMinFire && fwdVel < _peakFwdVel * _config.releaseDrop)
                    {
                        Fire(wp, _peakArmVel);
                        _peakFwdVel = 0f;
                        _swingFwdDist = 0f;
                    }
                    break;
            }
        }

        private void Load()
        {
            _state = ThrowState.Loaded;
            _peakFwdVel = 0f;
            _swingFwdDist = 0f;
            ShowHeld(true);
        }

        private void Cancel()
        {
            _state = ThrowState.Empty;
            ShowHeld(false);
        }

        private void Fire(Vector3 origin, Vector3 swingVel)
        {
#if PHOTON_FUSION
            WeaponConfig ppuCfg = ResolveEquippedConfig();
            if (ppuCfg != null && PlayerCombat.Local != null
                && !PlayerCombat.Local.UseOrBuyAmmo(PlayerCombat.Local.EquippedIndex, ppuCfg))
                return;
#endif
            // Ballistic: launch with the body-relative swing velocity (no aim cone) → goes exactly where you threw.
            Vector3 dir = swingVel.sqrMagnitude > 1e-4f ? swingVel.normalized : FlatForward();
            float speed = Mathf.Clamp(swingVel.magnitude * _config.velocityScale, _config.minLaunchSpeed, _config.maxLaunchSpeed);
            Vector3 v0 = dir * speed;
            float power = Mathf.Clamp01(speed / Mathf.Max(_config.maxLaunchSpeed, 0.01f));
#if UNITY_EDITOR
            DrawTrajectoryDebug(origin, v0);
#endif
            SpawnProjectile(origin, v0, power);

            Haptic(_config.hapticRelease, 0.06f);
            if (!string.IsNullOrEmpty(_config.throwSfx)) Bill.Audio.PlayPitched(_config.throwSfx, Mathf.Lerp(0.85f, 1.25f, power));
            ReleaseFlash.Show(origin, power);
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
            if (!string.IsNullOrEmpty(_config.throwSfx)) Bill.Audio.PlayPitched(_config.throwSfx, Mathf.Lerp(0.85f, 1.25f, 0.6f));
            ReleaseFlash.Show(origin, 0.6f);
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
#if PHOTON_FUSION
            WeaponConfig equippedCfg = ResolveEquippedConfig();
            if (proj != null)
            {
                proj.ApplyWeaponVisual(equippedCfg);
                if (equippedCfg != null && equippedCfg.isUncatchable) proj.SetUncatchable();
            }
            SpawnNetworkProjectile(pos, rot, go.transform);
#endif
        }

        private void OnBallLanded(BallLandedEvent e)
        {
            Haptic(_config.hapticImpact, 0.05f);
            ImpactBurst.Show(e.Position, e.Power);
#if PHOTON_FUSION
            if (e.Ball != null) DespawnTwin(e.Ball.transform);
#endif
        }

#if PHOTON_FUSION
        private void SpawnNetworkProjectile(Vector3 pos, Quaternion rot, Transform localProj)
        {
            if (_netProjectilePrefab == null) return;
            TryGetRunner();
            if (_runner == null || !_runner.IsRunning) return;
            DespawnTwin(localProj);
            // T20: stamp the equipped weapon's visual id before Spawned (proxies dress it from their catalog).
            int equippedIdx = PlayerCombat.Local != null ? PlayerCombat.Local.EquippedIndex : -1;
            int visualIndex = equippedIdx >= 0 ? equippedIdx + 1 : 0;
            Fusion.PlayerRef shooter = _runner.LocalPlayer;
            Fusion.NetworkObject netObj = _runner.Spawn(_netProjectilePrefab, pos, rot, null,
                (runner, o) => { if (o.TryGetComponent(out NetworkProjectile p)) { p.VisualIndex = visualIndex; p.Shooter = shooter; } });
            if (netObj == null) return;
            _twins[localProj] = netObj;
            NetworkProjectile np = netObj.GetComponent<NetworkProjectile>();
            if (np != null)
            {
                np.LinkTo(localProj);

                // ThrowBallistic weapons other than the default Rock (Grenade/BigBoom/LandMine) still fly via
                // this swing-throw path — apply their configured damage/AoE so they aren't silently identical
                // to a plain Rock throw.
                WeaponConfig cfg = ResolveEquippedConfig();
                if (cfg != null)
                {
                    if (cfg.damage > 0) np.SetDamage(cfg.damage);
                    if (cfg.aoeRadius > 0f) np.SetAoe(cfg.aoeRadius);
                    if (cfg.crossFireZones) np.SetCrossZones(cfg.crossZoneWidth, cfg.crossZoneLength, cfg.crossZoneSeconds);
                    if (cfg.fuseDelay > 0f) np.SetMine(cfg.fuseDelay);
                    np.Uncatchable = cfg.isUncatchable;
                }
            }
        }

        /// <summary>The currently equipped WeaponConfig (null = index -1 / Rock / default — the projectile's own
        /// base damage applies). Reads the LOCAL player's combat state; only meaningful for the throw authority.</summary>
        private WeaponConfig ResolveEquippedConfig()
        {
            PlayerCombat combat = PlayerCombat.Local;
            if (combat == null) return null;
            int idx = combat.EquippedIndex;
            if (idx < 0) return null;
            WeaponConfig[] catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            return (catalog != null && idx < catalog.Length) ? catalog[idx] : null;
        }

        private void DespawnTwin(Transform localProj)
        {
            if (localProj == null || !_twins.TryGetValue(localProj, out Fusion.NetworkObject netObj)) return;
            _twins.Remove(localProj);
            if (netObj == null || !netObj.IsValid) return;
            NetworkProjectile np = netObj.GetComponent<NetworkProjectile>();
            if (np != null && (np.Exploded || np.PersistsAfterLanding)) return;
            TryGetRunner();
            if (_runner != null && _runner.IsRunning) _runner.Despawn(netObj);
        }

        private void TryGetRunner()
        {
            if (_runner != null && _runner.IsRunning) return;
            var instances = Fusion.NetworkRunner.Instances;
            _runner = instances.Count > 0 ? instances[0] : null;
        }
#endif

        /// <summary>T19: force the held visual to re-resolve next Tick — called by <see cref="WeaponHolder"/>
        /// when it activates/deactivates so the cosmetic ball hands over to the real grabbable (and back)
        /// without waiting for the next weapon change.</summary>
        public void ReevaluateHeldVisual() => _lastEquippedIndex = -999;

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

        /// <summary>T17: rebuild the held visual for whichever ThrowBallistic weapon is currently equipped
        /// (Rock/Grenade/BigBoom/LandMine all share this swing-throw path). WeaponConfig.heldPrefab was
        /// authored per-weapon but nothing ever read it — every ThrowBallistic weapon showed the same generic
        /// ball regardless of which one was equipped. Rock resolves to a null WeaponConfig by design
        /// (<see cref="ResolveEquippedConfig"/> returns null for the default index -1) and falls back to
        /// <see cref="_heldBallPrefab"/>, matching the pre-T17 behavior exactly for Rock.</summary>
        private void RefreshHeldModel()
        {
            if (!_showVisualHeldBall) return;   // permanently opted out in the inspector
            bool wasShown = _heldBall != null && _heldBall.gameObject.activeSelf;
            if (_heldBall != null)
            {
                // PulseHeld's yoyo tween may still be running on the old ball — kill it BEFORE Destroy or
                // BillTween keeps ticking a dead Transform (same MissingReferenceException class the BuffRing
                // consume tween hit earlier this project).
                BillTween.KillTarget(_heldBall);
                Destroy(_heldBall.gameObject);
                _heldBall = null;
            }
            // T19: a ready WeaponHolder puts the REAL equipped grabbable in this hand — the parented visual
            // would double up with it, so hand the held visual over entirely while one is active (checked
            // AFTER the destroy so a late-activating holder also purges an already-created ball).
            if (WeaponHolder.IsActiveFor(_rightHand)) return;

#if PHOTON_FUSION
            WeaponConfig cfg = ResolveEquippedConfig();
            if (cfg != null && cfg.handSource == HandSource.AppearInHand && cfg.heldPrefab != null)
            {
                // Weapon-specific visual: cosmetic stripped copy with per-weapon hold offsets (shared spawner
                // with HandWeapon — see SpawnHeldVisual for why a raw Instantiate of MS_WP_* props is unsafe).
                GameObject holder = HandWeapon.SpawnHeldVisual(cfg, _wrist);
                if (holder != null)
                {
                    _heldBall = holder.transform;
                    _heldBaseScale = 1f;   // offsets/scale live on the model INSIDE the holder
                    ShowHeld(wasShown);
                    return;
                }
            }
#endif
            CreateHeldBallFrom(_heldBallPrefab);
            ShowHeld(wasShown);
        }

        private void CreateHeldBallFrom(GameObject prefab)
        {
            if (_heldBall != null) return;
            GameObject ball;
            if (prefab != null)
            {
                ball = Instantiate(prefab);                 // prefab path — keep its own scale/material (you tune it)
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
            LocalHoldingBall = on;   // replicated by NetworkAvatar.FixedUpdateNetwork → remote players see the ball
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

#if UNITY_EDITOR
        /// <summary>Debug visual (T17) — draws the predicted ballistic arc the instant a throw fires, using the
        /// exact same formula ThrowProjectile.OnFlight replays (p(t) = origin + v0·t + ½·g·t²). Scene view only.</summary>
        private void DrawTrajectoryDebug(Vector3 origin, Vector3 v0)
        {
            if (_config == null) return;
            Vector3 gravity = Vector3.down * Mathf.Max(_config.gravity, 0.01f);
            Vector3 prev = origin;
            const int Steps = 24;
            const float Duration = 1.2f;
            for (int i = 1; i <= Steps; i++)
            {
                float t = (i / (float)Steps) * Duration;
                Vector3 p = origin + v0 * t + 0.5f * gravity * (t * t);
                Debug.DrawLine(prev, p, Color.magenta, 1.5f);
                if (p.y < origin.y - 5f) break;   // stop once it's clearly past ground level
                prev = p;
            }
        }

        /// <summary>Always-on so it's visible while testing with the T/G/F keyboard debug keys, not just when
        /// selected. Wrist sphere color = state (grey Empty / green Loaded-ready / red cooldown); magenta ray =
        /// the current swing peak direction once it clears vMinFire.</summary>
        private void OnDrawGizmos()
        {
            if (_wrist == null) return;
            Gizmos.color = _onCooldown ? Color.red : (_state == ThrowState.Empty ? Color.grey : Color.green);
            Gizmos.DrawWireSphere(_wrist.position, 0.04f);
            if (_config != null && _peakFwdVel >= _config.vMinFire * 0.5f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(_wrist.position, _peakArmVel.normalized * 0.25f);
            }
        }
#endif
    }
}
