#if PHOTON_FUSION
using Fusion;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Simple AI driver for the <see cref="DummyAvatar"/> (M2 solo testing). Authority-only.
    /// Finds the nearest real player every tick; when the throw timer fires it spawns a
    /// <see cref="NetworkProjectile"/> from <see cref="_throwOrigin"/> aimed at the target's chest.
    ///
    /// Attach alongside <see cref="DummyAvatar"/> on the DummyAvatar prefab. If <see cref="_netProjPrefab"/>
    /// is not assigned the bot won't throw (silent no-op — M1 static dummy still works).
    /// </summary>
    public class DummyBotDriver : NetworkBehaviour
    {
        [SerializeField] private NetworkObject _netProjPrefab;
        [SerializeField] private Transform _throwOrigin;
        [SerializeField] private float _throwIntervalMin = 2f;
        [SerializeField] private float _throwIntervalMax = 3.5f;
        [SerializeField] private float _muzzleSpeed = 6f;
        [SerializeField] private int _damage = 1;

        [Networked] private TickTimer ThrowTimer { get; set; }

        private PlayerCombat _combat;

        public override void Spawned()
        {
            _combat = GetComponent<PlayerCombat>();
            if (_combat != null) _combat.IsPlayer = false;

            if (HasStateAuthority) ScheduleNextThrow();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _netProjPrefab == null) return;
            if (_combat != null && _combat.Health <= 0) return;
            if (!ThrowTimer.Expired(Runner)) return;

            PlayerCombat target = FindNearestPlayer();
            if (target != null) FireAt(target);

            ScheduleNextThrow();
        }

        private void FireAt(PlayerCombat target)
        {
            Transform origin = _throwOrigin != null ? _throwOrigin : transform;
            Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
            Vector3 dir = (targetPos - origin.position).normalized;

            NetworkObject proj = Runner.Spawn(_netProjPrefab, origin.position,
                Quaternion.LookRotation(dir), PlayerRef.None);

            if (proj != null && proj.TryGetComponent(out NetworkProjectile np))
            {
                np.Shooter = PlayerRef.None;
                // Velocity applied via Rigidbody if present (basic approach for bot throws).
                if (proj.TryGetComponent(out Rigidbody rb))
                    rb.linearVelocity = dir * _muzzleSpeed;
            }
        }

        private void ScheduleNextThrow()
        {
            float delay = UnityEngine.Random.Range(_throwIntervalMin, _throwIntervalMax);
            ThrowTimer = TickTimer.CreateFromSeconds(Runner, delay);
        }

        private PlayerCombat FindNearestPlayer()
        {
            PlayerCombat nearest = null;
            float nearestSqDist = float.MaxValue;
            Vector3 myPos = transform.position;

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Health <= 0) continue;
                float sq = (pc.transform.position - myPos).sqrMagnitude;
                if (sq < nearestSqDist) { nearestSqDist = sq; nearest = pc; }
            }

            return nearest;
        }
    }
}
#endif
