#if PHOTON_FUSION
using Fusion;
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;
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
            if (_combat == null || _runner == null) return;

            int equipped = _combat.EquippedIndex;
            if (equipped != _lastEquippedIndex) OnEquipChanged(equipped);

            // Deflect is a continuous physical sweep (no trigger press), independent of fireMode — runs
            // whenever the equipped weapon allows it (Sword: canDeflect=true, attacksPlayers=false).
            if (_activeConfig != null && _activeConfig.canDeflect) HandleDeflectSweep();

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

            DeflectSingleProjectiles(_prevBladePos, cur, bounceVel);
            DeflectBurstProjectiles(_prevBladePos, cur, bounceVel);

            _prevBladePos = cur;
        }

        /// <summary>Single NetworkProjectile (has its own collider+Rigidbody) — redirect in place, no
        /// despawn/respawn needed. MVP limitation: only redirects projectiles this client already has authority
        /// over (Shared Mode requires an async RequestStateAuthority + AllowStateAuthorityOverride hand-off to
        /// take someone else's — deferred, same class of gap as T12's buff-ring RPC).</summary>
        private void DeflectSingleProjectiles(Vector3 from, Vector3 to, Vector3 bounceVel)
        {
            Vector3 mid = (from + to) * 0.5f;
            float radius = Mathf.Max(_deflectRadius, Vector3.Distance(from, to) * 0.5f + 0.05f);
            int n = Physics.OverlapSphereNonAlloc(mid, radius, _deflectOverlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (!_deflectOverlap[i].TryGetComponent(out NetworkProjectile np)) continue;
                if (np.Object == null || !np.Object.IsValid || !np.Object.HasStateAuthority) continue;
                np.Shooter = _runner.LocalPlayer;
                np.Launch(bounceVel, 0f, _activeConfig.damage);
            }
        }

        /// <summary>Burst-rain projectiles: split the deflected one OUT of the mass burst into a normal pooled
        /// single (<see cref="ProjectileBurstSystem.ResolveDeflect"/>) — matches the design doc's model.</summary>
        private void DeflectBurstProjectiles(Vector3 from, Vector3 to, Vector3 bounceVel)
        {
            ProjectileBurstSystem sys = ProjectileBurstSystem.Instance;
            if (sys == null) return;
            int n = sys.TryDeflectAlong(from, to, _deflectRadius, _burstDeflectSlots, _burstDeflectIndices, _burstDeflectSlots.Length);
            for (int k = 0; k < n; k++)
                sys.ResolveDeflect(_burstDeflectSlots[k], _burstDeflectIndices[k], bounceVel, _runner.LocalPlayer, _defaultNetProjPrefab);
        }

        private void OnTriggerPressed()
        {
            if (_activeConfig == null || Time.time < _cooldownEnd) return;

            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;
            if (elapsed < _activeConfig.unlockTime) return;

            if (_activeConfig.IsPayPerUse && !_combat.UseAmmo()) return;

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

            NetworkObject proj = _runner.Spawn(prefab, _muzzle.position,
                Quaternion.LookRotation(_muzzle.forward), _runner.LocalPlayer);
            if (proj == null || !proj.TryGetComponent(out NetworkProjectile np)) return;

            np.Shooter = _runner.LocalPlayer;
            // Gun: muzzleSpeed fast + gravity 0 (straight line). Bazooka/Grenade: gravity > 0 arcs down.
            np.Launch(_muzzle.forward * _activeConfig.muzzleSpeed, _activeConfig.projectileGravity, _activeConfig.damage);
            if (_activeConfig.aoeRadius > 0f) np.SetAoe(_activeConfig.aoeRadius);
        }

        private void FireHitscan()
        {
            if (_muzzle == null) return;
            if (!Physics.Raycast(_muzzle.position, _muzzle.forward, out RaycastHit hit,
                _hitscanRange, _hitscanMask)) return;

            PlayerCombat victim = hit.collider.GetComponentInParent<PlayerCombat>();
            if (victim == null || victim == _combat) return;
            victim.RPC_TakeHit(_activeConfig.damage, hit.point, _runner.LocalPlayer);
            _combat.RewardHit();
        }

        private void FireMelee()
        {
            // Sword: attacksPlayers=false → deflect-only (see HandDeflector / T5), no direct swing damage.
            if (!_activeConfig.attacksPlayers) return;

            Transform center = _bladeTip != null ? _bladeTip : transform;
            int count = Physics.OverlapSphereNonAlloc(center.position, MeleeRadius,
                _overlap, 1 << LayerHittable);
            for (int i = 0; i < count; i++)
            {
                PlayerCombat victim = _overlap[i].GetComponentInParent<PlayerCombat>();
                if (victim == null || victim == _combat) continue;
                victim.RPC_TakeHit(_activeConfig.damage, center.position, _runner.LocalPlayer);
                _combat.RewardHit();
                break;
            }
        }

        private bool ReadTrigger()
        {
            XRNode node = _rightHand ? XRNode.RightHand : XRNode.LeftHand;
            InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            return dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
        }

        private static WeaponConfig GetConfig(int index)
        {
            if (index < 0 || CombatSession.Instance == null) return null;
            WeaponConfig[] catalog = CombatSession.Instance.CurrentCatalog;
            return (catalog != null && index < catalog.Length) ? catalog[index] : null;
        }
    }
}
#endif
