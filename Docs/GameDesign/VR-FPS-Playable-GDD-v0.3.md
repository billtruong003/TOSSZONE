# TOSSZONE — GDD v0.3 (bản chính thức hiện hành)

> **Trạng thái:** DESIGN v0.3 — viết 2026-07-13. Thay thế toàn bộ v0.1/v0.2 (đã xóa).
> Bản HTML kèm sơ đồ: xem artifact "v0.2 dễ đọc + sơ đồ flow" (đã cập nhật lên v0.3).
> Pivot 100% khỏi game ném bóng: chỉ giữ **asset, IK, network/avatar foundation**
> (`Docs/Network_Architecture_Lessons.md`, `Docs/Fusion_Shared_Mode_Gotchas.md` vẫn dùng).
> **Stack (fact):** Unity 6000.3 · URP · Quest/Android · Fusion 2.0.12 Shared Mode · không có Physics
> Addon · BillGameCore · AutoHand · hệ input mới của Unity.
> Mọi con số là **giả thuyết chờ chơi thử**, mọi chỗ ghi **CẦN KIỂM CHỨNG KỸ THUẬT** là ý định
> thiết kế, chưa phải điều đã xác nhận chạy đúng trên Fusion.
> **Bước kế tiếp của tài liệu này: technical review** — chuyển mục 17 cho lập trình viên soi.

---

## 1. Kết luận và những gì đổi so với v0.2

Giữ toàn bộ khung v0.2 (điều khiển, kỹ năng, kinh tế, ba chế độ, hành trình người chơi). Bản v0.3 thay đổi bốn nhóm việc theo duyệt của owner:

1. **Dàn vũ khí mở rộng từ 4 súng thành 11 súng + dao**, chia theo cấu trúc mới sạch hơn: **súng phụ (1 tay) = 3 khẩu lục + dao · súng chính (2 tay) = tất cả còn lại** (2 SMG, 2 AR, 2 Heavy, 2 Sniper). Luật một tay/hai tay giờ trùng luôn với ô phụ/ô chính — hết ngoại lệ.
2. **Định nghĩa lại "giật súng" cho VR:** giật = nòng súng nảy lên (chỉ hình ảnh) cộng độ tản đạn tăng dần khi giữ cò, nhả cò thì hồi. Không bao giờ đẩy camera, không đẩy tay người chơi. Cảm giác chơi giống Valorant (bắn dài thì tỏa, phải bắn nhịp), khác cách thể hiện.
3. **Bản đồ được viết rõ luật kết nối:** một sân liền, không phải hai sân tách biệt; luật phòng hồi sinh, luật chống camp, luật số đường đi tối thiểu — chi tiết mục 11.
4. **Bổ sung luật cho các tình huống lạ** chưa rõ ở v0.2 — mục 12.

Súng nhắm (sniper) được duyệt kèm **bốn điều kiện cân bằng bắt buộc** (mục 6.5) và chỉ vào game ở lát cắt có chế độ ngắm (ADS) cộng bản đồ nới thêm trục dài 20m.

## 2. Giả thuyết đang muốn kiểm chứng

Nếu người chơi VR lúc nào cũng có súng trong tay, chọn từ một dàn vũ khí mà mỗi class có một cặp lựa chọn thật (chuẩn-ít giật đối đầu mạnh-giật nhiều), cộng hai kỹ năng trên hai nút mặt, thì 2 đến 4 người sẽ tạo ra những trận đấu súng dễ hiểu, chơi lại được, và việc chọn súng trở thành quyết định có ý nghĩa. Khi cùng bộ điều khiển đó chạy qua ba chế độ nối tiếp (học súng → luyện → đấu có tiền), người chơi tự đi hết chuỗi mà không cần màn hướng dẫn riêng.

Chưa test: tiến trình dài hạn, đồ trang trí, xếp hạng, độ sâu nội dung.

## 3. Bối cảnh câu chuyện (giữ ngắn, phục vụ luật chơi)

TOSSZONE là giải đấu thể thao ảo tương lai gần dùng công nghệ ánh sáng cứng (hardlight — ánh sáng chạm được, cứng như vật thật). Đấu sĩ ("Runner") không cầm vũ khí thật — bộ giáp thi đấu chiếu trang bị thẳng lên tay. Vì thế: súng dính chặt vào tay, không rơi, không nhặt, không cướp được. Rào Chắn cùng công nghệ nên nhìn nhất quán. Chết = giáp mất nguồn, tan thành hạt sáng, hồi sinh ở buồng tái tạo. Tiền trong chế độ kinh tế = năng lượng chiếu do ban tổ chức phát theo thành tích từng hiệp.

Lore dừng ở đây. Mở rộng sau này phải gắn với một luật chơi cụ thể.

## 4. Nguyên tắc nền tảng

1. Súng là trung tâm — kỹ năng không bao giờ mạnh hơn việc bắn.
2. Một luật cho mọi tay, mọi súng, mọi chế độ.
3. Thoải mái VR có cầu dao ngắt: không chuyển động camera ép buộc; nhảy là ngoại lệ duy nhất được thử — một ca say VR quy cho nhảy trong buổi chơi thử là tắt nhảy ngay, không bàn cãi.
4. Cảm giác chạy ngay trên máy mình; kết quả thật (sát thương, hạ gục, điểm, tiền) xác nhận qua mạng.
5. Ba chế độ, một lõi: chế độ chỉ được khác nhau ở luật chơi, cấm đòi cơ chế riêng.
6. Lát cắt trước phải pass rồi mới làm lát cắt sau.

## 5. Điều khiển và tay thuận

Người chơi chọn tay thuận trong cài đặt (mặc định phải, đổi được). Súng xuất hiện thẳng vào tay thuận và khóa cứng — không bao giờ rời tay. Tay còn lại là tay tự do: di chuyển, nắm báng trước của súng hai tay, tương tác. Thuận trái thì mọi nút đảo gương hoàn toàn.

| Thao tác | Tay súng (phải) | Tay tự do (trái) |
|---|---|---|
| Cò | Bắn | (để dành) |
| Nắm tay | Giữ súng tự động | Nắm báng trước súng 2 tay / tương tác |
| Cần ngang | Xoay người nhảy góc | Di chuyển ngang |
| Cần dọc | Gạt = đổi vũ khí (vòng 3 ô) | Tiến/lùi |
| Ấn cần | Nạp đạn | (để dành — candidate: shop) |
| Nút dưới (A) | Nhảy | — |
| Nút trên (B) | Kỹ năng 1 | — |
| Nút dưới (X) | — | Khu hồi sinh: shop · trong trận: menu (sau) |
| Nút trên (Y) | — | Kỹ năng 2 |

Luật nhớ: **nút trên = kỹ năng · nút dưới tay súng = nhảy · nút dưới tay kia = shop.**

**Rủi ro bấm nhầm** (cần tay súng gánh xoay/đổi súng/nạp đạn): chia vùng theo góc gạt — gạt dọc trên 80% biên độ mới là đổi súng, gạt ngang là xoay, ấn cần chỉ nhận khi cần gần vị trí giữa. Chơi thử đếm số lần bấm nhầm trong cửa sổ 0.3 giây sau thao tác trước; quá 2 lần/người/trận thì chuyển nạp đạn sang cử chỉ chạm tay tự do vào súng. Lưới an toàn: hết đạn tự nạp.

**Nhảy:** đỉnh ~0.8m, lơ lửng ~0.5 giây, điều khiển hướng tối thiểu trên không, không nhảy đúp, không leo trèo, làm mờ viền màn hình khi rời đất. Cầu dao ngắt ở mục 4.

> **CẦN KIỂM CHỨNG KỸ THUẬT:** (a) tay tự do nắm báng trước không xung đột AutoHand; (b) chia vùng gạt trên hệ input mới; (c) nhảy với ground-check của AutoHandPlayer; (d) khóa grab vĩnh viễn không cho buông.

## 6. Hệ vũ khí

### 6.1 Luật nền

- Không bao giờ tay trống. Không thả, không nhặt, không trao đổi; súng người chết không rơi ra.
- **Ba ô, gạt cần dọc để xoay vòng: Súng chính (2 tay) → Súng phụ (lục, 1 tay) → Dao → quay lại.** Đổi mất ~0.5 giây, lúc đổi không bắn được. Đang nắm báng trước mà gạt đổi súng thì tay tự do tự nhả.
- **Tốc độ chạy phụ thuộc vũ khí đang cầm trên tay** (giả thuyết): Dao/Lục 100% · SMG 95% · AR 90% · Heavy 75% · Sniper 75% (riêng Sniper Hủy Diệt 70%). Cầm Heavy muốn chạy nhanh thì phải gạt sang lục hoặc dao — đúng ý đồ: hỏa lực đổi bằng cơ động.
- Nạp đạn: ấn cần (chủ động) hoặc tự động khi hết; thanh tiến trình hardlight trên thân súng; đổi vũ khí giữa chừng thì hủy nạp.
- **Giật súng (định nghĩa VR):** nòng nảy lên (chỉ hình ảnh) + độ tản tăng dần khi giữ cò, nhả cò thì hồi dần. Ba nút chỉnh mỗi khẩu: tản nền, tốc độ nở, tốc độ hồi. Cộng thêm phạt tản khi đang di chuyển và khi bắn súng hai tay bằng một tay.
- Ngắm: chĩa nòng theo tay. **ADS (chế độ ngắm) vào ở lát cắt 3 cùng sniper** — dạng ngắm (ống kính render hay khe ngắm sắt vật lý) do technical review quyết, vì ống kính render tốn hiệu năng đáng kể trên Quest.

### 6.2 Cấu trúc dàn súng

Mỗi class có một **cặp lựa chọn**: một khẩu chuẩn-ít giật, một khẩu mạnh-giật nhiều (công thức Phantom/Vandal). Người chơi có 100 máu, trúng đầu nhân đôi sát thương, chưa có giáp.

### 6.3 Súng phụ — 1 tay (ô 2) và dao (ô 3)

| Khẩu | Dmg thân | Nhịp bắn | Băng | Nạp | Tính cách | Giá |
|---|---|---|---|---|---|---|
| **Lục Chuẩn** (free, mặc định) | 25 | Từng phát, ~300 RPM | 12 | 1.2s | Chuẩn, dễ dùng, dmg trung bình | 0 |
| **Lục Nặng** | 50 (100 đầu — 1 phát trúng đầu là bay màu) | Chậm ~150 RPM | 6 | 1.6s | Dmg mạnh, giật mạnh, thưởng tay chuẩn | 700 |
| **Lục Burst** | 11 × 3 viên/loạt | Loạt 3 viên, nhịp nhanh gần SMG | 21 (7 loạt) | 2.2s | Trút nhanh, giật tương đối, băng nhỏ nạp chậm | 500 |
| **Dao** (ô 3, luôn có) | 60 cố định | Chờ 0.7s giữa 2 nhát | — | — | 2 nhát hạ gục; chạy nhanh nhất | 0 |

"Đạn giới hạn" của Lục Burst thể hiện bằng băng nhỏ + nạp chậm (game không có hệ đạn dự trữ — đã cắt).

Dao: **không** tính sát thương theo tốc độ vung tay thật (dễ bị lợi dụng bằng rung tay, khó kiểm chứng qua mạng) — chém trúng là 60, có thời gian chờ.

### 6.4 Súng chính — 2 tay (ô 1)

| Khẩu | Dmg thân | Nhịp bắn | Băng | Nạp | Tính cách | Giá |
|---|---|---|---|---|---|---|
| **SMG Nhanh** | 12 (→8 ngoài 12m) | 850 RPM full-auto | 30 | 1.6s | Áp sát, trút mưa, tản nở nhanh khi giữ cò | 900 |
| **SMG Burst** | 14 × 3 viên/loạt | Loạt 3 viên nhịp nhanh | 24 (8 loạt) | 1.9s | Chuẩn hơn ở 10–15m, thưởng bắn nhịp | 1100 |
| **AR Chuẩn** | 16 | 600 RPM | 30 | 1.8s | Ít giật, giảm dmg nhẹ ngoài 20m, dễ điều khiển | 1900 |
| **AR Mạnh** | 20 (40 đầu) | 500 RPM | 25 | 1.9s | Không giảm dmg theo tầm, giật mạnh — ghim được thì hạ nhanh hơn | 1900 |
| **Heavy Nòng Xoay** | 8 | 900 RPM, khởi động 0.5s (spin-up — nòng quay lấy đà) | 100 | 4.0s | Trải hỏa lực, siêu giật, giữ góc/áp chế | 3200 |
| **Heavy Máy** | 10 | 650 RPM, không cần khởi động | 60 | 3.2s | Hỏa lực bền, giật mạnh, cơ động kém | 2700 |
| **Sniper Kỹ Năng** | 60 (120 đầu — trúng đầu là bay màu) | 1 viên/lần, lên đạn 1.2s | 5 | 2.5s | Thưởng tay chuẩn; bắn không ngắm cực tản | 2400 |
| **Sniper Hủy Diệt** | 150 mọi vị trí (1 viên bay màu bất kể trúng đâu) | 1 viên/lần, lên đạn 1.8s | 5 | 4.0s (lâu nhất game) | Đắt nhất game; bắn không ngắm gần như vô dụng | 4500 |

AR Chuẩn và AR Mạnh **cùng giá** có chủ đích — đây là lựa chọn phong cách, không phải bậc tiền.

### 6.5 Bốn điều kiện cân bằng bắt buộc của Sniper (điều kiện duyệt)

Súng một-phát-bay-màu trong sân 5–15m sẽ thống trị nếu thả tự do — tay thật trong VR ngắm tầm đó rất nhanh, và chết tức thì cộng độ trễ mạng sẽ tạo cảm giác "chết sau góc tường" bất công. Sniper chỉ vào game khi đủ cả bốn:

1. **Bản đồ có trục dài 20m+** (bản đồ v2, mục 11) — sniper phải có đất riêng thay vì mạnh khắp nơi.
2. **Giá cao nhất game** (4500 cho Hủy Diệt) — trong chế độ kinh tế, giá là bộ cân bằng chính.
3. **Phạt cơ động cực nặng:** chậm 25–30% khi cầm, lên đạn từng viên, nạp lâu nhất game.
4. **Bắn không ngắm cực tản** — buộc dùng ADS, nên sniper vào cùng lát cắt với ADS (lát cắt 3).

Ngưỡng theo dõi: sniper chiếm quá 35% tổng hạ gục trong bản đồ v2 → tăng thời gian lên đạn hoặc tăng giá.

> **CẦN KIỂM CHỨNG KỸ THUẬT:** (a) hòa trộn hướng ngắm hai tay + IK; (b) đồng bộ "đang cầm khẩu nào" cho avatar người khác; (c) spin-up/lên đạn là trạng thái local, kết quả bắn vẫn qua RPC; (d) loạt burst 3 viên = 3 lần dò tia trong các tick liên tiếp hay 1 lần — quyết ở technical review; (e) tốc độ chạy theo vũ khí áp lên AutoHandPlayer — local hay networked; (f) chém dao = dò tầm gần, quyền người tấn công; (g) ADS: ống kính render vs khe ngắm sắt — chi phí hiệu năng Quest.

## 7. Hệ kỹ năng

Giữ nguyên ba kỹ năng: **Quét Xung** (thu thập thông tin), **Rào Chắn** (chặn khu vực), **Hơi Thở Thứ Hai** (hồi phục) — luật chi tiết đầy đủ như hợp đồng kỹ năng đã viết (một nút, một hiệu ứng, có cách đối phó, tối đa một vật thể mạng, luật dùng hụt rõ).

Chọn 2 trong 3 khi chọn loadout: kỹ năng 1 = nút B (tay súng), kỹ năng 2 = nút Y (tay tự do). Dùng kỹ năng không hạ súng.

| Cách hồi | Chế độ | Luật |
|---|---|---|
| Theo thời gian | Đấu Nhanh, Leo Súng | Chờ hồi riêng từng kỹ năng, 15–20 giây |
| Mua bằng tiền | Vòng Kinh Tế | 300₵/lượt, tối đa 2 lượt/kỹ năng/hiệp; hết là hết; không giữ sang hiệp sau |

Kỹ năng chỉ nghiêng cán cân, không quyết định trận — ngưỡng cắt/làm yếu giữ nguyên.

> **CẦN KIỂM CHỨNG KỸ THUẬT:** số lượt kỹ năng là trạng thái đồng bộ trên nhân vật (như máu), chống lệch khi rớt mạng vào lại.

## 8. Kinh tế (chỉ trong Vòng Kinh Tế)

- **Thu nhập:** thắng hiệp +1000₵ · thua hiệp +1400₵ (cố định, không theo chuỗi — bên thua nhận nhiều hơn để chống tuyết lăn) · mỗi hạ gục +200₵ · đầu trận 800₵.
- **Chi:** súng theo bảng mục 6 · lượt kỹ năng 300₵ (tối đa 2/kỹ năng).
- **Giữ đồ:** súng đã mua giữ đến khi chết trong hiệp; sống sót qua hiệp thì giữ tiếp — tạo quyết định để dành hay mua.
- **Trần ví 6000₵.** Không giáp, không bán lại, chết không rơi tiền, chưa mua hộ đồng đội.

Mua trong 20 giây đầu hiệp qua shop. Quan sát bắt buộc: có hiệp "để dành tiền" tự nguyện không — không có nghĩa là bảng thu nhập sai.

> **CẦN KIỂM CHỨNG KỸ THUẬT:** tiền/điểm/giai đoạn/đồng hồ do chủ phòng (Master Client) quản lý theo pattern MinigameManager, xử lý được chủ phòng rớt mạng.

## 9. Ba chế độ — học, luyện, thi đấu

Chung bản đồ, điều khiển, cảm giác bắn, luật kỹ năng, đo lường. Khác nhau đúng ở luật:

| | 1. Leo Súng (LEARN) | 2. Đấu Nhanh (PRACTICE) | 3. Vòng Kinh Tế (COMPETE) |
|---|---|---|---|
| Người | 2–4 tự do | 2–4 tự do | 2 đấu 2 |
| Hồi sinh | Liền, 2s | Liền, 2–4s | Không — chờ hết hiệp |
| Vũ khí | Bậc thang ép: Lục Chuẩn → SMG Nhanh → AR Chuẩn → Heavy Máy → Dao. 3 hạ gục/bậc, hạ bằng dao ở bậc cuối là thắng. (Sniper thêm vào thang khi được mở ở lát cắt 3) | Tự chọn ở shop khu hồi sinh (X) | Mua bằng ₵, 20s đầu hiệp |
| Kỹ năng | Tắt — chỉ học súng | 2/3, hồi thời gian | 2/3, mua |
| Kinh tế | Không | Không | Mục 8 |
| Thắng | Hết thang trước | 12 hạ gục hoặc 5 phút | Thắng trước 5 hiệp (tối đa 9), hiệp 90 giây |
| Hết giờ hiệp | — | — | Đội đông người sống hơn thắng; bằng thì hòa, không ai ăn thưởng thắng |

Chuỗi dẫn dắt: Leo Súng ép cầm đủ dàn súng → biết vai trò từng khẩu → Đấu Nhanh chọn có chủ đích + học kỹ năng → Vòng Kinh Tế biến hiểu biết thành quyết định tiền. Không cần tutorial riêng.

Thứ tự build ngược thứ tự trải nghiệm: Đấu Nhanh (lát 1) → Vòng Kinh Tế (lát 2) → Leo Súng (lát 3).

## 10. Hành trình người chơi

1. **Mở game → Sảnh chờ.** Lần đầu: bắt buộc chọn tay thuận + chỉnh chiều cao (~30 giây). Sảnh là không gian xã hội nhỏ (tái dùng asset lobby), thấy người khác.
2. **Chọn chế độ:** ba cổng vật lý trong sảnh; vào cổng = vào hàng chờ/tạo phòng.
3. **Chọn loadout:** bảng giao diện chọn bằng con trỏ AutoHand (trỏ + bấm, chưa cầm nắm vật lý). Chọn súng chính (Đấu Nhanh) / xem bậc thang (Leo Súng) / xác nhận (Vòng Kinh Tế — mua trong trận). Chọn 2/3 kỹ năng nếu chế độ có.
4. **Vào trận:** từ đây mới vật lý đầy đủ, súng dính tay. Đấu Nhanh/Leo Súng: shop ở khu hồi sinh (X). Vòng Kinh Tế: shop tự hiện mỗi giai đoạn mua.
5. **Kết thúc:** bảng điểm + MVP + 3 chỉ số cá nhân (chính xác, hạ gục/bị hạ, dùng kỹ năng đúng lúc) → bỏ phiếu chơi lại (≥50% = chơi ngay, giữ phòng) hoặc về sảnh.

Luật xuyên suốt: không màn hình chết cứng — mọi bước trong không gian VR; đeo kính → phát bắn đầu **dưới 90 giây**; con trỏ ngoài trận / vật lý trong trận là ranh giới cứng.

## 11. Bản đồ

**Khẳng định: một sân liền, không phải hai sân tách biệt.** Hai phòng hồi sinh chỉ là hai căn phòng kín ở hai đầu; toàn bộ khu đấu là một khối liền đi được khắp nơi.

Luật kết nối (mới ở v0.3):

- **Phòng hồi sinh:** cửa **một chiều** — trong đi ra được, ngoài không vào được, không bắn xuyên. Mỗi phòng **2 cửa ra hướng khác nhau** (chống camp cửa) + **2 giây bất tử sau hồi sinh** (mất ngay khi bắn phát đầu).
- **Tối thiểu 3 đường** nối hai nửa sân — để một tấm Rào Chắn (tồn tại 5 giây) không bao giờ khóa được toàn bộ đường đi.
- Giữ từ bản trước: sân đối xứng, tầm giao tranh 5–15m, sàn phẳng, vật cản ngang gối; thêm vật cản ~1.2m ở 2–3 chỗ để nhảy ló đầu bắn qua; ít nhất một hành lang áp sát dưới 8m (đất của SMG) và một đường ngắm 12–15m (đất của AR/Heavy).
- **Bản đồ v2 (lát cắt 3, đi cùng sniper + ADS):** nới thêm **một trục dài 20m+** có vật cản dọc đường — đất riêng của sniper, đồng thời là đường rủi ro cao cho người khác.
- Không có gì trên bản đồ ép người chơi di chuyển ngoài ý muốn.

## 12. Luật cho tình huống lạ (mới ở v0.3)

- **Rào Chắn khi chủ nhân chết:** tấm chắn **giữ nguyên đến hết 5 giây** của nó (chết không xóa công trình đã đặt). Chủ nhân **rớt mạng** thì tấm chắn hủy ngay (như v0.1).
- **Hai đội chết sạch cùng lúc trong một hiệp Vòng Kinh Tế:** hiệp hòa — không ai ăn thưởng thắng, cả hai nhận thưởng thua.
- **Đổi loadout giữa trận Đấu Nhanh (shop khu hồi sinh):** đổi súng thoải mái, nhưng **không reset thời gian chờ kỹ năng** — chống lạm dụng vào shop để hồi kỹ năng.
- **Đang nắm báng trước mà gạt đổi súng:** tay tự do tự nhả, không kẹt trạng thái.
- **Đổi vũ khí giữa lúc nạp đạn:** hủy nạp (đã ghi ở mục 6, nhắc lại cho đủ bộ).
- **Người chơi rớt mạng giữa hiệp Vòng Kinh Tế:** đội thiếu người chơi tiếp; vào lại trong cùng trận thì trở lại từ hiệp kế tiếp với ví tiền như trước khi rớt.

## 13. Cân bằng — giả thuyết và nút chỉnh

- **TTK (thời gian hạ gục) chuẩn: 0.6–1.0 giây trúng thân** ở đúng đất của từng khẩu. Ngoại lệ có luật riêng: AR Mạnh chạm biên dưới (~0.5 giây) đổi bằng giật mạnh khó ghim; Heavy tính cả khởi động được tới 1.2 giây; hai khẩu một-phát (Lục Nặng trúng đầu, Sniper) cân bằng bằng giá + chậm chân + nạp lâu, không bằng TTK.
- **Cặp cùng class phải cân:** tỉ lệ chọn trong cặp nằm ngoài khoảng 35–65% qua nhiều trận → chỉnh khẩu lệch.
- **Ít nhất 60% hạ gục đến từ súng** (kỹ năng chỉ hỗ trợ). Không khẩu nào quá 70% tỉ lệ chọn qua 6 trận. Sniper không quá 35% tổng hạ gục ở bản đồ v2. Heavy không quá 40%.
- **Kinh tế:** đội thắng hiệp 1 không thắng cả trận quá 65% — vượt thì tăng thưởng thua.
- **Nút chỉnh không cần thiết kế lại:** dmg/nhịp bắn/băng/giảm-theo-tầm từng khẩu · ba tham số giật (tản nền, tốc nở, tốc hồi) · thời gian đổi súng, nạp, khởi động, lên đạn · tốc độ chạy theo vũ khí · dmg + thời gian chờ dao · độ cao/thời gian nhảy · giá từng khẩu + kỹ năng · ba mức thu nhập · số mạng/thời gian trận · độ trễ hồi sinh · chờ hồi hoặc số lượt kỹ năng.
- **Theo dõi sát:** chiến thuật luôn-đúng (thủ Heavy + Rào Chắn?), cặp khắc chế quyết định từ lúc chọn đồ, người chết có hiểu vì sao chết không (chỉ số số một), tỉ lệ bấm nhầm, tuyết lăn kinh tế.

## 14. Dữ liệu đo lường

Giữ từ bản trước: hạ gục/bị hạ từng người, TTK thực tế, độ chính xác theo khoảng cách, số lần dùng kỹ năng, thời lượng trận, số lần hồi sinh.

Thêm: hạ gục theo **từng khẩu** + khoảng cách hạ gục (kiểm tra đất của súng) · tỉ lệ chọn trong từng **cặp** cùng class · số lần bấm nhầm + đổi súng + chết-khi-đang-nạp/đổi · tần suất nhảy + ca say VR đếm riêng cho nhảy · quyết định mua/để dành từng hiệp + tương quan tiền→thắng · thời gian mở game→phát bắn đầu + điểm rơi rớt trong chuỗi sảnh→loadout→trận · % người tự chuyển chế độ sau Leo Súng · riêng sniper (khi mở): % hạ gục toàn trận + khoảng cách trung bình.

## 15. Kế hoạch chơi thử theo lát cắt

**Lát 1 — Lõi Đấu Nhanh (4 khẩu đại diện):** Lục Chuẩn + SMG Nhanh + AR Chuẩn + Dao, nhảy, đổi súng, nạp, 2 kỹ năng hồi thời gian, tốc độ chạy theo súng. 4 người, một bản đồ, 12 mạng. Hiệp đầu khóa AR Chuẩn, hiệp sau tự chọn. **Đạt khi:** TTK trung vị 0.6–1.0s · 0 ca say VR (nhảy theo dõi riêng) · bấm nhầm dưới 2/người/trận · ≥60% hạ gục từ súng · SMG và AR ăn mạng ở đúng tầm sở trường · người chơi đòi trận nữa. **Trượt thì:** say VR do nhảy → tắt nhảy · bấm nhầm cao → nạp đạn đổi sang cử chỉ · một khẩu quá 70% → làm yếu một nấc · nhầm nút → xem lại bố trí.

**Lát 2 — Vòng Kinh Tế 2v2 + đủ 10 khẩu (chưa sniper):** thêm Lục Nặng, Lục Burst, SMG Burst, AR Mạnh, Heavy ×2, shop, tiền, hiệp, Hơi Thở Thứ Hai. 4 người, đấu tới thắng 5 hiệp. **Đạt khi:** có hiệp để-dành-tiền tự nguyện · đội thắng hiệp 1 thắng trận ≤65% · giai đoạn mua 20 giây không ai lúng túng · Heavy ăn mạng đúng thế giữ góc nhưng dưới 40% tổng · mỗi cặp cùng class có tỉ lệ chọn 35–65%.

**Lát 3 — Leo Súng + toàn hành trình + bản đồ v2 + ADS + Sniper ×2:** 4 người mới hoàn toàn, từ lúc mở game. **Đạt khi:** mở game→bắn dưới 90 giây · người mới hiểu vai trò các khẩu sau một trận Leo Súng (phỏng vấn) · ≥50% tự chơi tiếp chế độ khác cùng buổi · sniper dưới 35% tổng hạ gục và có đất riêng ở trục 20m.

## 16. Phân loại việc

**LÀM NGAY (lát 1):** tay thuận + nút đảo gương · súng dính tay · 3 ô + gạt đổi · tốc độ chạy theo súng · Lục Chuẩn, SMG Nhanh, AR Chuẩn, Dao · nạp bằng ấn cần + tự nạp · nhảy có cầu dao · Quét Xung + Rào Chắn hồi thời gian · Đấu Nhanh 2–4 · shop tối giản khu hồi sinh · bản đồ theo mục 11 (chưa cần trục 20m) · đo súng/bấm nhầm/nhảy.

**KẾ TIẾP (lát 2):** Lục Nặng · Lục Burst · SMG Burst · AR Mạnh · Heavy Nòng Xoay + Heavy Máy · Hơi Thở Thứ Hai · Vòng Kinh Tế 2v2 trọn gói (tiền, giai đoạn mua, hiệp, chủ phòng rớt mạng) · giao diện loadout đầy đủ · bảng điểm + bỏ phiếu chơi lại · các luật tình huống lạ mục 12.

**SAU NỮA (lát 3+):** Leo Súng · sảnh + 3 cổng · hướng dẫn lần đầu · **ADS + bản đồ v2 trục 20m + Sniper Kỹ Năng + Sniper Hủy Diệt** · nạp đạn cử chỉ vật lý · shotgun (ứng viên khẩu 12) · mua hộ đồng đội · menu hệ thống.

**BỎ HẲN:** nhặt súng từ xác · giáp · dmg dao theo tốc độ vung · thu nhập theo chuỗi thắng · hệ đạn dự trữ · camera ép buộc ngoài nhảy · lore không gắn luật chơi · mọi phần của cơ chế ném bóng cũ.

## 17. Bàn giao technical review (cho $expert-developer)

Mười điểm cần đối chiếu với Fusion 2.0.12 Shared Mode + AutoHand + hệ input mới. Tất cả là ý định thiết kế:

1. Khóa súng dính tay vĩnh viễn bằng AutoHand; báng trước súng 2 tay; hòa trộn hướng ngắm hai tay ăn khớp IK hiện giữ.
2. Chia vùng một cần điều khiển (xoay / đổi súng / nạp) trên hệ input mới; đảo gương trọn bộ theo tay thuận.
3. Nhảy trên AutoHandPlayer: ground-check, vị trí mạng khi trên không.
4. Đồng bộ "đang cầm khẩu nào" (3 ô) cho avatar người khác; spin-up/lên đạn sniper là trạng thái local.
5. Xác nhận trúng đạn giữ nguyên: người bắn tự dò tia → RPC → nạn nhân tự trừ máu — áp cho mọi khẩu + dao (dò tầm gần). **Câu hỏi riêng cho burst:** 3 viên/loạt = 3 lần dò trong các tick liên tiếp hay 1 RPC gộp?
6. Tốc độ chạy theo vũ khí: áp local lên AutoHandPlayer, có cần đồng bộ thêm không?
7. Tiền/hiệp/giai đoạn trên chủ phòng (pattern MinigameManager) + chuyển quyền khi chủ phòng rớt; ví tiền người rớt-vào-lại.
8. Số lượt kỹ năng là trạng thái đồng bộ trên nhân vật.
9. Shop/loadout bằng con trỏ AutoHand trong phòng hồi sinh; ranh giới con trỏ ngoài trận / vật lý trong trận; cửa một chiều + 2 giây bất tử.
10. **ADS trên Quest:** ống kính render (đắt hiệu năng — render texture mỗi mắt) hay khe ngắm sắt vật lý (miễn phí) — khuyến nghị kỹ thuật quyết định thiết kế sniper.

## 18. Câu hỏi còn mở

1. **Vòng Kinh Tế:** bắt buộc đúng 2 đấu 2, hay chấp nhận 1 đấu 1 khi thiếu người? (Ảnh hưởng phòng hồi sinh + thu nhập.)
2. **Nhảy:** xác nhận lần cuối cầu dao ngắt "một ca say VR do nhảy = tắt ngay" — hay nhảy là bất khả xâm phạm?
3. **ADS:** chờ khuyến nghị từ technical review (điểm 10 mục 17) rồi chốt dạng ngắm — quyết định này định hình cả hai khẩu sniper.

**Việc kế tiếp:** chuyển mục 17 cho `$expert-developer` chạy technical review. Lát cắt 1 đủ chi tiết để review ngay, không cần chờ chốt ba câu hỏi trên.
