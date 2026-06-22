# TOSSZONE! — Task Breakdown (Roadmap 4 Phase)

> Tài liệu chia nhỏ công việc cho game VR **TOSSZONE!**, dựa trên GDD (`TOSSZONE.docx`) + định hướng kỹ thuật cho bản demo.
> Ngày tạo: 2026-06-22. Đây là **living document** — tick checkbox khi xong, cập nhật khi đổi quyết định.
> **Trạng thái task:** `[ ]` Todo · `[/]` In-Progress · `[x]` Done · `[!]` Blocked. Claude quản lý các trạng thái này qua Task Board. Xem [`TASKBOARD.md`](TASKBOARD.md).

---

## 0. Tóm tắt định hướng kỹ thuật

| Hạng mục | Quyết định cho Demo (Phase 1) | Ghi chú |
|---|---|---|
| Tay/Tương tác | **AutoHand**, chỉ 2 bàn tay, **không hiện full body** | Đã có sẵn `Assets/AutoHand` |
| Di chuyển | **Joystick locomotion** (smooth move) | Không teleport ở demo |
| Hiện diện body | **Capsule** đại diện thân + **leg IK đơn giản** | Để người khác nhìn thấy "có thân", không phải tay bay lơ lửng |
| Networking | **Photon Fusion 2 — Shared Mode** | ⚠️ **CHƯA cài** — task đầu tiên của Phase 1. CHỐT: Shared Mode (ưu tiên nhanh cho demo) |
| Chia đội | 2 team (A/B), spawn 2 bên sân | Gán team lúc join |
| Cơ chế ném | **KHÔNG grab/nhặt vật**. Đưa tay ra sau đầu + bấm grab → haptic → "nạp" vật vào tay. Khi ném: **inactive vật trong tay**, **spawn projectile** riêng bay ra | Xem mục Phase 1.4 |
| Đường bay đạn | **Tween** (DOTween), **không dùng physics** | Để control lực/số lần nảy chính xác |

### Trạng thái project hiện tại
- ✅ Unity project tại `d:\Project\ThrowingShot`, URP package đã có, AutoHand đã có, Meta XR core 74 + OpenXR đã có.
- ✅ Scene skeleton: `Assets/_Game/Scenes/00_Bootstrap.unity`, `01_TOSSZONE_Main.unity` (đang trống logic).
- ✅ `BillGameCore/BillInspector` — framework attribute/inspector kiểu Odin (dùng để author data weapon/buff sau này).
- ❌ Photon Fusion: **chưa cài**.
- ❌ Chưa có script gameplay nào trong `_Game`.

### Quyết định cần chốt trước khi code (xem mục 5 — Open Questions)
1. ✅ **CHỐT — Fusion topology = Shared Mode** (ưu tiên nhanh cho demo). Mỗi client giữ State Authority cho player + projectile của mình. Đánh đổi: khi lên economy/hit-detection (Phase 3) cần thêm validation chống cheat — chấp nhận cho demo.
2. **DOTween**: dùng DOTween (Asset Store) hay tự viết tween nhỏ? → Đề xuất DOTween Pro (có path/curve sẵn).

---

## PHASE 1 — Demo Gameplay (Vertical Slice)

> **Mục tiêu Phase 1:** Từ Bootstrap → bấm 1 nút "FIND MATCH" → matchmaking (timeout ~30s) → 2 người vào chung 1 room → nhìn thấy nhau (capsule + 2 tay) → di chuyển bằng joystick → ném đồ bằng cơ chế "tay ra sau đầu" với projectile bay bằng tween. Tất cả sync qua Photon Fusion.

### 1.1 — Setup project & nền tảng Networking
- [ ] Cài **Photon Fusion 2** (PUN/Fusion package + import vào project).
- [ ] Tạo **Photon App ID** (Fusion) trên dashboard, gắn vào `PhotonAppSettings`.
- [ ] Tạo cấu trúc thư mục chuẩn dưới `Assets/_Game/`:
  - `_Game/Scripts/Core` (bootstrap, scene flow)
  - `_Game/Scripts/Network` (Fusion runner, callbacks, spawner)
  - `_Game/Scripts/Player` (rig, locomotion, body IK)
  - `_Game/Scripts/Throwing` (grab gesture, projectile, tween launcher)
  - `_Game/Prefabs`, `_Game/ScriptableObjects`, `_Game/Materials`
- [ ] Tạo `NetworkRunner` bootstrap (`RunnerBootstrap.cs`) + handler `INetworkRunnerCallbacks`.
- [x] Chốt **Fusion topology** → **Shared Mode** (ưu tiên nhanh cho demo).
- [ ] Tag/Layer setup: `TeamA`, `TeamB`, `Projectile`, `Throwable`, layer collision matrix.

### 1.2 — Scene Flow & Matchmaking
- [ ] **00_Bootstrap**: scene load đầu tiên → init systems (audio, settings, XR rig persistent) → load scene menu/hub.
- [ ] Scene menu (có thể là `01_TOSSZONE_Main` ở trạng thái lobby, hoặc tách scene riêng `00b_Menu`): hiện **1 nút "FIND MATCH"** (world-space button, đấm/bấm bằng tay VR theo phong cách VR — không dùng cursor 2D).
- [ ] Bấm nút → gọi `StartMatchmaking()`:
  - [ ] Fusion `StartGame()` với SessionName random / matchmaking pool (Phase 1 chưa làm room code — auto-join session đang thiếu slot).
  - [ ] Hiện UI "Đang tìm người chơi..." + đồng hồ đếm.
- [ ] **Timeout ~30s**: nếu sau 30s không đủ người:
  - [ ] Xử lý fallback (chọn 1 trong: hủy về menu / chờ tiếp / spawn 1 mình để test). → Demo: cho hủy về menu + có nút "test 1 mình".
- [ ] Khi **2 người match** → cùng vào 1 Fusion session → load `01_TOSSZONE_Main` → spawn 2 player ở 2 phía → **nhìn thấy nhau**.
- [ ] Loading transition (fade 1-2s) giữa menu → arena.

**Acceptance:** 2 build/2 headset bấm Find Match trong cửa sổ 30s → vào chung phòng, thấy nhau di chuyển.

### 1.3 — VR Rig & Hiện diện Player (network-synced)
- [ ] Player prefab (NetworkObject) gồm: AutoHand rig (head + 2 hands), **không** model full body.
- [ ] **Joystick locomotion** (smooth locomotion từ Input System / AutoHand mover) — di chuyển trong giới hạn sân.
- [ ] **Capsule body**: capsule đặt dưới head, follow head theo trục ngang (XZ), cao theo head Y → đại diện thân để đối thủ thấy.
- [ ] **Leg IK đơn giản**: 2 chân procedural (foot IK theo capsule + locomotion) để body trông có chân, không phải capsule trơn. (Có thể dùng simple two-bone IK hoặc placeholder animation.)
- [ ] **Network sync qua Fusion** (NetworkTransform cho từng phần):
  - [ ] Head pose, Left hand pose, Right hand pose, Capsule body, locomotion position.
  - [ ] Input authority cho local player; remote player render từ synced transforms.
  - [ ] Interpolation cho remote (mượt, không giật).
- [ ] **Chia 2 team**: gán Team A/B khi join (theo thứ tự / cân bằng), set màu capsule + spawn point đúng phía sân.
- [ ] Spawn points cho 2 đội ở 2 bên (theo kích thước sân 1v1 ~6m x 5m, có khoảng hở trung tâm).

**Acceptance:** Người A thấy người B là 1 capsule có 2 tay + chân, di chuyển/giơ tay realtime, đứng đúng phía đội mình.

### 1.4 — Cơ chế Ném (Core mechanic) ⭐
> Đây là phần quan trọng nhất của demo.

**A. Gesture "tay ra sau đầu" + Grab**
- [ ] Detect khi bàn tay nằm **sau đầu** (so sánh local position tay vs head: phía sau + trong bán kính ngưỡng).
- [ ] Khi tay sau đầu **và bấm grab** → trigger **haptic feedback** (controller rung).
- [ ] "Nạp" vật ném vào tay: **activate** một throwable visual gắn ở tay (KHÔNG nhặt vật từ thế giới — vật chỉ là model trang trí trong tay).
- [ ] State machine tay: `Empty → ReachingBehindHead → Loaded(holding) → Thrown(empty)`.

**B. Ném ra**
- [ ] Khi thả grab (release) trong lúc đang Loaded → **inactive vật trong tay** (vật trong tay biến mất, KHÔNG bay ra).
- [ ] **Capture** hướng + lực từ vận tốc tay lúc release (AutoHand/controller velocity).
- [ ] **Spawn 1 projectile riêng** (NetworkObject) tại vị trí tay, với param: origin, direction, power.

**C. Đường bay bằng Tween (KHÔNG physics)**
- [ ] `ProjectileLauncher` / `TweenProjectile`: nhận origin + direction + power → tính **path** (DOTween path/curve).
- [ ] Tham số config (ScriptableObject) cho đường bay: tầm xa theo power, độ cong (arc), **số lần nảy (bounce) 1 hoặc 2 lần**, thời gian bay.
- [ ] Tween di chuyển projectile theo path → đáp xuống đất / nảy → kết thúc.
- [ ] **Network**: spawn projectile có authority (host hoặc local tùy topology); sync vị trí cho client khác.
  - Shared Mode: client ném giữ **State Authority** cho projectile của mình → chạy tween local + NetworkTransform sync sang remote. (Hoặc spawn deterministic + tick start để client tự tween — chọn khi code.)
- [ ] Hit/Land cơ bản: projectile chạm đất → VFX nổ nhỏ + despawn. (Hit damage để Phase 2/3, demo chỉ cần thấy đạn bay & nổ.)
- [ ] Cooldown ném cơ bản (Internal Cooldown ~0.4s như GDD).

**Acceptance:** Đưa tay ra sau đầu → rung tay → có vật trong tay → vẩy tay ném → vật trong tay biến mất, 1 projectile bay theo cung tween, nảy & nổ; đối thủ thấy projectile bay qua network.

### 1.5 — Tổng kết Phase 1 (Demo Build)
- [ ] Sân demo tối giản (1 map, floor + khoảng hở giữa + ranh giới 2 đội).
- [ ] Build & test 2 device thực tế (matchmaking → ném nhau).
- [ ] Fix sync lag / jitter / sai lệch trajectory giữa 2 client.

---

## PHASE 2 — Thay Player + Vòng Buff (Buff Rings)

> **Mục tiêu:** Nâng cấp avatar player thật + dựng hệ thống Vòng Buff cốt lõi của gameplay.

### 2.1 — Thay Player (avatar thật)
- [ ] Thay capsule placeholder bằng **avatar body thật** (rig sẵn sàng gắn skin sau).
- [ ] Full-body IK xịn hơn (head + 2 hands + 2 feet IK, hip/spine), giữ network sync.
- [ ] Chuẩn bị socket gắn skin (mũ, kính, găng) cho Phase 3.

### 2.2 — Hệ thống Vòng Buff (Master Config)
- [ ] Data-driven config bằng ScriptableObject (tận dụng **BillInspector** attributes để author đẹp): bảng ma trận Tier 1-5.
  - [ ] Thuộc tính mỗi Tier: đường kính, tốc độ trôi, giá trị buff, tỷ lệ xuất hiện theo cửa sổ thời gian (0-30s / 31-60s / 61-90s).
- [ ] **Ring Spawner**: vòng trôi liên tục trái↔phải qua sân.
- [ ] **5 loại vòng**:
  - [ ] **Số Lượng (Multiplier)** — x2/x4/x8/x12/x15: nhân bản projectile thành "mưa đạn".
  - [ ] **Tốc Độ (Velocity)** — +20%..+100% tốc độ đạn.
  - [ ] **Vùng Sát Thương / Tăng Kích Thước (Area Expansion)** — x1.25..x2.25 bán kính nổ.
  - [ ] **Băng (Ice/Freeze)** — đóng băng + tạo **tường băng** tồn tại đến hết thời gian Tier; dính damage thì giải băng.
  - [ ] **Lửa (Fire/Zoning)** — sau khi nổ tạo **vùng lửa** = phạm vi nổ, ai đi qua mất 1 mạng; tồn tại đến hết thời gian Tier.
- [ ] **Cơ chế xuyên vòng**: projectile bay xuyên vòng → apply buff (modify tween/param đạn).
- [ ] **Stacking**: tối đa **3 vòng** áp lên cùng 1 viên đạn nếu xuyên qua cả 3.
- [ ] **S_max** số vòng tối đa: `S_max = 3 + floor((diện tích 1 bên - 30)/35)`.
- [ ] **Chống trùng lặp**: không cho 2 vòng cùng tên cùng Tier 4-5 xuất hiện đồng thời (khác Tier hoặc khác loại thì OK).
- [ ] **Spawn rarity control**: vòng Tier 4-5 hiếm hơn, trôi nhanh hơn (rút ngắn window).
- [ ] VFX/áp dụng hiệu ứng buff lên projectile (kết hợp với tween Phase 1).

**Acceptance:** Vòng trôi qua sân; ném đạn xuyên vòng → đạn nhân bản / nhanh hơn / to hơn / có băng / có lửa; stack được tối đa 3.

---

## PHASE 3 — Vũ khí + Kinh tế/Shop + Vòng trang trí (mua đồ/skin)

> **Mục tiêu:** Đủ 6 vũ khí, hệ thống kinh tế, cơ chế mua đồ, và vòng ngoài trang trí để mua skin/cosmetic.

### 3.1 — Arsenal 6 vũ khí
- [ ] Data ScriptableObject cho từng vũ khí (giá, hồi đạn, AoE, thời điểm unlock, internal cooldown 0.4s).
  - [ ] **Đá** ($0, 0.4s, AoE 0.8m, từ 0s) — vũ khí khởi đầu, đạn vô hạn.
  - [ ] **Súng Viên** ($2, 0.1s, 0.35m, từ 1s) — bắn nhanh.
  - [ ] **Bom Nhỏ** ($5, 1.0s, 1.5m, từ 5s).
  - [ ] **Bazooka** ($8, 1.2s, 2.5m, từ 10s).
  - [ ] **Bom Chữ X** ($13, 2.3s, vệt lửa X dài ~47% chiều sâu sân, từ 20s) — cắt đường di chuyển.
  - [ ] **Bom Nguyên Tử** ($20, 3.0s, 4.5m, từ 45s) — hủy diệt diện rộng.
- [ ] Tích hợp vũ khí vào cơ chế ném Phase 1 (mỗi vũ khí = projectile/tween + AoE riêng).
- [ ] Cơ chế chọn/đổi vũ khí (vũ khí đang sở hữu sẽ được "nạp" khi đưa tay ra sau đầu).

### 3.2 — Hệ thống Kinh tế (Economy)
- [ ] Ví tiền per-player, **reset $0 mỗi hiệp**.
- [ ] Thu nhập: **+$2/giây** (thụ động).
- [ ] Chiến tích: hạ đối thủ **+$5**.
- [ ] Đền bù: mất 1 mạng → **+$10 + 3s bất tử**.
- [ ] **Shutdown**: mỗi mạng giết được, giá trị mạng người đó **+$2**.
- [ ] Sync ví + giá trị mạng qua network (server-authoritative).

### 3.3 — Shop & Vòng trang trí
- [ ] Cơ chế **mua vũ khí** (UI VR / vật lý — mua bằng tiền trong trận).
- [ ] **Vòng ngoài trang trí** (shop ring / cosmetic hub): khu mua thêm đồ, **mua skin**.
- [ ] **Skin system**: mũ, kính, găng tay gắn lên avatar (socket đã chuẩn bị Phase 2).
- [ ] Lưu cosmetic đã mua (persistent — PlayerPrefs / save data; có thể dùng `BillFav`/data layer).

**Acceptance:** Trong trận có thể mua vũ khí bằng tiền kiếm được, đổi vũ khí khi ném; ngoài trận mua & gắn skin.

---

## PHASE 4 — Polish + Hệ thống trận đấu đầy đủ + Lobby

> **Mục tiêu:** Hoàn thiện vòng đời trận đấu đầy đủ theo GDD, lobby flow, và polish cuối. "Có làm thêm gì thì làm ở phase này."

### 4.1 — Cấu trúc trận đấu đầy đủ (Match Structure)
- [ ] **Bo3** (best of 3), mỗi hiệp **khóa cứng 90s đếm ngược**.
- [ ] **Nghỉ 5s** giữa hiệp: đổi bên + hiển thị bảng điểm.
- [ ] **Life Pool** theo mode: 1v1 = 7 mạng; 2v2 & 3v3 = 5; 4v4 & 5v5 = 4.
- [ ] **Cơ chế Khán đài (Linh hồn)**: hết mạng → bay lên khán đài, ném vật vô hại (cà chua, trứng thối) gây nhiễu.
- [ ] **Tie-breaker**: Wipe Out / Time Out Win (so tổng mạng) / Round Draw / Match Result (hòa 1-1-1 = Hòa Chung Cuộc).

### 4.2 — Sân đấu theo mode
- [ ] Kích thước sân auto theo mode (1v1 6×5m → 5v5 18×13m), khoảng hở trung tâm.
- [ ] Map themes: Sân Neon, Trạm Vũ Trụ, Căn cứ Dung nham...

### 4.3 — Out-Game / Lobby Flow (Interactive 3D Hub)
- [ ] **Personal Hub + Server Board** sau splash: 3 luồng:
  - [ ] **Host Game** (đấm nút HOST → sinh Room Code 5 ký tự, VD `TOSS1`).
  - [ ] **Join by Code** (ném khối chữ cái / bàn phím hologram).
  - [ ] **Quick Play** (đứng vào Teleport Pad → auto join public, ưu tiên ping thấp).
- [ ] **Waiting Room**: 3 vùng đứng — Vùng Xanh (Đội A) / Vùng Đỏ (Đội B) / Trung Lập; bảng đếm số người.
- [ ] **Host Control Panel** (lever/dial): Game Mode (1v1..5v5), Arena Size (auto-lock size không hợp lệ), Map Theme.
- [ ] **Ready Up**: nút hologram, đập tay → tick xanh.
- [ ] **Start Conditions**: nút START mở khóa khi (1) số người Xanh = Đỏ, (2) 100% người trong vùng đã READY.
- [ ] **Transition**: black-out / sàn mở rơi xuống → loading 2-3s → vào sân (khởi tạo $0, đếm ngược 90s).

### 4.4 — Pre-match Activities
- [ ] **Voice Chat Proximity** (âm thanh theo khoảng cách) + đập tay high-five.
- [ ] **Wardrobe**: góc gương, kéo thả skin lên avatar.
- [ ] **Warm-up Target**: máy bắn bóng + vài vòng buff vô hại để làm nóng tay.

### 4.5 — Polish cuối
- [ ] VFX/SFX/haptic cho từng vũ khí, vòng buff, hit, chết.
- [ ] Optimization (VR perf: draw call, đạn nhiều khi x15, batching).
- [ ] Balancing pass (theo bảng cân bằng GDD).
- [ ] UX/UI polish, tutorial/onboarding nếu cần.

---

## 5. Open Questions / Quyết định cần chốt

1. ✅ **RESOLVED — Fusion topology = Shared Mode** (ưu tiên nhanh). Ảnh hưởng: projectile spawn client-authoritative; economy/anti-cheat xử lý sau ở Phase 3.
2. **Projectile network model**: (a) Authority chạy tween + NetworkTransform sync, hay (b) spawn deterministic + mỗi client tự tween từ start tick? → Ảnh hưởng độ chính xác đường đạn.
3. **Tween lib**: DOTween (Pro có path/curve) hay tự viết?
4. **Matchmaking timeout 30s fallback**: hủy về menu / chờ tiếp / cho test 1 mình?
5. **Leg IK Phase 1**: procedural two-bone IK hay placeholder đơn giản (đủ cho demo)?
6. **Menu scene**: tách scene riêng hay dùng `01_TOSSZONE_Main` ở trạng thái lobby?

---

## 6. Mapping GDD → Phase (tham chiếu nhanh)

| Nội dung GDD | Phase |
|---|---|
| Matchmaking tối giản, 2 người vào 1 room, thấy nhau | 1 |
| Cơ chế ném (tay sau đầu + projectile + tween) | 1 |
| Joystick move, 2 team, capsule + leg IK, Fusion sync | 1 |
| Vòng Buff (5 loại, Tier 1-5, stacking, S_max, rarity) | 2 |
| Thay avatar player thật | 2 |
| 6 vũ khí (Đá → Bom Nguyên Tử) | 3 |
| Kinh tế ($2/s, kill $5, đền bù $10+3s, shutdown, reset/hiệp) | 3 |
| Shop mua vũ khí + vòng trang trí + skin | 3 |
| Bo3, 90s, đổi bên 5s, life pool, Linh hồn, tie-breaker | 4 |
| Lobby flow (Server Board, Waiting Room, team zones, Host Panel, Ready, Transition) | 4 |
| Map themes, arena size per mode | 4 |
| Pre-match (voice proximity, wardrobe, warm-up) | 4 |
| Polish VFX/SFX/haptic/optimization/balance | 4 |
