# M3 — Networked Avatar Redesign (thin 3-point + IK)

**Status:** DESIGN — awaiting owner review (architect-first). **Supersedes** the unified `NetworkPlayer` rig (2026-06-24).

---

## Problem (observed in the 2-player ParrelSync test)
The current `NetworkPlayer` spawns the **entire AutoHand rig on every client** and disables it on proxies
(`NetworkPlayerRig.DisableRigForRemote`, type-name matching). Symptoms:
- **Remote body does not follow** — the body capsule is pinned to the rig **root**, and the root only translates on
  **joystick locomotion**. Room-scale (physical) movement moves only the camera/hands *relative to* the root, so the
  body stays at the spawn point. Only the head/hands (their own `NetworkTransform`s) move on remotes.
- The whole approach is **heavy** (full AutoHand rig + physics hands replicated) and **fragile** (disabling AutoHand by
  reflected type name, camera/locomotion fighting `NetworkTransform`).

## Target (locked with owner)
Split the player into **(1) a local-only heavy rig** and **(2) a thin networked avatar** reconstructed with IK.
Standard VR multiplayer pattern (Gorilla Tag / VRChat / Meta Avatars).

### What crosses the wire — per player
| Networked field | Drives on remote |
| --- | --- |
| **Head** pos+rot | neck/head bone (follows HMD) |
| **Wrist L** pos+rot | left-arm IK target |
| **Wrist R** pos+rot | right-arm IK target |
| **Root** pos+yaw | stance position + body facing |
| **ColorIndex** (existing `[Networked]`) | per-player tint |

**No hand-state / no finger data** — the remote avatar has **no hands, just low-poly arms ending at the wrist**.
Grab / hold / throw is purely **local** (toon hands + physics); remotes never need it.

### Local-only (never networked; only the local player sees it)
AutoHand **2 toon hands** (physics grab/hold/throw) + camera (HMD) + locomotion.

### Decisions (default — confirm before Task 5)
- **D1 — First-person:** hide the local player's **own** networked avatar entirely; the player sees only their local
  toon hands. (Add a "look-down body" later if wanted.)
- **D2 — Remote arms, Phase A:** simple **forearm stretch** (shoulder → wrist), cheap. **Phase B:** upgrade to
  **Animation Rigging** `TwoBoneIK` (shoulder → elbow → wrist), free package `com.unity.animation.rigging`. Same 4 synced
  points — no netcode change between phases.

---

## Components
1. **`PlayerRig`** (local-only, `DontDestroyOnLoad`) — the AutoHand `XRPlayer` rig **minus** `NetworkObject` /
   `NetworkTransform`. Spawned once for the local player only. Exposes `Head`, `WristL`, `WristR`, `Root` transforms and
   runs all gameplay (grab/throw).
2. **`NetworkAvatar`** (Fusion prefab, thin) — `NetworkObject` + `NetworkTransform` on `{Root, Head, WristL, WristR}` +
   low-poly placeholder visuals (head, body capsule, 2 arms, **no hands**) + `[Networked] ColorIndex`. Label
   `FusionPrefab` so it auto-registers.
3. **`AvatarDriver`** (on `NetworkAvatar`) — `HasStateAuthority` ⇒ each frame copy the local `PlayerRig`'s
   `Head/WristL/WristR/Root` into the avatar's networked transforms. Proxy ⇒ `NetworkTransform` interpolates; pose
   head node + arms from those nodes; hide own avatar per **D1**.

```
PlayerRig (local-only, DDOL)            NetworkAvatar (Fusion prefab, thin)
  AutoHand 2 toon hands + cam + loco       NetworkObject + NT{Root,Head,WristL,WristR}
  grab / throw (physics, local)            low-poly body + arms (NO hands) + ColorIndex
            │  owner writes each frame
            └──────────────►  AvatarDriver (HasStateAuthority)
                              copy Head/WristL/WristR/Root → avatar
   Remote (proxy): NT interpolates 4 nodes → pose head + arms by IK. No physics, no AutoHand.
```

---

## Fusion verify checklist (against Fusion 2.0.12 docs — do per step)
- **Shared Mode authority:** the spawner holds **State Authority** over its own avatar ⇒ the owner writes
  `NetworkTransform`. Verify the copy runs for authority only (not on proxies).
- **Interpolation:** proxies use `NetworkTransform` built-in interpolation — no manual lerp.
- **Spawn:** `PlayerSpawnManager` spawns `NetworkAvatar`, calls `SetPlayerObject`, re-checks on
  `FusionSceneLoadDoneEvent` (persist Main → Arena, no duplicate).
- **Runner root:** apply the `EnsureRunner` `DontDestroyOnLoad`-root fix (separate console finding) so the runner
  survives the Single-mode Main→Arena load.

---

## Tasks
1. ✅ This design doc (review gate).
2. Thin `NetworkAvatar` Fusion prefab (NetworkObject + 4× NetworkTransform + low-poly visuals, no hands).
3. `PlayerRig`: make the AutoHand rig local-only (strip NetworkObject/NT, DDOL, expose tracking points).
4. `AvatarDriver`: owner copies Head/WristL/WristR/Root → avatar each tick (Shared Mode authority).
5. Remote posing Phase A (head node + forearm-stretch arms + body at root; hide own avatar per D1).
6. Update `PlayerSpawnManager` (spawn `NetworkAvatar` + ensure one local `PlayerRig`; link driver).
7. Remove old `NetworkPlayer` prefab + `NetworkPlayerRig.cs`; clean references.
8. 2-player ParrelSync test (remote body+arms+head follow; local sees only toon hands; Main→Arena persists).

## Prefab gut list
- **Remove:** `NetworkPlayer` (heavy rig prefab), `NetworkPlayerRig.cs` (type-name disable logic).
- **New:** `NetworkAvatar` (thin) — built from primitives for Phase A.
- **Repurpose:** AutoHand `XRPlayer` rig → local-only `PlayerRig` (keep camera/hands/locomotion; drop NetworkObject).
