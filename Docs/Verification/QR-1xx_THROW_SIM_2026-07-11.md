# QR-1xx — Throw/Inventory single-client verification (XR Device Simulator)

- **Ngày:** 2026-07-11, Session 17.16
- **Môi trường:** Main Unity Editor, XR Device Simulator (KHÔNG Meta XR Simulator), script driver qua `EditorApplication.update` + reflection (đọc `_state/_onCooldown/_peakFwdVel/_peakArmVel` của `ThrowController`, mutate `m_RightControllerState.devicePosition` của simulator, grip = phím G qua `QueueStateEvent`).
- **Tiền đề:** QR-100 XRI PREFLIGHT đã pass (rig tracking OK).

## Kết quả

| Case | Cách test | Kết quả | Verdict |
|---|---|---|---|
| THR-02 | Swing 0.40m @2.2m/s, grip giữ | 1 fire, peak fwdVel 1.74 m/s | ✅ PASS |
| THR-03 | Flick 0.12m | 0 fire, peak 0.82 < vMinFire 1.0 | ✅ PASS |
| THR-10 | Double-swing liên tiếp | 2 fires cách 0.48s ≥ cooldown 0.35s | ✅ PASS |
| THR-09 | Grip press/release ×20 nhanh | State về Empty, 0 fire kẹt; recovery throw OK | ✅ PASS |
| THR-01 | `AutoHandPlayer.Move()` tới 0.37m + lùi, grip giữ | 0 fire, maxPeakFwdVel 0.59 < 1.0 | ✅ PASS² |
| THR-06 | Loco fwd (body 1.06m) + swing 0.40m @2.4m/s | Đúng 1 fire, không double-fire | ✅ PASS¹ |
| THR-07 | `RPC_Freeze(3)` → grip+swing; chờ thaw → grip+swing | Frozen: state=Empty, 0 fire. Thaw ~180 frames: Loaded, 1 fire | ✅ PASS |
| THR-08 | — | Visual grab pose, cần headset/owner | ⏸ chờ owner |

## Ghi chú / hạn chế phương pháp

1. **¹ THR-06 open question:** `_peakArmVel` tại thời điểm fire = **5.80 m/s world-frame** khi đang loco (đứng yên: 1.67 m/s). Gate fire dùng body-relative vel (đúng, không double-fire), nhưng nếu launch velocity lấy trực tiếp `_peakArmVel` world-frame thì bóng ném khi chạy sẽ nhanh hơn đáng kể so với cùng cú vung khi đứng yên. Cần xác nhận thiết kế: momentum cộng dồn là chủ đích hay phải trừ body vel — check `BallThrownEvent`/launch path khi lên headset.
2. **² THR-01 caveat:** joystick injection vào `m_LeftControllerState.primary2DAxis` bị simulator ghi đè mỗi frame (axis là absolute từ input action, khác devicePosition là tích lũy), và `TossLocomotionInput.Update` cũng `Move(ReadMove())` liên tục. Nên test đã tắt tạm `TossLocomotionInput` và gọi thẳng `AutoHandPlayer.Move(axis, dz, rel)` — đúng call locomotion thật dùng, chỉ bỏ qua tầng đọc thumbstick. Tầng stick→Move đã được cover riêng ở QR-100 preflight (rig input hoạt động).
3. Grip mô phỏng qua phím G của simulator; dt clamp [1/200, 1/30] khi tính bước swing.
4. Log gốc trong Editor.log: `[SIMTEST-A DONE]`, `[SIMTEST-B2 DONE]`, `[SIMTEST-C DONE]` (bỏ qua `[SIMTEST-B DONE]` — run hỏng vì joystick không ăn, và 1 zombie `[SIMTEST-B2 ERROR]` từ callback arm hụt, đã tự gỡ).

## Trạng thái TEST_CASES.md

THR-01/02/03/06/07/09/10 → đánh dấu `✅(sim)`; THR-04 ✅, THR-05 ✅(logic) từ trước; THR-08 ⚪ chờ owner. Cảm giác ném thật (hướng/lực theo cú vung tay người) vẫn cần headset xác nhận cuối cùng.
