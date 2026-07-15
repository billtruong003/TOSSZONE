# P0 Legacy Player Interaction Impact Audit — 2026-07-15

## Decision

The current P0 build is not yet a gun-only interaction loop. The legacy throwing/combat stack is live in parallel with the new AR stack. Cleanup must disable the legacy producers and their scene bindings without removing the shared AutoHand/PlayerRig foundation used by VR locomotion and input.

## Confirmed live paths

| Surface | Evidence | Effect on player | Disposition |
| --- | --- | --- | --- |
| Main-scene rock grab | `01_TOSSZONE_Main/[ThrowSystem]` has enabled `ThrowController` and `WeaponHolder`; holder fallback is `_throwBallPrefab` | Right grip force-grabs the rock/ball | Disable/remove P0 scene binding |
| Avatar legacy weapon dispatcher | `NetworkAvatar.prefab` has enabled `ThrowController` + `HandWeapon`; `NetworkAvatar.Spawned()` initializes every child `HandWeapon` | Old ballistic/projectile/hitscan/melee inputs remain available beside AR | Disable legacy components for P0 |
| Remote held-rock state | `ThrowController.LocalHoldingBall` → `NetworkAvatar.HoldingBall` → proxy `_heldBallVisual` | Remote avatars can still show a rock | Retire/force false after producers are disabled |
| Legacy direct damage | `HandWeapon`, `NetworkProjectile`, `ProjectileBurstSystem` and `BuffZone` call `PlayerCombat.RPC_TakeHit` | Bypasses reliable `ShotClaim` validation and catalog-derived gun damage | Disable all P0 producers; do not route AR through this seam |
| Rings and projectile buffs | `RingSpawner` creates `BuffRing`; projectiles apply ring stack/element; `ProjectileBurstSystem` samples rings | Old projectile cause/effect remains active | Disable spawner, rings and burst system in P0 scenes |
| Element hazards | Ice/Fire projectiles spawn `BuffZone`; Fire calls `RPC_TakeHit`, Ice freezes | Persistent legacy damage/freeze can affect players | Disable hazard producer and existing scene/runtime spawn paths |
| Legacy weapon/economy UI | `WristWeaponSelector` writes `PlayerCombat.EquippedIndex`; `WeaponHolder` and `HandWeapon` consume it | Can switch back into old weapons and rock fallback | Disable/hide legacy selector for gun-proof scenes |
| Ring reset lifecycle | `ArenaManager.StartRound` calls `_ringSpawner.ResetRings()` and round reset despawns `BuffZone`/legacy projectiles | Arena still owns legacy lifecycle | Null/unbind legacy scene references; keep round phase lifecycle |

## New AR independence from buff rings

The new AR does **not** require a ring/buff. `GunInput.Update()` gates `Gun.MatchPhaseAllowsFire` only on `ArenaManager.Instance == null || Phase == Playing`. `HitscanGun` produces `ShotInfo`; `AvatarWeaponSync` relays cosmetics and sends reliable `ShotClaim`; the victim validates and resolves damage from `GunCatalog`. No `BuffRing`, `RingSpawner`, `BuffZone`, `WeaponConfig` or `EquippedIndex` participates in this path.

Therefore the cleanup acceptance condition is explicit: during `Playing`, AR fire and damage must work with zero ring/buff objects in the scene.

## Must keep

- AutoHand hands and generic grab infrastructure required by the VR rig; disable the automatic legacy weapon holder, not the entire grab package.
- `PlayerRig`, tracked wrists/head, locomotion and controller discovery.
- `GunInput`, `Gun`, `HitscanGun`, `GunCatalog`, `AvatarWeaponSync`, gun proxy and gun feedback.
- `ArenaManager.Phase` because it is the current AR round fire gate.
- `PlayerCombat` 100 HP, validated damage seam, death/respawn and spawn protection.
- `NetworkAvatar` transform/arm replication and `IBillPlayer` registration.
- Shared cosmetic assets such as `ImpactBurst` if the new gun feedback still references them; do not delete the whole Throwing namespace or pool catalog mechanically.

## Blast radius

GitNexus index at `5510000`:

- `PlayerCombat`: CRITICAL — 12 direct dependants, 215 upstream symbols, 86 affected flows.
- `RingSpawner`: CRITICAL — 3 direct dependants, 189 upstream symbols, 85 affected flows.
- `ProjectileBurstSystem`: CRITICAL — 2 direct dependants, 257 upstream symbols, 85 affected flows.
- Scene-bound `WeaponHolder`, `HandWeapon`, `ThrowController`, `BuffZone` and `GunInput` show LOW class-level upstream counts in the graph; direct YAML bindings and runtime initialization above remain authoritative evidence that they are live.

No gameplay code or scene object was changed during this audit.

## Cleanup verification matrix

1. In `01_TOSSZONE_Main` and the P0 arena/FPS map, squeeze either grip: no rock/ball/legacy weapon is force-grabbed; normal hand tracking and locomotion still work.
2. Pull right trigger during `Playing` with no ring/buff in the scene: AR fires, ammo decrements, remote cosmetic relays and accepted `ShotClaim` changes victim HP.
3. During Warmup/RoundEnd: AR remains blocked by phase, not by ring state.
4. Two clients: zero `NetworkProjectile`, `BuffRing`, `BuffZone` or legacy burst spawned from player input; no call to `RPC_TakeHit` from player weapon input.
5. Remote avatar shows one AR proxy and no held-ball visual before/after death, respawn and late join.
6. Right grip reload still works after the automatic legacy grab owner is disabled.

