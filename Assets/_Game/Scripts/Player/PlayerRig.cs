using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Local-only player rig — the heavy AutoHand XR rig (camera + toon hands + locomotion). NEVER networked
    /// and never shown to other players. It only exposes the tracking points (head, both wrists, root) that
    /// the owner's <see cref="NetworkAvatar"/> samples each tick and replicates. One per local player; it
    /// survives scene loads so the rig carries Main -> Arena.
    /// Wire the four transforms to the AutoHand rig (head = "Camera (head)", wrists = "RobotHand (L/R)",
    /// root = the rig root that locomotion moves).
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Local { get; private set; }

        [Header("Tracking points (wired to the AutoHand rig)")]
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _wristL;
        [SerializeField] private Transform _wristR;
        [SerializeField] private Transform _root;

        public Transform Head => _head;
        public Transform WristL => _wristL;
        public Transform WristR => _wristR;
        public Transform Root => _root != null ? _root : transform;

        private void Awake()
        {
            if (Local != null && Local != this)
            {
                // A rig already persists (e.g. carried across a scene load) — drop the duplicate.
                Destroy(gameObject);
                return;
            }
            Local = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Local == this) Local = null;
        }
    }
}
