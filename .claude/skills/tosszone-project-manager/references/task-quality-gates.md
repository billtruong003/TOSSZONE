# Task Quality Gates

Tiêu chuẩn cho mọi task viết vào backlog TOSSZONE. Task không đạt chuẩn này
thì chưa được ghi vào board — quay lại làm rõ với user trước.

## Nguyên tắc chia task

- Mỗi task đủ nhỏ để hoàn thành VÀ verify độc lập. Nếu không nghĩ ra cách
  verify riêng cho nó, task đang quá to hoặc chia sai chiều.
- Chia theo **outcome chơi được hoặc kiểm chứng được**, không chia thuần theo
  tên file/class. "Sửa GunController.cs" không phải task; "Bắn AR thấy tracer
  và nghe tiếng ở local" là task.
- Task title mô tả outcome. Cấm tiêu đề mơ hồ kiểu: "Làm gun system",
  "Fix network", "Hoàn thiện gameplay", "Polish UI".

## Template task — mỗi task phải thể hiện đủ

```
- [ ] <Title mô tả outcome>
  - Outcome: kết quả quan sát được khi task xong
  - Scope: những gì nằm trong task
  - Out of scope: những gì cố tình KHÔNG làm (chống scope creep)
  - Dependencies: task/gate nào phải xong trước
  - Risk: rủi ro kỹ thuật/network/kiến trúc; mức LOW/MED/HIGH/CRITICAL
  - Acceptance criteria: điều kiện pass, đo được
  - Verify recipe: các bước kiểm chứng cụ thể (Play Mode, 2 client, log gì)
  - Evidence: cần thu gì (screenshot, log, clip, số liệu)
  - Decision/Assumption: gate hoặc giả định task đang dựa vào
```

Field nào thật sự không áp dụng thì ghi "n/a" — không bỏ trống lặng lẽ.
Task sẽ sửa symbol trong code phải ghi thêm yêu cầu GitNexus impact analysis
vào Verify recipe hoặc Dependencies.

## Thứ tự ưu tiên

Khi chọn task tiếp theo hoặc sắp backlog, ưu tiên theo thứ tự:

1. **Blocker của playable** — thứ đang chặn việc chơi thử được.
2. **Technical uncertainty có thể làm sai kiến trúc** — spike/proof để tránh
   xây trên nền sai (ví dụ: hit authority, Fusion Shared Mode behavior).
3. **Một vertical slice end-to-end** — mảnh gameplay hoàn chỉnh từ input tới
   feedback, dù xấu.
4. **Reliability và verification** — làm cho cái đã có chạy ổn và đo được.
5. **Content expansion** — thêm súng, map, ability.
6. **Polish** — đẹp, mượt, juice.

Yêu cầu ở tầng 5–6 khi core loop chưa pass: ghi nhận vào backlog phía sau
gate, nói thẳng với user là nó chưa tới lượt, không lặng lẽ nhét lên trước.

## Test nhanh trước khi ghi task vào board

- Đọc title có biết game/hệ thống thay đổi gì không?
- Một người có thể verify task này trong một phiên ngắn không?
- Nếu decision gate liên quan đổi kết quả, task này có phải viết lại không?
  Nếu có → Blocked theo gate, đừng để Todo tự do.
