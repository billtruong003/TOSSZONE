# AutoHand grab & held-object physics — patterns (learned from the Shmackle project)

**Purpose:** the "right way" to grab + hold + throw objects with AutoHand so they (a) actually follow the hand,
(b) auto-pose the fingers, and (c) don't make the hand jitter. Distilled from studying
`D:/Projects/Shmackle/Shmackle` (a shipping AutoHand VR project) + AutoHand source. Follow this for any grabbable
in TOSSZONE.

---

## 0. The #1 gotcha — a grabbable Rigidbody must be **NON-kinematic**

`Hand.Grab(RaycastHit, Grabbable, …)` (`Assets/AutoHand/Scripts/Hand/Hand.cs:277-282`) early-outs:

```csharp
bool objectFree = grab.body.isKinematic != true && grab.body.constraints == RigidbodyConstraints.None;
if (!grabbing && holdingObj == null && CanGrab(grab) && objectFree) { … grab + auto-pose … }
```

If `isKinematic == true` (or the body has constraints), **the grab routine never starts** → no follow, no
auto-pose, fingers stay open. This was the TOSSZONE held-ball bug. **Grabbables are dynamic** (`isKinematic = false`).
Networking that forces kinematic on remote copies is fine — only the _locally held_ copy must be dynamic.

## 1. How to grab (so auto-pose engages) — raycast, then `hand.Grab(hit, grabbable)`

Do **not** rely on `ForceGrab` for posed grabs. Shmackle's `AutoGrabber.cs` pattern:

```csharp
if (grabbable.IsHeld() || hand.IsGrabbing()) return;               // guard
Vector3 dir = (grabbable.transform.position - hand.transform.position).normalized;
if (Physics.Raycast(hand.transform.position, dir, out RaycastHit hit, dist, LayerMask.GetMask("Grabbable"))
    && (hit.collider.gameObject == grabbable.gameObject || hit.collider.transform.IsChildOf(grabbable.transform)))
    hand.Grab(hit, grabbable);                                      // RaycastHit overload
```

The `RaycastHit` is what `Hand.AutoPose(hit, grabbable)` (`Hand.cs:574`) needs to compute the grab point and bend
the fingers. `ForceGrab`/instant grab can skip the gentle-grab path → snap + no/weak pose. If you spawn the ball
_at_ the hand, place it a few cm in front first + `Physics.SyncTransforms()` so the raycast has a target (see
`ThrowBallHolder.GrabBall`). The grab GrabType is taken from the **grabbable's** `grabType` field.

## 2. Auto-pose needs a **Collider on the "Grabbable" layer**

Fingers spherecast and bend until they hit a collider (`Finger.cs:208 BendFingerUntilHit(steps, layermask)`,
radius at `Finger.cs:58`). During the grab, `Hand.AutoPose` temporarily moves the grabbable to the **"Grabbing"**
layer (`Hand.cs:574-576`), so the _resting_ layer doesn't drive posing — but the **grab raycast** uses the
**"Grabbable"** layer, so the grabbable must live there. No collider = no finger wrap (a bare mesh never poses).

## 3. Grabbable prefab recipe (copy this)

**Rigidbody** (authored / at-rest — AutoHand overrides interpolation+collision at grab time, see §4):
`isKinematic = false` · `useGravity = true` · `mass = 1` · `angularDrag ≈ 0.05` · `interpolation = None` ·
`collisionDetection = Discrete` · no constraints. Optionally `excludeLayers` = the player locomotion-body layer so
the held object can't fight the player capsule.

**Grabbable** (hold/throw tuning, from Shmackle's `InteractableObject.prefab`):
`grabType = GrabbableToHand (2)` · `singleHandOnly = true` · `instantGrab = false` · `useGentleGrab = true` ·
`parentOnGrab = false` · `heldNoFriction = true` · `minHeldDrag = 1.5` · `minHeldAngleDrag = 3` ·
`minHeldMass = 0.1` · `maxHeldVelocity = 10` · `jointBreakForce = 2000` · `throwPower = 1`.

A `SphereCollider` (or fitted collider) sized to the visual; on the **"Grabbable"** layer.

## 4. What AutoHand does automatically on grab (so you DON'T pre-set it)

`Grabbable.SetGrabbedRigidbodySettings()` (`Grabbable.cs:953-988`) runs on grab and is the anti-jitter core for the
_held object_:

- `collisionDetectionMode = ContinuousDynamic` (or ContinuousSpeculative if kinematic)
- `interpolation = None`
- `solverIterations = 100`, `solverVelocityIterations = 100` (so you don't need to raise the global solver)
- drag floors: `linearDamping ≥ minHeldDrag (1.5)`, `angularDamping ≥ minHeldAngleDrag (3)`
- `mass ≥ minHeldMass (0.1)`
- applies a **NoFriction** physics material if `heldNoFriction`
  On release, `ResetGrabbedRigidbodySettings()` (`Grabbable.cs:992-1009`) restores the authored values.
  **Held objects follow the hand via AutoHand's internal ConfigurableJoint** (`Resources/DefaultJoint.prefab`),
  not via parenting and not via `PhysicsFollower` (that one is for the _hand body_).

## 5. Hand jitter — fix on the HAND, not the object

The classic AutoHand hand shake comes from a light/under-damped **hand Rigidbody**. Shmackle's player hands use:

- Hand **Rigidbody**: `mass = 10` · `interpolation = Extrapolate` · `linearDamping = 10` · `angularDamping = 35` ·
  `collisionDetection = ContinuousDynamic`
- Hand **follow**: `followPositionStrength = 100` · `followRotationStrength = 50` · `maxVelocity = 200` ·
  `maxFollowDistance = 10` · `dragDamper = 0.2` · `angleDragDamper = 2` · `startAngularDrag = 1`
- Hand: `grabType = GrabbableToHand` · `reachDistance ≈ 0.15`

## 6. Project physics settings (meaningful VR anti-jitter)

- **Fixed timestep ≈ 0.01 s (100 Hz)** — `Time.fixedDeltaTime` (Shmackle `TimeManager.asset`). Default 50 Hz is a
  real jitter source for physics hands. Biggest global lever.
- Solver iterations can stay at the project default (10) because held objects self-bump to 100 (§4).
- `DefaultMaxAngularSpeed`, contact offset etc. left at defaults in Shmackle.

## 7. What we applied in TOSSZONE (2026-06-29)

- `Assets/_Game/Prefabs/HeldBall.prefab`: added `Rigidbody (dynamic, gravity, mass 1)` + `SphereCollider` +
  `Grabbable` (the §3 recipe), layer **Grabbable**. (Was a bare mesh → no grab/pose.)
- `ThrowBallHolder.GrabBall`: switched `ForceGrab` → raycast + `hand.Grab(hit, grabbable)` (§1), with a `ForceGrab`
  fallback. Fixes "kinematic, doesn't follow, no auto-pose".
- **Still recommended (not yet applied — global/rig-wide):** the §5 hand-Rigidbody/follow values on `RobotHand (L/R)`
  and the §6 **100 Hz fixed timestep** — these target the _hand jitter_. Apply + test feel.

## 8. Source references

- Shmackle: `Assets/_Shmackle/Models/SoundMachine/Shader/AutoGrabber.cs` (grab pattern),
  `…/ConfigurableTransformJoint.cs` (constrained grabbables: lever/slider via hand-delta + force-release),
  `Assets/_Shmackle/Prefabs/FunctionalObjects/Backup/InteractableObject.prefab` (recipe),
  `Assets/_Shmackle/Prefabs/Character/Shmackle Player Fast IK.prefab` (hand config), `ProjectSettings/*`.
- AutoHand: `Hand.cs` (Grab/AutoPose/objectFree guard), `Grabbable.cs:953-1009` (SetGrabbedRigidbodySettings),
  `Finger.cs` (BendFingerUntilHit auto-pose), `Resources/DefaultJoint.prefab` (held follow joint),
  `Tools/PhysicsFollower.cs` (hand-body follow, PD torque).
