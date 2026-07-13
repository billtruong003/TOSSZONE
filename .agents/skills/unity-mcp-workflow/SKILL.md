---
name: unity-mcp-workflow
description: Quy trình làm việc hiệu quả khi điều khiển Unity Editor qua Unity MCP (MCP for Unity của CoplayDev) — tạo/sửa scene, edit script, chạy test, và quan trọng nhất là preview trực quan kết quả bằng screenshot thay vì đoán mù. LUÔN kích hoạt khi người dùng nhắc tới "Unity MCP", "MCP for Unity", muốn AI thao tác trực tiếp trong Unity Editor (tạo GameObject, sửa scene, chỉnh prefab, chạy test qua MCP), hoặc khi có tool nào đó trong danh sách công cụ hiện tại rõ ràng là tool điều khiển Unity Editor. Dùng cùng expert-game-developer để đảm bảo code/scene sinh ra qua MCP vẫn theo đúng convention game dev, và unity-project-setup để biết cấu trúc project đang thao tác.
---

# Unity MCP Workflow

Unity MCP là cầu nối thật giữa AI và Unity Editor — không phải chat suông, mỗi lệnh gọi tool thật sự thay đổi scene/asset/code trong project của người dùng. Nguyên tắc quan trọng nhất: **thao tác rồi luôn kiểm chứng bằng mắt (screenshot) trước khi báo đã xong**, vì kết quả trong Editor không tự động hiện ra trong chat như code text — sai sót về vị trí, scale, hay component thiếu sẽ không ai biết nếu không nhìn lại scene.

## Trước khi bắt đầu

- **Kiểm tra kết nối trước khi thao tác**: xác nhận Unity Bridge đang chạy và đúng project/instance đang target (Unity MCP hỗ trợ nhiều instance cùng lúc — nếu người dùng có nhiều project Unity mở, xác nhận đang nhắm đúng cái nào trước khi sửa, tránh sửa nhầm project).
- **Nếu Unity MCP chưa được kết nối** trong phiên làm việc hiện tại, không tự ý bịa tên tool hay đoán schema — đề xuất kết nối qua luồng connector chuẩn thay vì giả định tool đã sẵn sàng.
- **Đọc trạng thái scene hiện tại trước khi sửa** (liệt kê GameObject, component đang có) thay vì giả định cấu trúc dựa trên trí nhớ — scene có thể đã được người dùng chỉnh tay sau lần cuối AI thao tác.

## Nhóm tool — chỉ kích hoạt nhóm cần dùng

Unity MCP chia tool thành nhiều nhóm theo domain (scene, script, asset, prefab, material, animation, vfx, ui, physics, testing, profiler...). Kích hoạt đúng nhóm cần cho task hiện tại thay vì bật tất cả — vừa giữ prompt gọn, vừa giảm khả năng AI chọn nhầm tool gần giống nhau giữa các domain không liên quan.

Ví dụ phân loại task → nhóm tool:
- "Tạo player controller di chuyển WASD" → nhóm **script** + **scene** (tạo GameObject, gắn component, edit script).
- "Chỉnh hiệu ứng nổ cho đẹp hơn" → nhóm **vfx**.
- "Kiểm tra frame rate có ổn không" → nhóm **profiler**.
- "Setup animation nhân vật" → nhóm **animation**.
- "Chạy test xem code có lỗi không" → nhóm **testing**.

## Quy trình chuẩn: Sửa → Preview → Xác nhận

Không bao giờ báo "đã xong" chỉ dựa trên việc gọi tool không báo lỗi — lệnh chạy thành công không có nghĩa kết quả nhìn đúng như mong đợi (sai vị trí, sai scale, material chưa gán, ánh sáng chưa đúng...). Quy trình bắt buộc:

1. **Thực hiện thay đổi** (tạo/sửa GameObject, component, script, prefab, material).
2. **Chụp screenshot scene** ngay sau khi thay đổi để xem kết quả thật trong Editor.
3. **Đối chiếu với yêu cầu ban đầu** — nếu lệch, sửa tiếp và lặp lại bước 2 thay vì để người dùng tự phát hiện.
4. Chỉ báo hoàn thành với người dùng sau khi đã tự xác nhận qua screenshot, kèm mô tả ngắn gọn những gì thấy trong ảnh (không chỉ liệt lại lệnh đã gọi).

Với thay đổi script thuần túy (logic không ảnh hưởng hình ảnh, ví dụ sửa công thức tính damage), bước screenshot có thể thay bằng chạy test (nhóm **testing**) hoặc kiểm tra compile không lỗi — chọn cách xác nhận phù hợp với loại thay đổi, không máy móc screenshot mọi trường hợp.

## Sửa code qua MCP — vẫn theo convention của project

Khi dùng tool script-editing (bao gồm cả runtime code execution qua Roslyn nếu có), code sinh ra vẫn phải theo đúng chuẩn ở skill `expert-game-developer` (MonoBehaviour discipline, composition, tránh allocation trong Update...) và convention đặt tên ở `unity-project-setup`. Tool MCP chỉ là cách thực thi thay đổi — chất lượng code kỳ vọng không thấp hơn khi viết tay.

## Hiệu quả gọi tool

- **Gộp thao tác liên quan trong ít lần gọi nhất có thể** thay vì gọi tool lặt vặt từng bước nhỏ (ví dụ: tạo GameObject + gắn component + set giá trị ban đầu nên đi liền mạch, không tách thành nhiều turn hỏi lại người dùng giữa chừng nếu yêu cầu đã đủ rõ).
- **Không lặp lại thao tác đọc trạng thái đã biết** trong cùng phiên nếu chưa có gì thay đổi kể từ lần đọc trước — đọc lại chỉ khi vừa tự thực hiện thay đổi hoặc nghi ngờ trạng thái đã cũ.
- Với task lớn (dựng cả một scene nhiều object, setup cả hệ thống), chia nhỏ thành các bước có preview trung gian thay vì cố làm hết một lượt rồi mới kiểm tra — phát hiện sai sớm rẻ hơn nhiều so với phát hiện ở cuối.

## Giới hạn cần nói rõ với người dùng

Unity MCP thao tác trực tiếp trên project thật — không có "undo toàn cục" đảm bảo như Ctrl+Z thủ công trong mọi trường hợp (một số thay đổi asset/script có thể không nằm trong Undo stack của Editor). Với thao tác có rủi ro cao (xóa asset, ghi đè file lớn, thay đổi ProjectSettings), xác nhận với người dùng trước khi thực hiện thay vì tự ý làm rồi báo sau.
