using UnityEngine;
#if PHOTON_FUSION
using BillGameCore;
using TossZone.Player;
#endif

namespace TossZone.Network
{
    /// <summary>
    /// Walk-through portal. When the LOCAL player's rig enters, asks Fusion to load the arena scene — but
    /// ONLY once every active player has pressed "SẴN SÀNG" on <see cref="PortalReadyGate"/>. Before this
    /// gate existed, a single player brushing the trigger (or just hosting) yanked everyone else across the
    /// map with no warning. Shared Mode: only the master client actually performs the load (Fusion guards
    /// LoadScene); every other client follows automatically. Sits on the [ArenaPortal] trigger in
    /// 01_TOSSZONE_Main.
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

            PortalReadyGate gate = PortalReadyGate.Instance;
            if (gate == null || !gate.AllReady)
            {
                Debug.Log("[Portal] Chưa đủ mọi người bấm SẴN SÀNG — không teleport.");
                return;
            }

            // LoadScene is master-only (FusionNet guards it) — a NON-master walking through gets refused, and
            // burning _used on that refusal would permanently disarm this client's portal. Only latch after a
            // successful request; non-master walk-throughs stay retriable no-ops (they ride along automatically
            // when the master loads).
            if (net.LoadScene(_arenaSceneIndex)) _used = true;
            Debug.Log("[Portal] Arena load requested (accepted=" + _used + ", master-only).");
#endif
        }
    }
}
