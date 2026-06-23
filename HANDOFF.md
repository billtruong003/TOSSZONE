# TOSSZONE — Handoff

VR party game (Photon Fusion 2, Shared Mode) built on the **BillGameCore** framework. Unity **6000.3.10f1**, target **Android (Quest)**, **URP**, **AutoHand** for hands.

## How we work / where things are
- **Coding guide (read first):** project skill `.claude/skills/billgamecore/` — `SKILL.md` + `reference/{conventions,tween,services,billinspector,fusion-module}.md`. All gameplay code uses `Bill.*` services + `BillTween` (NEVER DOTween). Conventions: `_camelCase` private, public-up/private-down, **zero-GC** hot paths, namespace `TossZone.<Feature>`, no asmdef under `_Game/Scripts/`.
- **Roadmap / plans:** `Docs/TOSSZONE_TaskBreakdown.md` (4 phases), `Docs/PHASE1_BUILD_PLAN.md` (M0–M4), `Docs/M0_Activation_Design.md`, `Docs/TASKBOARD.md` + `Docs/tasks.*`.
- **Conventions for tooling:** Unity MCP drives the editor; verify each Fusion step against `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`. Always design-doc → review → implement.

## Architecture (BillGameCore)
Auto-bootstrap via `[RuntimeInitializeOnLoadMethod]` → `BillBootstrapConfig` (in `Assets/Resources/`) → static `Bill.*` facade (Tween/Pool/Timer/Scene/Audio/Save/Config/Events/UI/Net/State) over a `ServiceLocator`. A `CoroutineRunner` ticks services. **Scene 0 must be the bootstrap scene.**

**Fusion module** (`Assets/BillGameCore/Runtime/Network/Fusion/`): `FusionNet` (full controller) + `FusionNetworkAdapter` (→ `Bill.Net`) + `FusionEvents`. Activated by the `PHOTON_FUSION` define. Two access paths: `Bill.Net` (simple) and `FusionNet.Instance` (full — connect/spawn/authority/events). Fusion 2.0.12 installed; App ID set.

## Scene flow (3 scenes, build order locked)
`00_Bootstrap (0)` → BillStartup splash → `01_TOSSZONE_Main (1)` = **Main / social hub** (walk around, buttons, ring/portal) → walk through ring → `02_Arena (2)` = combat.

## ✅ Done so far

### 2026-06-23 — Section 1.1 complete + M2/M3 scaffolded + PC test sim
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

## ▶️ Next
- **Verify M2/M3 (device/sim):** Activate Meta XR Simulator (or build to Quest) → walk into `[ArenaPortal]` → connect → Arena → spawn avatar; 2 clients see each other. Editor flow-only test: Play → right-click `PortalMatchmaker` → `DEV ▸ Start Match (no VR)`.
- **M3 pass 2:** team color (`[Networked]` byte) + procedural leg IK; move Arena rig to team spawn side on join (both rigs currently start at `(0,0,-2)` → overlap until they walk apart).
- **M4 — throw:** behind-head grab + haptic → `Bill.Pool.Spawn("projectile")` flying via `BillTween.Float(0,1,t=>arc.Evaluate(t))` (1–2 bounces, NOT physics); Shared-mode authority.
- **VR splash:** make the boot splash world-space (currently screen-space → not visible in headset).

## ⚠️ Known harmless warnings (NOT bugs)
- `Camera.stereoTargetEye only with built-in renderer` — URP+XR cosmetic; stereo works via XR plugin on-device.
- `Cannot add menu item 'Tools/BillInspector/Validation Window' ... already exists` — duplicate menu in updated `BillMenuItems.cs`; 1-line fix available.
- `No Theme Style Sheet set to PanelSettings` — `Bill.UI` (UI Toolkit) unused in VR; ignore.
- `Method 'Init' is in a generic class ...` — engine warning from a package.

## Build / run
Open Unity, press Play from `00_Bootstrap` (or any scene — `enforceBootstrapScene` redirects). For VR, build to Android/Quest. Photon App ID already configured.

## External dependencies (NOT in the repo — re-import after clone)
- **AutoHand** (paid, Unity Asset Store) — excluded via `.gitignore` so the paid asset isn't redistributed publicly. A fresh clone will have missing scripts/prefabs (e.g. the `XRPlayer` rig in `01_TOSSZONE_Main`) until AutoHand is re-imported. The project owner's local copy is unaffected (files remain on disk). Path: `Assets/AutoHand/`.
- **Photon Fusion 2** and **Meta XR SDK** — free SDKs, committed to the repo.
