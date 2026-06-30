# TOSSZONE — Session Handoff

> Cập nhật mỗi session. Đọc file này đầu tiên khi bắt đầu session mới.

---

## Session 6 — 2026-06-30 (session vừa xong)

### Đã làm được

**Fix IK + rig (carry-over từ S5 verify)**
- `AvatarArmPoser.cs` — forearm roll fix: `LookRotation(forearmFwd) * lowerRest` thay `FromToRotation`
- `AvatarLegPoser.cs` — shin roll fix: cùng pattern, field `_lLowerLegRest / _rLowerLegRest`
- Camera eye offset: `TrackedPoseDriver.originPose = (0, -0.05, 0.07)` + `UseRelativeTransform = true`
- `PlayerRig` + `TossLocomotionInput` thêm vào scene (thiếu từ S5), wire đầy đủ
- `AutoHandPlayer.minMaxHeight.y = 1.6f` — fix capsule tự reset về 1.7

**S2 — Networked Held Ball** ✅
- `NetworkAvatar.cs`: thêm `[Networked] bool HoldingBall`, field `_heldBallVisual (Renderer)`
  - `FixedUpdateNetwork()`: đọc `ThrowController.LocalHoldingBall`
  - `Render()` (proxies): `_heldBallVisual.enabled = HoldingBall`
- `ThrowController.cs`: thêm `public static bool LocalHoldingBall`, set trong `ShowHeld()`
- Prefab `NetworkAvatar.prefab`: thêm sphere `HeldBallVisual` con của `WristR` (0.09 scale, ThrowBall.mat), `_heldBallVisual` wired

**S3 — Networked Projectile** ✅
- `NetworkProjectile.cs` (new): thin NetworkBehaviour — `LinkTo(Transform)`, FUN copy pos từ local proj, Spawned ẩn renderer cho authority
- `ThrowController.cs`: field `_netProjectilePrefab (Fusion.NetworkObject)`, methods `SpawnNetworkProjectile()`, `DespawnNetworkProjectile()`, `TryGetRunner()`, tất cả trong `#if PHOTON_FUSION`
- Prefab `NetworkProjectile.prefab` (new): sphere + NetworkObject + NetworkTransform + NetworkProjectile, wired vào ThrowController trong scene

---

### Trạng thái hiện tại

| Layer | Status |
|---|---|
| Local throw (grab → swing → fire) | ✅ Done |
| IK avatar (arm + leg) | ✅ Done (roll fix session 6) |
| NetworkAvatar sync (4 transforms) | ✅ Done |
| S2 — held ball visible to others | ✅ Done session 6 |
| S3 — projectile visible to others | ✅ Done session 6 |
| S5 — Haptics 3-tier (wind/release/impact) | ⬜ Not started |
| S6 — Juice T2 (flash, squash-stretch, trail glow) | ⬜ Not started |
| S8 — Hit detection + cross-player haptic RPC | ⬜ Next priority |
| S9 — 2-player full verify | ⬜ Blocked on S8 |
| MinigameManager networking | ⬜ Not started |
| Team assignment + spawn points | ⬜ Not started |

---

### Việc cần làm tiếp (theo thứ tự ưu tiên)

**1. Verify S2 + S3 với 2 player thật** (quan trọng nhất trước khi build thêm)
```
[ ] Player A thấy HeldBallVisual ở wrist của B khi B đang hold
[ ] Player A thấy NetworkProjectile bay khi B throw
[ ] Không có double-ball trên authority (HeldBallVisual.enabled = false cho owner)
[ ] NetworkProjectile despawn đúng khi ball land
```

**2. S8 — Hit detection**
```csharp
// Trong ThrowProjectile.OnLanded() hoặc OnTriggerEnter:
// Nếu hit collider là player → fire RPC_OnHit

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_OnHit(PlayerRef hitPlayer, Vector3 hitPoint)
{
    SpawnImpactVFX(hitPoint);
    if (Runner.LocalPlayer == hitPlayer)
        TriggerHaptic(HapticStrength.Strong, 0.3f);
}
```
NetworkProjectile cần collider (trigger) để detect va chạm với avatar. Avatar cần collider trên `NetworkAvatar/Head` hoặc body.

**3. S5 — Haptics 3-tier**
- Wind-up: light buzz khi `fwdVel > vMinArm` (dấu hiệu chuẩn bị throw)
- Release: sharp pulse khi Fire() (đã có `hapticRelease` config)
- Impact: rumble khi nhận hit (S8 RPC)
Tất cả local — không cần thêm networking.

**4. S9 — 2-player verify checklist** → xem `Docs/Network_Architecture_Lessons.md §3.1`

---

### Gotchas cần nhớ

- **Runner.Spawn() trong Shared Mode**: synchronous return, caller thành State Authority. Gọi từ MonoBehaviour OK nếu dùng `NetworkRunner.Instances[0]`.
- **NetworkProjectile.LinkTo()** phải gọi ngay sau Spawn (trong cùng frame). FixedUpdateNetwork tick đầu sẽ copy pos. Không có race condition vì FUN chạy sau frame hiện tại.
- **HeldBallVisual** là con của WristR trong `NetworkAvatar` prefab — không phải scene object. Khi test solo thì không thấy (owner bị ẩn); cần 2 client mới verify được.
- **Meta XR Simulator**: kích hoạt mỗi session qua `Meta > Meta XR Simulator > Activate`. Process-level env var, không persist.
- **AssetImportWorker windows**: nếu thấy nhiều cửa sổ nhỏ 136×39px khi compile → là AssetImportWorker bình thường, không phải bug. Fix: `Edit > Preferences > Asset Pipeline > Standby Import Worker Count = 0`.

---

### Files thay đổi trong session 6

```
M  Assets/_Game/Scripts/Player/AvatarArmPoser.cs        — forearm roll fix
M  Assets/_Game/Scripts/Player/AvatarLegPoser.cs        — shin roll fix
M  Assets/_Game/Scripts/Player/NetworkAvatar.cs         — HoldingBall + HeldBallVisual
M  Assets/_Game/Scripts/Throwing/ThrowController.cs     — LocalHoldingBall + S3 Runner.Spawn
+  Assets/_Game/Scripts/Throwing/NetworkProjectile.cs   — new, thin Fusion wrapper
M  Assets/_Game/Prefabs/NetworkAvatar.prefab            — HeldBallVisual sphere child of WristR
+  Assets/_Game/Prefabs/NetworkProjectile.prefab        — new, NetworkObject+NT+NetworkProjectile
M  Assets/_Game/Scenes/01_TOSSZONE_Main.unity           — PlayerRig + TossLocomotionInput + cam offset
M  Docs/Network_Architecture_Lessons.md                 — S2/S3 marked done, actual impl documented
+  Docs/HANDOFF.md                                      — this file
```
