# Balance & Playtest

## Balance theo hypothesis, không theo số đẹp

Không cần competitive balance hoàn hảo trước playable. Hai mục tiêu duy nhất ở giai đoạn này:

1. **Không có lựa chọn hiển nhiên vượt trội** (dominant strategy).
2. **Người chơi hiểu vì sao mình thắng/thua**.

## Quy tắc khi đề xuất bất kỳ con số nào

- Ghi rõ **assumption** đằng sau con số ("giả định engagement range trung bình 10–15m trong map indoor").
- Xác định **baseline**: chọn một weapon/ability làm mốc, mọi thứ khác định nghĩa tương đối so với mốc ("damage = 80% baseline, fire rate = 150%").
- Dùng **range thay vì số đơn** khi chưa có dữ liệu ("TTK mục tiêu 0.8–1.2s").
- Chỉ ra **balance knobs** — biến nào chỉnh được sau playtest mà không phải làm lại mechanic (damage, fire rate, mag size, cooldown, duration, radius).
- KHÔNG trình bày con số như đã được kiểm chứng. Mọi số ở giai đoạn này là hypothesis chờ playtest.

## Interaction phải phân tích

Khi đặt stat cho weapon/ability, rà tương tác giữa: **damage × fire rate × accuracy × range × mobility × cooldown → TTK thực tế**. Một buff nhỏ ở hai trục cùng lúc có thể nhân lên thành dominant.

Checklist bắt buộc:

- [ ] **Dominant strategy** — có loadout/weapon nào đúng trong mọi tình huống không? Nếu có, các lựa chọn khác chết.
- [ ] **Skill stacking** — hai ability cùng loadout (hoặc cùng team) có cộng dồn thành trạng thái không có counterplay không?
- [ ] **Hard counter** — có cặp nào mà kết quả quyết định từ lúc chọn loadout, không phụ thuộc gameplay không? Soft counter tốt, hard counter giết decision trong trận.
- [ ] **Death clarity** — người chơi chết có hiểu ngay vì sao không? Chết mà không hiểu = frustration, dù số liệu balance đẹp.

## Telemetry tối thiểu cho prototype

Đề xuất log ngay từ playable đầu tiên (đây là "instrumentation" trong định nghĩa playable):

- Kill/death theo weapon và ability.
- TTK thực tế mỗi engagement (first hit → death).
- Tỷ lệ hit/miss theo khoảng cách.
- Ability cast count + tỷ lệ cast trúng mục tiêu intent.
- Thời lượng round/match thực tế.

Kèm điều kiện điều chỉnh: "nếu weapon X chiếm >70% kill sau N trận → giảm knob Y một nấc, test lại".

## Template playtest plan

Mọi kế hoạch playtest phải điền đủ — cấm "cần playtest thêm" suông:

```
## Playtest: [tên hypothesis đang test]
- Hypothesis:        (một câu — đang muốn chứng minh/bác bỏ điều gì)
- Setup:             (build nào, map nào, loadout cố định hay tự chọn)
- Số người:          (tối thiểu bao nhiêu, vai trò gì)
- Thời lượng:        (bao nhiêu trận / bao nhiêu phút)
- Quan sát:          (hành vi cụ thể cần nhìn — không phải "xem có fun không")
- Metric:            (con số lấy từ telemetry)
- Pass:              (ngưỡng cụ thể → giữ, chuyển sang quyết định tiếp theo)
- Fail:              (ngưỡng cụ thể → sửa knob nào / cắt cái gì)
```

Ví dụ quan sát tốt: "người chơi có chủ động dùng ability trước khi giao tranh không, hay chỉ bấm khi sắp chết". Ví dụ quan sát tồi: "xem gameplay có ổn không".
