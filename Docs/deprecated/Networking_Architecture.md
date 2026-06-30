# TOSSZONE — Networking architecture & plan

**Status:** DESIGN REFERENCE — the Shmackle-derived patterns + roadmap ("what to build & why"), distilled from
studying the **Shmackle** project (a shipping Fusion VR game) + BillGameCore's Fusion module. This is NOT the live
status.

> **For the LIVE state read `Docs/Network_Architecture_Lessons.md`** — what's actually implemented (S2 networked
> held-ball + S3 networked projectile shipped Session 6, via a thin `NetworkProjectile` + `NetworkAvatar.HoldingBall`),
> the bugs-and-why lessons, and the current build checklist.
>
> **Pivot note (2026-06-30):** direction changed to **minigame-loop-first**. The §7 roadmap below predates this —
> P2 (player system) / P3 (networked throw) / P4 (scale) are now **deferred**; the live priority order lives in the
> Lessons doc + `Docs/tasks.json` (section `1.MG`). §4 (minigame networking pattern) is the active reference, and
> §5 is partly superseded by the shipped S2/S3 impl (which already used NetworkTransform-style sync, not the Physics Addon).
>
> Sister doc: `Docs/AutoHand_Grab_Notes.md` (grab/held-object physics).

---

## 0. Constraints / context (the box we design inside)
- **Photon Fusion 2, Shared Mode, NO Physics Addon.** (Shmackle uses the Physics Addon — so its held/thrown
  transform sync via `NetworkRigidbody3D` is the one thing we cannot copy; everything else ports.)
- Gorilla-Tag-style **hub** (`01_TOSSZONE_Main`) + **many minigames** (arenas; `02_Arena` is the first).
- **3-point networked avatar** (head + 2 wrists). Local rig = heavy AutoHand XR rig (local-only).
- **BillGameCore** framework: gameplay rides on `Bill.*` services; networking goes through `Bill.Net` /
  `FusionNet`. Read the `billgamecore` skill before writing gameplay.
- **Multi-scene loading** (one scene per minigame).

---

## 1. CORE DECISION — runner + scene model
**One persistent Shared runner (owned by `FusionNet`) + networked scene switching. Do NOT tear the runner
down per scene** (Shmackle destroys + recreates the runner on every scene change, losing all NetworkObjects and
carrying state through a static handoff — we avoid that).

- Hub stays connected; entering a minigame = **`FusionNet.LoadScene(arenaBuildIndex)`** (Single mode, host/master
  triggers, all clients follow). The runner **survives** the load, so no reconnect/stutter and no static handoff.
- **Start with Single-mode** hub↔arena (you're either in the hub or in one arena). This fits `FusionNet` **as-is**.
- **Additive** (be in the hub AND a minigame at once, several minigames live in one session) is a **later
  extension** — it needs an additive `LoadScene` overload on `FusionNet` (§2) and careful per-scene NetworkObject
  lifecycle. Don't build it until the hub-as-shared-space genuinely needs it.

**Trade-off:** Single-mode unloads the hub while in an arena (can't see hub players from inside a minigame). That's
fine for "lobby hub → enter a match". Go additive only when minigames must coexist with a live hub.

---

## 2. BillGameCore integration — USE vs BUILD vs EXTEND
`FusionNet` (`Assets/BillGameCore/Runtime/Network/Fusion/FusionNet.cs`) already owns the `NetworkRunner` and
exposes a game-agnostic API + bridges every runner callback to `Bill.Events`. Layer game code ON TOP.

### ✅ USE the BillGameCore API (do NOT reimplement Shmackle's NetworkService/RoomFetcher)
| Need | BillGameCore |
|---|---|
| Start/join session | `FusionNet.StartShared(session, sceneIndex, maxPlayers)` (`:121`); phases via `Bill.Net.Cycle` |
| Room list / matchmaking | `FusionNet.SessionListUpdated` event (`:60`); `JoinSessionLobby(SessionLobby.Shared)` |
| Runner callbacks | All bridged to `Bill.Events`: `FusionPlayerJoinedEvent`, `FusionSceneLoadDoneEvent`, `FusionShutdownEvent`… (`:251-320`) |
| Spawn / despawn | `FusionNet.Spawn(prefab, pos, rot, inputAuth)` / `Despawn(obj)` (`:212`) |
| Shared-mode authority | `FusionNet.RequestAuthority/ReleaseAuthority/HasAuthority` (`:231`) |
| Per-player object registry | `SetPlayerObject / GetPlayerObject / TryGetPlayerObject` (`:239`) |
| Networked scene load | `FusionNet.LoadScene(buildIndex)` — host-only, Single, **runner persists** (`:199`) |
| Raw runner (advanced) | `FusionNet.Instance.Runner` (`:75`) |

### 🟦 BUILD as raw Fusion `NetworkBehaviour`s (run ON FusionNet's runner — BillGameCore doesn't provide these)
`BillNetworkPlayer` (avatar pose sync, §3), `MiniGameBase` (§4), networked grabbable authority (§5), slot opt-in
(§4), RPC relays, AOI. **Idiom:** these fire `Bill.Events` for local reactions and use `Bill.Tween`/`Bill.Audio`
for juice, so the rest of the game stays decoupled. Spawn them via `FusionNet.Spawn`.

### 🔧 EXTEND FusionNet (two small additions, only when needed)
1. **Pooled networked spawns.** `FusionNet` hard-codes `NetworkObjectProviderDefault` (`:368-373`). For pooled
   thrown balls/projectiles, swap in a custom `INetworkObjectProvider` (subclass `NetworkObjectProviderDefault`,
   override `InstantiatePrefab`/`DestroyPrefabInstance` — exactly Shmackle's `PoolNetworkObjectProvider`). This is
   Physics-Addon-independent.
2. **Additive `LoadScene`.** `FusionNet.LoadScene` is Single (`:204`). Add an additive overload only when going to
   the additive scene model (§1).

### ⚠️ Two traps
- **`Bill.Pool` does NOT pool `NetworkObject`s** — it's for local objects only (VFX, the held visual). Networked
  objects pool via the Fusion provider above.
- **`Bill.SyncList<T>` / `SyncState<T>` are NOT network replication** (local observable containers only). For
  networked roster/state use Fusion `[Networked]` / `NetworkLinkedList` / `NetworkDictionary`.

---

## 3. Player system — the `Bill.Players` upgrade (2 layers)
Goal: gameplay grabs the local player + hands instantly, Shmackle-style — but **the "local player" is a SCENE
object (one per machine), not a static set by a spawned NetworkObject** (a NetworkObject is null until Fusion
spawns it and dies on scene change; the local rig is stable from boot).

### Layer 1 — `LocalPlayer` (scene component, 1 per machine) ← the thing you reference
- The heavy local AutoHand XR rig (today: `PlayerRig`). Head + 2 hands, DontDestroyOnLoad, **always present**.
- Implements `IBillPlayer`; registers itself into `Bill.Players` on `Awake` (`BillPlayers.SetLocal(this)`).
- `Bill.Players.Local` returns THIS — value is the stable scene object, looked up via the registry (not a static
  field on a NetworkBehaviour).

### Layer 2 — `BillNetworkPlayer` (NetworkBehaviour, Fusion-spawned, 1 per player)
- Replication of head + 2 hands via the canonical Shmackle pattern: a `[Networked] struct NetPose : INetworkStruct`
  + write-on-change in `FixedUpdateNetwork` (local) + lerp in `Render` (remote). (Replaces today's `NetworkAvatar`.)
- The **local** instance (`HasInputAuthority`) reads its poses from the scene `LocalPlayer`; remotes drive the
  visual avatar.
- Registers into `Bill.Players` keyed by `PlayerRef`.

### Shared interface
```csharp
public interface IBillPlayer {
    Fusion.PlayerRef Player { get; }
    bool IsLocal { get; }
    Transform Head { get; }
    Transform HandLeft { get; }
    Transform HandRight { get; }
}
```

### Access API (`Bill.Players`, a light static registry — facade or `BillPlayers`)
```csharp
Bill.Players.Local            // scene LocalPlayer (this machine; always present once Awake ran)
Bill.Players.Local.HandRight  // the local AutoHand right hand
Bill.Players.Get(playerRef)   // a remote BillNetworkPlayer
Bill.Players.All              // IBillPlayer: 1 local (scene) + N remote (networked)
event PlayerRegistered / PlayerUnregistered
```
**Usage:** `ThrowController`/`ThrowBallHolder` drop their own rig lookups → `Bill.Players.Local.HandRight`.

> Open decisions to lock before coding: (a) confirm `Bill.Players.Local` returns the scene `LocalPlayer`;
> (b) rename `PlayerRig` → `LocalPlayer` or wrap it; (c) `Bill.Players` on the `Bill` facade vs a standalone
> `BillPlayers` static.

---

## 4. Minigame networking pattern (the core of "hub + many minigames")
**Each minigame = a `NetworkBehaviour` (`MiniGameBase`) holding `[Networked]` authoritative state owned by State
Authority.** (Shmackle's `MiniGameBase` / BoxingRing / ShootingArea managers.)

```csharp
public abstract class MiniGameBase : NetworkBehaviour {
    [Networked] public GameState State { get; set; }              // enum (Idle/Countdown/Playing/Over)
    [Networked] public float TimeRemaining { get; set; }          // only StateAuthority decrements
    [Networked, Capacity(20)] public NetworkLinkedList<PlayerRef> Roster => default;
    // lifecycle via [Rpc(RpcSources.StateAuthority, RpcTargets.All)] Rpc_StartGame() / Rpc_EndGame()
    // each client caches a local copy of State to detect transitions and fire Bill.Events for juice/UI
}
```
- **Timer:** plain `[Networked] float`, decremented only by `HasStateAuthority` in `Update`/`FixedUpdateNetwork`.
- **Start/stop:** `[Rpc(StateAuthority, All)]`.
- **Opt-in (two options):** (a) zone proximity — distance-check vs an activation centre, add to `Roster`;
  (b) **networked slot** — a `SpotPoint : NetworkBehaviour` with `[Networked] NetworkBool Occupied` + `RPC_SetOccupied`
  (Shmackle `ShootingRange/SpotPoint.cs`).
- **Scores:** Shmackle keeps them **local**. For a **shared/competitive scoreboard**, promote scores to
  `[Networked]` (Shmackle did NOT — we add it).
- **Where it lives:** in the minigame's scene (loaded via `FusionNet.LoadScene`), or spawned via `FusionNet.Spawn`.
  Fires `MinigameEnteredEvent`/`MinigameExitedEvent` (we already have these) so UI/juice react decoupled.

This sits ABOVE the existing local `MinigameManager` (`Assets/_Game/Scripts/Minigame/`), which currently only does
local scene-flow — upgrade it to drive `FusionNet.LoadScene` (networked) + spawn/despawn the `MiniGameBase`.

---

## 5. Networked grabbable / throw (no Physics Addon)
- **Grab = authority transfer:** on grab, `await Object.WaitForStateAuthority()` (Shared-mode equiv of
  RequestStateAuthority) + grab-gating RPCs (`RPC_DontAllowOthersGrab`/`RPC_AllowGrab`) so only one player holds it
  (Shmackle `ShmackleGrabbleObject.cs`). The current `NetworkGrabbable` (authority override) is the seed.
- **Held/thrown transform:** Shmackle rides `NetworkRigidbody3D` (Physics Addon) — **we can't**. Keep its grab/release
  *RPC structure* (parent + kinematic toggle + apply release velocity in `ManuallyThrowObject`) but sync the
  transform via **`NetworkTransform` + "Allow State Authority Override"** (our existing networked-ball approach).
- **Projectiles:** pool via the custom `INetworkObjectProvider` (§2.🔧.1); spawn with `FusionNet.Spawn`, despawn on
  hit/expire (routes back through the pool).

---

## 6. Shmackle learnings — ADOPT / ADAPT / SKIP (with sources)
**ADOPT (port largely as-is):**
1. 3-point avatar `[Networked] NetPose` + lerp-in-`Render` + change-threshold write gate — `ShmackleNetworkRig.cs:32-53,648-787`.
2. `MiniGameBase : NetworkBehaviour` with `[Networked]` state/timer/`NetworkLinkedList<PlayerRef>` roster + StateAuthority RPCs — `Minigames/MiniGameBase.cs` + BoxingRing/ShootingArea managers.
3. Networked slot opt-in `[Networked] NetworkBool Occupied` + `RPC_SetOccupied` — `ShootingRange/SpotPoint.cs:13`.
4. Pooled `INetworkObjectProvider` (subclass `NetworkObjectProviderDefault`) — `PoolNetworkObjectProvider.cs`.
5. Grab = `await Object.WaitForStateAuthority()` + grab-gating RPCs — `ShmackleGrabbleObject.cs:153,354-383`.
6. Request→authority→fan-out RPC relay — `ShmackleRPC_Calling.cs`.

**ADAPT (concept yes, impl changes for no-Physics-Addon / multi-scene):**
7. Held/thrown transform: replace `NetworkRigidbody3D` with `NetworkTransform + authority override` (keep the grab/
   release RPC structure + release velocity).
8. Scene flow: do NOT copy `TransitionRoom`/runner-teardown; use one persistent runner + `FusionNet.LoadScene`
   (Single now, additive later).

**SKIP (or defer):**
9. AOI / interest management (`AOIManager`) — premature unless one session holds many players.
10. `RoomFetcher` debug matchmaking UI — build our own room flow on `FusionNet`.

**Connection model reference (Shmackle):** `GameMode.Shared`, `SessionName = "{scene}_{roomId}"`, custom
`SessionProperties { ELevelType, roomId }` for matchmaking, room-full → increment roomId + rejoin. `NetworkService.cs`.

---

## 7. Roadmap (phased)
| Phase | Build | Source pattern |
|---|---|---|
| **P0 — Foundation** | Upgrade `MinigameManager` → drive `FusionNet.LoadScene` (networked, persistent runner). Lock the 3 player-system decisions (§3). | (avoid Shmackle runner-teardown) |
| **P1 — Minigame core** ⭐ | `MiniGameBase : NetworkBehaviour` ([Networked] state/timer/roster + StateAuthority RPCs). Arena = first networked minigame. | `MiniGameBase.cs` |
| **P1.5 — Opt-in** | Zone proximity OR networked slot (`SpotPoint`). | `ShootingRange/SpotPoint.cs` |
| **P2 — Player system** | `IBillPlayer` + `Bill.Players` registry + `LocalPlayer` (formalize `PlayerRig`) + `BillNetworkPlayer` (pose sync). Refactor throw to `Bill.Players.Local`. | `ShmackleNetworkRig.cs` |
| **P3 — Networked throw** | NetworkTransform + authority ball; pooled `INetworkObjectProvider`; grab gating. | `PoolNetworkObjectProvider.cs`, `ShmackleGrabbleObject.cs` |
| **P4 — Scale / polish** | Networked scoreboard (`[Networked]` scores) if competitive; AOI when the hub is crowded. | `AOIManager.cs` |

*(P0/P1 first — networked minigame is the goal. P2 player-system can run in parallel since it's pure code.)*

---

## 8. Don't-do / gotchas
- Don't tear down/recreate the runner per scene (Shmackle does; we keep it persistent).
- Don't put networked roster/state in `Bill.SyncList`/`SyncState` (local-only). Use Fusion `[Networked]`.
- Don't pool `NetworkObject`s with `Bill.Pool`. Use a Fusion `INetworkObjectProvider`.
- Don't copy `NetworkRigidbody3D` held-object sync (needs the Physics Addon we don't run).
- `Bill.Players.Local` is null until the scene `LocalPlayer` `Awake` ran — null-check early.

## 9. Source references
- BillGameCore: `Assets/BillGameCore/Runtime/Network/Fusion/FusionNet.cs` (+ `FusionNetworkAdapter`, `FusionEvents`).
- TOSSZONE today: `_Game/Scripts/Network/PlayerSpawnManager.cs`, `_Game/Scripts/Player/NetworkGrabbable.cs`,
  `NetworkAvatar`, `_Game/Scripts/Minigame/*`, `Docs/Fusion_Shared_Mode_Gotchas.md`.
- Shmackle (`D:/Projects/Shmackle/Shmackle/Assets/_Shmackle/Scripts/`): `Runtime/Services/Network/NetworkService.cs`,
  `RoomFetcher`, `Player/Network/ShmackleNetworkRig.cs`, `Player/ShmackleGrabbleObject.cs`,
  `ShmackleNetworkObjectSpawner.cs`, `PoolNetworkObjectProvider.cs`, `ShmackleRPC_Calling.cs`, `Player/AOIManager.cs`,
  `ShootingArea/*`, `ShootingRange/*`, `Minigames/MiniGameBase.cs`.
