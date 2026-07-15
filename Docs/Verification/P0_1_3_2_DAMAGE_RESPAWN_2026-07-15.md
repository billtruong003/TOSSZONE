# P0 1.3.2 — Catalog damage, death, respawn (2026-07-15)

## Result

PASS in two live Unity Editor clients (`TOSSZONE` player 4 and ParrelSync clone player 3), Shared Mode, scene `01_TOSSZONE_Main`.

D3 is locked for v0.3-P0: 100 HP, death at HP <= 0, respawn resets to 100 HP. Shooter payload contains no trusted damage; the victim resolves damage from its local `GunCatalog` after `ShotClaim` validation.

## Verified state transitions

| Case | Wire evidence on victim authority | Result |
| --- | --- | --- |
| Body, weapon 0 | `damage=16 head=False hb=100 ha=84` (also observed full sequence 100→84→68→52→36→20→4→0) | PASS |
| Head, weapon 0, shot 77016001 | `damage=32 head=True hb=100 ha=68` | PASS |
| Lethal + simultaneous second claim, shots 77015001/77015002, tick 51090 | first `claim_accept damage=16 hb=16 ha=0`; second `claim_reject reason=VictimDead` | PASS |
| Delayed pre-respawn claim, shots 77017001/77017002 | lethal accepted at tick 68549; a separate later MCP call reached victim at tick 69062 and rejected `VictimDead` | PASS |
| Event cardinality for the same lethal cycle | marker `P0132EXACT`: exactly one `died local=True`, exactly one `respawn local=True` | PASS |
| Respawn state | authority query after the natural 5-second timer: `hp=100` | PASS |
| Late claim during spawn protection, shot 26182667 | `claim_reject reason=SpawnProtected` | PASS |

Before the exact-cardinality run, a separate 30-second-delay probe observed `hp=0`, `RespawnTimer.IsRunning=True`, `expired=False`, proving the dead state is held before respawn. A test-harness-only timer write performed after an earlier natural respawn produced an extra probe event; that contaminated marker was discarded. The clean `P0132EXACT` run above did not mutate the timer after lethal and is the evidence used for event count.

## Implementation boundary

- `AvatarWeaponSync.ProcessShotClaim` remains the reliable victim-authority wire seam and resolves damage locally.
- `PlayerCombat.ApplyValidatedDamage` is the single validated gun Health-write seam, clamps HP at zero and emits death only on the `previous > 0 -> 0` transition.
- `NetworkAvatar.HandleRespawn` keeps the existing timer/teleport lifecycle; `RestoreLives` now restores 100 HP and arms 3 seconds of protection.
- Score/killer attribution is not written here; it remains task 1.3.3.

## Console and environment notes

No new exception was correlated with the damage/death/respawn path. Existing environment noise remained: Meta XR form-factor unavailable, PanelSettings/theme and missing-package-script warnings, SRP `stereoTargetEye`, plus prior Photon reconnect/scene-load churn. `PlayerSpawnManager` logged caught retryable NREs during reconnect and later spawned both players successfully. The `ArenaNetworkLoadGate` runtime-object pre-load cleanup in this batch addresses the observed Fusion `NetworkAvatar(Clone) already owned Runner` reconnect failure.

## Follow-up risk

GitNexus marks `PlayerCombat` CRITICAL (12 direct dependants, 215 upstream symbols, 86 affected flows). The old throwing/rock, ring/buff and legacy combat interactions still depend on player/combat state. They require a separate inventory-and-disable batch before the gun-only loop can be considered product-clean; this task does not silently mark those legacy interactions removed.
