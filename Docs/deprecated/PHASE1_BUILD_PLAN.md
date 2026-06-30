# Phase 1 — Build Plan (Demo Vertical Slice)

> Hướng đi cụ thể để code Phase 1. Bám theo [TOSSZONE_TaskBreakdown.md](TOSSZONE_TaskBreakdown.md) (Shared Mode đã chốt) + framework [BillGameCore](../.claude/skills/billgamecore/SKILL.md).

## A. Trạng thái dự án (đã verify)

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| Photon Fusion | ✅ **Fusion 2.0.12** (`Assets/Photon/Fusion`) | Hỗ trợ Shared Mode |
| Photon App ID | ✅ **đã set** (`AppIdFusion` trong PhotonAppSettings) | Matchmaking chạy được luôn |
| BillGameCore config | ✅ `Resources/BillBootstrapConfig.asset` tồn tại | `enforceBootstrapScene=1`, nhưng `defaultGameScene` rỗng, `defaultNetworkMode=Offline`, `defaultPools=[]` |
| Resources structure | ✅ `Resources/Pools/`, `Resources/Configs/` | sẵn cho pool/config |
| AutoHand rig | ✅ `AutoHand/Examples/Scenes/XR/Prefabs/XRPlayer.prefab` + `Resources/OpenXRAutoHandMapper.prefab` | rig OpenXR chuẩn |
| Scenes | ✅ `00_Bootstrap.unity`, `01_TOSSZONE_Main.unity` (đang trống) | — |
| **Build Settings** | ❌ chỉ có `SampleScene` | **phải** đặt 00_Bootstrap = index 0 |
| **`PHOTON_FUSION` define** | ❌ chưa có (chỉ có `FUSION2`...) | BillGameCore dùng `#if PHOTON_FUSION` để bật Fusion adapter |
| **FusionNetworkAdapter** | ❌ chưa viết | stub `#if PHOTON_FUSION` trong NetworkService.cs |

→ Hạ tầng gần như đủ. Việc còn lại = wiring + content + gameplay code.

## B. Kiến trúc scene & object (đề xuất)

3 scene (build index = số prefix):
```
00_Bootstrap (0) ── BillBootstrap auto-boot + BillStartup splash ──► load 01_Menu
01_Menu      (1) ── AutoHand XRPlayer + nút "FIND MATCH" world-space + NetworkRunner
                    bấm nút → Bill.Net.Cycle.StartCycle("TOSSZONE_DEMO") → Fusion Shared
                    đủ người / master → runner.LoadScene → 02_Arena   (timeout 30s → fallback)
02_Arena     (2) ── AutoHand XRPlayer (local) + Fusion spawn NetworkPlayer avatar mỗi người
                    sàn + 2 spawn point (team A/B) + cơ chế ném
```
*(Tùy chọn rút gọn: gộp Menu vào Bootstrap còn 2 scene. Đề xuất giữ 3 scene cho sạch.)*

**Object trong mỗi scene:**
- **Local VR rig** (mỗi scene Menu+Arena): **`XRPlayer.prefab`** (AutoHand) = camera tracked + 2 Hand + AutoHandPlayer (joystick locomotion). Đây là rig điều khiển cục bộ, **không** networked.
- **Networked avatar** (`NetworkPlayer.prefab`, spawn bởi Fusion trong Arena): capsule body + 2 hand mesh + head, mỗi phần 1 NetworkTransform. Local player copy pose từ XRPlayer rig → NetworkPlayer (StateAuthority); remote render từ transform synced. + leg IK procedural + màu team.
- **Throwable visual** (`Throwable.prefab`): model nhỏ gắn ở tay, active khi grab-sau-đầu.
- **Projectile** (`Projectile.prefab`, pooled + NetworkObject): vật bay ra, đường bay bằng BillTween.

## C. Wiring Fusion vào BillGameCore (custom bootstrap cho Photon)

1. **Thêm define `PHOTON_FUSION`** (Player Settings → Scripting Define Symbols, cho Android + Standalone). → bật nhánh `#if PHOTON_FUSION` trong `NetworkService.Initialize()`.
2. **Viết `FusionNetworkAdapter : INetworkAdapter`** (`_Game/Scripts/Network/`), guard `#if PHOTON_FUSION`, wrap `NetworkRunner` (Shared Mode):
   - `CreateRoom/JoinRoom(id)` → `runner.StartGame(new StartGameArgs{ GameMode = GameMode.Shared, SessionName = id, SceneManager = ... })` (Shared: join nếu có, tạo nếu chưa).
   - `PlayerCount` → `runner.SessionInfo.PlayerCount`; `IsHost` → `runner.IsSharedModeMasterClient`; `LeaveRoom` → `runner.Shutdown()`.
   - `DontDestroyOnLoad` runner (persist qua scene). Hook `INetworkRunnerCallbacks` (OnPlayerJoined/Left, OnConnected...) → đẩy vào `Bill.Net.Cycle.SetPhase(...)` + `Bill.Events`.
3. **`BillBootstrapConfig`**: `defaultNetworkMode = FusionShared`, `defaultGameScene = "01_Menu"`.
4. Matchmaking flow chạy qua `Bill.Net.Cycle` (Connecting → InRoom → Playing), UI nghe `NetworkPhaseChangedEvent`.

## D. Cơ chế ném (M4 — core)

- AutoHand `Hand` cung cấp: pose, velocity lúc thả, `PlayHapticVibration` (haptic). **Không** dùng grab vật của AutoHand.
- Detect tay sau đầu (so local position tay vs head) **+** nút grab → haptic → active `Throwable` trong tay.
- Thả grab → đọc velocity tay → `Bill.Pool.Spawn("projectile")` (networked) → inactive throwable trong tay.
- Đường bay: `BillTween.Float(0,1,flightTime, t => pos = arc.Evaluate(t))` — parabola + 1-2 lần nảy, designer tune (KHÔNG physics). Land → VFX → `ReturnToPool`.
- Shared Mode: client ném giữ StateAuthority cho projectile của mình.

## E. Milestones (thứ tự code)

| # | Milestone | Việc chính | Ai làm | Done khi |
|---|---|---|---|---|
| **M0** | Foundation | Build Settings (00=0,01=1,02=2); define `PHOTON_FUSION`; tạo `_Game/Scripts/{Core,Network,Player,Throwing,UI}`; rename `01_TOSSZONE_Main`→`02_Arena` + tạo `01_Menu` | Claude (MCP + code) | Compile sạch, scene order đúng |
| **M1** | Network layer | `FusionNetworkAdapter`; set `defaultNetworkMode=FusionShared`; RunnerManager | Claude (code) | `Bill.Net` connect được 1 session, `PlayerCount` đúng |
| **M2** | Scene flow + matchmaking | 00_Bootstrap (Camera+Light+BillStartup→01_Menu); 01_Menu (rig + nút FIND MATCH + UI tìm trận + timeout 30s); bấm → StartCycle → vào Arena | Claude (MCP scene + code) | 2 build bấm Find Match trong 30s → cùng vào 02_Arena |
| **M3** | Player presence | `NetworkPlayer` (capsule+hands+head) sync; local copy pose; team A/B + spawn 2 bên; leg IK đơn giản | Claude (code + MCP) | A thấy B là capsule có tay/chân, di chuyển realtime |
| **M4** | Throw mechanic | gesture sau đầu + haptic + load throwable; thả → spawn projectile tween bay/nảy/nổ; sync | Claude (code) | Ném ra projectile bay cung, đối thủ thấy |

M0+M1+M2 = "2 người tìm thấy nhau và vào chung sân". M3 = thấy thân thể nhau. M4 = ném nhau.

## F. Việc BẠN cần làm (phần Claude không tự làm được)
1. **Mở Unity Editor** + đảm bảo MCP bridge chạy (hiện MCP báo "No Unity Editor instances found") → để Claude thao tác scene/build/define qua MCP.
2. **Test trên 2 thiết bị/headset** (matchmaking + ném) — Claude không lên được headset.
3. **Chốt vài quyết định ở mục G** (hoặc để Claude theo đề xuất mặc định).
4. Photon App ID: ✅ đã có sẵn, không cần làm gì.

## G. Quyết định cần chốt (đề xuất sẵn)
1. **Scene:** 3 scene (Bootstrap → Menu → Arena) [đề xuất] hay 2 scene (Bootstrap kiêm Menu → Arena)?
2. **Matchmaking demo:** session cố định `"TOSSZONE_DEMO"` (2 người tự gặp nhau, đơn giản nhất) [đề xuất] hay random matchmaking?
3. **Nút FIND MATCH:** nút vật lý world-space đấm bằng tay VR [đề xuất, đúng tinh thần VR] hay UI canvas + ray pointer?
4. **Local rig qua scene:** mỗi scene 1 XRPlayer [đề xuất, đơn giản] hay 1 rig DontDestroyOnLoad mang theo?
