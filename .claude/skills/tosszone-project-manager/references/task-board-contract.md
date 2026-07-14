# Task Board Contract

Contract thao tác Task Board của TOSSZONE. Tuân thủ tuyệt đối — Task Board
package trong Unity (`Assets/_Game/Scripts/Editor/TaskBoard/`) parse các file
này bằng code, sai format là hỏng board.

## Ba file, ba vai trò

| File | Vai trò | Được sửa tay? |
|---|---|---|
| `Docs/TOSSZONE_TaskBreakdown.md` | Source of truth cho backlog | ✅ Đây là nơi bạn làm việc |
| `Docs/tasks.meta.json` | Metadata verification (verify result, evidence) | ✅ Cập nhật khi verify |
| `Docs/tasks.json` | Generated snapshot cho Task Board window | ❌ TUYỆT ĐỐI không sửa tay |

`tasks.json` được sinh ra bằng menu Unity:
`Tools/TOSSZONE/Export tasks.json`. Sau mỗi thay đổi backlog đáng kể
(thêm/xóa task, đổi status hàng loạt, sửa section), nhắc chạy hoặc chạy
menu này qua Unity MCP.

Trước khi tin vào contract cụ thể của parser (format heading, cách nhận
checkbox, section), đối chiếu code thật:

- `Assets/_Game/Scripts/Editor/TaskBoard/MarkdownTaskParser.cs` — cách parse
- `Assets/_Game/Scripts/Editor/TaskBoard/TaskModels.cs` — model + status
- `Assets/_Game/Scripts/Editor/TaskBoard/TaskBoardData.cs` — cấu trúc data
- `Assets/_Game/Scripts/Editor/TaskBoard/TaskBoardWindow.cs` — hiển thị
- `Assets/_Game/Scripts/Editor/TaskBoard/TaskBoardMenu.cs` — menu export

Code là contract cuối cùng; tài liệu này chỉ là tóm tắt.

## Status markers

```
- [ ]  Todo
- [/]  In Progress
- [x]  Done
- [!]  Blocked
```

Quy tắc chuyển trạng thái:

- **Bắt đầu làm**: `[ ]` → `[/]`. Chỉ MỘT implementation task được `[/]`
  tại một thời điểm, trừ khi user yêu cầu chạy song song rõ ràng.
- **Fail hoặc thiếu quyết định bắt buộc**: → `[!]` và ghi nguyên nhân CỤ THỂ
  ngay cạnh task (thiếu gate nào, fail ở bước nào, lỗi gì). "Blocked" không
  kèm lý do là vô dụng.
- **Done**: `[x]` CHỈ khi acceptance criteria pass VÀ verify result đã được
  ghi vào `tasks.meta.json`. Code compile không phải Done. Code review xong
  không phải Done. Chưa chạy verify recipe thì tối đa là "code complete" —
  vẫn `[/]`.

## Verification và evidence

- Khi Unity khả dụng, chạy verify recipe của task qua Unity MCP (Play Mode,
  screenshot, log) và lưu evidence.
- Ghi verify result vào `tasks.meta.json`: kết quả pass/fail, evidence là gì
  (đường dẫn screenshot, log excerpt, mô tả phiên test), thời điểm.
- Unity không khả dụng → task dừng ở "code complete, chưa verify". Nói rõ
  điều đó với user thay vì đánh Done.

## Quy tắc ID theo vị trí — QUAN TRỌNG

Task ID hiện được sinh theo VỊ TRÍ trong section. Hệ quả:

- **Không reorder task cũ trong một section.** Đảo thứ tự = đổi ID = lệch
  toàn bộ `tasks.meta.json`.
- **Ưu tiên append task mới** vào cuối section thay vì chèn giữa.
- Nếu buộc phải restructure (đổi section, chèn giữa, xóa task cũ): DỪNG LẠI,
  cảnh báo user về nguy cơ lệch `tasks.meta.json`, chỉ làm khi user xác nhận
  và có kế hoạch đồng bộ lại metadata.

## Khi backlog active chưa tồn tại

Nếu `TOSSZONE_TaskBreakdown.md`, `tasks.meta.json`, `tasks.json` chưa có:

1. Báo tình trạng cho user (file nào thiếu).
2. Đề xuất initialize một backlog active mới (skeleton section + Phase 0).
3. KHÔNG tự phục hồi `Docs/deprecated/TASKBOARD.md` như thể nó còn chính
   xác. File deprecated chỉ dùng để hiểu contract cũ khi cần đối chiếu.
