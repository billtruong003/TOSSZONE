# Fusion module (BillGameCore × Photon Fusion 2)

Reusable Fusion 2 networking module living **inside the framework** at `Assets/BillGameCore/Runtime/Network/Fusion/`, namespace `BillGameCore`. Built against the installed **Fusion 2.0.12**. Holds **no gameplay** — it's the full networking API; game logic layers on top.

Files: `FusionNet.cs` (controller + callbacks), `FusionNetworkAdapter.cs` (INetworkAdapter bridge), `FusionEvents.cs` (IEvent structs + `FusionConnectArgs`).

## Activation
Guarded by `#if PHOTON_FUSION`. To turn it on:
1. Add scripting define **`PHOTON_FUSION`** (Player Settings → Scripting Define Symbols, all active platforms). Fusion's own `FUSION2` define is NOT enough — BillGameCore's `NetworkService` gates on `PHOTON_FUSION`.
2. Set `BillBootstrapConfig.defaultNetworkMode = FusionShared` → at boot `NetworkService` uses `FusionNetworkAdapter` instead of `OfflineAdapter`.

Without the define the whole module compiles out and the project stays on the offline adapter (no errors).

## Two access paths
- **`Bill.Net`** (`INetworkService`) — the simple, transport-agnostic contract: `CreateRoom/JoinRoom/LeaveRoom`, `IsConnected/IsHost/PlayerCount/Mode`, `Bill.Net.Cycle` phases. Good for generic flow.
- **`FusionNet.Instance`** — the full Fusion API (scene-aware connect, spawn, authority, per-callback events). Use this for gameplay (e.g. `FusionNet.Instance.StartShared(session, arenaSceneIndex)`).

## FusionNet — API surface

### State getters
`Runner` (raw), `IsRunning`, `IsConnecting`, `IsShuttingDown`, `IsConnected`, `IsOffline`, `IsServer`, `IsClient`, `IsSharedModeMasterClient`, `IsHost` (server **or** shared-master), `GameMode`, `NetworkMode`, `LocalPlayer`/`LocalPlayerId`, `ActivePlayers`, `PlayerCount`, `MaxPlayers`, `SessionName`, `Region`, `Tick`, `IsLocal(PlayerRef)`, `GetRtt(PlayerRef)`.

### Lifecycle
```csharp
FusionNet.GetOrCreate();                                  // DontDestroyOnLoad host (runner added lazily on connect)
net.Connect(FusionConnectArgs args, Action<bool> onResult);
net.StartShared(session, sceneIndex = -1, maxPlayers = 0, onResult);
net.StartHost(session, sceneIndex, maxPlayers, onResult);
net.StartClient(session, onResult);
net.StartAutoHostOrClient(session, sceneIndex, maxPlayers, onResult);
net.Shutdown(ShutdownReason reason = Ok);                 // tears down the child runner GO; host persists for reuse
```
`FusionConnectArgs { NetworkMode Mode; string SessionName; int SceneIndex; int MaxPlayers; }` — `SessionName` null/empty = random matchmaking; fixed = lobby room. `SceneIndex >= 0` loads that build-index scene on connect (master loads, clients follow).

### Scene / Spawn / Authority / Players
```csharp
net.LoadScene(int buildIndex);                            // host/master only (guarded)
net.Spawn(NetworkObject prefab, pos, rot, inputAuthority, onBeforeSpawned);  // → NetworkObject
net.Despawn(NetworkObject obj);
net.RequestAuthority(obj) / ReleaseAuthority(obj) / HasAuthority(obj);       // Shared-mode state authority
net.SetPlayerObject(player, obj) / GetPlayerObject(player) / TryGetPlayerObject(player, out obj);
```

### Events (C# `event Action…`)
`Started`, `StartFailed(string)`, `Connected`, `Disconnected(string)`, `ConnectFailed(string)`, `PlayerJoined(PlayerRef)`, `PlayerLeft(PlayerRef)`, `DidShutdown(ShutdownReason)`, `SceneLoadStarted`, `SceneLoadCompleted`, `HostMigrating(HostMigrationToken)`, `SessionListUpdated(List<SessionInfo>)`.

### Bill.Events bridge (`struct : IEvent`)
`FusionStartedEvent{Mode,Session}`, `FusionStartFailedEvent{Reason}`, `FusionConnectedEvent`, `FusionDisconnectedEvent{Reason}`, `FusionConnectFailedEvent{Reason}`, `FusionPlayerJoinedEvent{PlayerId,IsLocal}`, `FusionPlayerLeftEvent{PlayerId}`, `FusionShutdownEvent{Reason}`, `FusionSceneLoadStartEvent`, `FusionSceneLoadDoneEvent`, `FusionHostMigrationEvent`. Also drives `Bill.Net.Cycle` phases (`Connecting→InRoom→Disconnecting→Disconnected`).

## Edge cases handled
- Double connect / connect-while-running → ignored + `onResult(false)`.
- Start failure (`StartGameResult.Ok == false`) → `StartFailed` + reason + phase reset.
- Exceptions during StartGame → caught, logged, `StartFailed`.
- Disconnect / connect-fail → reason surfaced, phase → Disconnected.
- Shutdown → state reset, runner reference nulled, child runner GO destroyed (host persists for next session).
- Scene manager / object provider auto-added if missing (matches FusionBootstrap).
- LoadScene by non-authoritative peer → warned + ignored.
- Spawn/despawn before running or with null → guarded.
- Host migration → surfaced via event/token (game logic decides resume).

## ✅ Verified against official Fusion 2.0.12 API (`Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`)

| API used | Fusion.Runtime.xml | Status |
|---|---|---|
| `INetworkRunnerCallbacks` (19 methods) | `RunnerEnableVisibility.cs` reference impl | ✅ signatures match exactly |
| `StartGameArgs { GameMode, SessionName, Scene, SceneManager, ObjectProvider }` | `FusionBootstrap.cs` | ✅ |
| `StartGameArgs.PlayerCount` (max players) | F:…StartGameArgs.PlayerCount (16183) | ✅ |
| `StartGameResult.{Ok, ShutdownReason, ErrorMessage}` | P:…StartGameResult.* (16355–65) | ✅ |
| `NetworkSceneInfo.AddSceneRef(SceneRef, LoadSceneMode, …)` | M:…AddSceneRef (16798) | ✅ |
| `SceneRef.FromIndex(int)` | `FusionBootstrap.cs` | ✅ |
| `NetworkRunner.Spawn(NetworkObject, Vector3?, Quaternion?, PlayerRef?, OnBeforeSpawned, …)` | M:…Spawn (14522) | ✅ |
| `NetworkRunner.Shutdown(bool, ShutdownReason, bool)` | M:…Shutdown (13723) | ✅ named-arg call |
| `NetworkRunner.Tick` → `int` | M:Fusion.Tick.op_Implicit(Tick)~Int32 (9850) | ✅ implicit cast exists |
| `AddCallbacks(params INetworkRunnerCallbacks[])` | M:…AddCallbacks (13919) | ✅ |
| `SessionInfo.{Name,Region,PlayerCount,MaxPlayers,IsValid,IsOpen}` | P:…SessionInfo.* (1506–1541) | ✅ |
| `ActivePlayers`, `IsSharedModeMasterClient`, `SetPlayerObject/GetPlayerObject/GetPlayerRtt` | (13625, 14810, 13821–70) | ✅ |
| `NetworkObject.RequestStateAuthority/ReleaseStateAuthority` | M:… (10930/10935) | ✅ |
| `NetworkRunner.LoadScene(SceneRef, …)` | **two SceneRef overloads** (14375, 14392) | ⚠️ → **fixed**: pass explicit `LoadSceneMode.Single` to disambiguate |

**Fixes applied after verification:** (1) `LoadScene` now passes `LoadSceneMode.Single` (overloads were ambiguous); (2) `StartGameArgs.Scene` always set to a non-null `NetworkSceneInfo` (empty when no initial scene), matching `FusionBootstrap`.

Final compile check still happens in-editor (`read_console`) once `PHOTON_FUSION` is defined — but every API above is confirmed present in the installed assembly.
