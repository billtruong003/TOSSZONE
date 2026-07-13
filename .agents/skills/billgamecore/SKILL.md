---
name: billgamecore
description: How to write gameplay C# in the ThrowingShot Unity project using the BillGameCore framework (Bill.* services, BillTween tweening — NEVER DOTween, object pooling, EventBus, GameStateMachine, network adapter) and BillInspector (Odin-style) attributes. Use whenever creating or editing C# scripts, tweens/animations, ScriptableObject data, scene flow, pooling, timers, audio, or networking in this project.
---

# BillGameCore — coding guide for ThrowingShot

`Assets/BillGameCore` is a **code-first, zero-config Unity 6 framework** (`com.bill.gamecore` v3.0.0). It auto-boots and exposes everything through a single static facade `Bill`. Write game code against `Bill.*`; never re-implement what a service already does.

> This is the canonical "how we code" reference. Deep tables live in `reference/` — read them when you need exact signatures or the code style:
> [reference/conventions.md](reference/conventions.md) · [reference/tween.md](reference/tween.md) · [reference/services.md](reference/services.md) · [reference/billinspector.md](reference/billinspector.md) · [reference/fusion-module.md](reference/fusion-module.md)

## 🔑 Golden rules (read first)

1. **Tween = `BillTween`. NEVER use DOTween** (do not import it, do not add the package). Every animation goes through `BillTween` / `Tween` / `TweenSequence`. See [reference/tween.md](reference/tween.md).
2. **Access services via the `Bill` facade** (`Bill.Tween`, `Bill.Pool`, `Bill.Events`, …). Don't `new` services or `FindObjectOfType`. Resolution is `ServiceLocator` under the hood.
3. **No asmdef on `_Game` runtime scripts.** BillGameCore `Runtime/` has **no asmdef** → it compiles into the predefined `Assembly-CSharp`. Our gameplay scripts must also live in `Assembly-CSharp` (i.e. **do not** add an `.asmdef` under `Assets/_Game/Scripts/`) or they lose access to `BillGameCore`. `BillInspector.Runtime` is a separate auto-referenced asmdef, so its attributes are still usable from `Assembly-CSharp`.
4. **Pool anything spawned frequently** (projectiles, VFX, rings) with `Bill.Pool`. Never `Instantiate`/`Destroy` in hot paths.
5. **Events are `struct … : IEvent`.** Fire/subscribe via `Bill.Events`. Always `Unsubscribe` (EventBus channels are static and persist).
6. **Author data as ScriptableObject + BillInspector attributes** (weapon tables, buff-ring matrices). See [reference/billinspector.md](reference/billinspector.md).
7. **Guard very-early access** with `Bill.IsReady` / `GameReadyEvent` (pattern below).
8. **Namespaces:** `using BillGameCore;` (framework) and `using BillInspector;` (attributes).

## 📐 Code conventions (TOSSZONE) — full doc in reference/conventions.md

Mandatory for all code under `Assets/_Game/`:
- **Clear declarations:** `PascalCase` types/public/const, `camelCase` locals, **`_camelCase` private fields**, `[SerializeField] private` for inspector data. `var` only when the type is obvious; no magic numbers; namespace `TossZone.<Feature>`; one type per file.
- **Public up, private down:** fields grouped at top (config → state), then public properties/events → public methods → **private helpers last**. See the class template in conventions.md.
- **Zero-GC (VR):** no LINQ / string-interp / capturing-lambdas / boxing / `Instantiate` / `GetComponent` in per-frame hot paths. Use `for` loops, `Bill.Pool`, NonAlloc physics, cached refs, `struct` events. Target **0 B/frame** GC in the Profiler.
- **Simplicity:** one job per function (≤ ~30 lines), guard clauses over nesting, extract named bools (`if (CanFire())`), split multi-responsibility classes.
- **Formatting:** Allman braces, 4-space indent, expression-bodied one-liners. Fail loud in editor (`Debug.LogError`); always `Unsubscribe` events.

## Architecture in 60 seconds

- **Auto-bootstrap** (`Bootstrap/Bill.cs` → `BillBootstrap`): runs via `[RuntimeInitializeOnLoadMethod]` (Before/AfterSceneLoad). No manual setup. It:
  - Reads `BillBootstrapConfig` from a **`Resources/`** folder (asset named `BillBootstrapConfig`; create via menu **BillGameCore ▸ Bootstrap Config**). If missing → hard error.
  - Optionally forces **Scene 0 = the bootstrap scene** (`enforceBootstrapScene`). In the editor it jumps to scene 0, boots, then returns to the scene you pressed Play in (`returnToEditSceneInEditor`).
  - Creates a `DontDestroyOnLoad` `[BillGameCore]` root + a `CoroutineRunner` MonoBehaviour whose `Update`/`LateUpdate` call `ServiceLocator.TickAll/LateTickAll`. **This tick is what drives `Bill.Tween`, `Bill.Timer`, the state machine, etc.** Nothing animates if bootstrap didn't run.
  - Registers services (EventBus → Config, Save, Timer, Tween, Audio, Pool, UI, Scene → GameStateMachine → Network → dev tools), fires `GameReadyEvent`, then loads `defaultGameScene`.
- **ServiceLocator** (`Infrastructure/ServiceLocator.cs`): `Register`, `Get<T>`, `TryGet<T>`, `Has<T>`. Auto-calls `Initialize()` (`IInitializable`) and `Cleanup()` (`IDisposableService`); auto-adds `ITickable`/`ILateTickable` to the tick lists. Built-in **dependency tracing** — use `Bill.Trace.Print()`, `.HealthCheck()`, `.Unused()` when a service is missing/misbehaving.
- **`Bill` facade** (`Bootstrap/Bill.cs`): `Tween, Scene, Pool, Audio, Save, UI, Timer, Config, Events, Net, State` + `IsReady` + `Trace`. Dev-only: `Cheat, Debug, Analytics` (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

### Early-access guard (use in Scene-0 / very early scripts)
```csharp
void Start()
{
    if (!Bill.IsReady) { Bill.Events.Subscribe<GameReadyEvent>(OnReady); return; }
    Init();
}
void OnReady(GameReadyEvent _) { Bill.Events.Unsubscribe<GameReadyEvent>(OnReady); Init(); }
```

## Tween — the essentials (full API in reference/tween.md)

`BillTween` is a **pooled, zero-alloc, float-based** tweener with 31 eases, loops, and sequences. Returns a `Tween` (nullable — use `?.` because it's null until bootstrap is ready).

```csharp
using BillGameCore;

// Static facade
BillTween.MoveY(t, 3f, 1f)?.SetEase(EaseType.OutBack).SetTarget(this);
BillTween.Fade(canvasGroup, 0f, 0.5f)?.SetEase(EaseType.InQuad);
BillTween.Scale(t, 1.2f, 0.2f)?.SetLoops(-1, LoopType.Yoyo);   // pulse forever
BillTween.DelayedCall(0.4f, () => Fire());                      // delay callback

// Fluent: SetEase / SetDelay / SetLoops(count,LoopType) / SetUnscaled / SetTarget / OnStart / OnUpdate / OnComplete
// LoopType: Restart | Yoyo | Incremental.  count: 0 once, -1 infinite, N repeat N more.

// Extension form: transform.TweenMoveX(5f,1f).SetEase(EaseType.OutBack);

// Kill: BillTween.Kill(tween) | KillTarget(obj) | KillAll() | CompleteAll()
```

**Projectile / arc / bounce path (Phase 1 throw mechanic — the right pattern):** `BillTween.Move(Vector3)` only joins 3 *linear* axis tweens. For a controlled arc with 1–2 bounces, **tween a normalized `t` and compute the position yourself** — this gives exact control (the whole reason we avoid physics):
```csharp
// In a pooled projectile (PooledObject). Linear t; the curve encodes the motion.
BillTween.Float(0f, 1f, flightTime, t => transform.position = arc.Evaluate(t))
    ?.SetEase(EaseType.Linear)
    .SetTarget(this)
    .OnComplete(OnLanded);
// arc.Evaluate(t): your parabola/bezier with bounce points → designer-tunable, deterministic.
```

## Services cheat-sheet (full signatures in reference/services.md)

```csharp
Bill.Pool.Spawn("projectile", pos, rot);   // string-key pool; <T> overloads; auto-loads Resources/Pools/<key>
go.ReturnToPool(2f);                         // extension; or Bill.Pool.Return(go, delay)
Bill.Timer.Delay(0.4f, Fire);                // → TimerHandle (.Cancel()); Repeat(interval,cb,count)
Bill.Scene.Load("01_TOSSZONE_Main", TransitionType.Fade, 0.5f);  // + Async/Additive/Reload/LoadNext
Bill.Audio.Play("throw");                    // keys from AudioLibrary; PlayMusic/StopMusic/SetVolume(AudioChannel,..)
Bill.Save.Set("coins", 100); Bill.Save.GetInt("coins");          // slot-prefixed PlayerPrefs; Set<T> = JSON
Bill.Config.GetFloat("gravity", 9.8f);       // GameConfigAsset in Resources/Configs
Bill.Events.Fire(new PlayerDiedEvent{ Id = id });                // struct : IEvent
Bill.State.GoTo<GameplayState>();            // Boot/Menu/Loading/Gameplay/Pause/GameOver built in
Bill.Net.Cycle.StartCycle(roomId);           // network lifecycle (see Networking)
```

**Pooled objects:** subclass `PooledObject` and override `OnSpawnedFromPool()` / `OnReturnedToPool()` to reset state. Register pools in `BillBootstrapConfig.defaultPools` or `Bill.Pool.Register(key, prefab, warm)`.

**Events:** define and use
```csharp
public struct PlayerDiedEvent : IEvent { public int Id; }
Bill.Events.Subscribe<PlayerDiedEvent>(OnDeath);   // remember Unsubscribe in OnDisable/OnDestroy
```
Built-in events: `GameReadyEvent`, `AppPauseEvent{IsPaused}`, `SceneLoadStartEvent/SceneLoadCompleteEvent{SceneName}`, `StateChangedEvent{From,To}`, `NetworkPhaseChangedEvent{Phase}`, `ConfigRefreshedEvent`.

**State machine:** built-in states are registered at boot. Add your own:
```csharp
public class MatchmakingState : GameState { public override void Enter() {/*…*/} public override void Tick(float dt){} }
Bill.State.AddState<MatchmakingState>();      // then Bill.State.GoTo<MatchmakingState>();
Bill.State.OnEnter<GameplayState>(() => …);   // hooks: OnEnter/OnExit/OnTransition
```

## Networking — adapter pattern (important for Phase 1: Fusion **Shared Mode**)

`Bill.Net` (`INetworkService`) wraps an `INetworkAdapter`. `OfflineAdapter` is the default (single-player null-object). A full **Fusion 2 module is now implemented** at `Runtime/Network/Fusion/` (`FusionNet` + `FusionNetworkAdapter` + `FusionEvents`) — see [reference/fusion-module.md](reference/fusion-module.md). To activate it:

1. Add the **`PHOTON_FUSION`** scripting define (Fusion 2.0.12 is already installed; its own `FUSION2` define is not enough — the gate is `PHOTON_FUSION`).
2. Set `BillBootstrapConfig.defaultNetworkMode = FusionShared` → boot uses `FusionNetworkAdapter`.

Two access paths: **`Bill.Net`** for the simple contract; **`FusionNet.Instance`** for the full API (scene-aware connect, spawn, authority, per-callback events) — e.g. `FusionNet.Instance.StartShared(session, arenaSceneIndex)`. Matchmaking via `Bill.Net.Cycle` (`CycleHandler`): `StartCycle(roomId, max)` → phases `Connecting → InRoom → Playing → Disconnecting → Disconnected`, each firing `NetworkPhaseChangedEvent`. `NetworkMode`: `Offline, FusionHost, FusionShared, FusionClient, FusionAutoHostOrClient`.

`SyncList<T>` / `SyncState<T>` are **observable containers** (`.OnChanged`, `.Bind`) — local change-notification helpers you wire to the transport; they are not automatic network replication.

## Authoring data — BillInspector (full catalog in reference/billinspector.md)

Use ScriptableObjects + BillInspector attributes for all designer data (weapon stats, buff-ring tier matrix, arena config). Attributes work on **any** `MonoBehaviour`/`ScriptableObject` that has ≥1 Bill attribute (a custom editor replaces the default; otherwise you get the stock inspector). Inherit `BillSerializedMonoBehaviour` / `BillSerializedScriptableObject` **only** when you need `Dictionary`/`HashSet`/`Tuple` serialized.

```csharp
using BillInspector;
[CreateAssetMenu(menuName = "TOSSZONE/Weapon")]
public class WeaponConfig : ScriptableObject
{
    [BillTitle("Weapon")] [BillRequired] public string id;
    [BillSlider(0, 20)] public int price;
    [BillSuffix("s")] [BillMinMaxSlider(0.1f, 3f)] public Vector2 cooldown;
    [BillInfoBox("AoE radius in meters")] public float aoe;
    [BillButton("Validate")] void Validate() { /* … */ }   // inspector button
}
```
Common attributes: layout `BillTitle/BillBoxGroup/BillFoldoutGroup/BillTabGroup/BillHorizontalGroup`, value `BillSlider/BillMinMaxSlider/BillProgressBar/BillDropdown/BillTableList`, meta `BillShowIf/BillHideIf/BillEnableIf/BillReadOnly/BillRequired/BillInfoBox/BillOnValueChanged`, actions `BillButton`. Full list in reference.

## Project recipes (map to the roadmap)

- **Throw projectile (P1):** `Bill.Pool.Spawn("projectile")` → `BillTween.Float(0,1,flightTime, t => pos = arc.Evaluate(t))` → `OnComplete` → `go.ReturnToPool()`. Haptics/cooldown via `Bill.Timer`. SFX via `Bill.Audio.Play`.
- **Scene flow (P1):** `00_Bootstrap` = Scene 0 (with `BillStartup` splash + `BillBootstrapConfig` in Resources) → `Bill.Scene.Load("01_TOSSZONE_Main", TransitionType.Fade)`; model phases with `GameStateMachine` (add `MenuState`-style states like `MatchmakingState`).
- **Matchmaking (P1):** implement `FusionNetworkAdapter` (Shared Mode) → `Bill.Net.Cycle.StartCycle(...)`; UI reacts to `NetworkPhaseChangedEvent`; 30 s timeout via `Bill.Timer.Delay`.
- **Buff rings / weapons (P2–P3):** ScriptableObject tables with BillInspector (`BillTableList` for the tier matrix); ring drift + buff stacking via `BillTween`; pool rings & projectiles.
- **Splash/loading screen:** reuse/copy `BillStartup` (`Runtime/Bootstrap/BillStartup.cs`) — logo tween + progress steps via `AddStep`/`AddStepAsync`.

## Gotchas

- A `[Bill] SERVICE NOT FOUND` error → bootstrap didn't run (Scene 0 not the bootstrap scene, or you accessed a service before `GameReadyEvent`). Run `Bill.Trace.HealthCheck()`.
- Tween/Timer "not animating" → same root cause (no `CoroutineRunner` tick) — ensure BillBootstrap booted.
- Don't add DOTween (rule #1). Don't add an asmdef under `_Game/Scripts` (rule #3).
- `Bill.Cheat/Debug/Analytics` only exist in editor/dev builds.
- `DynamicAnimationEventHub` (`Runtime/Utils/DynamicEventHub.cs`) is a **global-namespace** component (string→UnityEvent map, `Trigger(id)`) — handy for animation-event wiring.
```
