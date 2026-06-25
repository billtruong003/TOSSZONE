#if PHOTON_FUSION
using Autohand;
using Fusion;
using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Makes an AutoHand <see cref="Grabbable"/> network-synced in Fusion Shared Mode WITHOUT the Physics
    /// Addon. The state-authority peer simulates the rigidbody locally (AutoHand drives it while held, then
    /// physics carries the throw); a <see cref="NetworkTransform"/> replicates the world pose to proxies,
    /// whose rigidbody is kinematic so they just follow. Grabbing requests state authority so the grabbing
    /// player owns the physics; a remote player catching it grabs → steals authority → it becomes theirs.
    ///
    /// Requires <see cref="Rigidbody"/> + <see cref="NetworkTransform"/> + <see cref="Grabbable"/> on the
    /// same object. The Grabbable must NOT reparent on grab (the local hand is not a networked object), so
    /// the ball stays in world space and its pose replicates correctly — enforced here in Awake.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public class NetworkGrabbable : NetworkBehaviour
    {
        private Rigidbody _rb;
        private Grabbable _grabbable;
        private bool _hooked;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grabbable = GetComponent<Grabbable>();
            // Never reparent to the (local-only, non-networked) hand — keep world space so the pose syncs.
            _grabbable.parentOnGrab = false;
        }

        public override void Spawned()
        {
            if (!_hooked)
            {
                _grabbable.OnGrabEvent += OnGrabbed;
                _grabbable.OnReleaseEvent += OnReleased;
                _hooked = true;
            }
            ApplyAuthorityPhysics();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_hooked && _grabbable != null)
            {
                _grabbable.OnGrabEvent -= OnGrabbed;
                _grabbable.OnReleaseEvent -= OnReleased;
                _hooked = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Proxies follow the NetworkTransform (kinematic); the authority runs real physics.
            ApplyAuthorityPhysics();
        }

        private void OnGrabbed(Hand hand, Grabbable grab)
        {
            // The local player grabbed it — take ownership so our physics/AutoHand drives it and replicates
            // out. Go dynamic immediately (optimistic) so the grab feels instant; authority confirms next tick.
            if (!HasStateAuthority) Object.RequestStateAuthority();
            _rb.isKinematic = false;
        }

        private void OnReleased(Hand hand, Grabbable grab)
        {
            // Keep authority after the throw so the thrower's physics carries the arc until someone else grabs.
            ApplyAuthorityPhysics();
        }

        private void ApplyAuthorityPhysics()
        {
            bool kinematic = !HasStateAuthority;
            if (_rb.isKinematic != kinematic) _rb.isKinematic = kinematic;
        }
    }
}
#endif
