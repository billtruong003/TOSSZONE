using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Marks the local VR rig (AutoHand XRPlayer) and exposes its head + hands as a stable,
    /// AutoHand-decoupled handle. The networked avatar copies these poses via <see cref="Instance"/>.
    /// Attach to the XRPlayer root in 01_TOSSZONE_Main and 02_Arena; wire head/hands in the inspector.
    /// </summary>
    public class LocalPlayerRig : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _handLeft;
        [SerializeField] private Transform _handRight;

        public static LocalPlayerRig Instance { get; private set; }

        public Transform Head => _head;
        public Transform HandLeft => _handLeft;
        public Transform HandRight => _handRight;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LocalPlayerRig] A second instance was enabled; keeping the first.");
                return;
            }
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }
    }
}
