# TOSSZONE — Session Handoff

> Đọc file này ĐẦU TIÊN mỗi session. Nó là điểm vào: trạng thái mới nhất, flow test, và bản đồ docs.

## Bản đồ docs (đọc theo thứ tự khi cần)

| Doc | Khi nào đọc |
|---|---|
| **HANDOFF.md** (file này) | Đầu mỗi session — trạng thái + test flow |
| **TASKS_MASTER.md** | Danh sách task sống (đã audit). Chọn việc tiếp theo ở đây |
| **TASKS_DETAIL.md** | Chi tiết "làm gì" từng task còn lại (mục tiêu/file/verify/deps) |
| **Fusion_Shared_Mode_Gotchas.md** | BẮT BUỘC đọc trước khi viết/sửa bất kỳ networking code nào |
| **Burst_Projectile_System_Design.md** | Trước khi đụng đạn mưa / pool / GPU instancing (hướng đã chốt) |
| **Network_Architecture_Lessons.md** | Hiểu netcode + avatar IK + input + scene-load (bài học bền) |
| **Combat_Minigame_Design.md** | Thiết kế combat/vũ khí (7 vũ khí, win-condition, buff ring) |
| **Throw_Mechanic_Spec.md** | Spec cơ chế ném (peak-velocity, feel/juice) |
| `deprecated/` | Docs bị THAY HƯỚNG (không phải "đã xong"). Vẫn có thiết kế chưa build — vd `Networking_Architecture.md` chứa `Bill.Players` + additive scene chưa làm (xem TASKS_MASTER mục F) |

Stack: Unity 6000.3 · URP · Quest/Android · Fusion 2.0.12 Shared Mode (NO Physics Addon) · BillGameCore · AutoHand.
Guard mọi `execute_code`: `if (!Application.dataPath.Contains("TOSSZONE")) return "WRONG PROJECT";` (folder tên TOSSZONE, KHÔNG phải ThrowingShot).

---

## Session 10 — 2026-07-01 (session vừa xong)

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
| Weapons bắn (gun/grenade/bazooka...) | 🟡 code + config, chưa fire |
| WristWeaponSelector | 🟡 code, chưa có prefab |
| Catch / Sword deflect | ⬜ chưa (burst follow-up) |
| Team A/B + win-condition BO1/3/5 | ⬜ chưa |
| Buff zones (tường băng, vùng lửa) | ⬜ chưa (mới là config) |
| 2-player thật (ParrelSync) | ⬜ chưa test |

Chi tiết + thứ tự → `TASKS_MASTER.md`.

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

## Việc tiếp theo (→ TASKS_MASTER.md)

Thứ tự đề xuất còn lại: hoàn thiện burst (stack qua nhiều ring, dead-mask + catch/deflect, RenderMeshIndirect + compute cull cho VR stereo) → weapons fire + wrist selector → team + win-condition → buff zones → 2-player verify → build Quest.

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
- **S10** verify toàn bộ + 5 bug fix + player respawn + network pool + burst system (xem trên).
