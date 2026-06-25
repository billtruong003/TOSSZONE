#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Player;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Gorilla-Tag-style join: once BillGameCore is ready, connects to the shared session and spawns the
    /// LOCAL player's thin <see cref="NetworkAvatar"/> if one doesn't already exist. The heavy AutoHand rig
    /// (camera + toon hands) is a SEPARATE local-only <see cref="PlayerRig"/> in the scene that the spawned
    /// avatar follows; it is never networked. Put one PlayerSpawnManager in each scene the player should
    /// appear in (lobby + arena), positioned at the desired spawn spot. A player that persisted from a
    /// previous scene is reused (no duplicate).
    /// </summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        [Tooltip("Thin NetworkAvatar prefab (NOT the local AutoHand rig).")]
        [SerializeField] private NetworkObject _avatarPrefab;
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
            if (net == null || !net.IsRunning || _avatarPrefab == null) return;

            // The local avatar persists across a Single-mode networked scene load (it follows the
            // DontDestroyOnLoad PlayerRig), so a single avatar carries Main -> Arena. Fusion's player-object
            // registry does NOT survive that load, so guarding only on TryGetPlayerObject would spawn a second
            // avatar in the new scene. Guard on the live avatar instead, and re-register it if the registry lost it.
            if (NetworkAvatar.Local != null)
            {
                if (!net.TryGetPlayerObject(net.LocalPlayer, out _))
                    net.SetPlayerObject(net.LocalPlayer, NetworkAvatar.Local.Object);
                return;
            }
            if (net.TryGetPlayerObject(net.LocalPlayer, out _)) return; // already have a player

            if (PlayerRig.Local == null)
                Debug.LogWarning("[PlayerSpawn] No local PlayerRig found — the avatar will spawn but won't follow you. " +
                                 "Add an AutoHand rig with a PlayerRig component to the scene.");

            NetworkObject obj = net.Spawn(
                _avatarPrefab, transform.position, transform.rotation, net.LocalPlayer, OnBeforeSpawned);
            if (obj == null) return;

            net.SetPlayerObject(net.LocalPlayer, obj);
            Debug.Log("[PlayerSpawn] Spawned local avatar at " + transform.position);
        }

        private static void OnBeforeSpawned(NetworkRunner runner, NetworkObject obj)
        {
            NetworkAvatar avatar = obj.GetComponent<NetworkAvatar>();
            if (avatar != null) avatar.ColorIndex = Random.Range(0, NetworkAvatar.ColorCount);
        }
    }
}
#endif
