# TOSSZONE — Session Handoff

> Đọc file này ĐẦU TIÊN mỗi session. Nó là điểm vào: trạng thái mới nhất, flow test, và bản đồ docs.

## Bản đồ docs (đọc theo thứ tự khi cần)

| Doc | Khi nào đọc |
|---|---|
| **HANDOFF.md** (file này) | Đầu mỗi session — trạng thái + test flow |
| **GDD_Core_Reference.md** | ⭐ NGUỒN CHÂN LÝ thiết kế (chép từ GDD PDF owner 2026-07-02) — thắng mọi doc khác khi mâu thuẫn. LƯU Ý: không có ring Shield (vòng 5 = Tăng Kích Thước), Băng = freeze không damage, 6 vũ khí (có Bom X, không sword/mine), kinh tế/mạng/sân khác code hiện tại |
| **TASKS_WEAPON_UX.md** | ⭐ KẾ HOẠCH HIỆN HÀNH (Session 12+): task T18-T32, audit design-vs-code, spec từng vũ khí, UI inventory |
| **T17_Test_Report.html** | Checklist test 2 người khi build (mở bằng browser) |
| **BillGameCore_Usage.md** | Bản đồ framework: service nào dùng ở đâu + phần đã đắp thêm (tái dùng project khác) + gotchas |
| **Fusion_Shared_Mode_Gotchas.md** | BẮT BUỘC đọc trước khi viết/sửa bất kỳ networking code nào |
| **Burst_Projectile_System_Design.md** | ✅ đã build — reference khi đụng đạn mưa/GPU instancing |
| **Throw_Mechanic_Spec.md** | ✅ đã build — reference tune feel ném (levers ThrowConfig) |
| **Network_Architecture_Lessons.md** | Hiểu netcode + avatar IK + input + scene-load (bài học bền) |
| `deprecated/` | Hồ sơ cũ. Đáng chú ý: `TASKS_DETAIL.md` (T1-T17 ✅ xong hết), `TASKS_MASTER.md` (đóng băng S10), `Combat_Minigame_Design.md` (bị GDD supersede — CHỨA LỖI ring Shield; còn giá trị phần Sword/heckle/catch chờ owner chốt T31) |

Stack: Unity 6000.3 · URP · Quest/Android · Fusion 2.0.12 Shared Mode (NO Physics Addon) · BillGameCore · AutoHand.
Guard mọi `execute_code`: `if (!Application.dataPath.Contains("TOSSZONE")) return "WRONG PROJECT";` (folder tên TOSSZONE, KHÔNG phải ThrowingShot).

---

## Session 11 — 2026-07-02 (session vừa xong) — T1-T17 XONG HẾT

Chạy trọn 17 task của TASKS_DETAIL.md, mỗi task build + verify qua MCP + commit riêng (đọc `git log` theo
prefix "T<số>:" để xem chi tiết từng quyết định). Điểm nhấn:

- **T1-T7:** weapons fire + selector + team + dead-mask networked + catch burst + sword deflect + burst stacking.
  Fix 2 bug tồn đọng lớn: `CombatSession.CurrentCatalog` không bao giờ được nạp trong flow thật (ArenaManager
  giờ fire `MinigameEnteredEvent`), và BuffRing double-consume trong 0.25s shrink-tween (`_consumed` guard).
- **T8:** distance culling cho burst renderer (RenderMeshIndirect + compute cull HOÃN — cần Quest thật verify stereo).
- **T9-T11:** ring spawn random trong zone box + wander deterministic (fix bug NetworkTransform ghim ring đứng yên)
  + tier rarity theo cửa sổ thời gian + chống trùng Tier 4-5.
- **T12:** buff-ring áp qua RPC về authority của đạn (trước chỉ đúng khi solo/master).
- **T13:** `Bill.Players` registry + `FusionNet.LoadSceneAdditive` (BillGameCore, tái dùng được project khác).
- **T14:** fix crash `CollisionTracker` AutoHand (null-check) + **gỡ .gitignore AutoHand, commit cả pack vào repo**.
- **T15:** juice S4-S7 (haptic 3 tầng, SFX pitch theo lực, ReleaseFlash/ImpactBurst/BounceNumber pool, CombatJuice).
- **T16:** map blockout — 2 sân kín 14×12 (xanh/đỏ) + tường vô hình chặn NGƯỜI cho ĐẠN xuyên (layer
  PlayerWall×Projectile tắt collision) + spawn 3 điểm/đội random.
- **T17:** 2-client THẬT qua ParrelSync clone: cùng session, thấy nhau, bắn trúng đồng bộ máu đúng 2 phía.
  Còn nợ: round-end/respawn live (Photon free-tier RATE-LIMIT `DisconnectedByPluginLogic` khi connect/disconnect
  nhiều — chạy phiên dài thay vì nhiều phiên ngắn), T12 cross-authority thật.
- **Bug thật tìm ra & fix trong lúc verify:** `PlayerCombat.Local` race trỏ nhầm DummyAvatar (gate bằng
  `InputAuthority != None`); portal đốt `_used` khi non-master bước qua (giờ chỉ latch khi load được chấp nhận);
  dummy tự PASSIVE khi ≥2 người thật; CheatConsole crash gõ phím (clear TextField giữa KeyDown).
- **Dev tooling mới:** `DevCombatPanel` (nút equip từng vũ khí + $100/Heal/Ammo + bật tắt/xóa dummy, F1 toggle),
  cheat console lệnh `money/unlockall/equip/heal`, phím debug `T` ném / `G` grip / `F` trigger, gizmos debug
  (muzzle ray, quỹ đạo ném, bán kính AoE, đường quét kiếm).
- **CUỐI SESSION — feedback owner đổi hướng weapon UX:** flow selector hiện tại SAI so với vision (nút chọt vật
  lý + view-cone + grab hologram), kiếm vung vẫn ra bóng (root cause: `ThrowBallHolder` không đọc EquippedIndex),
  mọi đạn đều là bóng vàng generic. **→ Toàn bộ map + task T18-T24 nằm ở `TASKS_WEAPON_UX.md` — SESSION 12 BẮT
  ĐẦU TỪ ĐÓ (thứ tự: T19 → T18 → T20 → T21/T22).**

---

## Session 10 — 2026-07-01

Verify toàn bộ minigame (session 9-9c), fix bug, rồi build 4 task + chốt hướng đạn mưa.

**7 commit:**
| Commit | Nội dung |
|---|---|
| `c769ae4` | Fix 4 bug minigame: BuffRing màu, CombatSession NRE, ArenaManager spin round, DummyAvatar respawn |
| `2e710e6` | Network Phase 1: `ArenaNetworkLoadGate` (play thẳng arena) + fix 2-body player overlap |
| `730e3b5` | BuffRing MissingComponentException (dùng Collider chung, không ép SphereCollider) |
| `d232f61` | Fix bot ném (tắt gravity) + ring buff detection (thêm trigger collider vào NetworkProjectile) |
| `327b79b` | Doc: chốt thiết kế Burst System |
| `db8e5ea` | Task 1: Player death + respawn |
| `6fd9642` | Task 2: Network object pool (`PooledNetworkObjectProvider` + `NetworkPoolable`) + fix leak |
| `77b8ce1` | Task 3 cleanup: BuffRing tween exception, ring font glyph, Fusion tickrate |
| `c94c145` + `07a70d7` | Task 4: Burst System MVP + wire ring Multi → mưa đạn |

**Đã verify chạy (qua MCP):** hit dummy → máu giảm → chết → respawn; ring có màu/trôi/buff; arena loop; play thẳng arena (gate); portal Main→Arena ra 1 avatar; bot trúng player; player respawn; pool bounded (2 instance thay vì leak); burst 300 viên → player HP 5→0; ném xuyên ring Multi → burst 40 viên.

---

## Trạng thái toàn hệ thống

| Layer | Trạng thái |
|---|---|
| Throw mechanic (grab/swing/fire, IK, held-ball, projectile) | ✅ chạy |
| Minigame lõi: hit → death → respawn dummy → ring buff → bot | ✅ verify |
| Player respawn | ✅ (Task 1) |
| Network pool + hết leak đạn | ✅ (Task 2) |
| Burst System (đạn mưa data-oriented + GPU instance + hit RPC) | ✅ MVP (Task 4) |
| Ring Multi → burst | ✅ wired |
| Weapons bắn (gun/grenade/bazooka...) + model trên tay | ✅ chạy (model = cosmetic, T19 nâng lên Grabbable thật) |
| WristWeaponSelector | 🟡 chạy nhưng SAI FLOW theo vision owner → rework T18 |
| Catch / Sword deflect | ✅ (T4/T5) — kiếm còn bug ra bóng → T19 |
| Team A/B + win-condition BO1/3/5 | ✅ code (T3) — round-end live 2 máy chưa verify |
| Buff zones (tường băng, vùng lửa) | ✅ (T10) |
| Ring rules + zone drift | ✅ (T9/T11) |
| Map blockout 2 sân + tường | ✅ (T16) |
| Juice (haptic/VFX/impact) | ✅ (T15) |
| 2-player thật (ParrelSync) | ✅ core verify (T17) — checklist còn lại trong T17_Test_Report.html |
| Per-weapon projectile visuals | ❌ mọi đạn là bóng generic → T20 |

Việc tiếp theo → **`TASKS_WEAPON_UX.md`** (T18-T24, thứ tự T19 → T18 → T20 → T21/T22).

---

## ══ FLOW TEST ══

### Bước 0 — Môi trường

1. **ĐÓNG editor "Teabag - Copy"** nếu đang mở. Hai editor cùng lúc làm MCP routing loạn + gây domain-reload giữa play (bug tái diễn cả session 10). Đóng nó là ổn định hẳn.
2. Mở Unity project TOSSZONE. Kiểm console: 0 compile error.
3. Bật XR Device Simulator (hoặc Meta XR Simulator) — không bật thì AutoHand nổ exception, avatar không lên. (`Tools ▸ TOSSZONE ▸ XR Sim` auto-spawn khi Play.)

### Test A — Play thẳng 02_Arena (nhanh nhất, không cần rig)

1. Mở scene `02_Arena`, bấm Play.
2. Chờ 3-5s: bootstrap → connect Fusion → `ArenaNetworkLoadGate` tự Fusion-load lại scene cho scene objects sống (màn hình chớp 1 nhịp là đúng).
3. Kiểm: DummyAvatar đứng ở (0,0,4), thanh máu 5 cục. 3 ring nổi lên, mỗi ring 1 màu, trôi lên xuống, label "Lửa/Băng/Đạn Mưa/Chắn/Tốc Độ" (không còn ô vuông).
4. Ném bóng (bàn phím T nếu bind, hoặc grab bằng tay sim) vào dummy → máu tụt → chết (xám) → 3s sau sống lại đủ máu.
5. Đứng yên: bot tự ném lại → máu player tụt. Player chết → 3s sau respawn về spawn point.

### Test B — Portal Main→Arena (kiểm overlap fix)

1. Mở `01_TOSSZONE_Main`, Play.
2. Đi vào cổng `[ArenaPortal]`.
3. Sang arena: kiểm CHỈ CÓ 1 avatar mình (trước đây bị 2 body chồng ở gốc). Soi qua mirror hoặc bỏ tick `_hideOwnVisuals` trên NetworkAvatar prefab.

### Test C — Đạn mưa (Burst System, tính năng mới)

1. Trong arena, chờ ring **Multi** ("Đạn Mưa") xuất hiện (ngẫu nhiên 1/5). Nếu lâu, chỉnh RingSpawner cho ra Multi để test.
2. Ném bóng xuyên qua ring Multi.
3. Quả bóng đơn biến thành **mưa 40 viên** (render GPU instance, DrawMeshInstanced), bay theo hướng ném, arc xuống theo trọng lực.
4. Viên nào trúng player/dummy → trừ máu. Số viên chỉnh trong `RC_Multi.multiplier`; arc trong `BuffRing._burstGravity`; tốc độ/spread/lifetime trong `ProjectileBurstSystem` inspector.

### Dev check nhanh qua MCP (khi nghi ngờ, dùng execute_code)

```csharp
// đếm avatar (phải =1), scene objects sống, burst
var avs = Object.FindObjectsByType<TossZone.Player.NetworkAvatar>(FindObjectsSortMode.None);
var am  = Object.FindFirstObjectByType<TossZone.Combat.ArenaManager>();
var sys = TossZone.Combat.ProjectileBurstSystem.Instance;
return "avatars="+avs.Length+" arenaValid="+am.GetComponent<Fusion.NetworkObject>().IsValid
     + " burstSystem="+(sys!=null);
```
Nếu scene objects dormant khi play thẳng (gate chưa fire): gọi tay `BillGameCore.FusionNet.Instance.LoadScene(2)` (master) để attach.

---

## Việc tiếp theo (→ TASKS_WEAPON_UX.md)

T1-T17 xong hết. **Đọc `GDD_Core_Reference.md` TRƯỚC** (nguồn chân lý mới — nhiều thứ code đang lệch GDD).
Session 12 = weapon UX rework: **T19** (held grabbable thật, fix kiếm-ra-bóng) → **T18** (selector nút chọt +
view-cone + grab hologram) → **T25** (training range) → **T20** (đạn đúng model) → **T27** (RING OVERHAUL theo
GDD: Shield→Area, Băng=freeze, giá trị theo Tier, stack≤3...) → **T30/T31** (match/economy/weapon theo GDD —
2 câu hỏi chờ owner chốt trong T31). Backlog: T28 HUD, T23/T32 lobby, T24 host-migration. Song song: owner
build APK test theo checklist `T17_Test_Report.html`.

---

## ⚠️ Gotchas quan trọng nhất

1. **Đóng Teabag editor** — 2 editor làm routing MCP loạn + domain-reload giữa play. `execute_code` tự an toàn (guard TOSSZONE); `read_console`/`manage_scene` hay nhảy nhầm editor → đọc console qua `UnityEditor.LogEntries` bằng execute_code.
2. **Play thẳng minigame scene KHÔNG spawn scene NetworkObjects** trừ khi qua Fusion LoadScene. `ArenaNetworkLoadGate` xử lý điều này (sentinel = ArenaManager); nếu thêm minigame scene mới, đặt 1 gate + wire sentinel.
3. **Thêm [Networked] vào NetworkBehaviour hoặc file .cs mới** → cần `refresh_unity scope=all` (scope=scripts bỏ sót file mới) + force-reimport prefab để Fusion bake lại.
4. **MonoBehaviour phải nằm file TRÙNG TÊN class** mới add được vào prefab (không thì ra `<missing>` script). Vd `NetworkPoolable` phải ở `NetworkPoolable.cs`.
5. **Domain-reload giữa play** (compile xong trễ) reset statics (Bill/FusionNet null) nhưng isPlaying vẫn true → half-state hỏng. Stop rồi Play lại là sạch.
6. Đọc `Fusion_Shared_Mode_Gotchas.md` trước khi sửa networking. State Authority là authority duy nhất trong Shared Mode; grabbable cần `Allow State Authority Override`.

---

## Lịch sử session (tóm tắt — chi tiết trong git log)

- **S4** ballistic throw v1, Stickman avatar + procedural legs, bootstrap VR rig.
- **S5** peak-velocity throw, AutoHand grab + locomotion fix, networking groundwork.
- **S6** IK roll fix (arm+leg), S2 held-ball sync, S3 networked projectile.
- **S7** dọn merge, weapon system (7 WeaponConfig), combat foundation (PlayerCombat + hit + buff-hook).
- **S8** HealthUI, DummyAvatar (bot target), hit-detection guard fix.
- **S9-9c** full minigame pass: CombatSession, HandWeapon, CatchController, BuffRing/RingSpawner, ArenaManager, DummyBotDriver, WristWeaponSelector, RewardText; scene + prefab + data asset setup + ref audit.
- **S10** verify toàn bộ + 5 bug fix + player respawn + network pool + burst system.
- **S11** T1-T17 XONG HẾT (17 task + ~10 bug thật tìm & fix) + dev tooling + map blockout + 2-client verify;
  cuối session owner chốt rework weapon UX → `TASKS_WEAPON_UX.md` (T18-T24) cho session 12.
