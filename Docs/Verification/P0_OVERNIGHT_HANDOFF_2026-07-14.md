# P0 Overnight Handoff — 2026-07-14

> Session scope: Stage A preflight → Phase 0 dependencies (0.2.1/0.2.2) → new weapon-asset audit/selection →
> Phase 1.1 (local AR loop). Branch `codex/phase1-prep`, all work below is committed and pushed to
> `origin/codex/phase1-prep`. Nothing pending in the working tree except pre-existing/vendor noise (see §6).

## 1. Commits pushed this session (oldest → newest)

| SHA | Summary |
|---|---|
| `e252263` | Stage A preflight: GitNexus index refresh, duplicate BillInspector menu-item fix, P0 baseline report |
| `2f2c043` | Import owner-provided low-poly weapon packs; select AK74 as P0 AR placeholder |
| `4fbc518` | Stop tracking `.unitypackage.meta` for gitignored installer archives (cleanup) |
| `6d03f67` | Phase 0 dependencies: two-client runbook (0.2.1) + combat telemetry contract (0.2.2, Pass) |
| `b38c6e7` | Phase 1.1: local AR hitscan loop — config, fire gate, magazine, reload, local feedback |

`origin/codex/phase1-prep` is at `b38c6e7` (confirmed pushed, not just committed locally).

## 2. Task Board status (`Docs/TOSSZONE_TaskBreakdown.md` / `Docs/tasks.meta.json`)

| Task | Status | Evidence |
|---|---|---|
| 0.1.1 Lock Option A | `[x]` Pass (pre-existing, unchanged) | `GameDesign/TOSSZONE-Playable-Ready-Roadmap.md`, `Gun_System_Architecture.md` |
| 0.1.2 Lock D3 HEALTH-MODEL | `[!]` **Blocked — needs owner** | see §5, the one open question |
| 0.2.1 Two-client AR test runbook | `[/]` Document complete, execution pending | `Verification/P0_TWO_CLIENT_RUNBOOK.md` |
| 0.2.2 Combat telemetry contract | `[x]` Pass | `GameDesign/P0_Combat_Telemetry_Contract.md` |
| 1.1.1 Data-driven placeholder AR runtime | `[x]` Pass | `Verification/P0_1_1_LOCAL_LOOP_2026-07-14.md` |
| 1.1.2 Fire gate/magazine/reload | `[x]` Pass | same doc, §3 |
| 1.1.3 Immediate local feedback | `[/]` Code-complete, sensory verification pending | same doc, §4 |
| 1.2.1 Remote equipped AR proxy | `[ ]` Not started | — |
| 1.2.2 Unreliable remote shot cosmetic | `[ ]` Not started | — |
| 1.3.1 Reliable ShotClaim + validator | `[ ]` Not started | — |
| 1.3.2 HP/damage/death/respawn | `[!]` Blocked on D3 | — |
| 1.3.3 Kill/score exactly once | `[ ]` Not started | — |
| 1.4.1 Test Round 1 | `[ ]` Not started (depends on all above) | — |

`Docs/tasks.json` was re-exported via the Unity menu `Tools/TOSSZONE/Export tasks.json` after each Task Board
edit — not hand-edited.

## 3. What's actually built and proven (not just written)

- **GitNexus index**: refreshed to HEAD, 17,269 symbols.
- **Unity**: `6000.3.8f1`, project confirmed `TOSSZONE`, compiles clean (only the two known pre-existing
  baseline issues remain — see `Verification/P0_BASELINE_2026-07-14.md` §4 — one of which, the duplicate
  BillInspector menu item, was fixed this session).
- **New weapon asset packs** (owner-imported): audited all AR-shaped candidates across 4 packs; picked
  **AK74** from `Low Poly Weapons VOL.1` (simplest hierarchy, 1 shared material, no baked-in attachments).
  Converted only that one material's shader `Standard` → `Universal Render Pipeline/Lit` (no project-wide
  conversion, no HDRP touched). Full reasoning in `Verification/P0_ASSET_SELECTION_2026-07-14.md`.
- **`02_FPSMAP.unity`** added to Build Settings (index 3), additive only — existing indices 0-2 untouched.
- **New `TossZone.Guns` system** (`Assets/_Game/Scripts/Guns/`): `GunConfig`/`GunCatalog` (data-driven,
  deliberately separate from the older party-game `WeaponConfig`), `Gun`+`HitscanGun` (fire-gate/ammo/reload
  state machine + raycast/world-body-head classification/deterministic shotId), `GunInput` (new Input System),
  `GunFeedback` (muzzle/tracer/impact/haptic), `TracerFx` (pooled tracer). AK74 wrapper prefab at
  `Assets/_Game/Art/Weapons/P0/AK74_P0.prefab` (vendor model untouched, instantiated as a child, rotated 180°
  so the wrapper's +Z is the barrel — confirmed empirically via screenshot investigation).
- **Play Mode verification** (`Verification/P0_1_1_LOCAL_LOOP_2026-07-14.md`): 11 accepted shots fired via
  `execute_code` across World/Body/Head targets. Ammo decremented exactly 1 per accepted shot every time.
  ShotId strictly unique per session. Fire-rate gate blocks same-frame re-fire (1 of 5 rapid calls fired).
  Empty-magazine dry-fire + auto-reload cycle proven end-to-end (state `Reloading` → `Ready`, ammo refilled).
  **Two real bugs found and fixed during this testing** (not just "compiled, assumed done"):
  1. `LayerMask.NameToLayer` cannot run in a static field initializer — Unity throws at domain reload.
  2. `GunFeedback.OnEnable` subscribed to `Bill.Events` before `BillBootstrap` registers the EventBus, on a
     `DontDestroyOnLoad` object present from the very first scene — threw `NullReferenceException`. Fixed by
     copying the exact poll-until-`Bill.IsReady` pattern `PlayerSpawnManager`/`CombatSession` already use.

## 4. Unity compile / test status

Clean compile as of the last commit. No CS errors. Only pre-existing/known noise remains (URP GlobalSettings
dangling terrain-shader refs, MCP transport reconnect log spam) — both documented as accepted in
`Verification/P0_BASELINE_2026-07-14.md`. No automated EditMode/PlayMode test suite exists yet for the Gun
system (all verification this session was interactive Play Mode via `execute_code`, documented with raw
output, not a checked-in test file).

## 5. The one question still needed from the owner

**D3 HEALTH-MODEL is still open and blocks `1.3.2` (and therefore `1.4.1`).** Everything else in Phase 1
proceeded without it. Exact question, unchanged from the roadmap:

> "Xác nhận v0.3-P0 dùng 100 HP, death khi HP ≤ 0 và respawn reset về 100 HP chứ?"

Until answered, do **not** implement `1.3.2` (catalog damage → HP write) on an assumed model — `1.3.1`
(ShotClaim + validator, no HP write yet) can and should proceed independently.

## 6. Known gaps / things intentionally left alone

- 28 pre-existing uncommitted material/scene edits under `Assets/AutoHand/Examples/` (URP auto-upgrade churn
  from before this session) — still sitting uncommitted in the working tree, untouched per instruction not to
  stage them with gun work or revert them without proof they aren't a user edit.
- `Assets/Scenes/Demo.unity` + `Demo Lighting Settings.lighting` (+ `.meta`s) — vendor demo content bundled
  with one of the imported weapon packs, untracked. Not added to Build Settings (per instruction). Left as-is;
  not needed for P0.
- `Assets/Screenshots/` — six PNGs from this session's visual investigations (muzzle-anchor placement, AR
  candidate comparison, one Play Mode context shot). One (`screenshot-20260714-070301.png`) is committed as
  cited evidence in `P0_ASSET_SELECTION_2026-07-14.md`; the other five are untracked debug artifacts, safe to
  delete or ignore.
- No AudioLibrary configured anywhere in the project (pre-existing gap) — gun audio call sites are wired
  correctly but silent (`[Bill.Audio] No AudioLibrary set.` warning only) until real SFX content is authored.
- `1.1.3`'s visual/haptic *quality* (as opposed to correctness) needs a human with the headset, or at least a
  screenshot properly framed on the equipped gun — the one attempt this session had the XR Device Simulator's
  camera pointed away from the wrist.
- `0.2.1`'s two-client runbook is fully written and cross-checked against real source, but never actually
  executed end-to-end with two live clients — that requires a human at two keyboards/headsets simultaneously.

## 7. Manual steps for the owner

1. Answer the D3 question in §5 (one line is enough — "confirm" or a correction).
2. When ready to test two clients: open `D:\Project\TOSSZONE` in the main Editor and
   `D:\Project\TOSSZONE_clone_0` (existing ParrelSync clone) in a second Editor instance, both with
   `Assets/_Game/Scenes/02_FPSMAP.unity` open, and follow `Docs/Verification/P0_TWO_CLIENT_RUNBOOK.md` §4.
3. If continuing Phase 1 work in a fresh session: resume at task `1.2.1` (remote equipped AR proxy — networked
   `EquippedSlot` byte on `NetworkAvatar` + proxy gun rendered under the replicated `WristR` node for other
   clients). GitNexus `impact(target="NetworkAvatar", direction="upstream")` was already run this session and
   reported **HIGH risk** (10 impacted symbols, `ArenaManager.FixedUpdateNetwork` affected) for editing the
   `NetworkAvatar` class directly — any edit there needs care and should stay additive (new field + new
   method), not a rework of existing logic.
