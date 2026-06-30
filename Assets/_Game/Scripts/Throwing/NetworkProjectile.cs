#if PHOTON_FUSION
using Fusion;
using TossZone.Combat;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Thin Fusion-replicated wrapper around the locally-simulated <see cref="ThrowProjectile"/>. The authority
    /// client copies its local projectile's world transform into this NetworkObject every tick; NetworkTransform
    /// replicates that to proxies. Proxies show the attached mesh renderer (a sphere) interpolated by NT —
    /// they never run the BillTween arc themselves, they just display what the NT feed gives them.
    ///
    /// On the authority the local ThrowProjectile renderer is visible while this NetworkProjectile renderer is
    /// HIDDEN (set in <see cref="Spawned"/>), avoiding a doubled ball.
    /// Despawn is driven from <see cref="ThrowController.DespawnNetworkProjectile"/> when
    /// <see cref="BallLandedEvent"/> fires on the authority client.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkProjectile : NetworkBehaviour
    {
        private Transform _localProjectile;
        private Renderer _mr;

        [Header("Hit + damage")]
        [SerializeField] private int _baseDamage = 1;
        [SerializeField] private float _hitRadius = 0.3f;
        [Tooltip("Layers the projectile can hit (the networked avatar bodies).")]
        [SerializeField] private LayerMask _hittableMask = ~0;

        /// <summary>Who fired this — excluded from its own hits + rewarded on a landed hit.</summary>
        [Networked] public PlayerRef Shooter { get; set; }

        // ── Buff hooks (buff-aware from the start): buff rings + catch SET these; default = no buff. ──────────
        [Networked] public int Multiplier { get; set; }      // 1 = single; >1 = "đạn mưa" (spawns via ring system later)
        [Networked] public float VelocityScale { get; set; } // 1 = base flight speed
        [Networked] public float AreaScale { get; set; }     // 1 = base hit/explosion radius
        [Networked] public int Element { get; set; }         // 0 None · 1 Ice · 2 Fire

        private bool _hasHit;
        private static readonly Collider[] _overlap = new Collider[8];

        /// <summary>
        /// Called by the authority immediately after <see cref="Fusion.NetworkRunner.Spawn"/> so every
        /// FixedUpdateNetwork tick can copy the local projectile's position into the replicated transform.
        /// </summary>
        public void LinkTo(Transform localProj) => _localProjectile = localProj;

        public override void Spawned()
        {
            _mr = GetComponentInChildren<Renderer>();
            // Authority sees the real local ThrowProjectile; hide the network copy to avoid doubling.
            // Proxies keep it enabled — they have no local projectile, so this IS the ball for them.
            if (_mr != null) _mr.enabled = !HasStateAuthority;
            if (HasStateAuthority)
            {
                // Default = no buff (rings / catch overwrite these before + while flying).
                if (Multiplier < 1) Multiplier = 1;
                if (VelocityScale <= 0f) VelocityScale = 1f;
                if (AreaScale <= 0f) AreaScale = 1f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _localProjectile == null) return;
            // Mirror the local BillTween-driven transform every tick; NT sends the delta to all proxies.
            transform.SetPositionAndRotation(_localProjectile.position, _localProjectile.rotation);

            // Hit detection runs only on the authority (the shooter). One hit per projectile.
            if (_hasHit) return;
            int n = Physics.OverlapSphereNonAlloc(transform.position, _hitRadius * AreaScale, _overlap, _hittableMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null) continue;
                if (victim.Object.StateAuthority == Shooter) continue;   // never hit yourself
                _hasHit = true;
                victim.RPC_TakeHit(_baseDamage, transform.position, Shooter);
                if (PlayerCombat.Local != null) PlayerCombat.Local.RewardHit();   // reward the shooter (authority = local)
                break;
            }
        }
    }
}
#endif
