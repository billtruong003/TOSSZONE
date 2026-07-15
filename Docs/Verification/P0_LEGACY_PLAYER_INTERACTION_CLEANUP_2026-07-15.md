# P0 Legacy Player-Interaction Cleanup — 2026-07-15

## Status

**InProgress — implementation/static verification complete; final fresh runtime matrix pending.**

Task 1.3.5 must not move to Done until both Unity instances can run the complete matrix after the final `TrainingRangeController` ring bindings were cleared.

## Scope applied

- `NetworkAvatar.prefab`: disabled `ThrowController` and `HandWeapon`; the legacy wrist selector panel remains inactive.
- `01_TOSSZONE_Main`: deactivated `[ThrowSystem]`; cleared the training controller's ring buttons, burst button, ring-spawner prefab and anchor bindings.
- `02_Arena` and `02_FPSMAP`: deactivated `WeaponHolder`, `RingSpawner` and `[ProjectileBurstSystem]`; cleared `ArenaManager._ringSpawner`.
- `DummyAvatar.prefab`: disabled `DummyBotDriver`, which was independently producing `NetworkProjectile(Clone)` every 2–3.5 seconds.
- Preserved AutoHand, `PlayerRig`, locomotion, `GunInput`, `PlayerCombat`, `ArenaManager`, AR proxy/reload and all reusable legacy scripts/assets.

No gameplay C# symbol was modified in this batch. Changes are serialized composition only.

## Impact gates

GitNexus upstream impact was run before editing the affected composition owners. Avatar/hub scene-bound owners were LOW by symbol graph, while direct Unity YAML bindings were inspected as the authoritative runtime edges. `RingSpawner` and `ProjectileBurstSystem` were CRITICAL due to their broad arena/process reach; the cleanup therefore nulls/disables only composition roots and does not delete or rewrite those systems.

## Observed runtime evidence before the final Main ring unbind

### RED baseline

- Main initially reported four enabled `ThrowController` instances, three enabled `HandWeapon` instances and one enabled `WeaponHolder`.

### Solo cleanup checks

- Main after avatar/hub cleanup: enabled legacy input owners `0`; `GunInput=1`; `PlayerRig=1`.
- Main → FPSMAP: enabled legacy owners `0`; `GunInput=1`; `PlayerRig=1`; active `RingSpawner=0`; active burst system `0`.
- A delayed `NetworkProjectile(Clone)` was traced to `DummyBotDriver`; after disabling that prefab component and restarting, `enabledDummyBot=0` and `projectilesAfterWait=0`.
- Main → Arena: `GunInput=1`, active ring spawner `0`, active burst system `0`, network projectiles `0`.

### Two-client cleanup checks

- Primary and clone joined the same Shared Mode session as players 1 and 2.
- Both clients reported: enabled legacy owners `0`, `GunInput=1`, network projectiles `0`, held-ball visuals `0`, valid avatars `2`.
- Local and remote equipped slots were `0`; the AR proxy path remained present.
- A direct gun test returned `TryFire=True`, ammunition `30 -> 29`, and `phaseAllows=True`, proving the AR does not consume a ring/buff gate.

That direct test also exposed three runtime ring objects created by the Main training hub. The serialized `TrainingRangeController` ring bindings were then cleared. Static YAML assertions confirm all four fields are now empty/null, but the MCP bridge failed before a fresh Play Mode session could prove the final zero-ring state.

## Static verification after final edit

- `NetworkAvatar` legacy component records have `m_Enabled: 0`.
- `DummyBotDriver` has `m_Enabled: 0`.
- Main training ring fields are `[]` / `{fileID: 0}`.
- Arena and FPSMAP legacy producer roots remain deactivated and `ArenaManager._ringSpawner` is null.

## Verification blocker

Both editor processes remained alive, but Unity MCP command execution stopped responding. A final bridge probe on primary returned:

`Connection closed before reading expected bytes`

Editor telemetry also showed repeated command-TCS timeouts. We did not kill unidentified Unity processes or fabricate exporter/runtime results. `tasks.json` remains exporter-owned and is intentionally not hand-edited while the menu command is unavailable.

## Required closing run

After restarting/reconnecting both Unity MCP bridges, run a fresh Main → FPSMAP two-client session and record:

1. `ThrowController`, `HandWeapon`, `WeaponHolder`, wrist selector, held ball and legacy weapon counts remain zero.
2. `RingSpawner`, `BuffRing`, `BuffZone`, burst systems and `NetworkProjectile` remain zero after waiting beyond the former dummy fire interval.
3. AR fire decrements ammunition with zero rings/buffs; grip does not force-grab a rock; reload still works.
4. Warmup/RoundEnd still block fire through the existing phase gate.
5. Body/head ShotClaims, death, respawn protection and late join/reconnect still pass on two clients.
6. Console has no new cleanup-related error.

Only then mark 1.3.5 Done, unblock 1.3.3, and regenerate `Docs/tasks.json` with `Tools/TOSSZONE/Export tasks.json`.
