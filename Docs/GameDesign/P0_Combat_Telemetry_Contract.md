# P0 Combat Telemetry & Reject-Reason Contract — task 0.2.2

> Scope: the event schema Phase 1 (`1.1`–`1.3`) code must emit so a human can look at one client's log (or two
> logs side by side) and explain *every* fire, hit, accept, reject, damage, death, and respawn without
> guessing. This is a data contract for implementation to follow, not code — Phase 1 tasks implement against
> this file. Logging is `UNITY_EDITOR || DEVELOPMENT_BUILD` only (matches `BillGameCore_Usage.md` §3's
> `CheatConsole`/`DebugOverlay` guard — never shipped in release).
>
> Baseline: `Gun_System_Architecture.md` §2/§4/§7 (Option A data flow + validator table),
> `TOSSZONE-Playable-Ready-Roadmap.md` §0.2 (event list + correlation requirement).

## 1. Why this exists

Without correlation, "the hit didn't register" is undebuggable — was the shot never fired locally, did the
cosmetic RPC get lost, did the `ShotClaim` never arrive, or did the victim reject it (and why)? Every event
below carries enough shared keys that a human (or a script) can reconstruct one shot's full life story from
either client's log alone.

## 2. Correlation keys (present on every event that applies)

| Key | Type | Meaning |
|---|---|---|
| `shooter` | `PlayerRef` | Who fired |
| `victim` | `PlayerRef` | Who the ray/claim targets (`PlayerRef.None` for a world/miss shot) |
| `shotId` | `uint` (or `ulong`) | Deterministic per-shot id, unique per shooter for the match lifetime — see `1.1.1`'s acceptance criterion ("unique deterministic shotId"). Recommended: `(shooterPlayerId << 32) | localShotCounter`, or a `TickTimer`-seeded counter — implementation detail for `1.1.1`, but whatever scheme is chosen, `shotId` MUST be stable across the local→cosmetic→claim chain for the same shot. |
| `weaponId` | `byte` | Catalog index (`GunCatalog`) |
| `clientTick` | `int` | Shooter's `Runner.Tick` at fire time (Fusion tick, not `Time.frameCount`) |
| `fusionTick` | `int` | The **receiving** client's `Runner.Tick` when the event is logged locally — lets you compute apparent latency (`fusionTick - clientTick`) without trusting the shooter's clock for anything but display |
| `result` | enum (per event, see below) | What happened |
| `rejectReason` | enum or `null` | Only set when `result` is a rejection — see §4 |

None of these carry `finalDamage` from the shooter — Option A forbids the shooter supplying trusted damage
(`Gun_System_Architecture.md` §3, `ShotClaim` struct comment). Damage only appears in the `damage` event
(§3.6), computed victim-side from `GunCatalog`.

## 3. Event list

All events are `struct : IEvent`, fired via `Bill.Events.Fire(...)` — matching the project's existing
event-bus convention (`BillGameCore_Usage.md` §1). Namespace suggestion: `TossZone.Combat.Telemetry` (new file,
`Assets/_Game/Scripts/Combat/CombatTelemetryEvents.cs`), separate from the gameplay events they observe so a
dev-only listener can subscribe to telemetry alone without coupling to gameplay code.

### 3.1 `shot_local` — `ShotLocalEvent`

Fired the instant `Gun.TryFire()` accepts a shot on the shooter's own client (before any RPC).

Fields: `shooter`, `shotId`, `weaponId`, `clientTick`, `origin` (`Vector3`), `direction` (`Vector3`),
`hitVictim` (`PlayerRef`, `None` if no player hit), `hitPart` (enum: `World | Body | Head`), `hitPoint`
(`Vector3`).

### 3.2 `shot_remote` — `ShotRemoteEvent`

Fired on every **receiving** client (not the shooter) when `RPC_ShotFired` (unreliable cosmetic RPC) arrives.

Fields: `shooter`, `shotId`, `weaponId`, `fusionTick`, `hitPoint`, `surface` (byte, from `ShotInfo`).

A `shot_local` with no matching `shot_remote` on another client within a reasonable window is expected and
fine (cosmetic RPC is unreliable by design — `Gun_System_Architecture.md` §5/§9#11). Don't treat that as a
bug on its own; cross-reference with `claim_accept`/`claim_reject` instead, since gameplay truth never depends
on the cosmetic RPC arriving.

### 3.3 `claim_sent` — `ClaimSentEvent`

Fired on the shooter's client immediately after `RPC_SubmitShotClaim` is invoked (reliable, targeted at the
victim's State Authority).

Fields: `shooter`, `victim`, `shotId`, `weaponId`, `origin`, `direction`, `hitPoint`, `hitPart`, `clientTick`.
This is the full `ShotClaim` payload, logged once at the source — useful to diff against what the victim
actually received if a bug is suspected in RPC marshalling.

### 3.4 `claim_accept` — `ClaimAcceptEvent`

Fired **only** on the victim's State Authority client, inside the validator, when a claim passes every check
in §5.

Fields: `shooter`, `victim` (= self), `shotId`, `weaponId`, `fusionTick`, `hitPart`, `resolvedDamage` (int, from
`GunCatalog.ResolveDamage` — this is the ONE place a damage number appears in telemetry, and it's
victim-computed, never shooter-supplied), `healthBefore`, `healthAfter`.

### 3.5 `claim_reject` — `ClaimRejectEvent`

Fired **only** on the victim's State Authority client, inside the validator, when a claim fails any check.

Fields: `shooter`, `victim` (= self), `shotId`, `weaponId`, `fusionTick`, `rejectReason` (enum, §4).
**Exactly one** reason per rejection — if a claim fails multiple checks, log the first one hit by the
validator's check order (§5 of `Gun_System_Architecture.md` lists the order: dedupe → fire-rate → range/origin
→ shooter state → weapon/equipped → hit part → victim state). Don't silently swallow a reject with no reason —
that's precisely the "reject không rõ nguyên nhân" failure mode Test Round 1's pass criteria explicitly forbid
(`TOSSZONE-Playable-Ready-Roadmap.md` §6, pass criterion 7).

### 3.6 `damage` — `DamageEvent`

Fired on the victim's State Authority client after a `claim_accept`, once `Health` is actually written.
(Distinct from `claim_accept` because `1.3.2` — HP/death/respawn integration — is currently blocked on D3; once
unblocked, this event is where the real `Health -=` write is observed. Until then, `claim_accept` alone is
enough to validate the *validator*, and `damage` stays a stub/no-op fired with the same numbers as
`claim_accept.resolvedDamage`/`healthBefore`/`healthAfter` so the schema doesn't change shape later.)

Fields: `victim` (= self), `shooter`, `shotId`, `weaponId`, `damage`, `healthBefore`, `healthAfter`, `isHead`
(bool).

### 3.7 `death` — `DeathEvent`

Fired on the victim's own client when `healthAfter <= 0` transitions from `> 0` (i.e., exactly once per death,
not once per hit after already dead).

Fields: `victim` (= self), `killer` (`PlayerRef`, the `shooter` of the lethal claim), `shotId` (the lethal
shot's id, for cross-referencing back to the exact hit that killed).

### 3.8 `respawn` — `RespawnEvent`

Fired on the respawning client when the respawn timer expires and `RestoreLives()`/HP-reset runs.

Fields: `player` (= self), `spawnPosition` (`Vector3`), `protectedUntilFusionTick` (int — when spawn
protection/invulnerability expires, so a later `claim_reject` with `SpawnProtected` can be checked against
this timestamp).

## 4. Reject reason enum

```csharp
public enum ShotRejectReason
{
    Duplicate,          // (shooter, shotId) already accepted or rejected before
    InvalidShooter,     // shooter PlayerRef doesn't resolve to a live avatar, or shooter is dead/not in round
    CombatClosed,       // ArenaManager.Phase isn't Playing (Warmup/RoundEnd/MatchEnd) when the claim arrived
    InvalidWeapon,      // weaponId has no entry in GunCatalog
    EquippedMismatch,   // weaponId doesn't match the shooter's currently-replicated EquippedSlot
    FireRate,           // claim arrives faster than the weapon's rpm/burst window allows (sliding window, see Gun_System_Architecture.md §7)
    InvalidOrigin,      // origin is implausibly far from the shooter's last-known replicated wrist/root position
    OutOfRange,         // claim's implied distance exceeds catalog range * tolerance margin
    InvalidHitPart,     // hitPart isn't one of the valid enum values, or doesn't match a real collider tag on the claimed hitPoint
    VictimDead,         // victim Health already <= 0 when the claim arrived
    SpawnProtected,     // victim is within its post-respawn invulnerability window
}
```

This matches `Gun_System_Architecture.md` §7's validator table 1:1, plus the task's own required minimum list
(`invalid shooter`, `combat closed`, `invalid weapon`, `equipped mismatch`, `fire rate`, `invalid origin`,
`out of range`, `invalid hit part`, `victim dead`, `spawn protected`, `duplicate`). No reason is a catch-all
"other" — if `1.3.1`'s implementation finds a check that doesn't map cleanly to one of these, add a new named
value rather than overloading an existing one (evolving this enum is expected; keep it exhaustive, not vague).

## 5. Walkthroughs (per 0.2.2's own verify recipe)

### 5.1 Accepted body hit

```
[Shooter]  shot_local   {shooter=A, shotId=17, weaponId=0, hitVictim=B, hitPart=Body, hitPoint=(...)}
[Shooter]  claim_sent   {shooter=A, victim=B, shotId=17, weaponId=0, hitPart=Body, clientTick=4021}
[Victim B] claim_accept {shooter=A, victim=B, shotId=17, weaponId=0, resolvedDamage=16, healthBefore=100, healthAfter=84}
[Victim B] damage       {victim=B, shooter=A, shotId=17, weaponId=0, damage=16, healthBefore=100, healthAfter=84, isHead=false}
[Other clients] shot_remote {shooter=A, shotId=17, weaponId=0, hitPoint=(...)}  ← unreliable, may or may not appear
```

Every line shares `shotId=17` and `shooter=A`/`victim=B` — one grep finds the whole story.

### 5.2 Duplicate claim

```
[Victim B] claim_reject {shooter=A, victim=B, shotId=17, weaponId=0, rejectReason=Duplicate}
```

Happens if `RPC_SubmitShotClaim` fires twice for the same `shotId` (retry, or a bug in the shooter's send
path) — the SECOND arrival gets this, the first one already produced a `claim_accept` or a different
`claim_reject`.

### 5.3 Protected victim

```
[Victim B] respawn       {player=B, spawnPosition=(...), protectedUntilFusionTick=5200}
...
[Shooter A] shot_local   {shooter=A, shotId=18, weaponId=0, hitVictim=B, hitPart=Body}
[Shooter A] claim_sent   {shooter=A, victim=B, shotId=18, ...}
[Victim B] claim_reject  {shooter=A, victim=B, shotId=18, weaponId=0, rejectReason=SpawnProtected}
```

`fusionTick` on the reject should be `<= protectedUntilFusionTick` from the prior `respawn` event — that
inequality is the actual proof the reject was correct, not just asserted.

## 6. What is explicitly NOT logged

- **Trusted final damage from the shooter.** The shooter never sends a damage number in any event (`claim_sent`
  has no `damage` field) — Option A forbids it (`Gun_System_Architecture.md` §3). The only `damage`/
  `resolvedDamage` fields in this whole contract are computed and logged by the **victim**.
- PII / player identity beyond `PlayerRef` (no usernames, no device ids) — out of scope per the roadmap
  (`TOSSZONE-Playable-Ready-Roadmap.md` §0.2 out-of-scope list).
- Production analytics backend / dashboards — this is Editor/Development-Build console logging only, per
  `CombatCheats`/`DebugOverlay`'s existing guard convention.

## 7. Implementation note for Phase 1

This file defines the *contract*; the actual `CombatTelemetryEvents.cs` struct definitions and the
`Bill.Events.Fire(...)` call sites belong to whichever Phase 1 task touches that code path
(`1.1.1`/`1.1.2` → `shot_local`; `1.2.2` → `shot_remote`; `1.3.1` → `claim_sent`/`claim_accept`/`claim_reject`;
`1.3.2` → `damage`/`death` when D3 unblocks it; `NetworkAvatar`'s existing respawn path → `respawn`). Run
GitNexus `impact` before touching any of those existing symbols, per project rule.
