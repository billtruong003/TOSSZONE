# P0 Company Handoff — 2026-07-14

> Continue on `codex/phase1-prep`. Do not restart Stage A, Phase 0, Phase 1.1, or the
> network-gun work already verified below. The next implementation gate is D3 HEALTH-MODEL.

## 1. Verified state at handoff

| Task | Board state | Evidence actually obtained |
|---|---|---|
| 1.2.1 Remote equipped AR proxy | Done | Two live clients observed the replicated equipped slot and exactly one stripped visual proxy on the remote wrist (`slot=0`, `proxy=True`, `renderers=4`, `guns=0`, `enabledColliders=0`, muzzle resolved). |
| 1.2.2 Unreliable remote shot cosmetic | Done | Two-client cosmetic relay delivered the expected shooter, shot, weapon, hit part and victim payload. Shooter echo is disabled; the cosmetic path contains no gameplay damage. |
| 1.3.1 ShotClaim + victim validator | Done | Solo rejection matrix passed and the two-client reliable RPC reached victim State Authority with shooter identity from `RpcInfo.Source`. The victim accepted the claim and Health remained unchanged. |

Primary evidence:

- `Docs/Verification/P0_1_2_REMOTE_PROXY_2026-07-14.md`
- `Docs/Verification/P0_1_3_1_SHOTCLAIM_2026-07-14.md`
- `Docs/TOSSZONE_TaskBreakdown.md`
- `Docs/tasks.meta.json`

`Docs/tasks.json` was regenerated through `Tools/TOSSZONE/Export tasks.json`; it must not be edited by hand.

## 2. What “tested” means here

The networking seams before damage have been tested on two clients:

- equipped state reaches the remote proxy;
- the remote proxy is visual-only and has a muzzle anchor;
- unreliable shot cosmetics reach the remote process;
- reliable ShotClaim transport reaches victim State Authority;
- transport-derived shooter identity is correct;
- validation produces an accepted result without writing Health.

The complete P0 combat loop has **not** been tested. Damage, HP, death, respawn, kill credit and score do not exist in the new gun path yet. Test Round 1 therefore remains unavailable.

## 3. Accepted verification gaps

These gaps do not reopen the completed pre-damage tasks, but must remain visible:

- 1.2.1 respawn cleanup cannot be exercised until 1.3.2 creates the death/respawn path. When that lands, clear/update `AvatarWeaponSync.LocalEquippedWeaponId` on the real unequip/despawn transition and verify no stale proxy.
- 1.2.2 packet-loss simulation was unavailable. Cosmetic and gameplay messages use separate unreliable/reliable paths by construction; an impaired-network run is still required in Test Round 1 if tooling becomes available.
- 1.3.1 `EquippedMismatch` needs at least two valid catalog weapons; `VictimDead` needs distinct shooter/victim state; `CombatClosed` needs an arena phase source. Exercise these naturally as the dependent systems become available instead of adding fake production content.

## 4. Blocking decision — D3 HEALTH-MODEL

Do not implement 1.3.2 until the owner answers exactly:

> Xác nhận v0.3-P0 dùng 100 HP, death khi HP ≤ 0 và respawn reset về 100 HP chứ?

If approved, record D3 as locked before editing gameplay symbols. The expected v0.3-P0 contract is:

- maximum and respawn Health: 100 HP;
- catalog-derived damage is resolved by victim authority;
- death occurs once when HP reaches 0 or below;
- Health is clamped and never remains negative;
- respawn resets Health to 100 and enables the configured protection window;
- the existing lives-style meaning of `PlayerCombat.Health` must not silently survive as a second health model.

## 5. Required workflow after D3 approval

1. Re-read the active board, this handoff, gun architecture and Shared Mode gotchas.
2. Run GitNexus query/context for `PlayerCombat`, `ArenaManager`, respawn and score flows.
3. Run upstream impact analysis for every symbol before editing. Warn before HIGH/CRITICAL changes.
4. Implement and verify 1.3.2 only: accepted claim → catalog damage → HP → one death → respawn/protection. Do not mix score into this batch.
5. Run Unity compile/console checks and Play Mode injection tests, then run the real two-client body/head/lethal/respawn matrix.
6. Record evidence, update Markdown/meta, export `tasks.json`, run `detect_changes`, commit and push the verified batch.
7. Only then implement 1.3.3 kill/score exactly once as a separate impact/verify/commit batch.
8. Run 1.4.1 Test Round 1 only after every prerequisite is Done on the same build lineage.

## 6. Working-tree hygiene

The following editor churn was intentionally left uncommitted because it was not proven to belong to the gun tasks:

- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
- `Assets/XR/Settings/OpenXRPackageSettings.asset`
- `Assets/_Game/Art/Weapons/P0/Mat_TracerYellow.mat`

Do not stage or revert these files without first establishing ownership and intent.

## 7. Next smallest owner action

Answer the D3 question in §4. Without that decision, no further gameplay implementation in Phase 1 is authorized.
