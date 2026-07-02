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

            // T17: the dummy is a SOLO practice target — with 2+ real players in the match it goes passive so
            // it doesn't third-party the 1v1 (its stray hits kept killing testers, whose respawn round-reset
            // then wiped their equipped weapon mid-test).
            int realPlayers = 0;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                if (pc.IsPlayer) realPlayers++;
            if (realPlayers >= 2) { ScheduleNextThrow(); return; }

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
                // Velocity applied via Rigidbody if present (basic approach for bot throws). Gravity OFF so the
                // shot flies straight to the aimed chest point — with gravity + the low muzzle speed it dropped
                // below the target (and through the floor) long before covering the distance, never hitting.
                if (proj.TryGetComponent(out Rigidbody rb))
                {
                    rb.useGravity = false;
                    rb.linearVelocity = dir * _muzzleSpeed;
                }
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
