# Project Decision Gates & Default Milestone

Các quyết định CHƯA CHỐT của TOSSZONE. Vai trò của PM: giữ gate mở, block
task phụ thuộc, trình lựa chọn cho user — KHÔNG tự quyết thay user. Khi một
gate được chốt, ghi decision vào backlog/meta và unblock task liên quan.

## Gate đang mở

### 1. HIT-AUTHORITY

Kiến trúc hitscan: ownership của hit result nằm ở đâu (shooter / victim /
state authority), sanity validation mức nào, anti-cheat tới đâu.

- User sẽ bàn riêng. **Không khóa kiến trúc network hit trước quyết định
  này.** Task nào ép cứng một mô hình authority → Blocked theo gate.

### 2. HEALTH-MODEL

Xung đột tài liệu đã biết:

- GDD yêu cầu 100 HP.
- Code hiện tại dùng `Health` như **lives**.
- Tech Review yêu cầu chuyển sang HP; Gun Architecture nói giữ nguyên.

Task damage, death, UI health và economy đều phụ thuộc gate này. Trước khi
chốt: các task đó Blocked hoặc viết trung lập với cả hai mô hình (hiếm khi
khả thi — thường là Blocked).

### 3. TWO-HAND-V1

Xung đột: GDD + Tech Review yêu cầu foregrip/two-hand; Gun Architecture loại
khỏi v1.

**Recommendation mặc định (KHÔNG phải quyết định đã duyệt):**

- Phase 0 dùng một tay để chứng minh fire loop.
- Foregrip là gate phải qua TRƯỚC khi so sánh AR/SMG chính thức.

Trình bày đúng như recommendation; user duyệt thì mới thành decision.

### 4. SHOP-INTERACTION

Pointer shop (GDD) đối lập wrist shop (Tech Review).

- Không đưa shop vào Phase 0 dưới bất kỳ hình thức nào.
- Chỉ đưa gate này ra quyết sau khi combat loop pass.

### 5. CHEAT-SCOPE

Threat model và mức enforcement chưa chốt. User sẽ bàn riêng.

- Không tự mở rộng bất kỳ task nào thành anti-cheat production.
- Sanity check tối thiểu phục vụ debug thì được, ghi rõ là debug aid.

## Milestone mặc định — Phase 0: Network Gun Proof

Khi chưa có chỉ đạo khác, đề xuất milestone này. Mục tiêu: chứng minh core
hypothesis "bắn nhau qua mạng thấy sướng và đúng" với scope nhỏ nhất.

Trong scope:

- Hai người vào cùng arena.
- Một khẩu AR hitscan.
- Local muzzle flash / sound / haptic.
- Remote thấy súng và tracer.
- Damage → death → respawn.
- Hit feedback rõ (hitmarker/âm thanh).
- Telemetry tối thiểu (đủ để biết pass/fail).

NGOÀI scope Phase 0 (từ chối thẳng, xếp sau gate):

- Ability, shop, jump, economy, sniper, Heavy, two-hand.

**Điều kiện mở Phase 1:** Phase 0 có pass/fail evidence thật (phiên test 2
client, evidence ghi trong tasks.meta.json). Không mở Phase 1 bằng cảm giác.
