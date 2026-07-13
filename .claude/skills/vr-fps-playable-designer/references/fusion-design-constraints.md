# Photon Fusion — design constraints

Mục đích: đảm bảo mọi đề xuất gameplay khả thi về mặt network. Đây là **design awareness**, không phải hướng dẫn viết networking code.

## Quy tắc nền

- KHÔNG bịa API hoặc hành vi của Fusion. Khi chi tiết phụ thuộc version: (1) kiểm tra version Fusion trong project, (2) đối chiếu tài liệu chính thức đúng version, (3) phân biệt rõ *design recommendation* với *implementation fact*, (4) nếu chưa xác minh được → ghi rõ "cần technical validation".
- Mọi đề xuất gameplay phải trả lời được: ai giữ **State Authority**, ai giữ **Input Authority**, outcome được xác nhận ở đâu.

## Checklist khi đánh giá một mechanic multiplayer

Với mỗi mechanic, rà qua:

| Khía cạnh | Câu hỏi phải trả lời |
|---|---|
| State Authority | Server/host hay client quyết định kết quả? Có chấp nhận được client-authoritative ở prototype không? |
| Input Authority | Input nào cần gửi mỗi tick? Kích thước input struct có phình không? |
| Tick-based simulation | Mechanic có phụ thuộc frame-rate local không? Logic có chạy đúng trong tick simulation không? |
| Prediction | Client có cần predict để cảm giác responsive không? Predict sai thì nhìn thấy gì? |
| Reconciliation | Khi server sửa lại kết quả, người chơi thấy snap/rollback ở đâu? Có chấp nhận được trong VR không? |
| Lag compensation | Hit registration có cần rewind không? Prototype có thể bỏ qua và chấp nhận "bắn trúng theo server" không? |
| Object lifecycle | Mechanic spawn/despawn bao nhiêu networked object? Ai có quyền spawn? |
| RPC vs replicated state | Effect là sự kiện một lần (RPC) hay trạng thái kéo dài (replicated property)? State kéo dài = chi phí bandwidth kéo dài. |
| Bandwidth | Bao nhiêu property thay đổi mỗi tick? Có thứ gì replicate được ở tần suất thấp hơn không? |
| Outcome | Kết quả deterministic hay server-authoritative? Hai client có thể thấy hai kết quả khác nhau không? |
| Topology | Host mode hay dedicated server? Prototype ưu tiên topology đơn giản nhất chạy được với 2 người. |

## VR feedback local ≠ gameplay result network-confirmed

Nguyên tắc thiết kế quan trọng nhất cho VR multiplayer: **tách feedback tức thì (local) khỏi kết quả có thẩm quyền (network)**.

- Tay cầm súng, recoil hình ảnh, muzzle flash, tiếng súng → local, chạy ngay, không đợi network.
- Damage, kill, điểm số, trạng thái round → server-authoritative, hiển thị khi được xác nhận.
- Người chơi tha thứ cho damage đến trễ vài chục ms; không tha thứ cho khẩu súng phản hồi trễ trong tay mình.

## Feature network-risk cao — nhận diện và approximation

Khi gặp các dạng feature dưới đây, mặc định cảnh báo và đề xuất approximation đơn giản hơn **nếu approximation vẫn giữ được player fantasy**:

| Feature rủi ro cao | Vì sao rủi ro | Approximation mặc định |
|---|---|---|
| Ném vật thể dựa trên motion history | Quỹ đạo phụ thuộc sampling tay local, khó reproduce trên server, dễ desync | Snap sang projectile chuẩn hoá: hướng + tốc độ cố định lấy tại thời điểm release |
| Physics object tương tác tự do | Authority transfer liên tục, hai máy mô phỏng khác nhau | Object chỉ có physics local (cosmetic), hoặc kinematic + state rời rạc |
| Full-body collision | Chi phí sync cao, hitbox mơ hồ | Capsule + head hitbox; body chỉ là visual |
| Hai người cùng tác động một rigidbody | Không có single authority hợp lý | Cấm ở prototype; nếu là fantasy cốt lõi → turn-based ownership |
| Projectile vật lý tốc độ cao | Tunneling, khác biệt tick giữa các máy | Hitscan, hoặc projectile chậm có travel time thấy được |
| Destructible environment | State bùng nổ, late-join sync khó | Static environment; destructible = LATER |
| Skill spawn nhiều network object | Object lifecycle + bandwidth | Một object mỗi lần cast, pool sẵn, TTL ngắn |
| Continuous physics authority transfer | Nguồn bug lớn nhất trong Fusion physics | Tránh hoàn toàn ở prototype |

Nếu approximation làm mất player fantasy cốt lõi → nói thẳng trade-off và để user quyết, kèm ước lượng chi phí của bản "đúng fantasy".
