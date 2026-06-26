# TOSSZONE — Throw Mechanic Spec (M4 §2): Mechanic + Feel/Juice — **LOCKED**

**Status:** SPEC LOCKED 2026-06-26 (session 4) for implementation. Supersedes the prose in
`M4_Gameplay_Design.md` §2 and the old GDD `1.4.x` behind-head breakdown in `tasks.json`. Feel-first build
order at the end (= the tasks `1.4.s1`..`1.4.s9`).

> Context: avatar = **3-point networked** (head + 2 wrists), Stickman model, `AvatarArmPoser`/`AvatarLegPoser`.
> Throw is **LOCAL input** on the toon hands (AutoHand), only active in **`02_Arena`**. **No Fusion Physics
> Addon** → projectiles are **`BillTween`-driven, NOT physics**. Read `Docs/Fusion_Shared_Mode_Gotchas.md`
> before any networking. Code per the `billgamecore` skill: `Bill.*` services, `BillTween` (NEVER DOTween),
> `Bill.Pool`, `Bill.Events`, namespace `TossZone.Throwing`, zero-GC hot paths.

---

## 1. Locked decisions
| # | Decision | Locked value |
|---|---|---|
| Trigger | Wind-back required? | **YES** — arm when hand goes BEHIND the plane, fire on the FORWARD swing across it. Quơ tay lung tung không bắn. |
| Plane | Spawn / trigger plane | **Body/chest level**, in body-local space (moves + faces with the player). Height is a **tunable param** → move it up = behind-head overhand. |
| Velocity | Fire threshold | **LOW threshold + HIGH power floor** → flick nhẹ vẫn ném mạnh. |
| Cooldown | Throw rate | **~0.4s** internal cooldown (GDD basic weapon). |
| Release grab | Mid-hold release | **Cancel** — held ball hides, nothing flies. |
| Aim | Direction | **Swing direction at fire moment** (clamped to a forward+up cone). Aim by how you swing. |
| Projectile | Motion | **Pooled `BillTween` along an `AnimationCurve` arc, NOT physics.** |
| Held vs flying | Split ball | **Held** = networked in-hand ball (others see). **Flying** = separate networked projectile (state-auth = thrower). On fire: held **hides**, projectile **spawns** (a flash masks the swap → looks continuous). |
| Damage | Phase | **Deferred.** Feel + hit-juice (bounce number) first; no damage/health logic yet. |

## 2. Mechanic — hand state machine
Grab once → **hold** → wind-back arms → forward swing fires → auto-refill → repeat. Release = cancel.

```
Empty ──(press grab)──────────────► Loaded (ball materializes in hand)
Loaded ──(hand crosses BEHIND plane)──► Armed
Armed ──(hand swings FORWARD across plane, v > vMin)──► FIRE
        • spawn flying projectile (swing dir + arc), held ball hides, flash @ hand
        • start cooldown (~0.4s)
        • still holding grab → after cooldown, auto-refill ──► Loaded
ANY state ──(release grab)──► held ball hides ──► Empty   (cancel, nothing flies)
```

**Trigger detail (the wind-back nuance):**
- **Plane** = a horizontal plane at chest height in **body-local** space (origin ≈ chest, normal = body forward); it moves and rotates with the player.
- **Armed** when the controller hand crosses **behind** the plane (local −Z past `windBackDepth`).
- **FIRE** when, *while Armed*, the hand crosses **forward** through the plane with forward velocity `> vMin`.
- **Direction** = hand velocity dir at fire, clamped into a forward+up cone (so throws always go up-and-out).
- **Power** = `powerCurve(|hand velocity|)` with a high floor (light flick still launches hard).
- Cooldown gate after FIRE before the next refill (rate-limits continuous throw).

## 3. Split ball (held ≠ flying)
- **Held ball** — a networked object **followed to the hand** (reuse/evolve `NetworkGrabbable`), visible to all,
  NOT physics-grabbed (it's a display ball that the thrower "holds"). Hides on FIRE, re-shows on refill.
- **Flying ball** — a **separate** networked projectile spawned at the body-level plane on FIRE, transform driven
  by a `BillTween` arc. The player never throws the *held* ball itself — the swap is hidden by a launch flash so
  it reads as one continuous motion.

## 4. Juice / feel — by throw phase
**VR principle: haptic + spatial audio + world-space VFX = your "screenshake." NO camera shake / FOV yank (nausea).**

1. **Wind-up (tay ra sau)** — held ball pulse/glow grows + rising haptic rumble + charge whoosh. Telegraph power.
2. **Release (FIRE) — invest most:** sharp **haptic punch** (scaled by power) · launch SFX (pitch by power) ·
   held-ball **poof + flash** at the hand (masks the swap) · projectile spawns with **squash→stretch** + a muzzle
   ring · **hand recoil** (BillTween nudge back 1–2 frames).
3. **Flight (tween arc)** — glowing **trail ribbon** (color by ammo) · stretch along velocity + spin · **arc ease-out**
   (vọt fast then float, authored by `arcCurve`) · spatial **whoosh doppler** as it passes other players.
4. **Impact / land** — particle **burst** + expanding **shockwave ring decal** · **bounce number** punch-scale then
   fade (BillTween — pure juice, no damage yet) · haptic **tick** for thrower + strong **rumble for the hit player** ·
   **hit-stop** micro-freeze **(LOCAL visual only — never freeze the networked sim)** · punchy layered impact SFX.
5. **Continuous-throw rhythm** — auto-refill = **scale-in pop + haptic tick** → a "lên đạn" cadence that makes
   repeated throwing feel rhythmic.

## 5. Feel levers — `ThrowConfig : ScriptableObject` (BillInspector attributes)
`powerCurve` (AnimationCurve |vel|→power, high floor) · `arcCurve` (t→height) · `timeToTarget` ~0.4–0.7s ·
`vMinFire` · `planeHeight` · `windBackDepth` · `aimConeDeg` · `cooldown` ~0.4s ·
haptic envelopes (wind/release/impact: amplitude+duration) · sfx pitch range.

## 6. Networking (Fusion Shared, no Physics Addon)
- **Held ball** networked, followed to the hand, visible to all. **Flying projectile** networked,
  **state authority = thrower**; transform driven by the `BillTween` arc on the authority, replicated via
  `NetworkTransform`; proxies interpolate. End/hit → despawn (return to `Bill.Pool`).
- **Juice = cosmetic LOCAL**: each client plays VFX/haptic from networked events / spawn-despawn, so no juice
  needs authority. **Hit-stop is local-only.** The **hit player's haptic** = a networked hit event → that client
  rumbles itself. FIRE is detected on the thrower's client (input authority) which spawns the projectile.

## 7. BillGameCore mapping
- `ThrowController` (local, per-arena, on the rig): hand state machine; reads AutoHand grip + controller velocity; drives FIRE.
- `ThrowConfig : ScriptableObject` — all feel levers (§5).
- `Bill.Pool` — projectile, trail, impact VFX, bounce number (many spawns, zero-GC).
- `BillTween` — arc flight, scale-pop, recoil, number punch. **NEVER DOTween.**
- `Bill.Audio` — launch/impact SFX, pitch-vary by power.
- `Bill.Events` (EventBus) — `BallThrownEvent` / `BallLandedEvent` / `BallHitEvent` → juice listeners
  (audio / haptic / VFX) subscribe **decoupled**, so juice layers without touching the controller.
- Haptics — AutoHand hand API / XR `SendHapticImpulse`.

## 8. Implementation order (feel-first) — the process  → tasks `1.4.s1..s9`
- **S1** `ThrowController` + hand state machine + wind-back→fire trigger (local; debug-log FIRE, no ball yet).
- **S2** Held ball networked in-hand (auto-grab on press, refill after cooldown, release = cancel); others see it.
- **S3** Flying projectile — pooled `BillTween` arc, networked spawn (state-auth = thrower, `NetworkTransform`), split-ball swap on FIRE.
- **S4** Feel core — `ThrowConfig` powerCurve(high floor) + arcCurve + cooldown + aim cone. Tune until the throw *feels* good (no juice yet).
- **S5** Juice T1 — haptic 3-tier (wind/release/impact) + launch & impact SFX. *(most feel per effort)*
- **S6** Juice T2 — release flash + projectile squash-stretch + glow trail ribbon.
- **S7** Juice T3 — bounce-number punch + impact burst + shockwave decal; wire via `Bill.Events`.
- **S8** Hit feedback — bounce number on the hit player + cross-player haptic (networked hit event). Damage still deferred.
- **S9** 2-player verify (ParrelSync / 2 Quest) — others see held + flying ball + impact; tune lag/feel.

Each slice: compiles clean + verified via MCP where possible before the next.
