# P0 Two-Client AR Test Runbook — task 0.2.1

> Status: **document complete, execution pending.** This runbook was authored by tracing the real code paths
> (GitNexus `query`/`context`, direct file reads) — not guessed from file names — and the scene/build-settings
> prerequisites it depends on were set up and verified this session. Running it end-to-end requires two live
> Editor sessions (or two headsets) driven by a human, which this session cannot do unattended. Do not mark
> `0.2.1` `[x]` until someone has actually executed §6 and recorded the result in a dated
> `P0_ROUND1_<date>.md`-style log.

## 1. Preconditions

- Unity `6000.3.8f1`, project `D:\Project\TOSSZONE`, branch `codex/phase1-prep` or later.
- `Assets/_Game/Scenes/02_FPSMAP.unity` is in Build Settings at index 3 (added 2026-07-14 — see
  `P0_BASELINE_2026-07-14.md`). Confirm via `Assets/_Game/Scenes/02_FPSMAP.unity` → *File > Build Profiles* or
  `manage_scene(action="get_build_settings")`.
- A ParrelSync clone already exists on this machine at `D:/Project/TOSSZONE_clone_0` (sibling folder to the
  main project). If it doesn't exist on another machine, create one via **ParrelSync ▸ Clones Manager** first.
- Both Editor instances are on the same network and can reach Photon's cloud relay (no VPN/firewall blocking
  UDP to Photon), and no other tester is connected to the same Photon App ID at the same time (see §3 —
  QuickPlay is public matchmaking with no room code in this flow).
- No compile errors in either project copy (`read_console` clean, matches `P0_BASELINE_2026-07-14.md`).

## 2. Confirmed execution flow (via GitNexus + source, not guessed)

Traced with `context`/direct reads this session:

```
Editor "Play" with 02_FPSMAP.unity open
  └─ BillBootstrap (RuntimeInitializeOnLoadMethod) boots services, then — because BillBootstrapConfig has
     enforceBootstrapScene + returnToEditSceneInEditor — jumps to Scene 0 (00_Bootstrap), boots, and returns
     to 02_FPSMAP (the scene you pressed Play in). No splash UI runs in this path (that's Scene 0's own flow
     when you press Play from Scene 0 directly).
  └─ PlayerSpawnManager.OnEnable/Update → TryInit() → Connect()
       if a Fusion session is NOT already running/connecting:
         ConnectionFlowController.GetOrCreate().EnsureConnected()
           → QuickPlay(autoRetry: true)
           → Begin(FusionConnectArgs.Shared(code: null, region: -1, maxPlayers: 8), retries: 3)
           → FusionNet.Connect(...) — PUBLIC matchmaking, no session/room name, no code.
  └─ FusionConnectedEvent fires on success → PlayerSpawnManager.OnConnected → TrySpawn()
       → Runner.Spawn(_avatarPrefab, transform.position, transform.rotation, LocalPlayer, OnBeforeSpawned)
       → NetworkAvatar.Spawned() sets NetworkAvatar.Local + PlayerCombat.Local (HasStateAuthority + own
         InputAuthority) and resets Health = MaxLives (5) if it was <= 0.
  └─ ArenaManager.Spawned() (scene NetworkObject, State Authority = Fusion's Shared-Mode master client):
       fires MinigameEnteredEvent{Id="arena"} on EVERY client (CombatSession picks up the weapon catalog from
       this), subscribes FusionPlayerJoinedEvent/LeftEvent, and if HasStateAuthority: SyncTeams(),
       Phase = Warmup, PhaseTimer = 5s (_warmupDuration).
```

Confirmed by reading (not inferring from names): `PlayerSpawnManager.cs` (`TryInit`/`Connect`/`TrySpawn`),
`ConnectionFlowController.cs` (`QuickPlay`/`Begin`/`Attempt`), `ArenaManager.cs` (`Spawned`), `PlayerCombat.cs`
(`Spawned`, `RPC_TakeHit`, `RestoreLives`), `NetworkAvatar.cs` (`HandleRespawn`, `ResetForRound`).

### Death → respawn (already implemented, pre-dates the P0 gun work)

```
Any RPC_TakeHit(damage, point, shooter) on PlayerCombat (RpcSources.All, RpcTargets.All):
  if IsInvulnerable → ignored
  if HasStateAuthority (victim's own client): Health -= damage (floor 0)
    on Health hitting 0: clears FrozenTimer, starts InvulnTimer (3s) on next respawn, zeroes Bounty,
    AddMoney(compensation)
  fires PlayerHitEvent (all clients) and, if this client owns the victim and Health just hit 0,
  PlayerDiedEvent{IsLocal=true}

NetworkAvatar.FixedUpdateNetwork() [[victim's own client, every tick]]:
  HandleRespawn(): if _combat.Health <= 0 and no RespawnTimer running → arm a 3s TickTimer
                    when that timer expires → RestoreLives() (Health = MaxLives), TeleportToSpawn(),
                    fire PlayerRespawnedEvent{IsLocal=true}
```

Respawn is **fully automatic** — no manual respawn action needed. `ResolveSpawnPos()` reads
`ArenaManager.GetSpawnPosition(InputAuthority)`, so which team you're on determines which spawn point you
return to.

### What deals damage *today* (before Phase 1 builds the new AR gun)

There is no AR gun yet — `1.1.x`–`1.3.x` are what this runbook exists to eventually validate. The **existing**
in-scene `WeaponHolder` weapon (the pre-P0 rock/melee weapon already wired to `PlayerCombat.RPC_TakeHit`) is
the only damage source available right now, and is the "existing Fusion Shared Mode path" this task's
dependency line refers to. Use it to prove the connect → spawn → damage → death → respawn loop is repeatable
*today*; re-run this same runbook once `1.1`–`1.3` land, at which point the AR gun replaces the trigger for
damage but the rest of the loop (spawn/round/respawn) is unchanged.

## 3. Session/room

QuickPlay uses **public matchmaking, no explicit room name/code** (`FusionConnectArgs.Shared(null, -1, 8)`).
With only two testers connected to the project's Photon App ID, both clients' `QuickPlay()` calls will pair
into the same open room (first one creates it, second one joins it) — this is why "no other concurrent tester"
is a precondition (§1), not just a suggestion.

There is no in-scene UI in `02_FPSMAP` confirmed to expose `HostPrivateRoom`/`JoinPrivateRoom` (that UI,
`RoomCodeConsole`, lives in the hub `01_TOSSZONE_Main`). If QuickPlay pairing becomes unreliable in practice
(third party joins the same public room, etc.), the fallback is to route both clients through
`01_TOSSZONE_Main` first using a private room code, then manually load build index 3 — this is **not**
required for the current test and is noted only as a contingency.

## 4. Steps

1. **Main Editor**: open `D:\Project\TOSSZONE` in Unity `6000.3.8f1`. Open scene
   `Assets/_Game/Scenes/02_FPSMAP.unity` (File ▸ Open Scene). Do not press Play yet.
2. **Second client**: open `D:\Project\TOSSZONE_clone_0` in a second Unity Editor instance (ParrelSync clone —
   this is a separate Editor process with its own Library, safe to run concurrently). Open the same scene,
   `Assets/_Game/Scenes/02_FPSMAP.unity`.
3. Press **Play** in the main Editor first. Wait for the console to show
   `[ConnectionFlow] Connecting: Đang tìm phòng...` then `[ConnectionFlow] Connected: Đã vào phòng`, and
   `[PlayerSpawn] Spawned local avatar at ...`.
4. Press **Play** in the clone Editor. Same expected log sequence.
5. **Expected spawn state**: each client sees exactly two avatars — its own (first-person, hidden via the
   `RemoteVisual` layer per `Network_Architecture_Lessons.md` §Bài 8) and the other client's `NetworkAvatar`
   at a spawn point under `[Spawns]` in `02_FPSMAP`. `ArenaManager.Phase` should read `Warmup` for ~5s, then
   `Playing`.
6. **AR placeholder position**: until `1.1`–`1.2` land, there is no AR model on the wrist yet — the
   `WeaponHolder` object in the scene root drives the pre-existing weapon. Once Phase 1 lands, the AK74
   placeholder (see `P0_ASSET_SELECTION_2026-07-14.md`) will render parented under each avatar's `WristR` node
   — re-verify this step visually at that point (two-client screenshot, zero console errors).
7. **Body/head test target**: `02_FPSMAP` has a scene `DummyAvatar` (currently `activeSelf:false` per the
   hierarchy read this session — enable it if you want a stationary bot target independent of the second
   human client) as well as the second live client's own avatar (moving target, real head/body hitboxes via
   `NetworkAvatar`'s synced head/wrist nodes). Use the live avatar for the real damage→death→respawn cycles;
   the `DummyAvatar` is a solo-testing convenience, not a substitute for the two-client requirement.
8. **Run a damage→death→respawn cycle**: have Client A hit Client B with the current weapon until
   `PlayerCombat.Health` reaches 0 (5 hits at the current 1-per-life granularity, unless the existing weapon's
   damage-per-hit differs — check `RPC_TakeHit`'s `damage` argument at the call site). Expect, in order:
   `PlayerHitEvent` per hit → `PlayerDiedEvent{IsLocal=true}` on B's client when Health hits 0 → ~3s later
   `PlayerRespawnedEvent{IsLocal=true}` on B's client, B teleported to its team's spawn point, `InvulnTimer`
   active for 3s (verify by having A try to hit B immediately after respawn — it should have **no effect**;
   note in the log if it does, that's a genuine bug, not expected behavior).
9. Repeat step 8 two more times (three total cycles) to satisfy 0.2.1's own acceptance criterion ("hoàn thành
   ba damage→death→respawn cycles").
10. **Reset procedure**: Stop Play on both clients (Editor Stop button), then repeat from step 3. Each Play
    session is a fresh Fusion room — no manual state cleanup needed between runs.

## 5. Logs to capture

- Both Editors' Console output for the full session (filter to `[ConnectionFlow]`, `[PlayerSpawn]`,
  `PlayerHitEvent`/`PlayerDiedEvent`/`PlayerRespawnedEvent` if a listener logs them — none currently do by
  default; consider a temporary `Debug.Log` subscriber or wait for the `0.2.2` telemetry contract to land
  before treating this as sufficient evidence for Test Round 1).
- Screenshot or short clip of both Game Views side by side at least once per cycle (post-hit, post-death,
  post-respawn).
- `ArenaManager.Phase`/`Round`/`ScoreA`/`ScoreB` values at the start and end of each cycle (read via the
  `CombatHud`/`ScoreboardUI` already in the scene, or a temporary on-screen debug readout).

## 6. Failure triage

| Symptom | Likely cause | Where to look |
|---|---|---|
| Client stuck on "Đang tìm phòng..." | Photon reachability, or a stale session from a crashed prior Play | `ConnectionFlowController.OnConnectResult` retry log; check Photon dashboard/App ID config |
| Two avatars don't appear (only one) | `PlayerRig` missing on `LocalPlayer`, or `_spawnInFlight` latch stuck from a prior crashed session | `PlayerSpawnManager.TrySpawn` — console will show `[PlayerSpawn] No local PlayerRig found` if that's it |
| Duplicate avatar for one player | Scene-load player-object registry loss (`Fusion_Shared_Mode_Gotchas.md` §6) — should already be guarded by `NetworkAvatar.Local`, but check console for spawn-guard misses | `PlayerSpawnManager.TrySpawn`, `NetworkAvatar.Spawned` |
| Hit doesn't reduce Health | `IsInvulnerable` still true (spawn protection or leftover `InvulnTimer`), or hit RPC not reaching the victim's State Authority | `PlayerCombat.RPC_TakeHit` |
| Death never triggers respawn | `NetworkAvatar.HandleRespawn` not running (check `HasStateAuthority` on the victim's own client — it must run on the victim's own machine, not the shooter's) | `NetworkAvatar.FixedUpdateNetwork`/`HandleRespawn` |
| Clients disagree on who's alive/dead | `[Networked] Health` not replicating — check Fusion connection state, tick rate, or console for RPC target mismatches | `PlayerCombat.Health`, Fusion Runner logs |

## 7. Editor-simulation vs. Quest-device limitation

This entire runbook runs in the Unity Editor (main + ParrelSync clone), not on physical Quest headsets. Per
the roadmap's own risk note (`TOSSZONE-Playable-Ready-Roadmap.md` §0.2): editor input (mouse/keyboard or XR
Device Simulator) may not match real headset/controller behavior, especially for aim precision, comfort
(vignette on jump), and true network RTT between two physical devices on the same Wi-Fi. A Pass in this
runbook demonstrates the **networked game-logic loop** is repeatable; it does **not** substitute for on-device
verification before Test Round 1 is declared Pass. Record explicitly in any evidence log which environment
(Editor-only vs. device) each run used.

## 8. Execution status

**Not yet run.** This session authored and internally cross-checked the runbook against real source
(`ConnectionFlowController`, `PlayerSpawnManager`, `ArenaManager`, `PlayerCombat`, `NetworkAvatar`) and set up
its prerequisites (02_FPSMAP added to Build Settings, ParrelSync clone confirmed present at
`TOSSZONE_clone_0`), but actually driving two Play sessions and observing three real damage→death→respawn
cycles requires a human at the keyboard/headset for both clients simultaneously — outside what this agent
session can execute unattended. Task `0.2.1` stays `[/]` (document complete, execution pending) until someone
runs §4–§6 and appends a dated result.
