#if PHOTON_FUSION
using Fusion;
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
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _localProjectile == null) return;
            // Mirror the local BillTween-driven transform every tick; NT sends the delta to all proxies.
            transform.SetPositionAndRotation(_localProjectile.position, _localProjectile.rotation);
        }
    }
}
#endif
