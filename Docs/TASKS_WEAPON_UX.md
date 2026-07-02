# TOSSZONE — Weapon UX Rework (kế hoạch Session 12)

> Chốt từ feedback owner 2026-07-02 cuối Session 11. Đọc kèm `TASKS_DETAIL.md` (T1-T17 đã xong) và
> `T17_Test_Report.html` (checklist test 2 người). Mai chạy theo thứ tự T19 → T18 → T20 → T21/T22.

---

## 1. Vision của owner vs hiện trạng — map chính xác cái gì đang SAI

| # | Vision (owner mô tả) | Hiện trạng | Kết luận |
|---|---|---|---|
| 1 | Bảng chọn vũ khí = **2 nút mesh có collider** ở cổ tay, **chọt ngón tay** vào để swap qua lại | Gạt joystick trái + bóp grip xác nhận — "đẩy rồi chọn khó cực kì, k biết lúc nào chọn được" | ❌ SAI FLOW → T18 |
| 2 | Chọn xong, **GRAB vũ khí** đó (đang hiện dạng **hologram xanh** — shader owner tự làm) để equip | Bóp grip = equip ngay, không có bước grab, không hologram | ❌ SAI FLOW → T18 |
| 3 | Bảng hiện khi cổ tay nằm trong **vùng trung tâm tầm nhìn** (dựng "phễu"/cone từ camera; trong cone = hiện, ngoài = tắt) | Palm-up check (`transform.up.y < -0.3`) — phải cúi xuống nhìn mới thấy | ❌ SAI → T18 |
| 4 | Vũ khí cầm tay = **AutoHand Grabbable THẬT** spawn vào tay (auto-pose tạm, GrabbablePose owner làm sau — mục đích là ngón tay ôm súng/kiếm tự nhiên) | Model cosmetic gắn cứng cổ tay (đã strip hết Grabbable) — nhìn được nhưng ngón tay không ôm | 🟡 TẠM, cần nâng → T19 |
| 5 | Cầm **kiếm** thì vung kiếm — KHÔNG được ra bóng | Vung kiếm vẫn "đọng lại 1 quả bóng". **Root cause đã tìm ra:** `ThrowBallHolder.Update()` force-grab bóng khi bóp grip **VÔ ĐIỀU KIỆN** — nó không hề đọc `EquippedIndex`, nên kiếm/súng gì grip cũng ra bóng | ❌ BUG → T19 |
| 6 | Viên đạn bay đúng loại: Rock = **cục đất đá**, Gun = viên đạn, Bazooka = rocket, Grenade = lựu đạn... | MỌI vũ khí đều bay ra **quả bóng vàng generic** (ThrowProjectile/NetworkProjectile dùng chung 1 mesh sphere). Prefab model đã có sẵn đủ: `MS_WP_Rock/Gun_Bullet/Rocket/Grenade/BigBoom/LandMine` | ❌ SAI → T20 |
| 7 | Có tín hiệu cho người chơi biết **đã đổi vũ khí** | Chỉ có model đổi trên tay + dấu equipped trên bảng — không haptic/SFX/text | 🟡 THIẾU → T21 |
| 8 | Biết **cái gì là cái gì** khi mua | Field `icon` trong 7 file `WC_*` chưa gán sprite nào — chỉ có tên chữ + giá | 🟡 THIẾU → T22 |

Cái gì ĐANG ĐÚNG (đừng phá khi rework): swing-throw peak-velocity fire · catch tay trái · burst/đạn mưa ·
ring buff · dead-mask · map 2 sân + tường · dummy tự passive khi 2 người thật · dash (click joystick phải) +
jump (nút A) — CÓ SẴN trong `TossLocomotionInput`, chỉ chưa test máy thật.

---

## 2. Task list mới (T18-T24)

### T19 — Held item = AutoHand Grabbable thật per-weapon ⬅ LÀM TRƯỚC (fix bug kiếm-ra-bóng)
**Mục tiêu:** mỗi vũ khí equip là 1 Grabbable THẬT trong tay (auto-pose tạm), kiếm cầm kiếm, KHÔNG BAO GIỜ ra bóng khi không phải Rock.
**Làm gì:**
- Generalize `ThrowBallHolder` → `WeaponHolder` (hoặc thêm gate): đọc `PlayerCombat.Local.EquippedIndex` mỗi frame; grip → `ForceGrab` **đúng** instance vũ khí hiện tại (dùng `heldPrefab` GỐC — KHÔNG strip, cần Grabbable thật để auto-pose ôm ngón); Rock giữ nguyên ThrowBall như cũ.
- Đổi weapon giữa chừng → release + swap instance (giữ 1 instance per weapon, SetActive để tránh Instantiate/Destroy liên tục — pattern ThrowBallHolder đang giữ 1 ball sẵn).
- Lớp cosmetic `SpawnHeldVisual` hiện tại: GIỮ cho **proxy/remote** (máy người khác không có Hand → không grab được, chỉ cần nhìn thấy model); OWNER chuyển sang grabbable thật. `ThrowController._showVisualHeldBall=false` khi holder hoạt động (cờ có sẵn).
- Grab pose: để field trống cho owner author `GrabbablePose` sau; auto-pose chạy tạm.
**Verify (MCP + XR sim):** equip từng vũ khí → grip → đúng model trong tay; kiếm vung KHÔNG ra bóng; Rock ném bình thường; đổi qua lại 10 lần không leak instance/exception.
**Deps:** không. File chính: `ThrowBallHolder.cs`, `HandWeapon.cs` (tắt cosmetic cho owner).

### T18 — Selector rework: nút chọt vật lý + view-cone + grab-hologram để equip
**Mục tiêu:** đúng flow owner mô tả (map dòng 1-3 bảng trên).
**Làm gì:**
- **View-cone visibility** (thay palm-up): hiện bảng khi `Vector3.Dot(cam.forward, (wrist.pos − cam.pos).normalized) > cos(halfAngle)` — camera = `Camera.main` (đầu player), `halfAngle` serialized ~20-25°, kèm khoảng cách max ~1m. Đây chính là cái "phễu" — không cần collider thật, 1 phép dot là đủ và rẻ.
- **2 nút chọt:** 2 mesh mũi tên (trái/phải) + SphereCollider trigger ở cổ tay trái; detect fingertip/Hand collider của AutoHand chạm vào (OnTriggerEnter + check `GetComponentInParent<Autohand.Hand>()`), cooldown ~0.4s chống double-poke, haptic tick mỗi lần swap.
- **Hologram + grab để equip:** slot giữa hiện model vũ khí (cosmetic copy — tái dùng `SpawnHeldVisual`) gắn material hologram (code expose 1 field `Material _hologramMat` — shader xanh owner TỰ LÀM, tạm dùng URP Unlit xanh trong lúc chờ); model này là 1 Grabbable proxy nhẹ — Hand grab nó → `EquipWeapon(index)` + destroy hologram + T19 spawn grabbable thật vào tay. Check tiền/unlock TRƯỚC khi cho grab (chưa đủ tiền → hologram đỏ/xám, không grab được).
- Bỏ flick-stick + palm-up code cũ trong `WristWeaponSelector`; canvas UI cũ (icon/tên/giá) giữ lại làm label phụ trên nút.
**Verify:** nhìn thẳng cổ tay → bảng hiện; quay đi → tắt; chọt nút → swap + haptic; grab hologram khi đủ tiền → equip + vũ khí thật vào tay; thiếu tiền → không grab được.
**Deps:** T19 xong trước (equip xong phải có grabbable thật vào tay ngay, không thì flow cụt). File: `WristWeaponSelector.cs` (viết lại phần lớn), prefab `WristSelector` trong `NetworkAvatar.prefab` (dựng lại: 2 nút mesh + anchor hologram).

### T20 — Per-weapon projectile visuals (viên bay đúng loại)
**Mục tiêu:** Rock bay ra cục đá, Gun bắn viên đạn (`MS_WP_Gun_Bullet`), Bazooka bắn rocket (`MS_WP_Rocket`), Grenade/BigBoom/LandMine bay đúng model — hết bóng vàng vạn năng.
**Làm gì:**
- 2 đường đạn cần sửa: (a) `ThrowProjectile` (BillTween local, đường ném) — thêm mesh override per-weapon (đọc từ WC mới field `projectileVisual` hoặc tái dùng `heldPrefab` mesh); (b) `NetworkProjectile` — tạo prefab variant per loại (bullet/rocket) với `NetworkPoolable` pool key riêng, gán vào `WeaponConfig.projectilePrefab` (field có sẵn, đang null).
- Đăng ký pool cho từng loại mới trong `BillBootstrapConfig.defaultPools` / pool provider.
**Verify:** bắn/ném từng vũ khí → model viên đúng cả local lẫn remote (2 client).
**Gotcha:** thêm prefab NetworkObject mới → nếu `Runner.Spawn` báo "guid failed to be translated" thì reimport `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` (bài học T10).

### T21 — Feedback đổi vũ khí (nhỏ ~30')
Equip xong → haptic tick + SFX + chữ nổi tên vũ khí (tái dùng pool `RewardText`/`BounceNumber` có sẵn). Fire từ chỗ `EquipWeapon` hoặc event mới `WeaponEquippedEvent` qua `Bill.Events`.

### T22 — Icon vũ khí (art, owner có thể tự làm)
Gán sprite vào field `icon` của 7 file `Assets/_Game/Data/Weapons/WC_*.asset`. UI đọc sẵn rồi (`WeaponSlotUI.Bind`).

### T23 — BillCore: matchmaking/session API (backlog)
Session browser + đặt tên phòng thay hardcode `TOSSZONE_DEMO` (FusionNet đã có `SessionListUpdated` event làm nền).

### T24 — BillCore: host migration (backlog)
Master thoát → trận hiện tại chết. `FusionNet` đã expose `HostMigrating` event, chưa có logic resume.

---

## 3. Trạng thái verify còn nợ từ T17 (làm cùng đợt test build)
- Round-end/win-condition + respawn đúng sân với 2 client thật (bị Photon rate-limit chặn — chạy 1 phiên dài thay vì connect/disconnect nhiều lần).
- T12 buff-ring cross-authority (người không-master ném xuyên ring).
- Dummy passive khi 2 người thật (đã verify giả lập, cần xác nhận 2 máy thật).
- Dash/jump/feel/haptic/FPS trên Quest thật.
Checklist đầy đủ: `T17_Test_Report.html`.
