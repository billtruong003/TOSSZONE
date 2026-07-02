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

## 3. AUDIT design-vs-code — các điểm SAI/CHƯA LÀM (quét 2026-07-02)

> ⚠️ **ĐÍNH CHÍNH sau khi owner đưa GDD PDF chính chủ** (đã chép thành `GDD_Core_Reference.md` — NGUỒN
> CHÂN LÝ mới): bản audit đầu tiên của phần này dựa trên code + Combat_Minigame_Design cũ nên có dòng SAI —
> **GDD không hề có ring Shield**; vòng thứ 5 là **Tăng Kích Thước (Area)**. Các dòng dưới đã sửa theo GDD.
> Toàn bộ lệch GDD lớn hơn (kinh tế/mạng/sân/vũ khí/lobby) xem mục 9.

Grep xác nhận: các field sau tồn tại trong config nhưng **KHÔNG CÓ DÒNG CODE NÀO ĐỌC** (design data chết):

| Field / cơ chế | Design nói gì | Code thực tế | Task |
|---|---|---|---|
| `fuseDelay` (LandMine) | Ném/đặt xuống → ARM → người đạp/hết fuse → nổ AoE *(lưu ý: LandMine KHÔNG có trong GDD — số phận chốt ở T31)* | Không được đọc — LandMine bay y hệt grenade thường | T26/T31 |
| `laserSight` (Gun/Bazooka) | Dot/line laser từ nòng để nhắm | Không được đọc — không có laser | T26 |
| `magazine` | >0 = hết băng phải chờ/mua | Không được đọc — bắn vô hạn | T26 |
| `costPerUse` + PayPerUse | "Mỗi phát tốn tiền" (design nghiêng PayPerUse cho đa số vũ khí) | Code trừ **Ammo** chứ không trừ tiền; VÀ cả 7 config đang set **BuyOnce** hết — data lẫn code đều lệch design | T26 (cần owner chốt lại: PayPerUse hay BuyOnce cho từng món) |
| **Ring thứ 5 SAI DANH TÍNH** | GDD: vòng 5 = **Tăng Kích Thước (Area)** x1.25→x2.25 — **KHÔNG có ring Shield nào** | Code enum đặt `Shield=5` + field `shieldSelf` (bịa, không trong GDD) + asset `RC_Shield` — và cũng NO-OP luôn | T27 |
| **Ring Băng SAI CƠ CHẾ** | GDD: Băng **ĐÓNG BĂNG người (KHÔNG damage)**; tường băng freeze khi chạm; **dính damage thì băng giải trừ**; tường sống theo Tier (1-3s) | BuffZone Ice đang GÂY DAMAGE 1 lần khi chạm + tan khi trúng đạn — cơ chế freeze (khóa movement/hands) hoàn toàn chưa có | T27 |
| **Ring Lửa lệch thông số** | GDD: đi qua **mất 1 mạng/lần**; vùng sống theo Tier **1-3 giây** | Code: DoT 1 máu/giây khi đứng trong + sống tới hết hiệp (90s failsafe) | T27 |
| `VelocityScale` / **Ring Tốc Độ** | Đạn nhanh hơn +20%→+100% theo Tier | Ring SET giá trị vào projectile nhưng **không chỗ nào nhân vào vận tốc bay thật** → NO-OP (chỉ AreaScale là ăn thật vào hit radius) | T27 |
| Stack "tối đa 3 vòng/viên" | Xuyên nhiều ring → buff cộng dồn, trần 3 vòng | Code dùng `Mathf.Max(cũ, mới)` → **không stack gì cả**, xuyên 2 ring = như 1 | T27 |
| **Giá trị buff DO TIER quyết định** | Ma trận GDD: Multi x2→x15, Area x1.25→x2.25, Velocity +20→+100%, Băng/Lửa 1→3s; đường kính ring 1.8m→0.6m; tốc trôi 1→3.5m/s | BuffRingConfig 1 giá trị tĩnh/element (`RC_Multi.multiplier=40` — GDD max x15!); Tier không scale giá trị/đường kính; bảng weight T11 là placeholder ≠ số GDD | T27 |
| Anti-dup Tier 4-5 | GDD: tối đa **1 vòng T4** và **1 vòng T5** cùng lúc (bất kể loại; cùng tên khác tier vẫn OK) | T11 đang chặn theo cùng-ELEMENT tier≥4 — sai rule | T27 |
| Quỹ đạo ring | GDD: **trôi ngang liên tục mép trái↔phải** | T9: wander Perlin ngẫu nhiên trong box (owner từng mô tả miệng "random ở giữa" — ❓cần owner chốt: GDD drift ngang hay wander) | T27 |
| Nổ AoE khi chạm đất | Grenade/BigBoom chạm đất là nổ | **Chỉ nổ khi overlap NGƯỜI** — ném hụt là bay tới hết lifetime rồi biến mất, không nổ | T26 |
| Effect nổ AoE | Cầu lửa/shockwave to theo `aoeRadius` | **CHƯA CÓ** — ImpactBurst nhỏ chỉ nổ khi trúng người/chạm đất đường ném | T26 |
| `isUncatchable` | Đạn súng/power throw KHÔNG bắt được | CatchController đang đoán qua `Element != 0` (comment trong code tự nhận "extend once networked") | T26 |
| Kiếm rút sau lưng | Combat_Minigame_Design cũ §9: đeo SAU LƯNG, với tay ra sau RÚT *(Sword KHÔNG có trong GDD — số phận chốt ở T31)* | Equip qua selector như mọi vũ khí | T29/T31 |
| Heckle khán đài | Chết → ra khán đài ném Egg/Tomato/Poop chọc người sống | Chưa build gì (prefab `MS_WP_Egg/Poop/Tomato` có sẵn) | backlog |
| Ring spawn nhiều | — | `RingSpawner` capacity 8 slot, đang config 3 — chỉnh `_slotCount` inspector là ra nhiều; CHƯA test >3 | T25 test |

---

## 4. SPEC từng vũ khí — phase tuần tự + trạng thái (note đầy đủ theo yêu cầu owner)

Format mỗi phase: ✅ có · 🟡 tạm/thiếu 1 phần · ❌ chưa có.

**ROCK (đá — free, vô hạn)**
1. Equip: mặc định index -1 ✅ → 2. Hold: grip → Grabbable thật (ThrowBallHolder) ✅ *nhưng model phải là CỤC ĐÁ, đang là bóng vàng* 🟡T20 → 3. Fire: vung ném peak-velocity ✅ → 4. Flight: ballistic + trail ✅ (model đá ❌T20) → 5. Impact: −1 máu + ImpactBurst ✅, bắt được ✅.

**GUN (súng — $15)**
1. Equip ✅(rework T18) → 2. Hold: model tay 🟡(grabbable thật T19); **laser sight ❌T26** → 3. Fire: trigger ✅; đạn phải là `MS_WP_Gun_Bullet` ❌T20; **magazine ❌** / **costPerUse ❌** T26; muzzle flash ❌T26 → 4. Flight: thẳng nhanh ✅ → 5. Impact: −1 máu ✅; **không-bắt-được chưa enforce ❌T26**.

**GRENADE (lựu đạn — $8)**
1. Equip ✅ → 2. Hold 🟡T19 → 3. Fire: NÉM (swing) ✅ → 4. Flight: arc ✅, model grenade ❌T20 → 5. **Impact: chạm ĐẤT phải nổ ❌ (hiện chỉ nổ khi trúng người)**; AoE damage ✅; **effect nổ (cầu lửa + shockwave theo aoeRadius + rung tay) ❌T26**.

**BAZOOKA ($20, mở 30s)**
1. Equip ✅ → 2. Hold 🟡T19 (cân nhắc cầm 2 tay sau); laser sight ❌ → 3. Fire: trigger → rocket ✅, model `MS_WP_Rocket` ❌T20 → 4. Flight: arc gravity ✅ → 5. Impact: nổ AoE như grenade — cùng gap ❌T26.

**BIGBOOM (bom nguyên tử — $25, mở 60s = finale)**
1. Equip ✅ → 2. Hold: 🟡T19 — **interaction phải KHÁC: bom to nặng, cân nhắc cầm 2 tay/ném vồng chậm** (owner chốt) → 3. Fire: ném ✅ → 4. Flight: arc ✅ (nên nặng/chậm hơn — tune) → 5. **Impact: nổ TO — AoE lớn ✅ damage nhưng effect ❌: cần cầu nổ lớn + shockwave + flash + rung mạnh cả 2 tay + rung màn theo khoảng cách (không camera-shake vì VR — dùng haptic+ánh sáng)** T26.

**LANDMINE (mìn — $12, mở 45s)**
1. Equip ✅ → 2. Hold 🟡T19 → 3. **Fire: ném/ĐẶT → nằm đất ARM (fuseDelay) ❌ HOÀN TOÀN CHƯA — hiện bay như đạn thường** → 4. **Trigger: người đạp lên → nổ ❌** → 5. Effect nổ ❌. Cả chuỗi mine là T26 (phần nặng nhất).

**SWORD (kiếm — $18, mở 20s, deflect-only)**
1. **Equip: design = đeo sau lưng, VỚI TAY RA SAU LƯNG RÚT ❌T29** (hiện qua selector) → 2. Hold: grabbable + pose 🟡T19 → 3. Swing: chém deflect đạn đơn + mưa ✅(T5); **vung không được ra bóng — bug, fix T19** → 4. Không damage người ✅ → 5. Feedback: **trail lưỡi ❌, SFX chém ❌, haptic khi deflect trúng ❌** T28.

---

## 5. UI FEEDBACK INVENTORY — note hết cái thiếu (T28)

| Feedback | Trạng thái |
|---|---|
| HUD tiền (ví hiện tại) | ❌ chỉ có RewardText "+$" bay lên lúc cộng — không thấy tổng |
| HUD ammo / magazine | ❌ |
| Máu bản thân | 🟡 HealthUI 5 cục gắn avatar — cần kiểm góc nhìn chính mình có thấy không |
| Score A-B / round / thời gian hiệp / countdown warmup | ❌ không UI nào (prefab `MS_ScoreBoard` có sẵn chưa dùng) |
| Đã đổi vũ khí (haptic+SFX+label) | ❌ = T21 |
| Icon vũ khí trong shop | ❌ = T22 |
| Unlock-time đếm ngược trên slot khóa | 🟡 có overlay mờ, không có số giây |
| Không đủ tiền (rung/nháy đỏ khi grab hụt) | ❌ |
| Bắt bóng thành công (event `BallCaughtEvent` ĐÃ fire, không ai nghe) | ❌ VFX/SFX/haptic |
| Deflect thành công | ❌ |
| Đạn mình được buff khi xuyên ring (ngoài chữ EFFECTIVE! trên ring) | ❌ đổi màu đạn/trail theo element |
| Giết địch / bị giết / thắng-thua hiệp / thắng trận | ❌ toàn bộ |

---

## 6. T25 — TRAINING RANGE (map test thuần cho owner) ⭐ ưu tiên theo yêu cầu

**Mục tiêu:** khu tập bắn kiểu training — test ring + mọi vũ khí không tốn tiền, không cần vào trận.
**Làm gì:**
- **Vị trí:** dựng ngay trong hub `01_TOSSZONE_Main` (hub = sân tập, đúng chất Gorilla-Tag; khỏi thêm scene/build index). Nếu owner muốn scene riêng `03_Training` thì nói lại.
- **Hàng nút RING:** 5-6 cube mesh có collider (mỗi nút 1 loại theo GDD: **Số Lượng/Tốc Độ/Băng/Lửa/Kích Thước** + 1 nút "random x8"), chọt tay + bóp trigger → spawn ring loại đó trước mặt (RingSpawner API thêm `SpawnSpecific(element, tier)`); nút thêm để test **spawn NHIỀU ring** (capacity 8 đã hỗ trợ, chưa test >3). GDD gọi khu này là "Khu Khởi Động (Warm-up Target)" — có thể thêm máy bắn bóng sau.
- **Hàng nút VŨ KHÍ:** 7 cube (mỗi vũ khí 1 nút) — chọt + trigger → equip FREE (không tiền/unlock). Cần cờ `CombatSession.TrainingMode` (KHÔNG dùng cheat DEV-only — phải chạy cả build thường) + fire `MinigameEnteredEvent` tại hub để catalog sống ngoài arena.
- **Targets:** 2-3 DummyAvatar đứng các khoảng cách + tường để test đạn xuyên/nổ.
- Nút = pattern chung với 2 nút selector T18 (poke detection dùng chung 1 component `PokeButton3D`).
**Verify:** vào hub → chọt nút Gun → súng vào tay free → bắn dummy; chọt nút ring Lửa → ring hiện → ném xuyên → đạn lửa → vùng lửa; chọt "x8" → 8 ring cùng lúc không lỗi.
**Deps:** T18 (PokeButton3D dùng chung), T19 (equip ra grabbable). Làm NGAY SAU T18/T19.

---

## 7. Task list cập nhật (thứ tự đề xuất Session 12+)

T19 held-grabbable → T18 selector poke/cone/hologram → **T25 training range** → T20 projectile visuals →
T26 weapon phases (nổ chạm đất + effect AoE + laser + magazine + isUncatchable — landmine/costPerUse chờ chốt T31)
→ **T27 RING OVERHAUL THEO GDD** (xem chi tiết dưới) → T30 match/economy theo GDD → T31 weapon roster theo GDD
→ T28 HUD/feedback → T21 equip feedback → T22 icons → backlog: T29 kiếm rút sau lưng (❓ngoài GDD), heckle
khán đài, T23 matchmaking (nâng cấp thành GDD lobby flow), T24 host-migration, T32 lobby epic.

### T27 — RING OVERHAUL theo GDD (thay spec cũ)
Nguồn: `GDD_Core_Reference.md` mục VI. Gồm: ① đổi element 5 `Shield` → **`Area`** (enum + asset `RC_Shield`
→ `RC_Area`, xóa `shieldSelf`; Area = nhân `AreaScale` — pipeline AreaScale→hit radius ĐÃ chạy sẵn, chỉ thiếu
ring cấp nó); ② **BuffRingConfig chuyển sang giá trị THEO TIER** (bảng ma trận: Multi x2/4/8/12/15, Area
x1.25→x2.25, Velocity +20→+100%, Băng/Lửa 1/1.5/2/2.5/3s, đường kính 1.8→0.6m, tốc trôi 1→3.5m/s) — thay
5 asset đơn trị bằng ma trận (mảng per-tier trong 1 config, hoặc AnimationCurve); ③ **VelocityScale áp vào
vận tốc bay thật** (cả ThrowProjectile local tween lẫn NetworkProjectile RB); ④ **stack cộng dồn tối đa 3
vòng/viên** (thay Max — cần đếm số ring đã áp per-projectile); ⑤ **Băng = FREEZE**: đóng băng player
(khóa AutoHandPlayer move + hands) theo thời gian tier, damage giải băng, KHÔNG gây damage — tường băng
freeze-on-touch, sống theo tier; ⑥ **Lửa** = mất 1 mạng/lần đi qua, vùng sống 1-3s theo tier (không phải
90s); ⑦ anti-dup sửa thành: tối đa 1 vòng T4 + 1 vòng T5 đồng thời (bỏ check cùng-element); ⑧ quỹ đạo:
GDD = trôi ngang trái↔phải — ❓owner chốt (đã có wander T9, đổi = thay `WanderPosition` bằng drift tuyến
tính X theo tốc độ tier); ⑨ scale đường kính ring theo tier; ⑩ weight spawn dùng ĐÚNG số GDD:
T1-T5 = (65,25,8,2,0) / (38,26,20,10,5) / (20,25,25,20,10) theo 3 cửa sổ.

### T30 — Match & Economy theo GDD (mới)
90s/hiệp (code 120) · nghỉ 5s + ĐỔI BÊN + bảng điểm · mạng theo chế độ 7/5/4 (bỏ MaxHealth=5 cứng) ·
timeout so **tổng mạng ĐỘI** (code đang so máu cá nhân) · hòa hiệp + **Hòa Chung Cuộc** (1-1-1) · thu nhập
+$2/s (code +1) · **+$5/KILL** (code +$10/HIT — sai cả giá trị lẫn điều kiện) · chết +$10 & **3s bất tử** ·
shutdown bounty +$2/kill.

### T31 — Weapon roster & bảng số theo GDD (mới — cần owner chốt 2 điểm)
GDD = 6 vũ khí: Đá $0/0.4s/AoE 0.8m · Súng $2/0.1s/0.35m/mở 1s · Bom Nhỏ $5/1s/1.5m/5s · Bazooka
$8/1.2s/2.5m/10s · **Bom Chữ X $13/2.3s (vệt lửa chữ X 1.1m × 47% sâu sân)/20s — CHƯA CÓ TRONG CODE** ·
Nuke $20/3s/4.5m/45s. Việc: sửa giá/cooldown/AoE/unlock 7 asset WC_* theo bảng; build Bom X (vệt lửa chữ
thập = 2 BuffZone dạng hộp xoay 90°); Đá/Súng cũng có AoE nhỏ. **❓Owner chốt:** (a) Sword + LandMine —
ngoài GDD, giữ làm extension hay bỏ? (b) BuyOnce vs PayPerUse — GDD chỉ ghi "Giá", không nói mua 1 lần
hay per-use.

### T32 — Lobby/Out-game epic theo GDD (backlog lớn)
Hub 3D tương tác: HOST đấm nút → room code 5 chữ · join = ném khối chữ/bàn phím hologram · Quick Play
teleport pad · Waiting room: host panel cần gạt (mode/size sân khóa theo mode/map theme), **chia đội bằng
vùng đứng** Xanh/Đỏ/Trung lập, ready = đập nút hologram, START khóa tới khi đội cân + 100% ready,
transition blackout · wardrobe gương + skin · voice proximity. (T23 matchmaking API là móng của phần này.)

---

## 8. Trạng thái verify còn nợ từ T17 (làm cùng đợt test build)
- Round-end/win-condition + respawn đúng sân với 2 client thật (bị Photon rate-limit chặn — chạy 1 phiên dài thay vì connect/disconnect nhiều lần).
- T12 buff-ring cross-authority (người không-master ném xuyên ring).
- Dummy passive khi 2 người thật (đã verify giả lập, cần xác nhận 2 máy thật).
- Dash/jump/feel/haptic/FPS trên Quest thật.
Checklist đầy đủ: `T17_Test_Report.html`.
