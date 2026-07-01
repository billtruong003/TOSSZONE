#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Dev/robustness gate for a networked minigame scene. When this scene is reached WITHOUT a Fusion scene
    /// load — e.g. the BillGameCore dev bootstrap returns you here via a plain <c>SceneManager.LoadScene</c>, or
    /// you Play the scene directly — the master client starts Fusion with an EMPTY scene, so the pre-placed
    /// scene NetworkObjects (ArenaManager, DummyAvatar, RingSpawner, rings) are never attached and stay dormant
    /// (<c>IsValid=false, Id=None</c>). This gate detects that and issues ONE Fusion <see cref="FusionNet.LoadScene"/>
    /// of the current scene so the master runs <c>RegisterSceneObjects</c> and everything comes alive.
    ///
    /// On the real shipping path (hub portal → <c>FusionNet.LoadScene</c>) the scene objects ARE attached — but
    /// registration finishes a beat after the scene GameObjects awake, so this gate waits a short settle window
    /// before deciding. If the sentinel becomes valid within that window (Fusion loaded us) it stays a strict
    /// no-op; only if it is STILL dormant after the window did we arrive without a Fusion load. Place ONE instance
    /// per minigame scene; it must NOT be DontDestroyOnLoad (it must die with the scene so the reload replaces it).
    /// </summary>
    public class ArenaNetworkLoadGate : MonoBehaviour
    {
        [Tooltip("Any scene NetworkObject in THIS scene (e.g. ArenaManager). Used to detect whether Fusion has " +
                 "attached this scene's objects yet. If its NetworkObject is still invalid after the settle " +
                 "window, this scene was not Fusion-loaded.")]
        [SerializeField] private NetworkObject _sentinelSceneObject;

        [Tooltip("Grace period (seconds) after connect for Fusion's RegisterSceneObjects to run before the gate " +
                 "concludes the scene is dormant. Keeps the real portal path a no-op.")]
        [SerializeField] private float _settleSeconds = 0.5f;

        private bool _warnedNoSentinel;
        private bool _loadRequested;     // set once; never reset — the Fusion reload replaces this whole object.
        private float _settleDeadline = -1f;

        private void Update()
        {
            if (_loadRequested || !Bill.IsReady) return;

            if (_sentinelSceneObject == null)
            {
                if (!_warnedNoSentinel)
                {
                    _warnedNoSentinel = true;
                    Debug.LogWarning("[ArenaNetworkLoadGate] No sentinel scene object wired — cannot detect a " +
                                     "dormant scene. Wire it to a scene NetworkObject (e.g. ArenaManager).");
                }
                return;
            }

            // Sentinel valid ⇒ Fusion already attached this scene's objects (portal path) ⇒ nothing to do, ever.
            if (_sentinelSceneObject.IsValid) return;

            FusionNet net = FusionNet.Instance;
            if (net == null || !net.IsRunning || !net.IsHost)
            {
                _settleDeadline = -1f;   // not connected as master yet; (re)start the window once we are
                return;
            }

            // Connected as master but the sentinel is still dormant. Give Fusion's RegisterSceneObjects a grace
            // window — on the portal path the sentinel flips valid within it and the check above short-circuits.
            if (_settleDeadline < 0f)
            {
                _settleDeadline = Time.realtimeSinceStartup + Mathf.Max(0f, _settleSeconds);
                return;
            }
            if (Time.realtimeSinceStartup < _settleDeadline) return;

            // Still dormant after the window ⇒ this scene was reached without a Fusion load ⇒ fix it, once.
            int buildIndex = gameObject.scene.buildIndex;
            if (buildIndex < 0) return;

            _loadRequested = true;
            Debug.Log("[ArenaNetworkLoadGate] Scene objects still dormant after settle — issuing Fusion LoadScene(" +
                      buildIndex + ") so the master attaches them.");
            net.LoadScene(buildIndex); // reloads this scene through Fusion; this gate instance is replaced afterwards
        }
    }
}
#endif
