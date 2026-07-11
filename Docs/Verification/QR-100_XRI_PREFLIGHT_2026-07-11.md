# QR-100 — XRI Preflight (XR Device Simulator) — 2026-07-11

**Verdict: PASS** — XR Device Simulator (XRI 3.3.1) chạy được toàn bộ vòng Play mode trên PC, không cần Meta XR Simulator, không cần Quest Link.

## Setup
- Unity 6000.3, XR Interaction Toolkit 3.3.1 (sample "XR Device Simulator").
- Auto-spawn: `Assets/_Game/Scripts/Editor/Dev/XrDeviceSimulatorAutoSpawn.cs` (Editor-only, `EditorPrefs` key `TOSSZONE.XrDeviceSimulatorAutoSpawn`).
- Play từ `00_Bootstrap` → bootstrap tự chuyển sang `01_TOSSZONE_Main` ([Bill] Ready, 14 services).

## Kết quả kiểm tra
| # | Hạng mục | Kết quả |
|---|----------|---------|
| 1 | Toggle `Tools > TOSSZONE > XR Sim: Toggle Auto-Spawn` | ✅ ON (sau khi sửa trạng thái pref — xem Issues) |
| 2 | Play từ `00_Bootstrap` | ✅ Spawn ngay frame 1 |
| 3 | Số instance | ✅ Đúng 1 — `XR Device Simulator (auto)` trong `DontDestroyOnLoad`, persist qua scene load sang `01_TOSSZONE_Main` |
| 4 | Head pose | ✅ HMD centerEye = `(0.00, 1.60, 0.00)` (standing pose); rig camera `Camera` follow đúng `(0.00, 1.60, 0.00)` |
| 5 | Left controller | ✅ Simulated state `(-0.25, 1.10, 0.30)`; `LocalPlayer/TrackerOffsets/Controller (left)` khớp chính xác |
| 6 | Right controller | ✅ Simulated state `(0.25, 1.10, 0.30)`; `Controller (right)` khớp chính xác |
| 7 | Input devices | ✅ 1× `XRSimulatedHMD` + 2× `XRSimulatedController` added vào Input System |
| 8 | Hands | ✅ `RobotHand (L)/(R)` + các Follow Offset (Classic/Robot, Oculus/OpenXR) đều bám controller anchors |
| 9 | Console | ✅ Không có lỗi XRI/simulator. Noise pre-existing (không chặn): `[MetaXRFeature] ErrorFormFactorUnavailable xrGetSystem` (kỳ vọng khi không có Quest Link), 1 missing script trên Behaviour, `No Theme Style Sheet set to PanelSettings` ×3 |

Lưu ý: project không dùng `XROrigin` (count=0) — rig custom `LocalPlayer/TrackerOffsets` + AutoHand, tracking vẫn đúng.

## Issues tìm thấy
1. **Menu toggle báo trạng thái sai (stale checkmark)** — `XrDeviceSimulatorAutoSpawn.ToggleValidate()` gọi `Menu.SetChecked` *trước khi* `Toggle()` flip giá trị, nên `Menu.GetChecked` trả về trạng thái cũ. Hệ quả thực tế: lần bật toggle đầu tiên đã **tắt** auto-spawn (default = true) trong khi checkmark hiển thị ON → lần Play đầu không spawn simulator. Workaround đã áp: set thẳng `EditorPrefs.SetBool("TOSSZONE.XrDeviceSimulatorAutoSpawn", true)`. Đề xuất fix: log giá trị hiện tại khi toggle, hoặc dùng `SettingsProvider`/`EditorPrefs`-backed checkbox thay checkmark trong validate.

## Kết luận cho QR-100
Pipeline XRI + XR Device Simulator đủ điều kiện làm nền cho preflight không-headset: spawn deterministic (1 instance, frame 1), standing pose reflection hack hoạt động trên XRI 3.3.1, rig/hands track đúng. Sẵn sàng cho các bước test tương tác (grab/throw) ở các QR tiếp theo.
