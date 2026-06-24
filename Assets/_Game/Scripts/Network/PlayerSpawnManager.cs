#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Player;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Gorilla-Tag-style join: once BillGameCore is ready, connects to the shared session and spawns
    /// the LOCAL player (a unified NetworkPlayer rig) if one doesn't already exist. Put one in each
    /// scene the player should appear in (lobby + arena) and position it at the desired spawn spot.
    /// If the player persisted from a previous scene, it is reused (no duplicate).
    /// </summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private string _sessionName = "TOSSZONE_DEMO";

        private bool _initialized;

        private void OnEnable() => TryInit();

        // Bootstrap may not be finished when this scene's objects enable (e.g. Play from any scene),
        // so poll until Bill is ready before touching Bill.Events.
        private void Update()
        {
            if (!_initialized) TryInit();
        }

        private void OnDisable()
        {
            if (!_initialized || !Bill.IsReady) return; // EventBus may be gone on Play-stop
            Bill.Events.Unsubscribe<FusionConnectedEvent>(OnConnected);
            Bill.Events.Unsubscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
        }

        private void TryInit()
        {
            if (_initialized || !Bill.IsReady) return;
            _initialized = true;
            Bill.Events.Subscribe<FusionConnectedEvent>(OnConnected);
            Bill.Events.Subscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
            Connect();
        }

        private void Connect()
        {
            FusionNet net = FusionNet.GetOrCreate();
            if (net.IsRunning || net.IsConnecting)
            {
                TrySpawn();
                return;
            }
            net.StartShared(_sessionName); // join the shared session; stay in this scene
        }

        private void OnConnected(FusionConnectedEvent _) => TrySpawn();
        private void OnSceneLoaded(FusionSceneLoadDoneEvent _) => TrySpawn();

        private void TrySpawn()
        {
            FusionNet net = FusionNet.Instance;
            if (net == null || !net.IsRunning || _playerPrefab == null) return;
            if (net.TryGetPlayerObject(net.LocalPlayer, out _)) return; // already have a player

            NetworkObject obj = net.Spawn(
                _playerPrefab, transform.position, transform.rotation, net.LocalPlayer, OnBeforeSpawned);
            if (obj == null) return;

            net.SetPlayerObject(net.LocalPlayer, obj);
            Debug.Log("[PlayerSpawn] Spawned local player at " + transform.position);
        }

        private static void OnBeforeSpawned(NetworkRunner runner, NetworkObject obj)
        {
            NetworkPlayerRig rig = obj.GetComponent<NetworkPlayerRig>();
            if (rig != null) rig.ColorIndex = Random.Range(0, NetworkPlayerRig.ColorCount);
        }
    }
}
#endif
