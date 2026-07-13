# Gunplay VR & Ability design

## Nguyên tắc chung

Mỗi recommendation về gunplay/ability phải trả lời đủ 5 câu:

1. **Player decision** mà mechanic tạo ra là gì? (không tạo decision → nghi ngờ giá trị)
2. Feature có cần cho **playable đầu tiên** không?
3. Chi phí implementation/network là gì?
4. **Phiên bản đơn giản nhất** có thể test là gì?
5. Tiêu chí để **giữ / sửa / cắt** sau playtest là gì?

Không mặc định "realistic hơn = fun hơn". Realism là một knob, không phải mục tiêu.

## Các trục quyết định gunplay VR

Rà từng trục, chọn phương án đơn giản nhất còn kiểm chứng được hypothesis:

- **Hitscan vs projectile** — hitscan rẻ hơn nhiều về network (một raycast server-side, không object lifecycle). Projectile chỉ đáng khi travel time là player decision (né, dẫn tâm). Mặc định prototype: hitscan.
- **Aiming** — VR physical aiming (giơ tay ngắm thật) là điểm mạnh bẩm sinh của VR, chi phí gần bằng 0. ADS/scope là NEXT trở đi. Không thiết kế accuracy cone phức tạp khi tay người chơi đã là accuracy cone tự nhiên.
- **One-hand vs two-hand** — two-hand tăng độ ổn định ngắm nhưng khoá một tay khỏi mọi việc khác (skill, reload, grab). Xung đột input này phải được chỉ ra khi concept có yếu tố "mỗi tay một chức năng".
- **Recoil** — recoil vật lý ép tay người chơi là rủi ro VR comfort. Ưu tiên: visual recoil (súng nảy, tâm không đổi) hoặc accuracy bloom. Không bao giờ giật camera trong VR.
- **Reload** — full manual (eject mag, đút mag, kéo khoá nòng) là fantasy mạnh nhưng nhiều edge case + state. Simplified manual (một gesture) hoặc auto-reload là mặc định prototype. Manual reload là thứ dễ DEFER nhất mà ít ảnh hưởng core hypothesis.
- **Ammo economy** — prototype chỉ cần mag size + reload gap để tạo nhịp. Ammo tổng/scavenging là LATER.
- **Damage model** — bắt đầu với body damage phẳng + headshot multiplier. Limb damage là LATER.
- **TTK** — quyết định quan trọng nhất của gunplay. TTK ngắn thưởng ai bắn trước (giống CS), TTK dài thưởng tracking + tạo cửa cho ability/teamplay (giống hero shooter). Chọn một hypothesis, ghi rõ, test.
- **Movement accuracy** — phạt accuracy khi di chuyển tạo decision đứng-bắn vs chạy; nhưng trong VR người chơi ít strafe kiểu mouse. Mặc định prototype: không phạt, quan sát playtest trước.
- **Hit confirmation** — bắt buộc có từ playable đầu tiên: hitmarker + âm thanh + damage direction. Không có hit feedback thì không đánh giá được gì từ playtest.
- **Death & respawn** — chọn một: respawn nhanh (đo combat loop được nhiều lần hơn) hoặc round reset (đo tension). Playable đầu tiên chỉ cần một.
- **Network fairness** — ai thấy gì khi lag: xem `fusion-design-constraints.md`.
- **VR comfort** — mọi thứ ép chuyển động camera ngoài ý muốn người chơi là CUT theo mặc định.

## Ability Contract — template bắt buộc

Mỗi skill/ability phải được mô tả bằng contract này trước khi bàn tiếp. Ability chỉ có fantasy mà không điền được contract = NEEDS EVIDENCE.

```
## [Tên ability]
- Player intent:            (người chơi muốn đạt gì khi bấm)
- Input:                    (nút/gesture nào, tay nào)
- Targeting rule:           (self / aim / area / auto)
- Activation condition:     (khi nào được cast, khi nào bị chặn)
- Immediate feedback:       (local, chạy ngay không đợi network)
- Authoritative outcome:    (server xác nhận cái gì)
- Duration:                 (nếu có state kéo dài — càng ngắn càng tốt)
- Cooldown / cost:
- Counterplay:              (đối thủ đọc và trả lời bằng cách nào)
- Failure cases:            (cast trượt thì thấy gì, mất cooldown không)
- Networked state:          (property nào replicate, object nào spawn)
- Edge cases quan trọng:
- Simplest implementation:
- Playtest metric:
- Kill/cut criterion:       (kết quả nào thì cắt ability này)
```

## Tiêu chí ưu tiên khi chọn ability

Ưu tiên ability có: **một input, một effect chính, targeting rõ, feedback tức thì, ít network object, ít state kéo dài, ít physics, counterplay dễ đọc, balance knob rõ ràng**.

Cảnh báo hoặc reject ability có: nhiều điều kiện kích hoạt, nhiều phase, nhiều object vật lý, hoặc tạo ngoại lệ riêng cho luật chung của game (ability "xuyên qua luật" là nguồn bug + confusion lớn nhất).

## Số lượng ability cho vertical slice

KHÔNG thiết kế roster lớn ngay. Vertical slice đầu tiên chỉ cần số ability nhỏ nhất đủ kiểm chứng ba câu hỏi:

1. Việc chọn loadout có tạo **decision** không?
2. **Phối hợp đồng đội** có xuất hiện không?
3. Ability có **bổ trợ** gunplay thay vì thay thế gunplay không?

Ba câu này thường kiểm chứng được với 3–4 ability thuộc các vai khác nhau (ví dụ: một info/vision, một zone/denial, một mobility hoặc sustain). Thêm ability thứ 5+ trước khi ba câu trên có câu trả lời = tăng scope không có bằng chứng.
