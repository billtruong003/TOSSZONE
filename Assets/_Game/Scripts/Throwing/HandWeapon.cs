#if PHOTON_FUSION
using Fusion;
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace TossZone.Throwing
{
    /// <summary>
    /// Per-hand weapon dispatcher. Sits alongside <see cref="ThrowController"/> on the local player's
    /// NetworkAvatar. Polls <see cref="PlayerCombat.EquippedIndex"/> each frame; on change it reconfigures
    /// the hand for the active <see cref="WeaponConfig"/>:
    /// <list type="bullet">
    ///   <item>ThrowBallistic (index -1 / Rock / Grenade / LandMine / BigBoom) — ThrowController enabled;
    ///         HandWeapon is passive.</item>
    ///   <item>ProjectileLaunch (Gun / Bazooka) — ThrowController disabled; trigger press spawns
    ///         a NetworkProjectile from <see cref="_muzzle"/>.</item>
    ///   <item>Hitscan — trigger press fires an instant raycast.</item>
    ///   <item>Melee (Sword) — trigger press checks an overlap sphere near the blade tip.</item>
    /// </list>
    /// Call <see cref="Initialize"/> from NetworkAvatar.Spawned() (authority only).
    /// </summary>
    [RequireComponent(typeof(ThrowController))]
    public class HandWeapon : MonoBehaviour
    {
        [Header("Hand")]
        [SerializeField] private bool _rightHand = true;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _bladeTip;

        [Header("Defaults")]
        [SerializeField] private NetworkObject _defaultNetProjPrefab;
        [SerializeField] private float _hitscanRange = 20f;
        [SerializeField] private LayerMask _hitscanMask = ~0;
        [Tooltip("Editor/dev: hold this key to simulate the trigger when no XR controller is present (mirrors ThrowController's _editorGripKey). Weapons here have no hand-held visual/grab pose — firing is pure trigger-press logic, so no XR Simulator or AutoHand Grabbable setup is needed just to test fire behavior.")]
        [SerializeField] private Key _editorTriggerKey = Key.F;

        private ThrowController _throwController;
        private PlayerCombat _combat;
        private NetworkRunner _runner;
        private WeaponConfig _activeConfig;
        private int _lastEquippedIndex = -999;
        private float _cooldownEnd;
        private bool _triggerLastFrame;

        private static readonly Collider[] _overlap = new Collider[8];
        private const float MeleeRadius = 0.35f;
        private const int LayerHittable = 15;

        [Header("Deflect (Sword — canDeflect weapons)")]
        [SerializeField] private float _deflectRadius = 0.15f;
        [SerializeField] private float _deflectSpeed = 10f;
        private Vector3 _prevBladePos;
        private bool _hasPrevBladePos;
        private static readonly Collider[] _deflectOverlap = new Collider[8];
        private static readonly int[] _burstDeflectSlots = new int[16];
        private static readonly int[] _burstDeflectIndices = new int[16];

        private void Awake() => _throwController = GetComponent<ThrowController>();

        /// <summary>Authority only — call from NetworkAvatar.Spawned().</summary>
        public void Initialize(PlayerCombat combat, NetworkRunner runner)
        {
            _combat = combat;
            _runner = runner;
        }

        private void Update()
        {
            if (_combat == null || _runner == null)
            {
                // Proxy path (T17): Initialize() only ever runs on the owner, but EquippedIndex is [Networked] —
                // read it straight off the sibling PlayerCombat so OTHER clients still see this avatar's equipped
                // weapon model. Display only; all fire/deflect logic below stays owner-exclusive.
                UpdateProxyHeldModel();
                return;
            }

            int equipped = _combat.EquippedIndex;
            if (equipped != _lastEquippedIndex) OnEquipChanged(equipped);

            UpdateLaser();

            // Deflect is a continuous physical sweep (no trigger press), independent of fireMode — runs
            // whenever the equipped weapon allows it (Sword: canDeflect=true, attacksPlayers=false).
            if (_activeConfig != null && _activeConfig.canDeflect && !(_combat != null && _combat.IsFrozen))
                HandleDeflectSweep();

            // Ballistic weapons are handled entirely by ThrowController.
            if (_activeConfig == null || _activeConfig.fireMode == FireMode.ThrowBallistic) return;

            bool trigger = ReadTrigger();
            if (trigger && !_triggerLastFrame) OnTriggerPressed();
            _triggerLastFrame = trigger;
        }

        private void OnEquipChanged(int newIndex)
        {
            _lastEquippedIndex = newIndex;
            _activeConfig = GetConfig(newIndex);
            bool isBallistic = _activeConfig == null || _activeConfig.fireMode == FireMode.ThrowBallistic;
            if (_throwController != null) _throwController.enabled = isBallistic;
            _hasPrevBladePos = false;   // don't sweep from a stale blade position after switching weapons

            // T17: ThrowBallistic weapons (Rock/Grenade/BigBoom/LandMine) show their held visual via
            // ThrowController instead (see ThrowController.RefreshHeldModel) — HandWeapon only owns the visual
            // for weapons IT actively fires (Gun/Bazooka/Sword). Was previously entirely unwired: WeaponConfig
            // already had heldPrefab/handSource authored, nothing ever read them.
            // T19: while a ready WeaponHolder serves this hand, the OWNER holds the real grabbable instead —
            // no cosmetic copy (remote proxies keep the cosmetic path via UpdateProxyHeldModel).
            bool holderActive = WeaponHolder.IsActiveFor(_rightHand);
            UpdateHeldModel(isBallistic || holderActive ? null : _activeConfig);
        }

        /// <summary>T19: force the equip state to re-resolve next Update — called by <see cref="WeaponHolder"/>
        /// when it activates/deactivates so the cosmetic wrist model hands over to the real grabbable (and
        /// back) without waiting for the next weapon change.</summary>
        public void ReevaluateEquip() => _lastEquippedIndex = -999;

        private GameObject _heldModel;
        private WeaponConfig _heldModelConfig;
        private PlayerCombat _proxyCombat;

        private void UpdateProxyHeldModel()
        {
            if (_proxyCombat == null) _proxyCombat = GetComponentInParent<PlayerCombat>();
            if (_proxyCombat == null || _proxyCombat.Object == null || !_proxyCombat.Object.IsValid) return;
            if (_proxyCombat.HasStateAuthority) return;   // owner drives via Initialize/OnEquipChanged instead

            int equipped = _proxyCombat.EquippedIndex;
            if (equipped == _lastEquippedIndex) return;
            _lastEquippedIndex = equipped;

            WeaponConfig cfg = GetConfig(equipped);
            bool isBallistic = cfg == null || cfg.fireMode == FireMode.ThrowBallistic;
            // ThrowBallistic held visuals on proxies stay the NetworkAvatar HoldingBall sphere (existing path);
            // this only mirrors the Gun/Bazooka/Sword models.
            UpdateHeldModel(isBallistic ? null : cfg);
        }

        private void UpdateHeldModel(WeaponConfig cfg)
        {
            if (_heldModelConfig == cfg) return;   // already showing the right thing (or correctly nothing)
            if (_heldModel != null) { Destroy(_heldModel); _heldModel = null; }
            _heldModelConfig = cfg;
            if (cfg == null || cfg.handSource != HandSource.AppearInHand || cfg.heldPrefab == null) return;

            // Owner: parent to the LOCAL rig wrist (tracking-rate, smooth against the real hand). Proxies have
            // no rig — they use the avatar's NT-synced wrist node (the muzzle's parent) instead.
            Transform parent = _muzzle != null ? _muzzle.parent : transform;
            PlayerRig rig = _combat != null ? PlayerRig.Local : null;
            Transform rigWrist = rig != null ? (_rightHand ? rig.WristR : rig.WristL) : null;
            if (rigWrist != null) parent = rigWrist;

            _heldModel = SpawnHeldVisual(cfg, parent);
        }

        /// <summary>Spawn a purely-cosmetic copy of <paramref name="cfg"/>.heldPrefab under
        /// <paramref name="parent"/>, with the per-weapon hold offsets applied. The MS_WP_* prefabs are full
        /// AutoHand Grabbable props (Rigidbody + Collider + Grabbable, and GrabbableBase.Awake() ADDS a
        /// GrabbablePoseCombiner + hooks Application.quitting the moment a live copy wakes) — so the copy is
        /// instantiated under an INACTIVE holder (children of an inactive parent never Awake) and stripped with
        /// DestroyImmediate BEFORE activation. A deferred Destroy-after-Instantiate ran Awake first and leaked
        /// the auto-added PoseCombiner — that's the "missing components + leftover junk" state this replaces.</summary>
        internal static GameObject SpawnHeldVisual(WeaponConfig cfg, Transform parent)
        {
            if (cfg == null || cfg.heldPrefab == null) return null;
            var holder = new GameObject("HeldVisual(" + cfg.id + ")");
            holder.SetActive(false);
            holder.transform.SetParent(parent, false);

            GameObject model = Instantiate(cfg.heldPrefab, holder.transform);
            foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            if (model.TryGetComponent(out Rigidbody rb)) DestroyImmediate(rb);

            model.transform.localPosition = cfg.holdPositionOffset;
            model.transform.localRotation = Quaternion.Euler(cfg.holdRotationOffset);
            if (!Mathf.Approximately(cfg.holdScale, 1f))
                model.transform.localScale *= Mathf.Max(0.01f, cfg.holdScale);

            holder.SetActive(true);
            return holder;
        }

        private void OnDestroy()
        {
            if (_heldModel != null) Destroy(_heldModel);
            if (_laser != null) Destroy(_laser.gameObject);
        }

        private LineRenderer _laser;

        private void UpdateLaser()
        {
            bool active = _activeConfig != null && _activeConfig.laserSight && _muzzle != null;
            if (!active)
            {
                if (_laser != null) _laser.enabled = false;
                return;
            }
            if (_laser == null) CreateLaser();
            _laser.enabled = true;
            Vector3 origin = _muzzle.position;
            Vector3 dir = _muzzle.forward;
            float length = Physics.Raycast(origin, dir, out RaycastHit hit, _hitscanRange, _hitscanMask,
                QueryTriggerInteraction.Ignore) ? hit.distance : _hitscanRange;
            _laser.SetPosition(0, origin);
            _laser.SetPosition(1, origin + dir * length);
        }

        private void CreateLaser()
        {
            var go = new GameObject("LaserSight");
            go.transform.SetParent(transform, false);
            _laser = go.AddComponent<LineRenderer>();
            _laser.useWorldSpace = true;
            _laser.positionCount = 2;
            _laser.startWidth = 0.004f;
            _laser.endWidth = 0.004f;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.1f, 0.1f, 0.9f));
            else mat.color = new Color(1f, 0.1f, 0.1f, 0.9f);
            _laser.material = mat;
        }

        // ── Deflect: sword sweep vs both single NetworkProjectiles (collider) and burst-rain projectiles
        //    (data query) — see Docs/Burst_Projectile_System_Design.md §5 "Deflect". Bounces along the blade's
        //    own swing direction (the design doc explicitly allows this simpler alternative to aiming back at
        //    the original shooter's live position).
        private void HandleDeflectSweep()
        {
            Transform tip = _bladeTip != null ? _bladeTip : transform;
            Vector3 cur = tip.position;
            if (!_hasPrevBladePos) { _prevBladePos = cur; _hasPrevBladePos = true; return; }
            if (cur == _prevBladePos) return;   // no motion this frame, nothing swept

            Vector3 bounceDir = (cur - _prevBladePos).normalized;
            Vector3 bounceVel = bounceDir * _deflectSpeed;

            int deflectedSingle = DeflectSingleProjectiles(_prevBladePos, cur, bounceVel);
            int deflectedBurst = DeflectBurstProjectiles(_prevBladePos, cur, bounceVel);
            int deflected = deflectedSingle + deflectedBurst;
            if (deflected > 0 && BillGameCore.Bill.IsReady)
                BillGameCore.Bill.Events.Fire(new DeflectEvent { Count = deflected, Point = (_prevBladePos + cur) * 0.5f });

#if UNITY_EDITOR
            // Debug visual: the actual swept segment each frame, cyan normally / yellow the instant it deflects
            // something — Scene view only (view via a keyboard test with the T/G/F debug keys). Query radius
            // sphere at the swing midpoint for a sense of the deflect "thickness".
            bool hit = deflectedSingle > 0 || deflectedBurst > 0;
            Debug.DrawLine(_prevBladePos, cur, hit ? Color.yellow : Color.cyan, hit ? 0.4f : 0.15f);
            if (hit) Debug.DrawRay((_prevBladePos + cur) * 0.5f, bounceDir * 0.3f, Color.red, 0.4f);
#endif

            _prevBladePos = cur;
        }

        /// <summary>Single NetworkProjectile (has its own collider+Rigidbody) — redirect in place, no
        /// despawn/respawn needed. MVP limitation: only redirects projectiles this client already has authority
        /// over (Shared Mode requires an async RequestStateAuthority + AllowStateAuthorityOverride hand-off to
        /// take someone else's — deferred, same class of gap as T12's buff-ring RPC).</summary>
        private int DeflectSingleProjectiles(Vector3 from, Vector3 to, Vector3 bounceVel)
        {
            Vector3 mid = (from + to) * 0.5f;
            float radius = Mathf.Max(_deflectRadius, Vector3.Distance(from, to) * 0.5f + 0.05f);
            int n = Physics.OverlapSphereNonAlloc(mid, radius, _deflectOverlap, ~0, QueryTriggerInteraction.Collide);
            int deflected = 0;
            for (int i = 0; i < n; i++)
            {
                if (!_deflectOverlap[i].TryGetComponent(out NetworkProjectile np)) continue;
                if (np.Object == null || !np.Object.IsValid || !np.Object.HasStateAuthority) continue;
                np.Shooter = _runner.LocalPlayer;
                np.Launch(bounceVel, 0f, _activeConfig.damage);
                deflected++;
            }
            return deflected;
        }

        /// <summary>Burst-rain projectiles: split the deflected one OUT of the mass burst into a normal pooled
        /// single (<see cref="ProjectileBurstSystem.ResolveDeflect"/>) — matches the design doc's model.</summary>
        private int DeflectBurstProjectiles(Vector3 from, Vector3 to, Vector3 bounceVel)
        {
            ProjectileBurstSystem sys = ProjectileBurstSystem.Instance;
            if (sys == null) return 0;
            int n = sys.TryDeflectAlong(from, to, _deflectRadius, _burstDeflectSlots, _burstDeflectIndices, _burstDeflectSlots.Length);
            for (int k = 0; k < n; k++)
                sys.ResolveDeflect(_burstDeflectSlots[k], _burstDeflectIndices[k], bounceVel, _runner.LocalPlayer, _defaultNetProjPrefab);
            return n;
        }

        private void OnTriggerPressed()
        {
            if (_activeConfig == null || Time.time < _cooldownEnd) return;
            if (_combat != null && _combat.IsFrozen) return;

            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;
            if (!CombatSession.TrainingModeActive && elapsed < _activeConfig.unlockTime) return;

            if (!_combat.UseOrBuyAmmo(_lastEquippedIndex, _activeConfig)) return;

            switch (_activeConfig.fireMode)
            {
                case FireMode.ProjectileLaunch: FireProjectile(); break;
                case FireMode.Hitscan:          FireHitscan();    break;
                case FireMode.Melee:            FireMelee();      break;
            }

            _cooldownEnd = Time.time + _activeConfig.cooldown;
        }

        private void FireProjectile()
        {
            if (_muzzle == null) return;
            NetworkObject prefab = _activeConfig.projectilePrefab != null
                ? _activeConfig.projectilePrefab.GetComponent<NetworkObject>()
                : _defaultNetProjPrefab;
            if (prefab == null) return;

            int visualIndex = _lastEquippedIndex >= 0 ? _lastEquippedIndex + 1 : 0;
            Fusion.PlayerRef shooter = _runner.LocalPlayer;
            NetworkObject proj = _runner.Spawn(prefab, _muzzle.position,
                Quaternion.LookRotation(_muzzle.forward), _runner.LocalPlayer,
                (runner, o) => { if (o.TryGetComponent(out NetworkProjectile p)) { p.VisualIndex = visualIndex; p.Shooter = shooter; } });
            if (proj == null || !proj.TryGetComponent(out NetworkProjectile np)) return;

            np.Uncatchable = _activeConfig.isUncatchable;
            np.Launch(_muzzle.forward * _activeConfig.muzzleSpeed, _activeConfig.projectileGravity, _activeConfig.damage);
            if (_activeConfig.aoeRadius > 0f) np.SetAoe(_activeConfig.aoeRadius);
        }

        private void FireHitscan()
        {
            if (_muzzle == null) return;
            bool didHit = Physics.Raycast(_muzzle.position, _muzzle.forward, out RaycastHit hit,
                _hitscanRange, _hitscanMask);
#if UNITY_EDITOR
            // Debug visual: the actual raycast — green = hit something, red = missed to full range.
            Debug.DrawRay(_muzzle.position, _muzzle.forward * (didHit ? hit.distance : _hitscanRange),
                didHit ? Color.green : Color.red, 0.5f);
#endif
            if (!didHit) return;

            PlayerCombat victim = hit.collider.GetComponentInParent<PlayerCombat>();
            if (victim == null || victim == _combat) return;
            victim.RPC_TakeHit(_activeConfig.damage, hit.point, _runner.LocalPlayer);
        }

        private void FireMelee()
        {
            // Sword: attacksPlayers=false → deflect-only (see HandDeflector / T5), no direct swing damage.
            if (!_activeConfig.attacksPlayers) return;

            Transform center = _bladeTip != null ? _bladeTip : transform;
            int count = Physics.OverlapSphereNonAlloc(center.position, MeleeRadius,
                _overlap, 1 << LayerHittable);
#if UNITY_EDITOR
            Debug.DrawRay(center.position, Vector3.up * 0.1f, count > 0 ? Color.yellow : Color.gray, 0.3f);
#endif
            for (int i = 0; i < count; i++)
            {
                PlayerCombat victim = _overlap[i].GetComponentInParent<PlayerCombat>();
                if (victim == null || victim == _combat) continue;
                victim.RPC_TakeHit(_activeConfig.damage, center.position, _runner.LocalPlayer);
                break;
            }
        }

        private bool ReadTrigger()
        {
            XRNode node = _rightHand ? XRNode.RightHand : XRNode.LeftHand;
            UnityEngine.XR.InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool pressed) && pressed) return true;
            Keyboard kb = Keyboard.current;
            return kb != null && kb[_editorTriggerKey].isPressed;
        }

        private static WeaponConfig GetConfig(int index)
        {
            if (index < 0 || CombatSession.Instance == null) return null;
            WeaponConfig[] catalog = CombatSession.Instance.CurrentCatalog;
            return (catalog != null && index < catalog.Length) ? catalog[index] : null;
        }

#if UNITY_EDITOR
        /// <summary>Debug visual (T17) — always-on so it's visible while testing with the T/G/F keyboard debug
        /// keys, not just when this object is selected. Yellow = muzzle position/aim (hitscan range) + melee
        /// radius; cyan = deflect query radius at the blade tip. Editor Scene view only — stripped from builds.</summary>
        private void OnDrawGizmos()
        {
            if (_muzzle != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_muzzle.position, 0.025f);
                Gizmos.DrawRay(_muzzle.position, _muzzle.forward * (_activeConfig != null && _activeConfig.fireMode == FireMode.Hitscan ? _hitscanRange : 0.4f));
            }
            if (_bladeTip != null)
            {
                Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.5f);
                Gizmos.DrawWireSphere(_bladeTip.position, MeleeRadius);
                Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.5f);
                Gizmos.DrawWireSphere(_bladeTip.position, _deflectRadius);
            }
        }
#endif
    }
}
#endif
