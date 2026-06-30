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
            if (proj != null && proj.TryGetComponent(out NetworkProjectile np))
                np.Shooter = _runner.LocalPlayer;
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
