# TOSSZONE — Quest-Ready Vertical Slice Plan

> Execution plan for Claude Code. Updated 2026-07-11 from the current source tree, the
> latest handoff, the canonical GDD, TEST_CASES, and a fresh GitNexus index.
>
> Goal: prove one stable, understandable, fun multiplayer match on the target Quest
> hardware before expanding product scope or performing broad refactors.

## 1. Outcome and scope

### Definition of done

A build can complete this flow without debug commands or manual scene repair:

`Boot -> connect -> hub -> both players ready -> arena -> complete match -> rematch or hub`

The flow must pass on Meta Quest Link, pass the critical Shared Mode regression with two
clients, and boot as an ARM64 IL2CPP Development APK on the target Quest headset.

### Current product scope to preserve

- Preserve the current Session 16 pivot: Rock-only production flow, only
  Multi/Speed/Area rings, wrist weapon panel disabled reversibly.
- Systems implemented but disabled are not missing features.
- Do not reactivate the full weapon roster, sword, mine, T21, T22, or T29 unless the
  owner explicitly changes the product scope.
- Do not rebuild match/economy/rings/HUD work already completed in T26–T31.
- Do not optimize burst rendering until a real Quest profile identifies a measured
  bottleneck.

### Source-of-truth order

1. `Docs/GDD_Core_Reference.md` — product rules.
2. Latest sections of `Docs/HANDOFF.md` — current implementation and operational state.
3. Updated task status in `Docs/TASKS_WEAPON_UX.md:180-236`.
4. `Docs/TEST_CASES.md` — verification inventory; individual statuses may be stale.
5. Source code and live evidence decide whether an old documented issue still exists.

## 2. Mandatory execution rules

Every Claude Code session must follow these rules:

1. Read `AGENTS.md`, this plan, and the documentation references listed in the task.
2. Before editing a function, class, or method, run GitNexus upstream impact analysis and
   report direct callers, affected processes, and risk. Stop and warn on HIGH/CRITICAL.
3. Read the exact source and copy an existing project pattern where one exists. Do not
   invent Fusion or BillGameCore APIs.
4. For networking work, read `Docs/Fusion_Shared_Mode_Gotchas.md` first.
5. Use Bill services and `BillTween`; never introduce DOTween or a second service system.
6. Keep authority writes in `FixedUpdateNetwork`, replicated-state presentation in
   `Render`, and ordinary Rigidbody writes in Unity `FixedUpdate`.
7. Make one coherent task per commit. Preserve unrelated working-tree changes.
8. Run targeted verification after each task. Before commit run GitNexus
   `detect_changes(scope="compare", base_ref="main")`.
9. Record evidence in `Docs/TEST_CASES.md` or a task report. A code change without a
   reproducible failure and a passing regression case is incomplete.

## 3. Phase 0 — Baseline and documentation discovery

### QR-000 — Capture a reproducible baseline

**Executor:** Claude Code + Unity MCP. Owner only supplies headset when requested.

**Read first**

- `Docs/HANDOFF.md:26-46`
- `Docs/TEST_CASES.md:7-17`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`

**Work**

- Record commit SHA, dirty files, Unity version, active build target, enabled scenes,
  headset/runtime, Photon region, and client count.
- Confirm the enabled flow scenes are `00_Bootstrap`, `01_TOSSZONE_Main`, `02_Arena`.
- Open the project in Unity 6000.3.8f1 and capture compile errors/warnings.
- Run one clean editor flow from bootstrap to hub. If a second player is unavailable,
  stop at the ready gate and record that the multiplayer continuation is blocked.
- Create `Docs/Verification/BASELINE_YYYY-MM-DD.md` with the evidence and exact blockers.

**Acceptance**

- No new compile errors.
- Baseline is tied to an exact commit and environment.
- Failures contain reproduction steps, expected/actual results, logs, and screenshots or
  timestamps when applicable.

**Guard**

- Do not fix anything during baseline capture.
- Do not classify an unavailable headset/client as a gameplay bug.

### QR-001 — Reconcile the live task board

**Executor:** Claude Code.

**Read first**

- `Docs/HANDOFF.md:575-617`
- `Docs/TASKS_WEAPON_UX.md:180-236`
- `Docs/TEST_CASES.md:272-345`

**Work**

- Mark each open item as `missing`, `implemented-disabled`, `needs verification`,
  `owner decision`, or `obsolete`.
- Correct stale test notes only when source or new evidence proves the current behavior.
- Preserve all existing test IDs.

**Acceptance**

- No completed T18–T31 feature is reopened accidentally.
- Rock-only scope and reversible feature flags are explicit.
- CTCH-02 and similar stale suspicions are reconciled against current source.

## 4. Phase 1 — Quest Link gameplay gate

This phase is blocking. Do not start structural refactors before it passes or produces
specific bug tasks.

### QR-100 — Execute THR-01..10 on Meta Quest Link

**Executor:** Owner operates the headset; Claude Code drives Unity, records evidence, and
diagnoses reproducible failures.

**Read first**

- `Docs/HANDOFF.md:34-46`
- `Docs/TEST_CASES.md:155-171`
- `Docs/Throw_Mechanic_Spec.md`
- `Assets/_Game/Scripts/Throwing/ThrowController.cs`

**Procedure**

- Disable `Tools > TOSSZONE > XR Sim: Toggle Auto-Spawn` before Play.
- Execute THR-01..10 exactly as written.
- Add real-hand checks for close-range hit, moving throw, Frozen input gate, and
  Speed-ring stacks 1/2/3.
- Record `case / pass-fail / expected / actual / reproduction / clip timestamp`.
- Convert every repeatable failure into a separate `BUG-THR-*` task. Do not tune several
  unrelated symptoms in one change.

**Acceptance**

- Locomotion does not trigger a throw.
- A deliberate swing throws in the intended direction; a short flick below the documented
  threshold does not.
- Grip/release and cooldown cannot become stuck.
- Moving, close-range, Frozen, and high-speed throws have recorded outcomes.
- All failures have evidence; no speculative fixes are applied.

### QR-101 — Quest comfort and readability pass

**Executor:** Owner + Claude Code.

**Cases**

- Run the headset-dependent parts of REFF, CTCH, SEL, TRNG, and UXH.
- Verify ring effectiveness is visually understandable, not merely numerically correct.
- Check haptic timing, hand pose, release flash, projectile readability, world-space text,
  and the current Rock-only status HUD.

**Acceptance**

- Each issue is classified as bug, tuning, intentional design, or owner decision.
- Tuning changes include before/after values and the exact cases rerun.

## 5. Phase 2 — Two-client Shared Mode gate

### QR-200 — Establish the two-client harness

**Executor:** Claude Code using main Editor + one ParrelSync clone.

**Read first**

- `Docs/Fusion_Shared_Mode_Gotchas.md`
- `Docs/Network_Architecture_Lessons.md`
- `Docs/HANDOFF.md:125-140`
- `Docs/TEST_CASES.md:172-203`

**Procedure**

- Never start two Editors and Meta XR Simulator simultaneously.
- Start client A and wait until `FusionNet.Instance.Runner.IsRunning == true`; then start B.
- Confirm both clients use the same session and Photon region.
- Document a repeatable start/stop/reset procedure in
  `Docs/Verification/TWO_CLIENT_HARNESS.md`.

**Acceptance**

- Both clients join the same room three consecutive times without stale avatars.
- Stop/restart instructions recover cleanly from domain reload or a failed join.

### QR-201 — Run critical Shared Mode regression

**Executor:** Claude Code; owner assists where physical input is required.

**Cases**

- NET-01..11.
- Both-player ready gate and portal load.
- Round end, respawn, side swap visible on both clients.
- 1-1-1 match draw.
- Ring applied by the non-master/state-authority client.
- Catch/PPU result converges on both clients.
- Close-range and Speed 2/3 projectile hit.
- Rematch and return to hub.
- Master leaves during hub, arena, and match end; record FLOW-02 separately.

**Special evidence rule**

For “projectile disappears before the face”, record the remote view and victim Health at
the same time. Classify it as visual linger/interpolation or an actual authority-side miss
before changing code.

**Acceptance**

- Exactly one authoritative result per hit, catch, ring, round, and scene transition.
- Replicated state converges on both clients.
- No ghost projectile, duplicate avatar, stuck round, or divergent match result.
- Master-leave behavior never silently hangs; unsupported behavior is documented clearly.

### QR-202 — Fix reproduced networking bugs

**Executor:** Claude Code, one bug per subtask/commit.

**Required pattern references**

- Replicated cause -> local presentation:
  `ArenaManager.Render/OnPhaseChanged` and
  `NetworkProjectile.Render/ApplyVisualIfChanged`.
- Reset on all peers: `ArenaManager.RPC_ResetRound`.
- Transferable authority rules: `Docs/Fusion_Shared_Mode_Gotchas.md`.

**Acceptance per bug**

- GitNexus impact report exists before edit.
- The original reproduction fails before and passes after the change.
- Relevant NET, regression, Quest, and scene-transition cases pass.
- `detect_changes` shows only expected symbols and flows.

## 6. Phase 3 — Quest Android build and performance gate

### QR-300 — Development APK smoke test

**Executor:** Claude Code builds; owner installs/runs on the target headset.

**Build contract**

- Android API min 32, target 34.
- ARM64.
- IL2CPP.
- Three enabled production scenes.
- Development Build for the first pass.

**Acceptance**

- APK installs and boots on the target headset.
- Full production flow completes without Editor-only helpers.
- No missing material, shader, prefab, scene, or serialized reference.
- Logs are attached to the verification report.

### QR-301 — Measure Quest performance

**Executor:** Claude Code + owner.

**Scenarios**

- Normal Rock-only match.
- Multi T5 / 15-projectile burst.
- More than eight simultaneous ring/training objects where supported.
- Rapid projectile/explosion stress.
- Two-client match and scene transition.
- Five-minute soak.

**Metrics**

- CPU and GPU frame time.
- GC allocation per frame and recurring spikes.
- Memory trend and object counts.
- Dropped frames and thermal/performance level.

**Acceptance**

- No sustained memory growth or projectile/ring leak.
- No recurring normal-combat GC spike caused by game code.
- Owner records the target Quest model and refresh rate, then approves a numeric frame
  budget before performance is declared PASS.

**Guard**

- Do not implement GPU instancing, ECS, or a new pool based only on code inspection.

## 7. Phase 4 — Small automated safety net

This phase begins after the gameplay gates are stable. Do not add a gameplay asmdef under
`Assets/_Game/Scripts` casually: current game code is in `Assembly-CSharp`, and an NUnit
test asmdef cannot reference that predefined assembly.

### QR-400 — Decide the test packaging seam

**Executor:** Claude Code. This is a short architecture decision, not a refactor.

**Investigate**

- Existing test pattern:
  `Assets/BillGameCore/BillInspector/Tests/Editor/BillInspector.Editor.Tests.asmdef`.
- Current predefined-assembly dependencies of candidate pure rules.
- Whether a tiny dependency-free rules assembly can be consumed by Assembly-CSharp without
  moving MonoBehaviours or BillGameCore.

**Preferred decision**

Create one small named runtime assembly containing only pure rule types, plus one Editor
test assembly referencing it. Do not migrate all gameplay code or BillGameCore.

**Fallback**

If the dependency graph makes even the small assembly high-risk, keep runtime code in
place and create a documented validation harness. Do not fake Fusion authority in NUnit.

**Acceptance**

- A short ADR records the chosen layout, dependency graph, alternatives, and rollback.
- GitNexus impact is LOW/MEDIUM or the owner explicitly approves higher risk.

### QR-401 — Add pure match-rule tests

**Executor:** Claude Code.

**Current seam**

Keep `ArenaManager` as the Fusion coordinator. Extract only deterministic decisions now
embedded in `CheckWinCondition`, `OnTimeout`, `EndRound`, and `AdvanceRound`.

**Minimum cases**

- No opponent has joined yet.
- Player leaves during a round.
- Team elimination.
- Double KO/draw.
- Timeout A win, B win, tie.
- Best-of-three completion and 1-1-1 draw.

**Acceptance**

- Tests require no Runner, scene, NetworkObject, or MonoBehaviour instance.
- ArenaManager network properties, RPC targets, and event timing remain unchanged.
- Existing 2P match regression passes after extraction.

**Guard**

- Do not create `IMatchService`, repositories, or factories for one pure calculator.

### QR-402 — Add pure configuration invariant tests

**Executor:** Claude Code.

**Candidates**

- `PlayerCombat.LivesForPlayerCount(int)` boundaries.
- `BuffRingConfig.DiameterForTier`, `ValueForTier`, and tier clamping.
- Weapon catalog unique indices, non-null required assets, prices, magazines, unlock times,
  and PPU/BuyOnce rules for the currently enabled scope.

**Acceptance**

- Tests encode the current canonical product decision, not stale GDD annotations.
- Boundary and invalid-input cases are included.

### QR-403 — Add pure throw-resolution tests only if QR-100 exposes regression risk

**Executor:** Claude Code.

Extract only a small `ThrowLaunch` calculation and fixed-size velocity sampling logic from
`ThrowController`. Keep XR input, haptics, Bill pool/timer/audio, and network spawning in
the controller.

**Cases**

- Zero swing fallback.
- Minimum/maximum speed clamp.
- Power normalization.
- Moving-player contribution.
- Sampling window boundaries.

**Guard**

- Do not add an input abstraction or throw service unless two real implementations exist.

## 8. Phase 5 — Controlled hotspot cleanup

Only execute a cleanup task when earlier phases prove the surrounding behavior and the
impact report is acceptable.

### QR-500 — Keep ArenaManager as coordinator

- Apply only the pure-rule extraction from QR-401.
- Leave Fusion lifecycle, network properties, RPCs, cleanup, and Bill events in
  `ArenaManager`.
- Verify match transition timing on two clients.

### QR-501 — Reduce NetworkProjectile risk incrementally

**Do first**

- Consider extracting only the pure ring-buff state transition or pure damage arithmetic.

**Do later, with PlayMode/2P evidence**

- Physics sweep/hit resolution.
- Mine/fuse/zone spawning.
- Twin visibility and delayed despawn.

**Guard**

- Do not introduce an `IProjectileService` or split the class into many stateful components.
- Never combine a physics rewrite, authority change, visual change, and despawn change in
  one task.

### QR-502 — Reduce ThrowController only around proven seams

- Keep input and orchestration in `ThrowController`.
- Extract launch math/sampling only if QR-403 is justified.
- Preserve `Bill.Pool`, `Bill.Timer`, `Bill.Audio`, haptic, event, and network-spawn flows.

## 9. Deferred product backlog — not part of Quest-ready completion

Create separate owner-approved plans for these items:

- T23: extend the existing connection/session API; do not rebuild
  `ConnectionFlowController` or current QuickPlay/private-room flow.
- T24: host migration after the room flow is stable.
- T32: lobby/out-game GDD delta audit and implementation.
- Arena scaling by mode, including ring zones and CrossBomb depth percentage.
- Late join: team spawn and temporary invulnerability; first decide whether the arena
  should remain closed after round start.
- Spectator/heckle mode.
- T21/T22/T29/full-weapon polish only if the owner ends the Rock-only pivot.
- Art-owner work: icons, hologram shader, weapon anchors/models, final UI art.

## 10. Claude Code execution order

Execute in this order and stop at every owner gate:

1. `QR-000` baseline.
2. `QR-001` documentation/task reconciliation.
3. `QR-100` Quest throw tests — **OWNER/HEADSET GATE**.
4. `QR-101` Quest UX pass — **OWNER/HEADSET GATE**.
5. `QR-200` two-client harness.
6. `QR-201` Shared Mode regression.
7. `QR-202` reproduced bug tasks, one at a time.
8. `QR-300` APK — **OWNER/DEVICE GATE**.
9. `QR-301` profiling — **OWNER approves numeric budget**.
10. `QR-400` test packaging ADR.
11. `QR-401` match-rule tests.
12. `QR-402` config invariant tests.
13. `QR-403` throw tests only when justified.
14. `QR-500..502` cleanup only when impact and regression evidence allow it.

## 11. Completion report template

For every task, Claude Code must report:

```text
Task:
Commit / working tree:
Docs and source read:
GitNexus impact before edit:
Files and symbols changed:
Why this is the smallest sufficient change:
Verification run:
Evidence / report path:
GitNexus detect_changes result:
Known gaps / owner gate:
```

