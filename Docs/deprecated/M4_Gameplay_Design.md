# M4 — Gameplay design: throw mechanic, ammo selector, procedural legs, teams, arm-swing run

**Status:** DESIGN — capturing the owner's 2026-06-26 vision dump for review BEFORE coding (architect-first).
Order of implementation is the owner's stated sequence. Each section lists open questions to lock first.

> Context: avatar is 3-point networked (head + 2 wrists → `NetworkAvatar` nodes, posed by `AvatarArmPoser`).
> Arena = `02_Arena`; hub = `01_TOSSZONE_Main`. No Fusion Physics Addon (see `Docs/Fusion_Shared_Mode_Gotchas.md`).

---

## 1. Procedural legs (avatar polish) — DO FIRST
3-point tracking has **no leg/foot data**, so legs must be **faked procedurally** (cannot IK "correctly").
Approach (standard procedural foot IK, per the owner's "raycast from knee/hip down to ground" idea):
- Each foot has a **planted target** on the ground. Each frame, compute a **desired** foot position under the
  hip (offset forward by the body's horizontal velocity / facing). **Raycast down** from above that desired spot
  to snap it to the floor.
- **Stepping:** when a foot's planted target drifts past a **step threshold** from its desired spot, play a quick
  **step** (lerp the planted target to the new desired spot over ~0.15s, with a small arc up). Alternate feet;
  don't step both at once.
- **Leg IK:** two-bone IK `UpperLeg → LowerLeg → Foot` toward the planted target (reuse the `AvatarArmPoser`
  solver). Bend the knee toward a forward hint.
- **Hips:** lower the hips slightly so knees bend; optionally bob/rotate hips toward the move direction.
- Standing still → feet stay planted (no sliding). Moving → feet step to follow. Turning → feet re-plant.
- **Component:** extend `AvatarArmPoser` (or a sibling `AvatarLegPoser`) running in the same LateUpdate, after the
  body root is placed. Bones available on Kyle: `Hips`, `Left_UpperLeg?/LowerLeg?/Foot?` — **VERIFY leg bone
  names** (we only confirmed arms/spine/head; need to inspect Kyle's leg bones).
- **Open Q:** (a) leg bone names on Kyle? (b) is the avatar root's horizontal velocity available to drive step
  direction (owner-only, from the rig) — or derive from the synced root delta on proxies?

## 2. Throw mechanic (core gameplay) — the meat
> 🔒 **LOCKED SPEC (mechanic + feel/juice + build order) → [`Docs/Throw_Mechanic_Spec.md`](Throw_Mechanic_Spec.md)** · tasks `1.4.s1..s9`. The prose below is the original vision; the spec doc supersedes it.

**Entering the Arena gives the LOCAL player a thrower/selector component** (only active in `02_Arena`).

### Grab → ball in hand
- Press **grab** in the arena → the hand **auto-grabs a ball** (you don't have to aim at one). The ball is a
  **networked object held in the hand** — **other players see it in your hand.**
- **Hold grab = keep throwing.** You do **NOT** release grab to throw; you can hold grab and throw repeatedly.

### Throw = swing, with a SPLIT ball (key nuance)
- The thrown ball is **not** the one in your hand. When you **swing** (hand goes behind → forward), at the moment
  the hand passes the **body-level spawn point** (ngang body), the **held network ball goes INACTIVE/hidden**, and
  a **separate networked projectile** is spawned that flies out driven by **`BillTween` along a smooth arc**
  (controllable, juicy feel) — **NOT raw physics.** So: held-ball (visual in hand) ≠ flying-ball (tween projectile).
- **Spawn point = body level**, slightly behind→front: the hand must wind back then come forward; the projectile
  spawns at the body-level point and flies forward along the swing direction.
- **Light swing still throws hard** — amplify the release velocity so even a gentle flick launches strongly.
- After the throw, the hand is "empty" of the flying ball but **still holding** (grab held) → grab auto-refills a
  new held ball so you can immediately swing again. (Continuous throw.)
- **Haptics + feel first.** Initial goal is the **throwing FEEL** (haptic pulse on release, the arc, the wind-up) —
  **no damage yet.**
- **Hit feedback (later):** when a projectile hits another player, pop a **bounce number** (dmg-style juice). Damage
  logic is deferred; just the number/juice later.

### Networking shape (per Fusion gotchas)
- Held ball: a networked object parented/followed to the hand, visible to all. (Reuse/evolve `NetworkGrabbable`.)
- Flying projectile: spawn a networked projectile (state-authority = thrower) whose transform is driven by a
  `BillTween` arc; replicate via `NetworkTransform`. On hit/end → despawn (or pool via `Bill.Pool`).
- **Open Q:** does the held ball + the flying projectile use the same prefab (toggle visual) or two prefabs?
  Recommend: one held "in-hand" ball (hidden on throw) + a pooled tween projectile spawned per throw.

## 3. Ammo / item selector (left hand)
- Per GDD there are **multiple ammo types.** The **left hand** shows a **hologram item carousel**; the player
  **scrolls up/down** (thumbstick?) to highlight an ammo type. To pick: **grab into the highlighted hologram**.
- The selected ammo type sets what the **right hand** auto-grabs/throws.
- **Open Q:** scroll input (left thumbstick Y?); ammo list source (a ScriptableObject per `Docs` GDD?); does
  switching mid-fight reload instantly?

## 4. Arm-swing locomotion (run faster)
- Gorilla-Tag-style: **swinging the arms while moving makes you run faster.** Detect hand/controller velocity
  (owner-side) → scale the locomotion speed (the AutoHand/rig move speed) by recent arm-swing magnitude.
- **Open Q:** does this replace or stack with joystick locomotion? Threshold/curve for the speed boost?

## 5. Team modes + chaos
- After the throw works: **team split 1v1 / 3v3 / 5v5** + a **chaos free-for-all** mode.
- Needs: team assignment (Fusion shared — a `[Networked] Team` per player), team spawns, scoreboard (later),
  mode selection (from the hub? a lobby UI?).
- **Open Q:** how is a match started / mode chosen (hub portal per mode? a lobby panel?); FFA vs team scoring rules.

---

## Suggested sequence (owner's order)
1. **Procedural legs** (§1) — finish the avatar so it reads as a full character.
2. **Throw mechanic** (§2) — grab→hold→swing→tween-projectile, body-level spawn, continuous, haptics. Core feel.
3. **Ammo selector** (§3) — left-hand hologram carousel.
4. **Arm-swing run** (§4).
5. **Team modes + chaos** (§5).

## Decisions to lock before coding each
- §1: Kyle leg bone names (inspect); step threshold + speed.
- §2: one prefab vs two (held vs flying); spawn-point exact placement; swing→throw trigger (velocity threshold at
  the body-level plane); how "amplify light throws" maps velocity.
- §3: input + ammo data source.
- §4: stack vs replace joystick; boost curve.
- §5: match/mode flow.
