# M0 — Activation & Foundation (Design — for review before execution)

> Mục tiêu M0: **bật module Fusion + dựng nền tảng scene/build**, rồi **verify compile + smoke test**. Chưa viết gameplay (đó là M1+). Theo working style: doc này để bạn duyệt trước; mình chỉ execute qua MCP sau khi bạn OK.

## ✅ M0 COMPLETE — đã execute + verify qua MCP (2026-06-22)
- **Define:** thêm `PHOTON_FUSION` (Android) → append vào defines Fusion sẵn có (không overwrite). Recompile + domain reload OK.
- **Module Fusion compiled:** reflection xác nhận `FusionNet`, `FusionNetworkAdapter`, `FusionConnectArgs` tồn tại trong `Assembly-CSharp`; `implementsINetworkRunnerCallbacks=True` (đúng 19 callback Fusion 2.0.12) → **0 fix lúc compile** (nhờ pre-verify với `Fusion.Runtime.xml`). Console: **0 compile error**.
- **Build Settings:** `0=00_Bootstrap · 1=01_TOSSZONE_Main · 2=02_Arena` (tạo mới 02_Arena).
- **BillBootstrapConfig:** `defaultNetworkMode=FusionShared`, `defaultGameScene="01_TOSSZONE_Main"`.
- **Smoke test (Play→Stop):** `[Bill] Ready. 14 services in 614ms.` → tự load `01_TOSSZONE_Main`. `Bill.Net.Mode=FusionShared`, `adapter=FusionNetworkAdapter`, `FusionNet.Exists=True`, `IsConnected=False`/`CyclePhase=Disconnected` (idle, **không auto-connect**). 0 error (chỉ warning vô hại: UIService theme, engine RuntimeInitializeOnLoad).

→ Nền tảng M0 sẵn sàng. Tiếp theo: **M1 (network layer gameplay) / M2 (scene flow Main→Arena qua vòng/portal)**.

## Trạng thái hiện tại (verify qua MCP)
- Active scene `00_Bootstrap` → **buildIndex = -1** (chưa nằm trong Build Settings), **rootCount = 0** (scene trống).
- Console: **không có compile error** (chỉ 1 warning vô hại của package + log MCP).
- Fusion module đã viết (`BillGameCore/Runtime/Network/Fusion/`), guard `#if PHOTON_FUSION` → hiện **chưa active**.
- `BillBootstrapConfig`: `enforceBootstrapScene=1`, `defaultGameScene=""`, `defaultNetworkMode=Offline`.

## Quyết định scene (CHỐT — 3 scene)
- **00_Bootstrap** = build index **0** (boot + splash).
- **01_TOSSZONE_Main** = build index **1** = **Main / Sảnh / Social Hub** (người chơi spawn, đi dạo, bấm nút). `defaultGameScene` = scene này.
- **02_Arena** = build index **2** = combat (tạo mới, M0 chỉ dựng tối thiểu Camera+Light).
- Flow: boot → splash → load **01_TOSSZONE_Main** (sảnh) → người chơi đi qua **vòng/portal** → switch sang **02_Arena**. Cơ chế vòng/portal + connect Fusion = **M2** (không thuộc M0).

## Các bước M0 (mỗi step: làm gì · cách verify)

### Step 1 — Build Settings
- **Làm:** thêm `Assets/_Game/Scenes/00_Bootstrap.unity` (index 0) + `Assets/_Game/Scenes/01_TOSSZONE_Main.unity` (index 1). Bỏ `SampleScene` khỏi index 0.
- **Use case:** BillGameCore ép Scene 0 = bootstrap; nếu sai, boot lỗi (`enforceBootstrapScene`).
- **Edge case:** đảm bảo đúng GUID/path; 00_Bootstrap thật sự ở index 0 (không chỉ enabled).
- **Verify:** `manage_scene get_build_settings` → list đúng thứ tự [00_Bootstrap, 01_TOSSZONE_Main].

### Step 2 — Thêm scripting define `PHOTON_FUSION`
- **Làm:** thêm `PHOTON_FUSION` vào Scripting Define Symbols cho **active build target** (hiện Android — Quest) **và Standalone** (để test trong editor PC nếu cần). Giữ nguyên các define Fusion sẵn có (`FUSION2`...).
- **Use case:** bật nhánh `#if PHOTON_FUSION` trong `NetworkService` + compile module Fusion (`FusionNet`/`FusionNetworkAdapter`/`FusionEvents`).
- **Edge case:** define theo từng platform — phải set cho platform đang active trong editor, nếu không module vẫn off. Thêm define → **domain reload** (đợi compile).
- **Verify:** sau reload, `read_console` không có error; `editor_state.isCompiling=false`.

### Step 3 — Compile-verify module Fusion (verify với Fusion docs)
- **Làm:** đọc console sau khi define bật.
- **3 điểm đã verify trước với `Fusion.Runtime.xml`** (giờ là confirm cuối qua compiler thật):
  - `StartGameArgs.PlayerCount` (max players) — đã thấy ở dòng 16183.
  - `Tick → int` implicit — đã thấy `Tick.op_Implicit(Tick)~Int32` dòng 9850.
  - `LoadScene(SceneRef, LoadSceneMode.Single)` — đã fix ambiguity 2 overload (14375/14392).
- **Edge case:** nếu compiler báo lệch (vd `SetPlayerObject`/`GetPlayerRtt` return type, `Shutdown` overload) → fix ngay theo XML rồi re-check.
- **Verify:** `read_console types=[error]` = rỗng (chỉ warning vô hại).

### Step 4 — Cấu hình BillBootstrapConfig
- **Làm:** set `defaultNetworkMode = FusionShared` (=2) + `defaultGameScene = "01_TOSSZONE_Main"`.
- **Use case:** boot → `NetworkService` dùng `FusionNetworkAdapter` (tạo `FusionNet` **idle**, KHÔNG auto-connect → không đụng App ID); sau splash tự load Arena.
- **Edge case:** FusionShared mode chỉ tạo adapter, connect là chủ động (M2). Không auto vào room ở M0.
- **Verify:** đọc lại asset, field đúng.

### Step 5 — Smoke test (an toàn, không cần headset/2 máy)
- **Làm:** vào Play Mode trong editor (`manage_editor play`), đọc console, rồi `stop`.
- **Verify:**
  - Có log `[Bill] Ready. N services …` (bootstrap chạy).
  - `Bill.Net` resolve = FusionNetworkAdapter (không phải Offline) — có thể log/kiểm qua console.
  - **Không** có error; **không** auto-connect (vì chưa gọi StartShared).
  - Splash → load `01_TOSSZONE_Main` (nếu BillStartup chưa setup trong scene thì chỉ cần boot sạch; setup splash UI là M2).
- **Edge case:** nếu `enforceBootstrapScene` nhảy scene khi Play từ Arena — đúng behavior (returnToEditSceneInEditor=1 sẽ quay lại).

## KHÔNG đụng ở M0 (để M1/M2)
- Không viết `NetworkPlayer`, projectile, throw, matchmaking logic.
- Không dựng UI/nút FIND MATCH, chưa connect Fusion thật.
- Không thêm gameplay GameObject vào scene (trừ Camera/Light tối thiểu nếu cần cho smoke test — sẽ hỏi).

## Quyết định (ĐÃ CHỐT)
1. ✅ Define `PHOTON_FUSION` → **chỉ Android** (active target).
2. ✅ **Cho phép Claude thao tác trực tiếp** qua MCP (gồm Play Mode smoke test).
3. ✅ Scene: `01_TOSSZONE_Main` = **Main/Sảnh** (giữ tên); thêm `02_Arena` riêng cho combat.
