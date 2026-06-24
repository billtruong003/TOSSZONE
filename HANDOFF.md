# TOSSZONE — Handoff

VR party game (Photon Fusion 2, Shared Mode) built on the **BillGameCore** framework. Unity **6000.3.10f1**, target **Android (Quest)**, **URP**, **AutoHand** for hands.

## How we work / where things are
- **Coding guide (read first):** project skill `.claude/skills/billgamecore/` — `SKILL.md` + `reference/{conventions,tween,services,billinspector,fusion-module}.md`. All gameplay code uses `Bill.*` services + `BillTween` (NEVER DOTween). Conventions: `_camelCase` private, public-up/private-down, **zero-GC** hot paths, namespace `TossZone.<Feature>`, no asmdef under `_Game/Scripts/`.
- **Roadmap / plans:** `Docs/TOSSZONE_TaskBreakdown.md` (4 phases), `Docs/PHASE1_BUILD_PLAN.md` (M0–M4), `Docs/M0_Activation_Design.md`, `Docs/TASKBOARD.md` + `Docs/tasks.*`.
- **Conventions for tooling:** Unity MCP drives the editor; verify each Fusion step against `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`. Always design-doc → review → implement.

## Architecture (BillGameCore)
Auto-bootstrap via `[RuntimeInitializeOnLoadMethod]` → `BillBootstrapConfig` (in `Assets/Resources/`) → static `Bill.*` facade (Tween/Pool/Timer/Scene/Audio/Save/Config/Events/UI/Net/State) over a `ServiceLocator`. A `CoroutineRunner` ticks services. **Scene 0 must be the bootstrap scene.**

**Fusion module** (`Assets/BillGameCore/Runtime/Network/Fusion/`): `FusionNet` (full controller) + `FusionNetworkAdapter` (→ `Bill.Net`) + `FusionEvents`. Activated by the `PHOTON_FUSION` define. Two access paths: `Bill.Net` (simple) and `FusionNet.Instance` (full — connect/spawn/authority/events). Fusion 2.0.12 installed; App ID set.

## Scene flow & product vision (3 scenes, build order locked)
`00_Bootstrap (0)` → BillStartup splash → `01_TOSSZONE_Main (1)` → `02_Arena (2)`.

**Vision (Gorilla-Tag-style — confirmed by owner 2026-06-24):**
- **`01_TOSSZONE_Main` = persistent COMMUNITY HUB.** Players connect the moment they enter and **meet / hang out / walk around together** here — a shared social "lobby world", NOT a 2D menu.
- **`02_Arena` = a SPECIFIC gameplay entered from the hub** (walk through `[ArenaPortal]`). The current Arena is **TOSSZONE** (the throwing game) = the **FIRST** game. Future: more games = more arenas/portals reachable from the same hub.
- Flow intent: **Bootstrap → Hub (gặp nhau, work around) → chọn game qua portal → Arena (gameplay của game đó)**. TOSSZONE is game #1; the hub is the long-lived shared space.

## ✅ Done so far

### 2026-06-24 — Gorilla-Tag UNIFIED player refactor (supersedes the M3 separate-avatar approach)
Reworked the player into **ONE prefab that is both the local rig AND the networked avatar**, spawned by Fusion (Gorilla-Tag pattern). **Compiles clean; NOT yet runtime-tested with 2 players.**
- **Prefab `NetworkPlayer`** (`_Game/Prefabs/`, `FusionPrefab` label, GUID unchanged `9184db33…` so refs stayed valid): the AutoHand `XRPlayer` rig (unlinked into this prefab) + `NetworkObject` + `NetworkTransform` on **root (locomotion)** / `Camera (head)` / `RobotHand (L)` / `RobotHand (R)` + a `HeadVisual` sphere (child of camera, what remotes see as a head) + a `Body` capsule (colliders removed).
- **`NetworkPlayerRig`** (`_Game/Scripts/Player/`, `NetworkBehaviour`): local owner (input/state authority) keeps the rig active and hides the head mesh (sits on the camera); **proxy/remote disables AutoHand + camera + AudioListener + locomotion + physics by type-name match** (decoupled from the AutoHand assembly) so NetworkTransform owns the visuals. `[Networked] int ColorIndex` → 8-colour palette tints Body + Head.
- **`PlayerSpawnManager`** (`_Game/Scripts/Network/`, one per scene): once `Bill.IsReady`, `FusionNet.StartShared("TOSSZONE_DEMO")` (no scene index → stay in the current scene), then spawns the local `NetworkPlayer` at its own transform if `TryGetPlayerObject` is empty (`SetPlayerObject` prevents duplicates; re-checks on `FusionSceneLoadDoneEvent`). **Connect happens on entering Main → the hub is networked; players meet there.**
- **`PortalMatchmaker`** rewritten: when the LOCAL rig enters `[ArenaPortal]` → `FusionNet.LoadScene(arenaIndex)` (master loads, clients follow). No more `StartShared` at the portal.
- **Scenes rewired:** `[PlayerSpawnManager]` in Main (origin) + Arena (`(0,0,-2)`); **XRPlayer rigs REMOVED from both scenes** (player is spawned now). Main keeps `[Floor]`/`[ArenaPortal]`/Light; Arena keeps `[Floor]`/`[SpawnA]`/`[SpawnB]`/Light.
- **Deleted** (replaced): `NetworkPlayerAvatar`, `LocalPlayerRig`, `NetworkPlayerSpawner`. Kept `MatchmakingStatusEvent` (unused, harmless).
- **Smoke test:** boot OK; fixed an early-access bug — `PlayerSpawnManager` touched `Bill.Events` before bootstrap finished → now guarded by a `Bill.IsReady` poll in `Update`. **Connect/spawn/see-each-other NOT verified via MCP** (Play dropped the bridge with 3 editors open).
- **⚠️ KEY RISK to test next:** the AutoHand rig is now **spawned at RUNTIME** (not placed in-scene). AutoHand is built for a single in-scene local player → dynamic spawn + remote-disabling may need tuning (head/hand tracking, joystick locomotion, the root `NetworkTransform` locomotion sync).

### 2026-06-23 — Section 1.1 complete + M2/M3 scaffolded + PC test sim
> ⚠️ The **player-presence (M3) parts below — `NetworkPlayerAvatar` / `LocalPlayerRig` / `NetworkPlayerSpawner` / the separate avatar prefab — were SUPERSEDED on 2026-06-24** by the unified rig (above). Section 1.1, M2 scene-flow concepts, and the tooling still apply.
- **Section 1.1 (setup) DONE:** `_Game/Scripts/{Core,Network,Player,Throwing,UI}` + `ScriptableObjects`/`Materials` folders; tags `TeamA/TeamB/Projectile/Throwable` + layers (slots 10–13).
- **M2 (scene flow / matchmaking) — code + wiring done, compiles clean:** `PortalMatchmaker` on `[ArenaPortal]` (walk-through → `FusionNet.StartShared("TOSSZONE_DEMO", 2)` → load Arena; 30s `Bill.Timer` timeout; detects player via `LocalPlayerRig` not layers) + `MatchmakingStatusEvent`.
- **M3 (player presence) — code + prefab + wiring done, compiles clean:** prefab `NetworkPlayer` (NetworkObject + Body/Head/HandL/HandR, 4× NetworkTransform, label `FusionPrefab` → auto-registered); `NetworkPlayerAvatar` (`FixedUpdateNetwork` copies rig pose when state-authority, hides own visuals); `NetworkPlayerSpawner` (Arena: spawns local avatar at team spawn by `LocalPlayerId%2`); `LocalPlayerRig` (AutoHand decouple). Arena dressed: `[Floor]`, `[SpawnA/B]`, `[Spawner]`, Directional Light.
- **AutoHand re-imported** → `XRPlayer` rig restored; `LocalPlayerRig` wired (head=`Camera (head)`, hands=`RobotHand (L/R)`) in **both** Main + Arena.
- **Smoke test boot→Main PASS** (0 error); build settings `00/01/02` confirmed; `NetworkPlayer` Fusion-registered.
- **Meta XR Simulator installed** (`com.meta.xr.simulator@74.0.0`) for PC testing without Quest Link. ⚠️ Still needs one-click **Meta ▸ Meta XR Simulator ▸ Activate** in the TOSSZONE editor (couldn't auto-toggle — a 2nd Unity editor was open, MCP routing ambiguous).
- Design doc `Docs/M2_M3_Design.md`; task tracking synced (`TOSSZONE_TaskBreakdown.md` + `tasks.meta.json` + `tasks.json`).

### Earlier (M0 + scaffold)
- **M0 (activation):** `PHOTON_FUSION` define (Android), build settings 00/01/02, `BillBootstrapConfig.defaultNetworkMode=FusionShared`, `defaultGameScene` cleared (BillStartup owns boot→Main). Fusion module verified-compiled (reflection: types + INetworkRunnerCallbacks). Smoke-tested.
- **BillStartup splash** in `00_Bootstrap` (Camera + EventSystem + SplashCanvas[Background/Logo/Status/ProgressSlider] + `[BillStartup]`, nextScene=`01_TOSSZONE_Main`). Boot→splash→Main verified, 0 errors.
- **Main scene scaffold (`01_TOSSZONE_Main`):** Directional Light, `[Floor]` (20×20 + collider), **AutoHand `XRPlayer` rig** (`Assets/AutoHand/Examples/Scenes/XR/Prefabs/XRPlayer.prefab`), `[ArenaPortal]` placeholder (emissive disc + trigger). Play-tested: boots + loads + no errors (without HMD the rig doesn't track — that's expected).
- **Framework updated** to latest from `github.com/billtruong003/MythFall-Suvivor` (hash-diff verified: only 5 editor-tooling files changed; runtime/public APIs byte-identical → skill docs still accurate; Fusion module preserved).

## ▶️ Next (resume here)
1. **TEST the unified player (2 players via ParrelSync):** both editors (original + `TOSSZONE_clone_0`) Play from `00_Bootstrap` → each connects + spawns its `NetworkPlayer` → **see each other in the Main hub** (coloured capsule + head + hands) → walk into `[ArenaPortal]` → both load Arena. Console: `[PlayerSpawn] Spawned local player`. **Watch the AutoHand-as-NetworkObject risk** (head/hand/joystick-locomotion tracking on the spawned rig + root NetworkTransform sync); tune as needed.
2. **Player persistence Main→Arena:** confirm the player carries over vs respawns (no duplicate, no missing player). `PlayerSpawnManager` handles both via `TryGetPlayerObject`/`SetPlayerObject`.
3. **Hub polish:** dress `01_TOSSZONE_Main` as a real community space; later add multiple game portals (one per game).
4. **M4 — throw (TOSSZONE gameplay, lives in `02_Arena`):** behind-head grab + haptic → `Bill.Pool.Spawn("projectile")` flying via `BillTween.Float(0,1,t=>arc.Evaluate(t))` (1–2 bounces, NOT physics); Shared-mode authority.
5. **VR splash:** make the boot splash world-space (currently screen-space → not visible in headset).

## ⚠️ Known harmless warnings (NOT bugs)
- `Camera.stereoTargetEye only with built-in renderer` — URP+XR cosmetic; stereo works via XR plugin on-device.
- `Cannot add menu item 'Tools/BillInspector/Validation Window' ... already exists` — duplicate menu in updated `BillMenuItems.cs`; 1-line fix available.
- `No Theme Style Sheet set to PanelSettings` — `Bill.UI` (UI Toolkit) unused in VR; ignore.
- `Method 'Init' is in a generic class ...` — engine warning from a package.

## Build / run
Open Unity, press Play from `00_Bootstrap` (or any scene — `enforceBootstrapScene` redirects). For VR, build to Android/Quest. Photon App ID already configured.

## Local testing & tooling (installed this session)
- **2-player local test = ParrelSync** (`com.veriorpies.parrelsync`): menu **ParrelSync → Clones Manager** → a `TOSSZONE_clone_0` exists (symlinks Assets/Packages, so package/asset changes affect both — close clones before big package installs). Open both editors, Play both → they join the same `TOSSZONE_DEMO` session.
- **PC VR test (two options, pick one — they conflict if both on):**
  - **Meta XR Simulator** (`com.meta.xr.simulator@74.0.0`) — currently **ACTIVE**. Toggle via **Meta ▸ Meta XR Simulator ▸ Activate/Deactivate**. Heavy (synthetic-env server); **conflicts across 2 ParrelSync editors**. `Deactivate` before building to a real Quest.
  - **XR Device Simulator (XRI)** — `com.unity.xr.interaction.toolkit@3.3.1` + the sample copied to `Assets/Samples/XR Interaction Toolkit/3.3.1/XR Device Simulator/`. Auto-spawned in Play by `Assets/_Game/Scripts/Editor/Dev/XrDeviceSimulatorAutoSpawn.cs` (toggle **Tools ▸ TOSSZONE ▸ XR Sim: Toggle Auto-Spawn**) — currently **OFF**. Lighter + no 2-editor conflict (like the Teabag project). **AutoHand compatibility with it is unverified.**
- **`Tools ▸ TOSSZONE ▸ Install …`** menus (`PackageInstaller.cs`) force-install packages via `Client.Add` when the editor isn't focused (MCP refresh is flaky with multiple editors open).
- **Stylized Toon World Kit** (owner's repo, NOT a UPM package — `package.json` is under `Assets/`) copied into `Assets/StylizedToonWorldKit/` for world/shader art.
- **MCP gotcha:** with multiple Unity editors open, the MCP bridge routing is unstable (commands can hit the wrong editor; Play often drops the bridge). Set the active instance to `TOSSZONE@…` before each batch; for clean work, keep only the TOSSZONE editor open.

## External dependencies (NOT in the repo — re-import after clone)
- **AutoHand** (paid, Unity Asset Store) — excluded via `.gitignore` so the paid asset isn't redistributed publicly. A fresh clone will have missing scripts/prefabs (e.g. the `XRPlayer` rig in `01_TOSSZONE_Main`) until AutoHand is re-imported. The project owner's local copy is unaffected (files remain on disk). Path: `Assets/AutoHand/`.
- **Photon Fusion 2** and **Meta XR SDK** — free SDKs, committed to the repo.
