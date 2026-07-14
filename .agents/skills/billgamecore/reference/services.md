# BillGameCore services — complete reference

Source: `Assets/BillGameCore/Runtime/`. Namespace `BillGameCore`. Access every service via the `Bill` facade. Interfaces in `Infrastructure/Interfaces.cs`.

## Bill facade map
`Bill.Tween` `Bill.Scene` `Bill.Pool` `Bill.Audio` `Bill.Save` `Bill.UI` `Bill.Timer` `Bill.Config` `Bill.Events` `Bill.Net` `Bill.State` · `Bill.IsReady` · `Bill.Trace` · (dev only) `Bill.Cheat` `Bill.Debug` `Bill.Analytics`.

## Pool — `Bill.Pool` (`IPoolService`)
String-keyed GameObject pool. Auto-loads `Resources/Pools/<key>` if the key isn't registered.
```csharp
GameObject Spawn(key) | Spawn(key,pos,rot) | Spawn(key,parent) | Spawn(key,pos,rot,parent)
T Spawn<T>(key) | Spawn<T>(key,pos,rot)            // where T : Component
void Return(go) | Return(go, delay)
void ReturnAll(key) | ReturnAll()
void WarmUp(key, count)
void Register(key, prefab, warmCount = 5)
int GetPooledCount(key) | GetActiveCount(key); string GetStats()
```
- **`PooledObject`** base MonoBehaviour: override `OnSpawnedFromPool()` / `OnReturnedToPool()` (reset state here); helpers `ReturnToPool()` / `ReturnToPool(delay)`. A `PooledObject` is auto-added to spawned instances if absent.
- Extension: `go.ReturnToPool()`, `go.ReturnToPool(delay)`, `component.ReturnToPool()`.
- Register defaults in `BillBootstrapConfig.defaultPools` (`PoolDefinition`: `key, prefab, warmCount, maxSize` (0 = unbounded), `autoReturnTime` (0 = off)).

## Timer — `Bill.Timer` (`ITimerService`)
```csharp
TimerHandle Delay(seconds, cb) | Delay(seconds, cb, unscaled)
TimerHandle Repeat(interval, cb) | Repeat(interval, cb, count)   // count = -1 infinite
void Cancel(handle) | CancelAll(); int ActiveCount
```
`TimerHandle`: `.Cancel()`, `.IsActive`, `.IsCancelled`. (For one-shot callbacks you can also use `BillTween.DelayedCall` or `CoroutineRunner.RunDelayed`.)

## Scene — `Bill.Scene` (`ISceneService`)
```csharp
void Load(name) | Load(name, TransitionType, dur = 0.5f) | Load(name, TransitionType, dur, EaseType) | Load(buildIndex)
void LoadAdditive(name, onComplete=null) | Unload(name, onComplete=null) | UnloadAllAdditive() ; bool IsAdditiveLoaded(name)
void LoadAsync(name, onProgress=null, onComplete=null)
void LoadWithTransition(name, TransitionType, dur, EaseType, onProgress=null, onComplete=null)
void Reload() | LoadNext() | LoadPrevious()
// props: CurrentSceneName, CurrentBuildIndex, IsLoading, LoadedAdditiveScenes
```
`TransitionType`: `None`, `Fade`, `CrossFade`. Fires `SceneLoadStartEvent` / `SceneLoadCompleteEvent`.

## Audio — `Bill.Audio` (`IAudioService`)
```csharp
void Play(key) | Play(key, pos) | Play(key, volume) | Play(key, pos, volume)
void PlayMusic(key) | PlayMusic(key, fadeDuration) | StopMusic(fadeDuration = 0)
void SetVolume(AudioChannel, v) | float GetVolume(AudioChannel) | Mute(AudioChannel) | Unmute(AudioChannel)
```
`AudioChannel`: `Master, Music, SFX, UI, Voice`. Keys resolve from an **`AudioLibrary`** ScriptableObject (entries: `key, clip, volume, pitch, loop, pitchVariation`) assigned in `BillBootstrapConfig.defaultAudioLibrary`.

## Save — `Bill.Save` (`ISaveService`)
Slot-prefixed PlayerPrefs (`s{slot}_{key}`).
```csharp
Set(key, string|int|float|bool) ; Set<T>(key, T value)   // T serialized as JSON
GetString/GetInt/GetFloat/GetBool(key, fallback) ; T Get<T>(key)
bool Has(key) ; Delete(key) ; SetSlot(int) ; Flush()
```

## Config — `Bill.Config` (`IConfigService`)
Loads `GameConfigAsset`(s) from `Resources/Configs`. Remote overrides local.
```csharp
string Get(key, fb="") ; GetInt/GetFloat/GetBool(key, fb) ; Set(key, val) ; bool Has(key) ; ApplyRemote(Dictionary<string,string>)
```

## Events — `Bill.Events` (`IEventBus`)
Events are `struct … : IEvent`. Channels are **static per type** → always unsubscribe.
```csharp
Subscribe<T>(Action<T>) ; SubscribeOnce<T>(Action<T>) ; Unsubscribe<T>(Action<T>)
Fire<T>(T data) ; Fire<T>()   // parameterless for struct events
```
Built-in: `GameReadyEvent`, `AppPauseEvent{IsPaused}`, `SceneLoadStartEvent{SceneName}`, `SceneLoadCompleteEvent{SceneName}`, `StateChangedEvent{From,To}`, `NetworkPhaseChangedEvent{Phase}`, `ConfigRefreshedEvent`.

## State machine — `Bill.State` (`GameStateMachine`)
```csharp
AddState<T>() | AddState<T>(instance) ; GoTo<T>() | GoTo(Type) | GoBack()
bool IsInState<T>() ; T GetState<T>() ; GameState Current/Previous ; string CurrentName ; History
OnEnter<T>(Action) ; OnExit<T>(Action) ; OnTransition(Action<GameState,GameState>) ; string GetHistoryLog()
```
`GameState` base: `Enter()/Tick(dt)/Exit()/Name`. Built-in states (registered at boot): `BootState, MenuState, LoadingState, GameplayState, PauseState` (sets `Time.timeScale=0` on enter, restores on exit), `GameOverState`. Define your own `class XState : GameState` and `Bill.State.AddState<XState>()`. Fires `StateChangedEvent` on every transition.

## Network — `Bill.Net` (`INetworkService`)
Adapter pattern. `OfflineAdapter` is the default; `FusionNetworkAdapter` is referenced behind `#if PHOTON_FUSION` but **not implemented yet** (must be written to use Photon Fusion).
```csharp
bool IsConnected/IsOffline/IsHost ; NetworkMode Mode ; int PlayerCount
CreateRoom(id, max=8, ok=null, fail=null) ; JoinRoom(id, ok=null, fail=null) ; LeaveRoom(done=null)
CycleHandler Cycle ; void SetAdapter(INetworkAdapter)
```
`NetworkMode`: `Offline, FusionHost, FusionShared, FusionClient, FusionAutoHostOrClient` (configure via `BillBootstrapConfig.defaultNetworkMode`).
`CycleHandler`: `Phase`, `event OnPhaseChanged`, `StartCycle(roomId, max=8)`, `StartPlaying()`, `EndSession()`; phases `Disconnected → Connecting → InLobby → InRoom → Playing → Disconnecting`; each fires `NetworkPhaseChangedEvent`.
**To add Fusion (Shared Mode):** install Fusion 2 → add `PHOTON_FUSION` define → implement `class FusionNetworkAdapter : INetworkAdapter` → set mode `FusionShared`.

`SyncList<T>` / `SyncState<T>` (`Runtime/Network/NetworkService.cs`): observable containers with `OnChanged` / `Bind` (and `SyncState.Value`). They notify local listeners only — wire them to the transport yourself; they are **not** automatic replication.

## UI — `Bill.UI` (`IUIService`) — screen-space UI Toolkit
```csharp
T Open<T>() | Open<T>(Action<T> setup) ; Close<T>() ; CloseAll() ; Toggle<T>() ; bool IsOpen<T>() ; AnyOpen()   // T : BasePanel, new()
```
`BasePanel` (UI Toolkit): override `Build(VisualElement root)`, `OnOpened()/OnClosed()`. The service auto-creates a screen-space `UIDocument` (1920×1080, ScaleWithScreenSize). **Note (VR):** this is a flat screen overlay — for ThrowingShot's world-space VR menus you generally won't use `Bill.UI`; build world-space canvases/3D interactables instead.

## Bootstrap config — `BillBootstrapConfig` (ScriptableObject in `Resources/`)
Create via **BillGameCore ▸ Bootstrap Config**. Fields: `enforceBootstrapScene`, `defaultGameScene`, `returnToEditSceneInEditor`; dev `includeDebugOverlay/includeCheatConsole/showOverlayOnStartup/enableTracing`; `defaultNetworkMode`; `defaultPools[]`; `defaultAudioLibrary` + `masterVolume/musicVolume/sfxVolume`; `targetFrameRate`, `vSyncCount`.

## Utilities — `BillExtensions` (`Runtime/Utils/Extensions.cs`)
- Transform: `DestroyAllChildren()`, `ResetLocal()`, `SetX/SetY/SetZ(float)`.
- GameObject: `GetOrAdd<T>()`, `Has<T>()`, `ReturnToPool([delay])`.
- Collections: `list.Random()`, `list.Shuffle()`, `list.SafeGet(i, fb)`, `collection.IsNullOrEmpty()`.
- Vector3: `Flat()` (zero Y), `WithY(y)`, `a.FlatDistance(b)` (XZ distance).

## Debug — `Bill.Trace`
`Print()` (dependency report), `Log(n)` (recent access log), `HealthCheck()` (live/dead services), `Unused()` (dead services), `Enabled`. Dev cheat console commands registered at boot: `trace, health, log, unused, states`.
