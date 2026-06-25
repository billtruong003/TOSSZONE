using UnityEngine;
#if PHOTON_FUSION
using BillGameCore;
using TossZone.Player;
#endif

namespace TossZone.Network
{
    /// <summary>
    /// Walk-through portal. When the LOCAL player's rig enters, asks Fusion to load the arena scene.
    /// Shared Mode: only the master client actually performs the load (Fusion guards LoadScene); every
    /// other client follows automatically. Sits on the [ArenaPortal] trigger in 01_TOSSZONE_Main.
    /// </summary>
    public class PortalMatchmaker : MonoBehaviour
    {
        [SerializeField] private int _arenaSceneIndex = 2;

        private bool _used;

        private void OnTriggerEnter(Collider other)
        {
            if (_used) return;
#if PHOTON_FUSION
            // Only react to OUR local rig. PlayerRig is local-only, so any PlayerRig found on the entering
            // collider's parents is ours (remotes are thin NetworkAvatars with no PlayerRig).
            PlayerRig rig = other.GetComponentInParent<PlayerRig>();
            if (rig == null || rig != PlayerRig.Local) return;

            FusionNet net = FusionNet.Instance;
            if (net == null || !net.IsRunning) return;

            _used = true;
            net.LoadScene(_arenaSceneIndex); // master-only inside FusionNet; clients follow
            Debug.Log("[Portal] Requested load of arena scene " + _arenaSceneIndex);
#endif
        }
    }
}
