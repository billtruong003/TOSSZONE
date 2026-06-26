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
        private bool _heldLocally;   // our own hand is holding it right now

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
            // While OUR hand holds it, keep it dynamic no matter what. Otherwise the per-tick authority
            // bookkeeping below flips a freshly-grabbed ball back to kinematic for the 1-2 ticks before our
            // RequestStateAuthority is granted — which yanked it to its network pose and broke the grab for any
            // non-authority (non-master) player. That is why only the first/host player could grab.
            if (_heldLocally)
            {
                if (_rb.isKinematic) _rb.isKinematic = false;
                return;
            }
            // Proxies follow the NetworkTransform (kinematic); the authority runs real physics.
            ApplyAuthorityPhysics();
        }

        private void OnGrabbed(Hand hand, Grabbable grab)
        {
            // The local player grabbed it — take ownership so our physics/AutoHand drives it and replicates
            // out. Go dynamic immediately (optimistic) so the grab feels instant; authority confirms next tick.
            _heldLocally = true;
            if (!HasStateAuthority) Object.RequestStateAuthority();
            _rb.isKinematic = false;
            Debug.Log($"[NetworkGrabbable] {name} grabbed — hadAuthority={HasStateAuthority} (requested if false)");
        }

        private void OnReleased(Hand hand, Grabbable grab)
        {
            // Keep authority after the throw so the thrower's physics carries the arc until someone else grabs.
            _heldLocally = false;
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
