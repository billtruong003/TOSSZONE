# P0 AR Weapon Asset Selection — 2026-07-14

> Scope: pick ONE placeholder AR model for `v0.3-P0 Network Gun Proof` (task 1.1.1). Not a final-art decision —
> P0 explicitly allows a placeholder; swapping the model later is a `modelPrefab` reference change on
> `GunConfig`, not an architecture change (`Gun_System_Architecture.md` §11).

## 1. Packs imported by owner (2026-07-14)

| Pack | State | Candidates considered |
|---|---|---|
| `Low Poly AR Weapon Pack 1` | Installer only (URP/HDRP `.unitypackage`), not extracted | none — no prefabs to evaluate |
| `Low Poly Pistol Weapon Pack 1` | Extracted (34 FBX/prefab) | not evaluated — pistol, not AR |
| `Low Poly Pistol Weapon Pack 2` | Installer only, not extracted | none |
| `Low Poly ShotGun Weapon Pack 1` | Extracted | `AR_A_1` |
| `Low Poly Weapon Pack 4_MW_1` | Extracted | `AR_T_PreSet`, `AR_U_PreSet` |
| `Low Poly Weapons VOL.1` | Extracted | `AK74`, `M4_8` |

Two packs (`AR Weapon Pack 1`, `Pistol Pack 2`) are installer-only — **not extracted**, per instruction not to
run global/unnecessary imports. Left untouched; not needed since a suitable candidate was already found in an
already-extracted pack.

## 2. Structural comparison (via `manage_prefabs get_hierarchy` + `manage_material get_material_info`)

| Candidate | Prefab object count | Materials | Shader (as imported) | Attachments baked in |
|---|---|---|---|---|
| `AR_T_PreSet` (Pack 4) | 24 | `Low Poly Weapon.mat`, `Low Poly Lens.mat`, `Plane.mat` | `Standard` (Built-in) | Grenade launcher, optic, 2 rails, muzzle flash hider, laser |
| `AR_U_PreSet` (Pack 4) | 19 | same 3 as above | `Standard` (Built-in) | Optic, forward grip, suppressor, rail, laser |
| `AR_A_1` (ShotGun Pack 1) | 13 | `Low Poly Weapon.mat`, `Low Poly Lens.mat`, `Plane.mat` | `Standard` (Built-in) | Barrel guard rail (no optic/laser baked in, but still 13 parts) |
| `M4_8` (VOL.1) | 6 | `Gun_MAT.mat` (pack-shared, 1 material) | `Standard` (Built-in) | None — Bolt/Mag/Sight/Rear_Sight/Trigger only |
| **`AK74` (VOL.1)** | **4** | **`Gun_MAT.mat` (pack-shared, 1 material)** | `Standard` (Built-in) → **converted to `Universal Render Pipeline/Lit`** | **None — Bolt/Mag/Trigger only** |

All five candidates ship with the Built-in `Standard` shader still assigned — none of the bundled URP
sub-packages had actually been run yet, so this wasn't a differentiator; every candidate would render pink in
this URP 17.3.0 project until fixed.

## 3. Selection: `AK74` (`Assets/Low Poly Weapons VOL.1/Prefabs/AK74.prefab`)

Scored against the P0 criteria:

- **Silhouette dễ đọc**: confirmed by screenshot (§4) — mag, pistol grip, front/rear sights, stock, trigger
  guard all read clearly at a glance.
- **Scale/pivot dễ chỉnh**: single root transform, no nested attachment prefabs to fight when placing a
  wrist/muzzle anchor.
- **Ít renderer/material**: lowest in the set — 4 objects total (root + 3 meshes), 1 shared material for the
  whole pack (`Gun_MAT.mat`). `AR_T_PreSet`/`AR_U_PreSet`/`AR_A_1` are 13–24 objects each with baked-in optics,
  lasers, muzzle devices, and (for `AR_T_PreSet`) a grenade launcher — all of that would need to be stripped
  back out for a clean P0 placeholder, which is wasted work now and a heavier remote-proxy render for no gain.
- **Không animation dependency**: static meshes only, no Animator/rig on any candidate.
- **URP material hoạt động**: fixed — `Gun_MAT.mat` shader changed `Standard` → `Universal Render Pipeline/Lit`
  (single-material, single-asset edit; no project-wide Render Pipeline Converter run, no HDRP touched).
- **Phù hợp Quest**: fewest draw calls/materials of the set (1 material, 3 mesh renderers vs. up to 3
  materials × up to 21 mesh renderers for the kitted-out candidates).
- **Muzzle rõ**: `Barrel` mesh at the front gives an unambiguous point to parent a `muzzleAnchor` empty to.
- **Proxy remote nhẹ**: same reasoning as Quest-fit — lightest option to replicate visually on remote clients.

`M4_8` was the runner-up (6 objects, same shared material) — kept as a documented fallback if AK74 turns out
to have a placement/collision issue once wrist-parented.

## 4. Evidence

- Fix applied: `Assets/Low Poly Weapons VOL.1/Gun_MAT.mat` — shader `Standard` → `Universal Render Pipeline/Lit`
  (via `execute_code`, single-material edit, `AssetDatabase.SaveAssets()`).
- Screenshot: `Assets/Screenshots/screenshot-20260714-070301.png` — `AK74` prefab instantiated standalone in
  `00_Bootstrap` (transient test object, created and deleted again; scene was not saved with it), confirms the
  material renders correctly (no pink/missing-shader) and the silhouette reads as an AR.
- No `manage_asset` project-wide conversion was run. No HDRP package was imported or touched.

## 5. Follow-up (not done now — belongs to task 1.1.1 itself)

- Add a `muzzleAnchor` empty child under `AK74/Bolt` (or the barrel-end position) once the wrapper prefab is
  built at `Assets/_Game/Art/Weapons/P0/`.
- `AK74.prefab` itself is **not** modified — per instruction, all P0-specific setup (wrist anchor, muzzle
  anchor, VR scale/orientation) goes on a separate wrapper prefab under `Assets/_Game/Art/Weapons/P0/`, not on
  the vendor prefab.
- The two not-yet-extracted packs (`AR Weapon Pack 1`, `Pistol Weapon Pack 2`) are left as installer-only;
  nothing currently requires extracting them for P0.
