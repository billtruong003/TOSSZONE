using UnityEngine;
#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Combat;
#endif

namespace TossZone.Network
{
    /// <summary>
    /// Master-only, spawns the <see cref="PortalReadyGate"/> once per hub visit. Hub scene NetworkObjects
    /// stay dormant (no Fusion scene load here — see TrainingRangeController's identical gotcha), so the
    /// gate has to be runtime-spawned from a prefab instead of scene-placed.
    /// </summary>
    public class PortalReadyBootstrap : MonoBehaviour
    {
#if PHOTON_FUSION
        [SerializeField] private NetworkObject _gatePrefab;
        [SerializeField] private Transform _gateAnchor;

        private bool _spawned;

        private void Update()
        {
            if (_spawned || !Bill.IsReady) return;

            NetworkRunner runner = FusionNet.Instance != null ? FusionNet.Instance.Runner : null;
            // Same window-guard as TrainingRangeController: Runner.IsRunning flips true a few frames before
            // Simulation can allocate ids — gate on the local avatar existing (same Runner.Spawn path) as
            // proof the window has passed.
            if (runner == null || !runner.IsRunning || PlayerCombat.Local == null) return;
            if (!runner.IsSharedModeMasterClient) { _spawned = true; return; }   // master's spawn replicates over

            _spawned = true;
            if (_gatePrefab == null) return;
            Vector3 pos = _gateAnchor != null ? _gateAnchor.position : transform.position;
            Quaternion rot = _gateAnchor != null ? _gateAnchor.rotation : transform.rotation;
            runner.Spawn(_gatePrefab, pos, rot, PlayerRef.None);
        }
#endif
    }
}
