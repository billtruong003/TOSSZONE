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

        private bool _initialized;

        // Guards the async gap between net.Spawn() returning and NetworkAvatar.Spawned() setting Local. Static so
        // it spans the Main->Arena transition, where a fresh PlayerSpawnManager instance takes over spawning.
        // BUG-NET-SPAWN-001: this used to be a plain static bool that (a) was never cleared when a spawn settled
        // without a follow-up TrySpawn event, and (b) survived runner shutdown + auto-reconnect, silently blocking
        // every spawn in the new session. Track the pending spawn result AND the runner it belongs to instead, so
        // a pending entry from a dead runner/session is detected as stale and ignored.
        private static NetworkObject _pendingAvatar;
        private static NetworkRunner _pendingRunner;

        private float _nextRetryTime;


        private void OnEnable() => TryInit();

        // Bootstrap may not be finished when this scene's objects enable (e.g. Play from any scene),
        // so poll until Bill is ready before touching Bill.Events.
        private void Update()
        {
            if (!_initialized)
            {
                TryInit();
                return;
            }
            // BUG-NET-SPAWN-001 (missing re-trigger): after a cloud kick + auto-reconnect, the first join's
            // OnConnected/OnSceneLoaded events are long gone and nothing calls TrySpawn() for the new session.
            // Throttled idempotent poll as a safety net — every early-out in TrySpawn is cheap.
            if (Time.unscaledTime >= _nextRetryTime)
            {
                _nextRetryTime = Time.unscaledTime + 0.5f;
                TrySpawn();
            }
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
            // The splash (StartupConnectStep) owns the real connect. This is the fallback for editor
            // direct-play in hub/arena, where the bootstrap returns to the edited scene without the splash.
            ConnectionFlowController.GetOrCreate().EnsureConnected();
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
            // Reuse the surviving local avatar. It is DontDestroyOnLoad, so it carries across a Single-mode load;
            // treat a stale/invalid reference as "gone" so a genuinely despawned avatar can still respawn.
            NetworkAvatar local = NetworkAvatar.Local;
            if (local != null && local.Object != null && local.Object.IsValid)
            {
                ClearPending(); // the avatar is settled; clear any pending record
                if (!net.TryGetPlayerObject(net.LocalPlayer, out _))
                    net.SetPlayerObject(net.LocalPlayer, local.Object);
                return;
            }

            // A spawn is already pending (net.Spawn returned but Spawned() hasn't set Local yet). TrySpawn is driven
            // from BOTH OnConnected and OnSceneLoaded, which fire together on scene entry — without this guard both
            // calls spawn before Local is set, producing two overlapping avatars. A pending record from another
            // runner (old session after kick/reconnect) or a despawned result is stale and must NOT block.
            if (IsSpawnPending(net)) return;

            if (net.TryGetPlayerObject(net.LocalPlayer, out _)) return; // already have a player

            if (PlayerRig.Local == null)
                Debug.LogWarning("[PlayerSpawn] No local PlayerRig found — the avatar will spawn but won't follow you. " +
                                 "Add an AutoHand rig with a PlayerRig component to the scene.");

            _pendingRunner = net.Runner; // mark pending for THIS session before the (synchronous) Spawn call
            _pendingAvatar = null;
            NetworkObject obj;
            try
            {
                obj = net.Spawn(
                    _avatarPrefab, transform.position, transform.rotation, net.LocalPlayer, OnBeforeSpawned);
            }
            catch (System.Exception e)
            {
                // Session-12 gotcha: IsRunning flips true before the simulation can assign ids, and Spawn in
                // that window throws. Leaving the STATIC record latched here would block avatar spawning for the
                // rest of the session — clear it so the next OnConnected/OnSceneLoaded/retry-poll retries.
                ClearPending();
                Debug.LogError("[PlayerSpawn] Spawn threw (will retry on next connect/scene event): " + e.Message);
                return;
            }
            if (obj == null) { ClearPending(); return; }

            _pendingAvatar = obj;
            net.SetPlayerObject(net.LocalPlayer, obj);
            Debug.Log("[PlayerSpawn] Spawned local avatar at " + transform.position);
        }

        // BUG-NET-SPAWN-001: a pending spawn only counts while it belongs to the CURRENT runner and its spawn
        // result is still alive (Spawn mid-call, or spawned object waiting for NetworkAvatar.Spawned() to set
        // Local). Anything else — old runner after shutdown/reconnect, despawned result — is stale: clear it so
        // the caller may spawn again.
        private static bool IsSpawnPending(FusionNet net)
        {
            if (_pendingRunner == null) return false;
            if (_pendingRunner != net.Runner)
            {
                ClearPending(); // record from a previous session/runner — never block the new one
                return false;
            }
            if (_pendingAvatar == null) return true;            // net.Spawn is mid-call this frame
            if (_pendingAvatar.IsValid) return true;             // result alive, waiting to settle as Local
            ClearPending();                                      // result was despawned — allow respawn
            return false;
        }

        private static void ClearPending()
        {
            _pendingRunner = null;
            _pendingAvatar = null;
        }

        private static void OnBeforeSpawned(NetworkRunner runner, NetworkObject obj)
        {
            NetworkAvatar avatar = obj.GetComponent<NetworkAvatar>();
            if (avatar != null) avatar.ColorIndex = Random.Range(0, NetworkAvatar.ColorCount);
        }
    }
}
#endif
