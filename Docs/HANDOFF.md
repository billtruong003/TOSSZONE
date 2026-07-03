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

## Session 12 — 2026-07-03 (session vừa xong) — T19 → T18 → T25 → T20 XONG, verify từng task qua MCP

> **PROMPT CHẠY TIẾP (paste nguyên văn vào session mới):**
> ```
> Đọc Docs/HANDOFF.md rồi Docs/GDD_Core_Reference.md rồi Docs/TASKS_WEAPON_UX.md.
> Session 12 đã xong T19 → T18 → T25 → T20 (đến commit 7dabfe8). Chạy tiếp tuần tự: T27 → T30 → T31 → T26 → T28.
> Mỗi task: build code thật, verify qua Unity MCP (set_active_instance TOSSZONE, guard WRONG PROJECT,
> đọc console qua LogEntries), commit riêng từng task.
> Đã chốt với t: ring TRÔI NGANG theo GDD (T27 ⑧); Sword+LandMine GIỮ làm extension; mua vũ khí kiểu
> MIX per-weapon — khi tới T31 đề xuất bảng mix cụ thể cho t duyệt trước khi code phần liên quan.
> Networking đọc Fusion_Shared_Mode_Gotchas.md trước. Framework đọc BillGameCore_Usage.md.
> Đọc mục "Session 12" trong HANDOFF.md để biết gotchas mới (headless input, meta stub, Runner.Spawn window).
> ```

**4 commit (mỗi task 1 commit, chi tiết đầy đủ trong message):**
| Commit | Task | Nội dung |
|---|---|---|
| `47718e8` | **T19** | `WeaponHolder` thay `ThrowBallHolder` (đã xóa): grip → force-grab ĐÚNG vũ khí đang equip làm Grabbable THẬT (auto-pose), 1 instance/vũ khí SetActive-swap, `EnsureAttached` tự retry (grab AutoHand là coroutine nhiều frame, swap nhanh có thể trượt). Kiếm cầm kiếm — HẾT bug ra bóng. Owner-side cosmetic (ThrowController sphere + HandWeapon wrist model) tự nhường khi holder sống; remote proxy giữ cosmetic. Scene: `[ThrowSystem]` (01_Main) swap component; 02_Arena thêm object `WeaponHolder`. **Data fix:** `WC_Bazooka.heldPrefab` trỏ nhầm `MS_WP_Rocket` (art đạn, không Grabbable) → `MS_WP_RocketLaucher`. Verify: cả 7 vũ khí + ball đều `+HELD`; 10 vòng swap nhanh → 8 instance, không leak. |
| `c0d278a` | **T18** | `WristWeaponSelector` viết lại: **view-cone** (dot camera > cos 22°, ≤1m — thay palm-up, có hàm pure test 5/5 case) · **2 nút chọt** `PokeButton3D` (component dùng chung với T25: cooldown 0.4s, haptic, scale-pulse, filter `AcceptedHands` — panel cổ tay trái chỉ nhận tay PHẢI, debounce per-hand) · **hologram xoay** ở anchor giữa (material field chờ shader owner — tạm `M_HologramBlue/Denied` URP Unlit) — **đưa tay vào + bóp grip để mua/equip**, thiếu tiền/khóa = material đỏ + zone từ chối. Prefab NetworkAvatar: subtree `WristL/SelectorPhysical` (nút ±0.1m, anchor y+0.07 — vị trí first-pass, owner tune trong headset). Verify: cone đúng công thức live; poke→navigate chạy qua physics thật; grab-hologram equip rock trong 3 frame → vũ khí thật vào tay (chuỗi T18→T19 liền mạch). |
| `91dfa21` | **T25** | Training range trong hub tại `(-8,0,4)`: 7 nút vũ khí equip FREE + 5 nút ring theo element + nút "x8 ngẫu nhiên" + 3 DummyAvatar (6-11m) + tường target. `CombatSession.TrainingMode` (KHÔNG phải dev-cheat — chạy cả release) bypass unlock-time + giá mua ở HandWeapon/WristWeaponSelector. `TrainingRangeController` tự tạo CombatSession DDOL (hub không có!), fire `MinigameEnteredEvent{arena}` → **catalog sống ngay tại hub**; ring spawner + dummy **runtime-spawn khi master** (scene NetworkObject ở hub bị DORMANT — hub không load qua Fusion). `RingSpawner.SpawnSpecific(element, tier)` — ring theo yêu cầu, ngoài slot system, không auto-respawn. Verify: bazooka bắn được ở giây 0 (unlock 30s → bypass OK); nút Lửa → 1 ring Fire tier 3; x8 → 9 ring sống không lỗi. |
| `7dabfe8` | **T20** | Đạn bay đúng loại MỌI client: thay vì N prefab variant + N pool, dùng **1 field `[Networked] int VisualIndex`** (0=sphere, i+1=catalog index) shooter stamp trong `onBeforeSpawned`; mỗi client tự đắp cosmetic từ catalog của mình (`WeaponVisuals.SpawnProjectileVisual`, cache qua pool-life) — sync cause not effect. `ThrowProjectile.ApplyWeaponVisual` cho đường ném local. Data: WC_Gun→`MS_WP_Gun_Bullet`, WC_Bazooka→`MS_WP_Rocket`; còn lại fallback `heldPrefab` (grenade/bigboom/mìn/đá bay đúng hình). **Fix bug sẵn có:** renderer NetworkProjectile bị tắt vô điều kiện trên authority → shooter không thấy đạn Gun/Bazooka của chính mình (giờ chỉ ẩn khi LinkTo local twin). Dummy hub spawn PASSIVE (bot active giết người chơi đang tập trong vài giây — mỗi lần chết reset EquippedIndex). Verify: gun VisualIndex=2 mặc bullet + sphere tắt; bazooka=4 rocket; grenade ném = model local + mirror ẩn đúng phía authority; default (-1) vẫn sphere. |

**Câu hỏi đã chốt với owner (2026-07-03):** ① Ring **TRÔI NGANG trái↔phải theo GDD** (bỏ wander Perlin — làm ở T27 ⑧). ② **Sword + LandMine GIỮ làm extension** (T26 build đủ chuỗi mìn + sword feedback). ③ Mua vũ khí kiểu **MIX per-weapon** — chưa có bảng chi tiết món nào PayPerUse/BuyOnce: **khi tới T31 phải đề xuất bảng mix cụ thể cho owner duyệt trước khi code.**

**Gotchas MỚI học được session này (đọc trước khi verify MCP):**
1. **Headless MCP play-test input:** editor không focus → InputSystem mute hết. Fix: set runtime `InputSystem.settings.backgroundBehavior = IgnoreFocus` + `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView`, và tạo **keyboard ẢO** (`InputSystem.AddDevice<Keyboard>("VirtualKeyboard")`) rồi `QueueStateEvent` lên nó — queue lên keyboard THẬT sẽ bị state OS ghi đè mỗi frame. Xong test **restore 2 settings** + remove device (session này đã restore). Input ảo thỉnh thoảng vẫn hụt — fallback chắc chắn nhất: invoke thẳng method private qua reflection (`OnTriggerPressed`, `DebugThrow`...).
2. **Ghi file .cs mới bằng tool ngoài trong lúc Unity đang import** → meta stub hỏng (chỉ 2 dòng, thiếu MonoImporter) → file bị DefaultImporter nuốt: **0 compile error nhưng class không vào Assembly-CSharp**. Fix: move file ra ngoài Assets → `AssetDatabase.Refresh` → move lại → Refresh.
3. **`Runner.IsRunning` bật TRƯỚC khi simulation cấp được id** — `Runner.Spawn` trong cửa sổ đó throw NRE từ `Simulation.GetNextId` và để lại xác object nửa vời. Gate spawn bằng `PlayerCombat.Local != null` (avatar spawn xong = qua cửa sổ).
4. **Round reset (ArenaManager) và player chết đều reset `EquippedIndex = -1`** — test dài phải tính đến (holder tự re-grab ball là ĐÚNG hành vi).
5. **XR Device Simulator: 2 tay cụm gần cổ tay ở rest pose** → phantom-poke các nút selector (headset thật không bị — tay để 2 bên). Filter RightOnly + debounce đã giảm; đừng hoảng khi thấy viewIndex tự trôi trong sim.
6. **MCP bridge rớt sau domain reload nặng** ("No Unity Editor instances found") → gọi `set_active_instance 6401` lại là ổn.
7. `.mcp.json` từng trỏ `--default-instance 6405` (chết) → đã sửa **6401**. Port đúng xem `%USERPROFILE%\.unity-mcp\unity-mcp-status-*.json`.
8. **CatchController.OnTriggerEnter đọc `NetworkProjectile.Element` trước Spawned** → `InvalidOperationException` (pre-existing, thấy trong test) — **fix ở T26** (1 guard `Object.IsValid`).

**Trạng thái local machine (KHÔNG commit các file này):** `packages-lock.json` bị flip sang `"source": "embedded"` (máy này đang có embed kit stylized — theo quy tắc mục Git bên dưới: làm xong thì push kit repo rồi XÓA embed + revert hunk lock); `Assets/Beautify*.meta` untracked (orphan meta — check .gitignore rule #4); `OpenXRPackageSettings.asset` + `LiberationSans SDF - Fallback.asset` churn editor; `.claude/settings.local.json` là permission MCP local.

---

## Session 11 — 2026-07-02 — T1-T17 XONG HẾT

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
| Weapons bắn (gun/grenade/bazooka...) + model trên tay | ✅ Grabbable THẬT trong tay per-weapon (T19) — remote proxy vẫn cosmetic |
| WristWeaponSelector | ✅ rework T18: view-cone + nút chọt + grab hologram (vị trí nút/anchor owner tune trong headset; shader hologram chờ owner) |
| Catch / Sword deflect | ✅ (T4/T5) — kiếm cầm kiếm, HẾT bug ra bóng (T19) |
| Team A/B + win-condition BO1/3/5 | ✅ code (T3) — round-end live 2 máy chưa verify |
| Buff zones (tường băng, vùng lửa) | ✅ (T10) |
| Ring rules + zone drift | ✅ (T9/T11) |
| Map blockout 2 sân + tường | ✅ (T16) |
| Juice (haptic/VFX/impact) | ✅ (T15) |
| 2-player thật (ParrelSync) | ✅ core verify (T17) — checklist còn lại trong T17_Test_Report.html |
| Per-weapon projectile visuals | ✅ (T20) — VisualIndex networked, đạn đúng model mọi client (T20 chưa test 2 máy thật — làm cùng đợt test build) |
| Training range hub (warm-up) | ✅ (T25) — equip free + ring theo yêu cầu + x8 + 3 dummy passive |

Việc tiếp theo → **`TASKS_WEAPON_UX.md`**: **T27 → T30 → T31 → T26 → T28** (T19/T18/T25/T20 ✅ xong Session 12). Prompt chạy tiếp ở đầu mục Session 12 phía trên.

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

## Việc tiếp theo (→ TASKS_WEAPON_UX.md) — cập nhật cuối Session 12

**Xong:** T19 ✅ T18 ✅ T25 ✅ T20 ✅ (commit `47718e8` → `7dabfe8`, mỗi task 1 commit + verify MCP).
**Đọc `GDD_Core_Reference.md` TRƯỚC** (nguồn chân lý — nhiều thứ code vẫn lệch GDD).

Chạy tiếp theo thứ tự — chi tiết từng việc trong `TASKS_WEAPON_UX.md` mục 7:
1. **T27 — RING OVERHAUL theo GDD** (10 điểm ①-⑩ trong spec; ⑧ ĐÃ CHỐT: trôi ngang trái↔phải theo tốc độ
   tier, bỏ wander Perlin; nhớ đổi enum `Shield`→`Area` + asset `RC_Shield`→`RC_Area`; Băng=FREEZE không
   damage; Lửa=mất 1 mạng/lần + sống 1-3s; stack cộng dồn ≤3; ma trận tier; anti-dup 1×T4+1×T5; weight GDD).
   Đụng nhiều [Networked] → đọc lại Fusion_Shared_Mode_Gotchas.md + refresh scope=all + reimport prefab.
2. **T30 — Match & economy theo GDD** (90s hiệp; nghỉ 5s + đổi bên + bảng điểm; mạng 7/5/4 theo chế độ;
   timeout so TỔNG MẠNG ĐỘI; hòa 1-1-1; +$2/s; +$5/KILL; chết +$10 + 3s bất tử; shutdown bounty +$2).
3. **T31 — Weapon roster theo GDD** (giá/cooldown/AoE/unlock 6 món GDD; BUILD Bom Chữ X — vệt lửa chữ thập
   = 2 BuffZone hộp xoay 90°, rộng 1.1m × dài 47% sâu sân; Đá/Súng thêm AoE nhỏ 0.8/0.35m). ĐÃ CHỐT:
   Sword+LandMine GIỮ làm extension (8 món tổng). **MIX per-weapon: phải đề xuất bảng BuyOnce/PayPerUse
   từng món cho owner DUYỆT trước khi code.**
4. **T26 — Weapon phases** (nổ khi chạm ĐẤT cho grenade/bazooka/nuke; effect nổ theo aoeRadius: cầu lửa +
   shockwave + haptic, Nuke rung mạnh 2 tay + flash; laserSight Gun/Bazooka; magazine; isUncatchable enforce
   trong CatchController — **tiện tay fix luôn InvalidOperationException đọc Element trước Spawned, thấy
   trong log Session 12**; chuỗi mìn ném/đặt→ARM fuseDelay→đạp→nổ; costPerUse theo bảng mix T31).
5. **T28 — HUD/feedback inventory** (bảng đầy đủ ở TASKS_WEAPON_UX.md mục 5: ví tổng, ammo, scoreboard
   MS_ScoreBoard, countdown unlock, catch/deflect feedback, kill/win-lose...).

Backlog sau đó: T21 equip feedback (~30') · T22 icons (owner tự làm được) · T29 kiếm rút sau lưng (ngoài
GDD) · heckle khán đài · T23 matchmaking API · T24 host-migration · T32 lobby epic.
Song song: owner build APK test theo `T17_Test_Report.html` + tune vị trí nút selector/hologram trong headset
+ làm shader hologram (gán vào field `_hologramMat` trên WristSelector).

---

## 🔄 Git / pull workflow (Session 11 — fix "pull về hư material")

Root cause đã tìm ra và fix (2026-07-02): bản **embed local** của StylizedToonWorldKit trong `Packages/` được tạo bằng export/re-import nên **toàn bộ 82 GUID khác** với kit repo — trong khi mọi material đã commit reference GUID của kit repo → máy nào có embed là pink material sau pull. Embed đã bị gỡ; kit giờ resolve qua UPM git dependency trong manifest.json (đúng thiết kế).

Quy tắc để pull mượt:

1. **Không tự tạo embed kit bằng export/import.** Nếu cần sửa shader in-place: clone `stylized-toon-world-kit` repo → copy folder `Assets/StylizedToonWorldKit` (GIỮ NGUYÊN .meta) vào `Packages/com.billtruong.stylized-toon-world-kit/`. Xong việc thì push lên kit repo rồi XÓA embed. Lưu ý embed làm `packages-lock.json` flip sang `"source": "embedded"` — revert hunk đó trước khi commit.
2. **Line endings:** repo đã có `.gitattributes` (Unity YAML = LF, binary được đánh dấu). Trên máy mới: `git config core.autocrlf false` (local) sau khi clone.
3. **Merge scene/prefab:** máy này đã config UnityYAMLMerge (SmartMerge). Máy mới chạy:
   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
   git config merge.unityyamlmerge.driver "\"C:/Program Files/Unity/Hub/Editor/<UNITY_VER>/Editor/Data/Tools/UnityYAMLMerge.exe\" merge -h -p --force %O %B %A %A"
   ```
   rồi copy 6 dòng `merge=unityyamlmerge` vào `.git/info/attributes` (xem máy này làm mẫu — cố ý để per-machine, không commit).
4. **Asset ignored thì .meta cũng phải ignored** (DevAgentSettings là bài học — meta orphan bị Unity xóa/tái tạo GUID mới liên tục). Sau khi thêm rule ignore mới, chạy check: meta tracked mà asset không tracked = bug.
5. **Sau khi pull mà thấy pink/miss ref:** đừng re-assign tay rồi commit (sẽ đè GUID đúng của máy khác). Kiểm tra trước: `Packages/` có embed lạ không, `packages-lock.json` có bị flip không, AutoHand đã import chưa (paid asset, ignored — phải import từ Asset Store mỗi máy).

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
