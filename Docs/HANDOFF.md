# TOSSZONE — Session Handoff

> Đọc file này ĐẦU TIÊN mỗi session. Nó là điểm vào: trạng thái mới nhất, flow test, và bản đồ docs.

## Bản đồ docs (đọc theo thứ tự khi cần)

| Doc | Khi nào đọc |
|---|---|
| **HANDOFF.md** (file này) | Đầu mỗi session — trạng thái + test flow |
| **GDD_Core_Reference.md** | ⭐ NGUỒN CHÂN LÝ thiết kế (chép từ GDD PDF owner 2026-07-02) — thắng mọi doc khác khi mâu thuẫn. LƯU Ý: không có ring Shield (vòng 5 = Tăng Kích Thước), Băng = freeze không damage, 6 vũ khí (có Bom X, không sword/mine), kinh tế/mạng/sân khác code hiện tại |
| **TASKS_WEAPON_UX.md** | ⭐ KẾ HOẠCH HIỆN HÀNH (Session 12+): task T18-T32, audit design-vs-code, spec từng vũ khí, UI inventory |
| **TEST_CASES.md** | ⭐ Bộ test case + edge case đầy đủ hệ chiến đấu (Session 14) — Fable rà bug theo checklist này. Cột Verify MCP/VR/2P, Pri 🔴🟡⚪, mục Regression cho bug đã fix |
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

## Session 17.3 — 2026-07-07 — VERIFY REPRO đủ 6 bug PT qua MCP (solo sweep) — bằng chứng trong TEST_CASES.md §J

Toàn bộ 6 bug PT-01..06 giờ có repro/bằng chứng live (không còn là suy đoán code-only). Điểm nhấn:
- **PT-03 nặng hơn báo cáo nhiều (nâng 🔴):** mesh `ColliderRing` sai scale ~100× (bounds gốc 211m)
  + convex hull torus = đĩa ĐẶC → mỗi ring là trigger ~150m trùm map. Repro: 1 đạn cắt mặt phẳng cụm
  ring cách tâm >10m → **3 ring tiêu thụ cùng lúc**. Chính là lời giải cho gotcha "ring nuốt đạn test"
  ghi từ S13. Convex hull KHÔNG BAO GIỜ có lỗ — phải thay bằng compound collider.
- **PT-02 root cause chính xác:** TMP `_CullMode=0` (double-sided) — thấy chữ mặt xa xuyên qua dạng gương.
  Fix 1 dòng: Cull Back cả 2 mặt.
- **PT-06 repro 100%:** Đá hit-radius hiệu lực 0.8m (AreaScale 2.667 đo live), không arm-gate — đạn bay
  cách dummy 0.6m despawn giữa không trung + dummy mất máu.
- **PT-04 bằng chứng trực tiếp:** SetDeadBit(300) trên burst Count=400 → IsDead=False (mask 256).
- **PT-05:** renderer đang chạy fallback Sphere+material cam runtime (field chưa từng gán).
- **PT-01:** đo thật cả arena (tâm 1.46-2.16m) lẫn hub (1.84-2.79m) — tâm không "sát đất" nhưng tier
  to mép dưới chỉ ~0.56m; là chuyện zone-Y-range vs đường kính ring, kèm hướng fix trong bảng.
- Verify thêm solo: V-6 PASS (freeze bị clear đúng khi round reset), C1-C3 PASS (fix ResetForRound sạch).
- 2P chạy được TRƯỚC khi Photon rate-limit (Code 104 ServerLogic) chặn tiếp: host/join mã `4YFZX` ✅,
  **II-2 PASS** (spawn 2 player deterministic đúng 2 phía (0,0,±9), hết random). Còn kẹt lại vì
  rate-limit: III-4 grace-kill live, II-7 side-swap live, VI-1/2 UI 2 máy, V-4 hazard-race.
  **Bài học cứng:** free-tier Photon đếm connect/disconnect CẢ NGÀY — hôm sau test 2P phải gom hết case
  vào 1-2 phiên play DUY NHẤT, không start/stop lặp. V-1/V-5 cần VR, III-16 cần 3 máy.

---

## Session 17.2 — 2026-07-07 — Fix spawn-overlap (`451b69e`) + log 6 bug owner playtest thật (Docs/TEST_CASES.md §J)

**Fix nhỏ:** `GetSpawnPosition` đổi random → rank ổn định theo PlayerId trong team (Teams dict) — đồng đội không
còn trúng chung 1 điểm spawn (`451b69e`).

**6 bug MỚI owner báo từ chơi thật (VR), đã research code + ghi đầy đủ vào TEST_CASES.md §J (PT-01..06),
CHƯA fix — chờ owner duyệt hướng trước khi code:**
1. **PT-01** Ring buff spawn sát đất — zone box Y half-extent (0.5m) nhỏ hơn cả bán kính ring tier 1 (0.9m).
2. **PT-02** Bảng điểm giữa sân 2 mặt đè nhau không đọc được cả 2 phía — REG-18 cũ chỉ offset 1cm, KHÔNG đủ +
   không có backing panel chắn giữa 2 mặt + `Update()` gán chung 1 string cho cả 2 mặt. Owner đề xuất: bỏ
   kiểu 2-mặt-chung-1-object, làm 1 bảng hiện theo local-viewer (world-space UI riêng mỗi client).
3. **PT-03** Ring "ăn" đạn mưa (burst) dù không xuyên miệng vòng thật — đạn đơn có collider/OnTriggerEnter
   ĐÚNG, nhưng burst check qua khoảng cách phẳng tới tâm ring (không xét hình học vòng).
4. **PT-04** Đạn mưa bay xuyên người không ngưng — `DeadMaskBits=256` << `MaxProjectilesPerBurst=4096`,
   pellet index ≥256 trúng damage nhưng không đánh dấu chết được nên render tiếp tục bay xuyên.
5. **PT-05** Đạn mưa hiện sphere cam generic thay vì model Đá — `ProjectileBurstRenderer` không hề nối với
   `WeaponVisuals`/`WeaponConfig`, 1 mesh/material cố định cho mọi burst.
6. **PT-06** Ném Đá nổ/despawn giữa không trung ngang người, chưa chạm đất — `HitFirstVictim()` (đường
   non-explosive) không có arm-gate như đường `_explosive` (`IsArmed()`/0.7m) — bay gần bất kỳ ai (không chỉ
   mục tiêu) trong 0.8m là despawn ngay từ tick đầu.

Chi tiết file:line + hướng fix đề xuất từng bug: xem bảng §J trong `Docs/TEST_CASES.md`.

---

## Session 17.1 — 2026-07-07 — Checklist 50 case spawn/team/late-join + fix 2 bug (`57ca207`)

Chạy checklist chi tiết end-to-end (spawn position, team assign, round lifecycle, disconnect/host
migration, cross-client UI — xem yêu cầu owner trong chat) qua 2-client MCP. Code-review tĩnh trả lời
nhiều case nhanh (side-swap parity đúng công thức, `GetSpawnPosition` fallback an toàn, `IsOpen` late-join
chỉ mở lại ở MatchEnd — xác nhận suốt CẢ TRẬN không ai vào thêm được, Warmup rage-quit không fast-end
phải chờ hết 90s — 2 điểm này để lại note, KHÔNG fix, vì là câu hỏi thiết kế thuộc backlog "late-join
polish" chứ không phải bug rõ ràng).

**🔴 Bug nặng nhất — fix rồi:** `ArenaManager` (scene NetworkObject) có flag `DestroyWhenStateAuthorityLeaves`
(262145 = V1+DestroyWhenStateAuthorityLeaves, verify bit-value qua reflection `Fusion.NetworkObjectFlags`,
KHÔNG đoán mò). Host (holder StateAuthority) rời giữa trận → Fusion HUỶ LUÔN object trên máy còn lại
(`ArenaManager.Instance=null`, `IsValid=False`) thay vì chuyển giao — mất sạch Round/Score/Phase/Teams,
kẹt vĩnh viễn, không tự hồi phục. Fix: đổi Flags → `MasterClientObject` (131073) — đúng theo
Fusion_Shared_Mode_Gotchas.md §7 ("current Master Client always holds State Authority").
**⚠️ CHƯA re-verify sạch được** — sau nhiều domain-reload/half-state dồn trong phiên dài, máy non-master
bị treo dormant (`IsValid=False`) khi attach ArenaManager **CẢ VỚI FLAG GỐC** (loại trừ do fix gây ra),
nghĩa là môi trường test đã xuống cấp chứ không phải do thay đổi. Recommend: verify lại migration thật
(disconnect master giữa Playing, xem máy còn lại có tiếp tục Round/Score đúng không) ở phiên Unity MỚI,
sạch, chưa qua nhiều domain-reload.

**🟡 Bug thứ 2 — fix rồi:** `NetworkAvatar.RespawnTimer` không bị `RPC_ResetRound` clear (field khác
component, ArenaManager chỉ loop `PlayerCombat.AllInstances`) — timer hồi sinh 3s cũ có thể trigger
`RestoreLives()+TeleportToSpawn()` lạc giữa round MỚI đã bắt đầu. Đồng thời `TeleportToSpawn`/side-swap
theo Round (T30) CHỈ chạy qua đường chết→respawn — người sống sót cả round không bao giờ re-position dù
thiết kế đổi bên mỗi round. Fix: `NetworkAvatar.ResetForRound()` mới (clear `RespawnTimer` + teleport về
spawn point đúng side) gọi từ `RPC_ResetRound` cho MỌI client — không riêng người vừa chết.

---

## Session 17 — 2026-07-07 — VERIFY Session 16 (compile + solo + 2-client thật qua ParrelSync) + fix 1 bug mới

Pull `17b3109` về công ty, verify toàn bộ theo checklist owner để lại cuối Session 16.

**Compile:** sạch, 0 lỗi (refresh_unity force + compile, đọc console 2 lần).

**Solo (MCP, 02_Bootstrap → hub):** connect gate hiện đúng, `[RoomConsole]` tại (0,1.05,2.6) status
"Phòng công khai 1/8" → HOST PHÒNG RIÊNG ra mã 5 ký tự (`isVisible=False`) → QUICK PLAY về đúng phòng
công khai. Không lỗi console.

**2-client thật (ParrelSync `TOSSZONE_clone_0`, 2 Unity editor riêng qua MCP hai port 6400/6403):**
- Quick-play 2 máy → cùng session GUID, players=2. ✅
- Host mã phòng máy A → nhập đúng mã máy B (`OnLetter`+`OnJoin`) → cùng phòng riêng `isVisible=False`. ✅
- Vào arena (`FusionNet.LoadScene(2)` từ master), damage Player non-master xuống 0 máu (gọi
  `RPC_TakeHit` từ ĐÚNG client sở hữu — gọi từ máy khác không tác dụng vì `HasStateAuthority` gate).
  Round-reset (`RPC_ResetRound`) verify ĐÚNG: máu/tiền client non-master reset về full, mọi client
  thấy cùng Round/Phase/Score (bug chính Session 16 đã fix thật). ✅
- Rage-quit (Stop Play 1 máy giữa round) → round kết thúc NGAY, không chạy hết 90s. ✅

**🐛 Bug MỚI tìm ra khi verify 2-client (fix trong session này, `56bc93a`):** `StartRound()` gọi
`RPC_ResetRound` cùng tick chuyển `Phase=Playing`, nhưng reset chỉ áp dụng thật khi tới đúng client có
`StateAuthority` của `PlayerCombat` đó rồi sync ngược lại master — có độ trễ mạng thật (không phải chỉ
MCP catch-up). Trong lúc đó `CheckWinCondition()` chạy MỌI tick vẫn đọc `Health=0` cũ (từ round trước)
trên proxy của master → xử thua tiếp round kế, có thể cascade qua nhiều round liên tiếp chỉ vì lag
(repro thấy: Round 1→3, ScoreA 0→2 trong 1 nhịp catch-up). **Fix:** `WinCheckGrace` TickTimer 0.5s set
trong `StartRound()`, `CheckWinCondition()` chỉ chạy sau khi hết grace. Re-verify: health=0 → thua đúng
1 round, round sau bắt đầu sạch (Health=7 cả 2 phía) không cascade; rage-quit vẫn kết thúc round ngay
(không bị chặn vì round đã chạy lâu hơn 0.5s lúc rage-quit xảy ra).

**Gotcha mới:** domain-reload/recompile giữa lúc đang Play (kể cả do MCP `refresh_unity`/git pull ngoài
ý muốn) làm `FusionNet.Instance` rớt null vĩnh viễn dù `isPlaying=true` — đúng gotcha cũ đã ghi, khắc
phục bằng Stop→Play lại, KHÔNG cần debug thêm.

**Còn lại (chưa làm — theo backlog owner để lại):** late-join polish (spawn theo team qua ArenaManager +
invuln khi vào giữa trận), rồi mới tới skin/cosmetics avatar.

---

## Session 16 — 2026-07-06 (session vừa xong) — PIVOT: rock-only + rework toàn bộ connect/room flow

Owner chốt hướng mới: tạm khoá hết trừ Đá, tập trung network UX. Audit 3-agent tìm ra: cả thế giới
chung 1 phòng cứng `TOSSZONE_DEMO`, cap thực 10 (không phải 8), connect chạy NỀN sau khi hub đã hiện
(đúng phàn nàn UX của owner), zero xử lý fail/disconnect, round-reset không tới client remote.

**Gỡ tạm (reversible, 1 chỗ mỗi cái):**
- Ring lửa/băng OFF: `RingSpawner.AllowedElements = {Multi, Speed, Area}` (KHÔNG đụng catalog — gỡ
  config asset sẽ ra ring ma kẹt slot). Nút training hub vẫn spawn được lửa/băng (owner chấp nhận).
- Vũ khí rock-only: `Resources/Minigames/arena.asset` weaponCatalog trim còn WC_Rock. WC_*.asset giữ nguyên.
- Wrist panel OFF: override `m_IsActive: 0` trong NetworkAvatar.prefab (untick lại là hồi). Đây từng là
  HUD tiền/đạn duy nhất — rock free/vô hạn nên không sao.

**Connect flow mới (schema):** Splash 00_Bootstrap giờ là gate thật — `StartupConnectStep` đăng ký
BillStartup step gọi `ConnectionFlowController.QuickPlay()` (random-join phòng mở, cap 8), retry vô hạn
kèm status text; hub chỉ load khi ĐÃ vào phòng. `ConnectionFlowController` (DDOL, file mới) là NƠI DUY
NHẤT start/switch session: state machine bắn `MatchmakingStatusEvent` (trước là dead code, giờ sống),
backoff retry, mất kết nối giữa trận → fade về hub + tự QuickPlay lại. `ConnectionStatusHud` (runtime,
không cần wire scene) hiện status trước mặt camera. PlayerSpawnManager không tự connect nữa (chỉ còn
fallback EnsureConnected cho editor direct-play) + fix race `_spawnInFlight` kẹt vĩnh viễn khi Spawn throw.

**Room flow:** cap 8 mọi đường (StartGameArgs + NetworkProjectConfig). Private room: API
`HostPrivateRoom()` → code 5 chữ (bộ ký tự không nhầm lẫn, IsVisible=false), `JoinPrivateRoom(code)`
(JoinOnly — không tự tạo phòng ma; fail → tự fallback QuickPlay sau 2s); FusionConnectArgs thêm
`HideFromMatchmaking`/`JoinOnly`. UI: `RoomCodeConsole` (`[RoomConsole]` trong hub, pos 0/1.05/2.6 —
owner chỉnh vị trí tuỳ ý) — console DỰNG RUNTIME toàn bộ (backboard + HOST/QUICK PLAY + bàn phím 31 ký
tự PokeButton3D + XÓA/VÀO PHÒNG), không có prefab để tune; muốn đổi layout thì sửa `Build()` trong
RoomCodeConsole.cs. Đây là bản pragmatic — GDD §VII (letter blocks/hologram keyboard) là bản art sau.

**ArenaManager rework (bug Shared-mode thật):**
- Round-reset qua `RPC_ResetRound(maxLives)` (StateAuthority→All) — trước đây master gọi thẳng
  `ResetForRound()` nên máu/tiền remote KHÔNG BAO GIỜ reset giữa round (chỉ lộ khi 2+ người thật).
  `NetMaxLives` [Networked] cho late-joiner. `NotifyRoundStart` cũng theo RPC (RoundElapsed từng sai trên non-master).
- `RoundEndEvent`/`MatchEndEvent` giờ bắn qua ChangeDetector trên Phase (Render) → MỌI client thấy
  THẮNG/THUA (trước chỉ master thấy). `LastWinnerTeam` [Networked].
- Team qua `Teams` NetworkDictionary (master gán join-order balance, sync khi join/leave) — thay
  `PlayerId % 2` từng ra 3v0 khi có gap ID. GetTeam giữ nguyên signature, fallback %2 ngoài arena.
- Team trống giữa round (rage-quit) → end round ngay thay vì chạy hết 90s (`RoundHadBothTeams`).
- `IsOpen=false` khi round start (chặn late-join rơi giữa trận), mở lại ở MatchEnd/Despawned.

**CHƯA VERIFY:** Unity chưa recompile lúc kết session (cần focus editor). Cần test: solo hub flow,
2-client (round reset remote — bug chính), quick-play 2 máy ra cùng phòng, host/join code qua console.
Task UI room code + polish late-join (spawn theo team + invuln khi vào giữa trận) còn mở.

---

## Session 15 — 2026-07-06 — RÀ TOÀN BỘ theo TEST_CASES + FIX 9 BUG, verify MCP từng cái

Chạy prompt kiểm tra của Session 14: rà checklist TEST_CASES (case MCP chạy live, VR/2P đọc code + note),
tìm 9 bug rồi owner duyệt fix hết. Chi tiết từng bug trong commit message + bảng Regression REG-13..21
của TEST_CASES.md.

**8 commit fix (mỗi cái verify MCP riêng):**
| Commit | Bug (Pri) | Tóm tắt |
|---|---|---|
| `9b0774c` | 🔴 Catch không despawn đạn | CatchController nhánh netProj thiếu `RPC_RequestSelfDespawn` — đạn "đã bắt" bay tiếp 5s |
| `e2c3524` | 🟡 Mìn nổ Error kinematic + ⚪ đạn không né xác chết | guard `!isKinematic` trong Explode; skip `Health<=0` ở HitFirstVictim/DamagePlayersAround/AnyVictimInRange |
| `f4c013b` | 🟡 Freeze không Max | RPC_Freeze chỉ ghi timer khi `incoming > remaining` (ice tier thấp từng GIẢI SỚM băng tier cao) |
| `a7e7b12` | 🔴 Wipe Out không theo đội | CheckWinCondition đếm sống PER-TEAM; end khi 1 đội về 0; cả 2 về 0 = Draw; guard roster lệch đội |
| `e7d7a30` | 🟡 Scoreboard 2 mặt đè khít | offset mỗi mặt 1cm về phía đọc (`localRot * (0,0,-0.01)`) |
| `a4d6e0d` | 🔴 **Đạn ném TREO vĩnh viễn trên map** | Tween pool tái cấp instance + `_tween?.Kill()` ref cũ giết nhầm tween bay (repro 100% ALIASED_WITH_STALE) → thay hết bằng `KillTarget(this)` owner-scoped, 6 file; luật mới trong BillGameCore_Usage.md §3 |
| `a4ddceb` | 🟡 Ném liên tiếp giết chéo twin network | slot đơn `_activeNetProj` + BallLandedEvent vô danh → Dictionary ball→twin + `BallLandedEvent.Ball`; spam ném giờ giữ đủ twin mọi viên |
| `7361eb8` | 🟡 Label training range đè chữ | rect 1.2m→0.34m + autoSize + NoWrap, reparent 13 label vào nút cha, "Chắn"→"Kích Thước" (khớp RC_Area) |

**Gotchas MỚI:**
1. **BillTween: KHÔNG giữ `Tween` ref qua frame rồi `.Kill()`** — pool tái cấp instance, Kill không check danh
   tính + không fire OnComplete → nạn nhân ngẫu nhiên đứng hình vĩnh viễn. Luật: `SetTarget(owner)` +
   `KillTarget(owner)`, không lưu field (chi tiết BillGameCore_Usage.md §3).
2. Session test mới PHẢI tắt lại DummyBotDriver — đạn bot NUỐT ring test (SpawnSpecific ring biến mất giữa
   2 call là do bot bắn xuyên, không phải bug).
3. `rings=0` ngay sau gate reload là BÌNH THƯỜNG (3 ring đầu chết theo scene load, respawn ~10s sau).

**Còn nợ verify 2P (cùng đợt test build):** Wipe Out per-team live 2 máy · twin race nhìn từ client thứ 2 ·
danh sách 2P cũ của Session 13. **Edge chưa chốt hỏi owner (cuối TEST_CASES.md):** MINE-05 shooter tự đạp
mìn mình (hiện KHÔNG nổ) · WPN-10 point-blank trúng từ tick 2 (~0.02s) đủ chưa · scale sân theo mode.

---

## Session 14 — 2026-07-05 — FIX 5 BUG feel/perf owner báo khi test, verify MCP từng cái

> **⚠️ QUY TẮC owner:** code CLEAN, KHÔNG viết comment (gotchas ghi Docs/commit message).
>
> **PROMPT KIỂM TRA TOÀN BỘ (đưa cho Fable khi lên cty — check bug):**
> ```
> Đọc Docs/HANDOFF.md (mục Session 14 + 13) rồi Docs/TEST_CASES.md rồi Docs/GDD_Core_Reference.md.
> Nhiệm vụ: RÀ SOÁT TOÀN BỘ hệ chiến đấu tìm bug theo CHECKLIST Docs/TEST_CASES.md (chạy các case Verify=MCP,
> đọc code + note các case VR/2P), KHÔNG code tính năng mới. Set active instance TOSSZONE
> (Unity MCP, guard "if(!Application.dataPath.Contains(\"TOSSZONE\")) return \"WRONG PROJECT\";"), đọc console
> qua UnityEditor.LogEntries. LƯU Ý MCP: sau vài lần play/stop editor hay rơi half-state (isPlaying=true nhưng
> Bill.IsReady=false, FusionNet.Runner=null) — stop/play lại là sạch, luôn check Bill.IsReady sau khi vào play.
> State sống ngắn (<5s: zone, freeze, đạn bay) HẾT ĐỜI giữa 2 lần execute_code — verify bằng recorder gắn
> EditorApplication.update ghi EditorPrefs trong 1 call rồi đọc call sau. Trước khi test đường đạn: tắt
> DummyBotDriver, set RingSpawner _slotCount=0 + despawn hết ring (ring nuốt đạn test), và làm lại mỗi lần play.
>
> Kịch bản cần verify trong 02_Arena (play thẳng, gate tự Fusion-load):
> 1. Ném/bắn TỪNG vũ khí (Đá/Súng/Bom Nhỏ/Bazooka/Bom X/Nuke/Mìn/Kiếm) — KHÔNG có phát nào nổ tại điểm bắt
>    đầu, KHÔNG để lại projectile stuck (đếm NetworkProjectile về 0 sau khi nổ). Đá/Súng KHÔNG ra cầu lửa.
> 2. Nổ chạm đất đúng chỗ; effect ExplosionFx pool bounded (nổ 20 lần → ≤8 fireball + ≤3 flash + 1 material).
> 3. Chuỗi mìn: ném → nằm → arm theo fuseDelay → người khác đạp → nổ. Không tự nổ trên không.
> 4. Kinh tế/mạng/match GDD (T30): $2/s, +$5/kill+bounty, mất mạng +$10+3s bất tử, 90s/Bo3/đổi bên, tổng
>    mạng đội khi timeout, Hòa Chung Cuộc.
> 5. Ring T27: tier matrix, trôi ngang, Băng=freeze không damage (damage giải băng), Lửa 1 mạng/lần, stack ≤3.
> 6. PPU ammo (T31): mua băng khi grab hologram, bắn hết tự nạp nếu đủ tiền, catch thưởng đạn đúng slot.
> 7. HUD T28: scoreboard live, announcer đúng event/màu, đạn nhuộm màu element khi xuyên ring.
> Bug feel VR (joystick không ném #8, grab pose) + haptic/flash CHỈ verify được trong HEADSET — note lại,
> đừng cố repro headless (VR tracking ghi đè transform). Báo cáo: bug tìm được + mức độ + file:line + cách repro.
> ```

**6 commit fix (mỗi cái verify MCP riêng) — owner test build phát hiện, session này fix:**
| Commit | Bug | Fix |
|---|---|---|
| `35be6b2` | Đạn tự nổ vào tay lúc buông + Rock ra cầu lửa | `Shooter` set trong onBeforeSpawned (trước Shooter=None vài tick đầu → proximity fuze nổ vào chính mình); tách `_explosive` (SetAoe radius≥1.0m) khỏi "có splash" — Rock 0.8/Súng 0.35 = non-explosive, `HitFirstVictim` damage+despawn không cầu lửa; explosive mới Explode; arm-gate 0.7m cho proximity. |
| `00ee5eb` | Ném ra bóng vàng generic + ball-leak khi đổi vũ khí | Default `EquippedIndex=0` (Đá thật `MS_WP_Rock`, bỏ sentinel -1); `ThrowController._showVisualHeldBall=false` (WeaponHolder cấp grabbable thật rồi). Generic HeldBall không còn resolve trong flow thường. |
| `f94cfcd` | Joystick tới lui = ném (không cần vung tay) | Đổi mốc đo vận tốc ROOT→**HEAD** (head+cổ tay đều XR-tracking, cùng timing → locomotion cancel sạch, hết residual physics-vs-tracking của root); + bắt buộc cổ tay đi tới ≥`MinSwingDistance`=0.25m relative to head mới FIRE (jiggle tạo spike nhưng không đi đủ quãng). CHỈ verify được trong headset. |
| `4e4b50b` | ExplosionFx "dơ" (CreatePrimitive+new Material mỗi nổ) | Pool 8 fireball + 3 flash + 1 material chia sẻ (MPB cho màu/alpha), root DontDestroyOnLoad. Fireball KHÔNG collider/damage — damage chạy OverlapSphere riêng. Verify: 20 nổ → 8 fireball, 1 material. |
| `4c68117` | **Nổ tại điểm bắt đầu + projectile stuck (nặng)** | Throw-snap: tick FUN đầu network-proj snap origin→twin-xa (twin chạy Update trước tick FUN), `TryGroundContact` raycast cả đoạn dài cắt sàn → nổ ở start. Fix: `_prevPosValid` bỏ qua detection tick đầu, chỉ ghi vị trí; tick sau raycast 2 vị trí thật liền kề. Verify: đoạn snap cắt sàn KHÔNG nổ; đạn nổ thật despawn sạch (netProj→0). |
| `22c0116` | (WIP owner) | Checkpoint việc editor của owner: ThrowConfig tune (gravity 3→5, velScale 3→2, maxLaunch 30→20) + 11 prefab MS_WP_* orientation/pose + scene 01_Main. **Nếu rotation 11 prefab là churn reimport ngoài ý → revert commit này.** |

**Còn lại (chưa làm — lý do):**
- **#10 visual child + muzzle/grip anchor drag-drop trên prefab** — BỊ CHẶN: phải sửa 11 prefab `MS_WP_*` mà owner đang có WIP orientation/pose ở đó. Đã commit WIP owner (`22c0116`) → giờ prefab sạch, session sau làm #10 được. Nội dung: bake child empty `Muzzle`/`GripPoint` lên prefab (thay serialized muzzle trên rig), visual thành child swap skin dễ.
- **#11 spike đạn instanced** — HOÃN (owner chốt). Note đầy đủ trong memory `spike-single-projectile-instancing`: đạn đơn đã pool NetworkPoolable, chỉ nặng khi 5v5+bắn nhanh; làm **hướng C** (hybrid: pellet đơn giản qua burst, đạn nổ phức tạp giữ NetworkObject) khi có profiling Quest thật.

**Gotcha mới:** `isPlaying=true` gửi sát compile xong → half-state IM LẶNG (Bill.IsReady=false, Runner=null vĩnh viễn, KHÔNG error). Stop/play lại là sạch — luôn check `Bill.IsReady` sau khi vào play (đã thêm vào prompt kiểm tra ở trên).

---

## Session 13 — 2026-07-04 — T27 → T30 → T31 → T26 → T28 XONG HẾT, verify từng task qua MCP

> **PROMPT CHẠY TIẾP (paste nguyên văn vào session mới):**
> ```
> Đọc Docs/HANDOFF.md rồi Docs/GDD_Core_Reference.md rồi Docs/TASKS_WEAPON_UX.md.
> Session 13 đã xong T27 → T30 → T31 → T26 → T28 (đến commit 674e021) — kế hoạch chính
> TASKS_WEAPON_UX đã XONG HẾT, chỉ còn backlog. Chạy tiếp theo thứ tự đề xuất:
> T21 (equip feedback ~30') → T29 (kiếm rút sau lưng) → scale sân theo mode (GDD §III).
> T22 icons + art Bom Chữ X + shader hologram là việc art của owner — đừng làm.
> Mỗi task: build code thật, verify qua Unity MCP (set_active_instance TOSSZONE, guard WRONG PROJECT,
> đọc console qua LogEntries), commit riêng từng task.
> QUY TẮC owner: code phải CLEAN, KHÔNG viết comment — gotchas ghi vào Docs/commit message.
> Networking đọc Fusion_Shared_Mode_Gotchas.md trước. Framework đọc BillGameCore_Usage.md.
> Đọc mục "Session 13" trong HANDOFF.md để biết gotchas mới (sim catch-up burst, ring nuốt đạn test,
> half-state runner=null im lặng).
> ```

**5 commit (mỗi task 1 commit, chi tiết đầy đủ trong message):**
| Commit | Task | Nội dung |
|---|---|---|
| `3c5ceac` | **T27** | Ring overhaul đủ 10 điểm GDD §VI: ① Shield→**Area** (rename enum + `RC_Shield`→`RC_Area` giữ GUID) · ② **giá trị theo TIER** (`valuePerTier[5]` per element + hằng `DiameterPerTier`/`DriftSpeedPerTier`) · ③ **VelocityScale áp vận tốc THẬT** (RB nhân thẳng; throw path twin re-launch — verify 6→14.4 m/s khớp scale) · ④ **stack cộng dồn ≤3 vòng/viên** (`RingsApplied` networked, cả Burst struct) · ⑤ **Băng=FREEZE** (`RPC_Freeze`+`FrozenTimer`, khóa move/ném/bắn/deflect, damage giải băng, KHÔNG damage) · ⑥ **Lửa=1 mạng/lần đi qua**, zone sống theo giây tier · ⑦ anti-dup 1×T4+1×T5 bất kể element · ⑧ **TRÔI NGANG** PingPong X theo tốc tier, deterministic · ⑨ đường kính scale theo tier (mesh 2.1m, `_prefabDiameter`) · ⑩ weight GDD 3 cửa sổ (verify 5000 roll). **+2 bug sẵn có**: ResolveConfig index OOB element 5; burst nhân đôi qua chính ring đang shrink (T7 — thêm `IsConsumed` guard). |
| `4629bcc` | **T30** | Match: 90s hiệp · nghỉ 5s · Bo3 (code+asset+scene) · **đổi bên theo parity Round** (verify z +9→-9) · mạng theo mode 7/5/4 (`MaxLives`+`LivesForPlayerCount`, bỏ const 5) · timeout so **TỔNG MẠNG ĐỘI** · round hòa không điểm · match end khi đủ wins HOẶC hết bestOf, bằng điểm = **Hòa Chung Cuộc** (-1) · `RoundEndEvent` mang WinnerTeam/ScoreA/ScoreB (sẵn cho scoreboard T28). Economy per-mạng: **+$2/s** (đo 1.97) · **+$5/mạng lấy được + bounty nạn nhân** (bỏ RewardHit $10/hit) · **shutdown bounty** +$2/mạng vào giá trị mạng mình, reset khi mất mạng · **đền bù +$10/mạng + 3s BẤT TỬ** (chặn damage hoàn toàn; dummy miễn bất tử cho training) · respawn giữa hiệp `RestoreLives()` giữ ví+vũ khí (trước reset sạch — sai GDD). |
| `67915b7` | **T31** | Roster GDD §V vào 8 asset: Đá $0/0.8m · Súng $2/0.1s/0.35m@1s · Bom Nhỏ $5@5s · Bazooka $8@10s · **Bom Chữ X $13@20s (MỚI — `WC_CrossBomb`)** · Nuke $20/3s/4.5m@45s · Kiếm $10@20s · Mìn $8@45s. **Mix per-weapon owner DUYỆT (2026-07-04, "Mix cân bằng")**: PPU = trả cost MỖI LẦN NẠP (Súng băng 10, Bom Nhỏ/Mìn băng 3, Bom X/Nuke từng quả); Bazooka/Kiếm BuyOnce; Đá free. Cơ chế PPU thật: `AmmoSlots` NetworkArray per-slot + `TryBuyAmmo`/`UseOrBuyAmmo` (tự nạp khi hết+đủ tiền), gate cả 2 đường bắn (trigger + NÉM — đường ném trước không có gate); selector mua băng khi grab hologram; catch thưởng đạn đúng slot; bỏ `costPerUse` chết. Bom X: nổ → 2 BuffZone **HỘP** xoay 45°/135° (1.1m × 5.64m = 47% sâu sân 12m, sống 3s, mất 1 mạng/lần) — BuffZone thêm box mode OBB. Model X tạm mượn grenade ×1.3 chờ art owner. |
| `83fac0c` | **T26** | **Nổ chạm ĐẤT** (raycast đoạn prev→current, mask Default) — ném hụt giờ nổ tại chỗ thay vì bay hết lifetime; `Explode()` hợp nhất mọi đường chết của đạn (damage AoE + zone + vệt X + despawn trễ 5 tick cho snapshot). **Effect nổ theo aoeRadius** (`ExplosionFx` mới, mọi client qua flag `[Networked] Exploded`): cầu lửa 2×radius + ImpactBurst + haptic 2 tay theo khoảng cách; Nuke (≥3.5m) thêm flash point-light + haptic 0.5s. **Laser sight** LineRenderer đỏ từ nòng theo raycast (Gun + Bazooka, owner-only). **isUncatchable enforce** ([Networked] `Uncatchable` stamp cả 2 đường bắn + twin) + fix InvalidOperationException đọc Element trước Spawned (guard `Object.IsValid` trong CatchController). **Chuỗi mìn**: ném → NẰM đất (kinematic, unlink twin, sống 60s) → ARM sau fuseDelay → người khác-shooter lọt 0.6m → nổ; mìn đang bay KHÔNG proximity-nổ; ThrowController không giết mìn khi bóng local land (`PersistsAfterLanding`). Verify: nổ đất trống y=-0.03 · mìn nằm→arm 1.02s→dummy đạp -3 · laser bật/tắt theo equip · catch chặn Uncatchable ✓ thường +1 ✓. |
| `674e021` | **T28** | 3 UI mới (scene object `CombatHud`, TMP tự build runtime): **ScoreboardUI** 2 mặt giữa sân (tỉ số XANH-ĐỎ + hiệp + mm:ss/SẴN SÀNG/NGHỈ-ĐỔI BÊN/KẾT THÚC) · **WristStatusHud** cổ tay trái ($ ví + đạn x/băng PPU, ∞ BuyOnce) · **AnnouncerUI** text lớn trước mặt (THẮNG/THUA/HÒA hiệp+trận theo team local, BẠN BỊ HẠ/HỒI SINH, BỊ ĐÓNG BĂNG Xs+haptic, BẮT ĐƯỢC+đạn+haptic, DEFLECT!+RewardText tại lưỡi — event mới `DeflectEvent` từ HandWeapon). Selector: slot khóa hiện **đếm ngược giây** 🔒Xs, slot PPU hiện xN đạn, grab thiếu tiền → "KHÔNG ĐỦ $" đỏ + buzz. **Đạn nhuộm màu element** (`BuffRingConfig.ElementColor`): sphere network MPB tint + trail local tint (restore khi về pool). Dọn: bỏ RoundEndEvent giả đầu hiệp (announcer sẽ hô nhầm) → `ClearLeftoverHazards()` quét mìn/zone sót đầu hiệp. Verify: scoreboard live, announcer đúng text/màu từng event, tint fire (1,0.4,0.1) ✓. |

**⚠️ QUY TẮC MỚI TỪ OWNER (2026-07-04, đã lưu memory):** code phải CLEAN, **KHÔNG viết comment** trong code — gotchas/lý do thiết kế ghi vào Docs + commit message thay vì inline. (Comment cũ ở vùng code không đụng tới thì để nguyên, đừng churn diff.)

**Gotchas MỚI học được session này (đọc trước khi verify MCP):**
1. **Editor không focus + MCP: sim catch-up theo BURST giữa các call** — giữa 2 execute_code roundtrip, Fusion tua nhiều giây sim trong 1-2 frame. State sống ngắn (zone 3s, freeze 2s, đạn bay 0.3s) HẾT ĐỜI trước khi poll bằng call tiếp theo → verify bằng **recorder gắn `EditorApplication.update`** ghi vào `EditorPrefs` trong 1 call, đọc kết quả ở call sau. Đừng tin poll 2-call cho anything <5s.
2. **Đạn test bay qua vùng giữa bị slot ring NUỐT** (OnTriggerEnter consume + Multi ring RPC despawn đạn) → trước khi test đường đạn: tắt `DummyBotDriver`, set `_slotCount=0` (reflection) + despawn hết ring. **Play session mới reset các hack runtime này — phải set lại.**
3. **RPC Fusion chạy INLINE trên client gọi** (cùng call thấy kết quả ngay) — nhưng timer TickTimer đọc chậm 1 tick.
4. Burst từng nhân qua CHÍNH ring nó vừa consume (ring sống thêm 0.25s shrink, `TryStackThroughRing` không check consumed) — đã fix T27, nhớ pattern này khi thêm consumer mới cho ring.
5. Sửa nhiều [Networked] cùng lúc (PlayerCombat/BuffZone/NetworkProjectile/Burst struct) → force-reimport đủ BỘ prefab liên quan (NetworkAvatar, DummyAvatar, BuffZone, NetworkProjectile, BuffRing, RingSpawnerHub) rồi mới Play.
6. **`isPlaying=true` gửi sát lúc compile xong → half-state IM LẶNG**: isPlaying=True nhưng Bill.IsReady=False, FusionNet/Runner=null vĩnh viễn (RuntimeInitializeOnLoadMethod không chạy lại sau domain reload dính vào play-entry). Triệu chứng: runner=null không error nào. Stop → Play lại là sạch. Luôn check `Bill.IsReady` sau khi vào play.

**Còn nợ verify (cần 2 client / headset thật — làm cùng đợt test build):** freeze gate trong headset (input headless không test được locomotion thật) · Speed buff trên đường ném local tween (cần swing thật — code path giống RB đã test) · đổi bên nhìn từ 2 máy · Hòa Chung Cuộc 1-1-1 thật · PPU cross-client · ExplosionFx/haptic cảm nhận thật + vị trí WristStatusHud/laser đọc được trong headset · Bom X vệt lửa nhìn 2 máy.

---

## Session 12 — 2026-07-03 — T19 → T18 → T25 → T20 XONG, verify từng task qua MCP

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
| Ring system theo GDD (tier matrix, trôi ngang, freeze, stack ≤3) | ✅ (T27) — đủ 10 điểm §VI, verify từng cơ chế |
| Match & economy theo GDD (90s/Bo3/đổi bên, mạng 7/5/4, $/mạng, bất tử) | ✅ (T30) — 2-client verify còn nợ (đổi bên 2 phía, hòa 1-1-1) |
| Weapon roster GDD + Bom Chữ X + mix PPU/BuyOnce | ✅ (T31) — mix owner duyệt; model Bom X chờ art owner |
| Weapon phases (nổ chạm đất, effect nổ, laser, uncatchable, chuỗi mìn) | ✅ (T26) |
| HUD/feedback (scoreboard, ví+ammo, announcer, deflect/catch/freeze, đạn nhuộm màu) | ✅ (T28) — vị trí/cảm giác tune trong headset |

Việc tiếp theo → backlog: **T21 equip feedback → T29 kiếm rút sau lưng → scale sân theo mode** (kế hoạch chính TASKS_WEAPON_UX ✅ XONG HẾT Session 13). Prompt chạy tiếp ở đầu mục Session 13 phía trên.

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

## Việc tiếp theo — cập nhật cuối Session 13

**KẾ HOẠCH CHÍNH TASKS_WEAPON_UX ✅ XONG HẾT:** T19/T18/T25/T20 (S12) + T27/T30/T31/T26/T28
(S13, commit `3c5ceac` → `674e021`, mỗi task 1 commit + verify MCP).

Backlog theo thứ tự đề xuất:
1. **T21 — equip feedback** (~30'): haptic tick + SFX + chữ nổi tên vũ khí khi equip — fire từ chỗ
   `EquipWeapon` hoặc event mới, AnnouncerUI/RewardText có sẵn để tái dùng.
2. **T29 — kiếm rút sau lưng** (ngoài GDD, owner từng muốn): đeo sau lưng, với tay ra sau RÚT.
3. **Scale sân theo mode** (GDD §III): blockout 14×12/bên vs chuẩn 1v1 6×5 — cần scale map + zone ring +
   crossZoneLength Bom X (47% sâu sân) theo mode.
4. Heckle khán đài (chết hết mạng → ném Egg/Tomato/Poop vô hại — prefab có sẵn) · T23 matchmaking API ·
   T24 host-migration · T32 lobby epic.

Việc ART của owner (đừng code hộ): T22 icons vũ khí (+icon Bom Chữ X) · model Bom Chữ X (đang mượn
grenade ×1.3) · shader hologram (`_hologramMat` trên WristSelector) · tune vị trí nút selector/hologram/
WristStatusHud trong headset. Song song: build APK test theo `T17_Test_Report.html` + danh sách
"còn nợ verify" ở mục Session 13.

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
