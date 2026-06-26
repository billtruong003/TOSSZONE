# Fusion 2 — Shared Mode gotchas & verified corrections (read before touching networking)

> **Why this file exists.** An AI session shipped networked grab code that compiled but failed at runtime
> (only the host could grab) because of a wrong mental model of Fusion authority. Every claim below is
> **verified against the official Photon Fusion 2 docs** (sources at the bottom), not assumed. If you are an
> AI continuing this project: read this before writing or "fixing" any Fusion code, and **verify any new
> claim against the docs the same way** — do not pattern-match from other engines or from memory.
>
> This project runs **Fusion 2.0.12, Shared Mode, NO Physics Addon**. Facts are scoped to that unless noted.

---

## 1. Authority: State Authority is the ONLY authority in Shared Mode

- **State Authority** = the one client allowed to modify a NetworkObject's `[Networked]` state. This is the
  authority that matters in **Shared Mode**.
- **Input Authority** exists **only in Server modes** (Host / Dedicated / Single). It is **not applicable in
  Shared Mode** — do not gate Shared-Mode logic on input authority.
- `HasStateAuthority` is **per-object** and answers "is THIS client the state authority of THIS object right
  now." It is **not** a stable "is this mine / is this local" flag — it changes as authority moves.

## 2. `RequestStateAuthority()` is ASYNCHRONOUS and can be DENIED  ⬅ this caused the grab bug

❌ **Wrong assumption:** "call `RequestStateAuthority()` and I have authority this frame; poll
`HasStateAuthority` to drive the object." — This is why the original grab only worked for the host.

✅ **Verified behavior:**
- `RequestStateAuthority()` is **async**. You do **not** have authority on the same frame.
- It **succeeds only if** one of these is true:
  1. the NetworkObject has **`Allow State Authority Override` = true**, **OR**
  2. the previous State Authority called `Object.ReleaseStateAuthority()`.
- An object **spawned** by a client (or the Shared-Mode master) **already has a State Authority** (the
  spawner). So another client can take it over a held/owned object **only if `Allow State Authority Override`
  is checked on that prefab's NetworkObject.** If it is NOT checked, every other client's request is silently
  denied → they can never control it. **This was the whole grab bug.** (Both our prefabs were at the default
  `Flags: 262145`, which does NOT include the override bit.)
- When authority is granted, **`IStateAuthorityChanged.OnStateAuthorityChanged()` fires on every client.**
  React to that (or `await Object.WaitForStateAuthority()`), do **not** busy-poll `HasStateAuthority`.

**Rule of thumb:** any object that more than one player needs to take over (grabbables, shared props) **must
have `Allow State Authority Override` checked**, and usually **`Destroy When State Authority Leaves` unchecked**
(so it isn't deleted when the grabber/ spawner leaves).

## 3. The official grab pattern (VR Shared sample) — copy this shape, not a naive kinematic toggle

The Photon VR Shared sample's `NetworkHandColliderGrabbable` does, in order:
1. On grab: store the hand offset, set a status like `WillBeGrabbedUponAuthorityReception`, then
   **`await Object.WaitForStateAuthority()`**; only AFTER authority arrives set the `[Networked]` grab vars
   (`CurrentGrabber`, local pos/rot offsets). `LockObjectPhysics()` on grab, `UnlockObjectPhysics()` on release.
2. **`FixedUpdateNetwork()` (authority only):** move the object to the grabber's hand + offset (the tick state).
3. **`Render()` (ALL clients, incl. proxies):** extrapolate the object to follow the grabber every frame —
   proxies can do this because the grab state is **networked**, so they know who holds it.
   `ExtrapolateWhileTakingAuthority()` runs **before** authority is confirmed, so the grab looks instant even
   during the async authority window.
4. **`[DefaultExecutionOrder]`** is set so the grabbable's `Render()` runs **after** `NetworkTransform`, letting
   it override the interpolated network position with the hand-follow position. (e.g. `NetworkRig.EXECUTION_ORDER
   = 100`, headset `+10`, grabbable renders after NetworkTransform.)

❌ Our simpler approach (toggle `isKinematic` by `HasStateAuthority`, keep dynamic while locally held) is an
**approximation**. It needs the override flag to work at all, and it has no `Render()` extrapolation, so the
grabbed ball can lag/snap during the ~1-RTT authority transfer. If the override-flag fix isn't smooth enough,
**adopt the sample pattern** (await authority + Render extrapolation + networked holder + execution order).

## 4. NetworkTransform + Rigidbody without the Physics Addon

- On **proxies** (non-authority), Fusion 2.1 sets the Rigidbody to **kinematic** and the object runs in "remote
  time," interpolated in **`Render()`**. (Fusion 2.0 did NOT auto-kinematic proxies — version-dependent.)
- **Without the Physics Addon**, "interpolation targets" no longer exist; the NetworkTransform object **itself**
  is interpolated in Render. Set the Rigidbody's **Interpolate** to `Interpolate`.
- For real networked physics, write physics in Unity's **`FixedUpdate()`**, *not* `FixedUpdateNetwork()`.
- We have **no Physics Addon**, so our balls use the manual pattern: authority runs the rigidbody, NetworkTransform
  replicates the world pose, proxies are kinematic. This is fragile by nature — prefer the Addon if physics gets
  central.

## 5. Tick model — `FixedUpdateNetwork()` vs `Render()` vs `FixedUpdate()`

- Fusion is **fixed-tick**. `FixedUpdateNetwork()` (FUN) runs **every tick** on every NetworkBehaviour of an
  object that `IsInSimulation`; the object's Snapshot is captured **after all FUN have run**. Write **networked
  state / authority logic** here.
- `Render()` runs **every rendered frame**, interpolating between the two latest snapshots. Write **visual-only**
  smoothing / extrapolation here (e.g. proxy posing, hand-follow visuals).
- `FixedUpdate()` (Unity) — use for **rigidbody physics** writes (per §4), separate from FUN.
- **Owner writes, proxies read.** The authority sets transforms/state in FUN; proxies just display the
  replicated/interpolated result.

## 6. Scene loading & spawning in Shared Mode  ⬅ caused the avatar dup-spawn

- `Runner.Spawn()` in Shared Mode may be called **only on the client that intends to be the State Authority** of
  the spawned object (in Host mode, only the server). The spawner becomes the State Authority.
- **Scene** NetworkObjects: on load the **Master Client** attaches them and assigns `NetworkId`s, then tells the
  other clients to attach with those IDs.
- ⚠️ **Fusion's per-player object registry (`SetPlayerObject`/`GetPlayerObject`) is NOT preserved across a
  Single-mode networked scene load.** A spawned avatar object can survive the load while its player-object
  mapping is lost → a spawn guard that checks only `TryGetPlayerObject` will spawn a **duplicate**. Our fix:
  keep a static `NetworkAvatar.Local` (set in `Spawned` when `HasStateAuthority`, cleared in `Despawned`) that
  survives the load, reuse it, and re-`SetPlayerObject` if the registry was lost. See `NetworkAvatar.cs` /
  `PlayerSpawnManager.cs`.

## 7. `NetworkObjectFlags` (the `Flags:` int on a NetworkObject in the prefab YAML)

- It's a `[Flags]` bitmask: `None, MaskVersion, V1, Ignore, MasterClientObject, DestroyWhenStateAuthorityLeaves,
  AllowStateAuthorityOverride, …`. The exact bit values live in the compiled `Fusion.Runtime.dll`, **not** in
  the XML doc — **do not guess a bit to hand-edit the YAML.** Toggle the checkbox in the Inspector (or via MCP)
  and let Unity write the value.
- This project's **default** NetworkObject is `Flags: 262145`. That default does **not** include
  `AllowStateAuthorityOverride`.
- `MasterClientObject`: the current Master Client always holds State Authority (don't set this on a grabbable —
  it blocks players from taking it).
- `DestroyWhenStateAuthorityLeaves`: object is destroyed when its authority disconnects (leave unchecked for
  shared props that should persist).

## 8. Concrete bugs this project hit (so the lesson sticks)

| Symptom | Wrong assumption | Real cause | Fix |
| --- | --- | --- | --- |
| Only the host can grab balls | `RequestStateAuthority` works for anyone instantly | object had no `Allow State Authority Override` → request denied | check that flag on `NetworkBall` |
| Grabbed ball snaps/breaks for clients | poll `HasStateAuthority` each tick to set kinematic | FUN flipped the ball kinematic during the async auth window | keep it dynamic while locally held (`_heldLocally`); ideally Render-extrapolate per §3 |
| Duplicate avatar on Main→Arena | `TryGetPlayerObject` survives scene load | the player-object registry does NOT survive it | static `NetworkAvatar.Local` reused across loads (§6) |
| Runner DDOL warning | runner can be parented | Fusion DDOLs the runner; it must be a **root** object | create the runner as a root GameObject (`FusionNet.EnsureRunner`) |

---

## Sources (verified 2026-06-26)
- [Fusion 2 — Network Object](https://doc.photonengine.com/fusion/current/manual/network-object) — State vs Input Authority, `RequestStateAuthority` async + override/release conditions, `OnStateAuthorityChanged`.
- [Fusion 2 — VR Shared technical sample](https://doc.photonengine.com/fusion/current/technical-samples/fusion-vr-shared) — grab pattern (`WaitForStateAuthority`, FUN vs Render, execution order, `Allow State Authority Override` checked / `Destroy When State Authority Leaves` unchecked), rig/head/hand sync.
- [Fusion 2 — Physics (shared)](https://doc.photonengine.com/fusion/2-shared/manual/physics) & [Physics Addon](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1) — proxy rigidbodies kinematic, interpolation, `FixedUpdate` for physics, no-addon behavior.
- [Fusion 2 — Network Behaviour](https://doc.photonengine.com/fusion/current/manual/network-behaviour) & [Spawning](https://doc.photonengine.com/fusion/current/manual/spawning) — tick model, FUN/Render/Spawned/Despawned, scene-object attach, `Runner.Spawn` authority rule.
