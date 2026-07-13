# TOSSZONE v0.3 — bản dễ đọc

> Bản này viết lại từ `VR-FPS-Playable-GDD-v0.3.md` cho dễ đọc. Mức người đọc: người quan tâm
> project, không cần biết thuật ngữ kỹ thuật. Giữ đủ mọi quyết định và con số của bản gốc; bản
> gốc vẫn là nguồn chuẩn khi cần trích số liệu chính xác cho dev.
>
> Tình trạng: đang thiết kế, chưa viết code. Mọi con số là giả thuyết, phải chơi thử mới biết
> đúng sai. Bước kế tiếp của tài liệu: đưa cho lập trình viên soi phần kỹ thuật (mục 17).

## 1. Bản này khác bản trước chỗ nào

Khung chính của bản v0.2 giữ nguyên hết: cách điều khiển, ba kỹ năng, cơ chế tiền, ba chế độ chơi, hành trình người chơi. Bản v0.3 đổi bốn thứ, theo đúng những gì bạn duyệt.

Thứ nhất, dàn súng tăng từ 4 khẩu lên 11 khẩu cộng một con dao. Cách chia mới gọn hơn hẳn: súng cầm một tay chỉ có 3 khẩu lục và dao, còn lại tất cả đều cầm hai tay. Nhờ vậy luật "một tay hay hai tay" trùng luôn với ô vũ khí phụ và ô vũ khí chính, không còn ngoại lệ nào phải nhớ.

Thứ hai, định nghĩa lại "giật súng" cho phù hợp VR. Trong game máy tính, súng giật là màn hình bị hất lên. Trong VR mà làm vậy thì người chơi chóng mặt ngay, nên giật ở đây nghĩa là: nòng súng nảy lên (chỉ là hình ảnh) và đạn tỏa rộng dần khi giữ cò, nhả cò thì gom lại. Súng giật mạnh là súng bắt bạn bắn từng nhịp ngắn thay vì kéo cò liên tục. Cảm giác chơi giống Valorant, chỉ khác cách thể hiện.

Thứ ba, bản đồ được viết rõ luật kết nối, trả lời thẳng câu bạn cấn: đây là một sân liền, không phải hai sân tách rời. Chi tiết ở mục 11.

Thứ tư, bổ sung luật cho sáu tình huống lạ mà bản trước chưa nói rõ, ví dụ tấm Rào Chắn ra sao khi chủ nhân chết. Chi tiết ở mục 12.

Riêng hai khẩu súng nhắm (sniper) được duyệt kèm bốn điều kiện bắt buộc, nói ở mục 6. Chúng chỉ vào game khi có chế độ ngắm và bản đồ được nới rộng.

## 2. Điều đang muốn chứng minh

Cho người chơi VR một dàn súng mà mỗi loại có hai khẩu để chọn, một khẩu bắn chuẩn ít giật và một khẩu mạnh nhưng giật nhiều, cộng hai kỹ năng trên hai nút bấm. Nếu thiết kế đúng, 2 đến 4 người sẽ có những trận đấu súng dễ hiểu, muốn chơi lại, và việc chọn súng nào trước trận trở thành một quyết định đáng suy nghĩ chứ không phải chọn đại. Thêm nữa, khi ba chế độ chơi nối với nhau thành một chuỗi từ học súng, luyện tập, đến thi đấu, người chơi sẽ tự học được cách chơi mà không cần màn hướng dẫn riêng.

Bản này chưa test: hệ thống lên cấp lâu dài, đồ trang trí, xếp hạng, độ sâu nội dung. Chưa đến lượt mấy thứ đó.

## 3. Bối cảnh câu chuyện

TOSSZONE là một giải đấu thể thao ảo trong tương lai gần, dùng công nghệ ánh sáng cứng: thứ ánh sáng có thể chạm vào được, cứng như vật thật. Đấu sĩ không cầm vũ khí thật. Bộ giáp thi đấu chiếu thẳng vũ khí lên tay họ.

Bối cảnh này giải thích được mọi luật chơi quan trọng. Súng làm từ ánh sáng nên dính chặt vào tay, không rơi, không nhặt được, không cướp được. Tấm Rào Chắn cũng cùng công nghệ nên nhìn ăn khớp với súng. Chết nghĩa là giáp mất nguồn, người tan thành hạt sáng rồi hồi sinh ở buồng tái tạo. Tiền trong chế độ kinh tế là năng lượng chiếu, ban tổ chức phát theo thành tích từng hiệp.

Câu chuyện dừng ở đó. Chưa có phe phái, chưa có nhân vật tên riêng. Sau này muốn thêm gì vào câu chuyện thì phải gắn với một luật chơi cụ thể, không thêm cho vui.

## 4. Sáu nguyên tắc nền

1. Súng là trung tâm. Kỹ năng không bao giờ được mạnh hơn việc bắn.
2. Một luật dùng cho mọi tay, mọi súng, mọi chế độ. Không đặt luật riêng lẻ tẻ.
3. Sự thoải mái trong VR có cầu dao ngắt. Không có chuyển động camera nào ép buộc người chơi. Nhảy là ngoại lệ duy nhất được thử, và hễ có một người say VR vì nhảy trong buổi chơi thử thì tắt nhảy ngay, không bàn cãi.
4. Cảm giác chạy ngay trên máy mình, kết quả thật xác nhận qua mạng. Tiếng súng và rung tay thì tức thì, còn ai mất máu ai chết thì phải được hệ thống mạng công nhận mới tính.
5. Ba chế độ, một lõi. Các chế độ chỉ được khác nhau ở luật chơi, cấm chế độ nào đòi một cơ chế riêng không dùng chung.
6. Lát cắt trước phải đạt rồi mới làm lát cắt sau. Game build theo ba đợt, đợt nào chơi thử đạt yêu cầu mới sang đợt kế.

## 5. Điều khiển và tay thuận

Người chơi chọn tay thuận trong phần cài đặt, mặc định là tay phải, đổi lại được bất cứ lúc nào. Súng xuất hiện thẳng vào tay thuận và khóa cứng ở đó, không bao giờ rời tay. Tay còn lại gọi là tay tự do: lo di chuyển, nắm báng trước của súng hai tay, và tương tác với môi trường. Ai thuận tay trái thì toàn bộ nút bấm đảo gương.

| Thao tác | Tay súng (phải) | Tay tự do (trái) |
|---|---|---|
| Cò | Bắn | Để dành sau |
| Nắm tay | Giữ súng tự động | Nắm báng trước súng hai tay, hoặc tương tác |
| Cần gạt ngang | Xoay người kiểu nhảy góc | Di chuyển ngang |
| Cần gạt dọc | Gạt lên xuống để đổi vũ khí | Tiến, lùi |
| Ấn cần | Nạp đạn | Để dành (có thể thành nút mở shop) |
| Nút dưới (A) | Nhảy | |
| Nút trên (B) | Kỹ năng 1 | |
| Nút dưới (X) | | Trong khu hồi sinh: mở shop |
| Nút trên (Y) | | Kỹ năng 2 |

Luật để nhớ: nút trên là kỹ năng, nút dưới ở tay cầm súng là nhảy, nút dưới ở tay kia là shop.

Chỗ đáng lo nhất của cả bộ điều khiển: cần gạt ở tay cầm súng đang gánh ba việc cùng lúc là xoay người, đổi súng, và nạp đạn. Cách xử lý là chia vùng theo góc gạt. Gạt dọc gần như thẳng đứng (quá 80% biên độ) mới tính là đổi súng. Gạt ngang là xoay người. Ấn cần để nạp đạn chỉ ăn khi cần đang ở gần vị trí giữa, không nghiêng bên nào. Trong buổi chơi thử sẽ đếm số lần bấm nhầm; ai nhầm quá hai lần một trận thì đổi cách nạp đạn sang cử chỉ khác, ví dụ lấy tay tự do chạm vào súng. Lưới an toàn luôn có: hết đạn thì súng tự nạp, không cần làm gì.

Về nhảy: nhảy thấp thôi, đỉnh khoảng 0.8 mét, lơ lửng khoảng nửa giây. Không nhảy đúp, không leo trèo. Lúc rời mặt đất, viền màn hình mờ nhẹ đi để đỡ chóng mặt.

Bốn điểm cần lập trình viên kiểm tra trước: tay tự do nắm báng trước có đụng hệ thống cầm nắm AutoHand không; cách chia vùng cần gạt có làm được trên hệ nhập liệu mới của Unity không; nút nhảy có hợp với cách nhân vật kiểm tra chạm đất hiện tại không; và có khóa được súng dính tay vĩnh viễn mà người chơi không tự buông ra được không.

## 6. Dàn vũ khí: 11 súng và một con dao

### Luật chung

Tay cầm súng không bao giờ trống. Không thả súng, không nhặt súng của người khác; người chết cũng không làm rơi súng ra sàn. Bỏ hẳn chuyện nhặt đồ từ xác để đỡ rắc rối cả về mạng lẫn tình huống lạ.

Có ba ô vũ khí, gạt cần theo chiều dọc để xoay vòng: súng chính (hai tay) sang súng phụ (một khẩu lục), sang dao, rồi vòng lại. Mỗi lần đổi mất khoảng nửa giây và trong lúc đó không bắn được. Nếu đang nắm báng trước súng hai tay mà gạt đổi, tay tự do sẽ tự nhả ra, không bị kẹt.

Tốc độ chạy phụ thuộc súng đang cầm trên tay. Cầm dao hoặc lục thì chạy nhanh nhất (100%), SMG chạy 95%, AR chạy 90%, súng nặng và súng nhắm chỉ còn 75% (riêng khẩu Hủy Diệt là 70%). Nghĩa là ai vác súng nặng mà muốn rút lui nhanh thì phải gạt sang lục hoặc dao rồi mới chạy. Hỏa lực đổi bằng sự chậm chạp, đó là chủ đích.

Giật súng, như đã nói ở mục 1: nòng nảy lên và đạn tỏa dần khi giữ cò, nhả cò thì gom lại. Mỗi khẩu có ba nút chỉnh riêng: độ tỏa ban đầu, tốc độ tỏa ra, tốc độ gom lại. Bắn trong lúc đang chạy thì đạn tỏa thêm, cầm súng hai tay mà chỉ dùng một tay thì tỏa nặng.

Nạp đạn bằng cách ấn cần, hoặc để súng tự nạp khi hết. Có thanh tiến trình sáng ngay trên thân súng. Đổi vũ khí giữa chừng thì việc nạp bị hủy.

Ngắm bắn: chĩa nòng theo tay, nhìn theo nòng mà bắn. Chế độ ngắm qua ống (ADS, kiểu ghé mắt vào ống ngắm để bắn xa cho chuẩn) chưa có ở đợt đầu, sẽ vào cùng đợt với súng nhắm. Ngắm kiểu nào (ống kính thật hay khe ngắm sắt) thì chờ lập trình viên trả lời, vì ống kính thật rất tốn sức máy trên kính Quest.

### Bảng súng

Người chơi có 100 máu. Trúng đầu ăn gấp đôi sát thương. Chưa có giáp. Mỗi loại súng có một cặp để chọn: một khẩu chuẩn ít giật, một khẩu mạnh giật nhiều. Mọi con số là giả thuyết.

**Súng phụ, cầm một tay, nằm ở ô thứ hai. Dao nằm ô thứ ba, ai cũng có.**

| Khẩu | Sát thương thân | Nhịp bắn | Băng đạn | Nạp | Tính cách | Giá |
|---|---|---|---|---|---|---|
| Lục Chuẩn (phát không, ai cũng có) | 25 | Từng phát | 12 viên | 1.2 giây | Chuẩn, dễ dùng | 0 |
| Lục Nặng | 50, trúng đầu 100 là chết luôn | Chậm | 6 viên | 1.6 giây | Mạnh, giật nhiều, thưởng cho tay bắn chuẩn | 700 |
| Lục Burst | 11 nhân 3 viên mỗi loạt | Mỗi lần bấm cò ra 3 viên liền nhau, nhịp nhanh gần bằng SMG | 21 viên (7 loạt) | 2.2 giây | Trút nhanh nhưng băng nhỏ, nạp chậm | 500 |
| Dao | 60 | Chờ 0.7 giây giữa hai nhát | | | Hai nhát chết, chạy nhanh nhất | 0 |

Khẩu Lục Burst "đạn có giới hạn" như bạn nói được thể hiện bằng băng nhỏ cộng nạp chậm, vì game không có hệ đạn dự trữ (đã cắt từ trước). Dao thì cố tình đơn giản: chém trúng là 60 máu, có thời gian chờ giữa hai nhát, và không tính sát thương theo tốc độ vung tay thật, vì cách đó dễ bị ăn gian bằng trò rung tay và khó phân xử đúng qua mạng.

**Súng chính, cầm hai tay, nằm ở ô thứ nhất.**

| Khẩu | Sát thương thân | Nhịp bắn | Băng đạn | Nạp | Tính cách | Giá |
|---|---|---|---|---|---|---|
| SMG Nhanh | 12, còn 8 nếu xa quá 12 mét | 850 viên/phút, giữ cò bắn liên tục | 30 viên | 1.6 giây | Áp sát trút mưa, đạn tỏa nhanh | 900 |
| SMG Burst | 14 nhân 3 viên mỗi loạt | Loạt 3 viên, nhịp nhanh | 24 viên (8 loạt) | 1.9 giây | Chuẩn hơn ở tầm 10 đến 15 mét, thưởng người bắn nhịp | 1100 |
| AR Chuẩn | 16 | 600 viên/phút | 30 viên | 1.8 giây | Ít giật, dễ điều khiển, giảm nhẹ sát thương khi xa quá 20 mét | 1900 |
| AR Mạnh | 20, trúng đầu 40 | 500 viên/phút | 25 viên | 1.9 giây | Không giảm theo tầm xa, nhưng giật mạnh | 1900 |
| Heavy Nòng Xoay | 8 | 900 viên/phút, cần nửa giây quay nòng lấy đà trước khi đạn ra | 100 viên | 4 giây | Trải mưa đạn, siêu giật, hợp giữ góc | 3200 |
| Heavy Máy | 10 | 650 viên/phút, bắn được ngay | 60 viên | 3.2 giây | Hỏa lực bền, giật mạnh | 2700 |
| Sniper Kỹ Năng | 60, trúng đầu 120 là chết luôn | Mỗi phát phải lên đạn lại, mất 1.2 giây | 5 viên | 2.5 giây | Thưởng tay bắn chuẩn; bắn mà không ngắm thì đạn đi tứ tung | 2400 |
| Sniper Hủy Diệt | 150, trúng bất cứ đâu cũng chết | Lên đạn lâu hơn, 1.8 giây mỗi phát | 5 viên | 4 giây, lâu nhất game | Đắt nhất game; không ngắm thì gần như vô dụng | 4500 |

Hai khẩu AR cùng giá là cố ý: chọn khẩu nào là chuyện phong cách bắn, không phải chuyện giàu nghèo.

### Bốn điều kiện bắt buộc của súng nhắm

Nói thẳng: một khẩu súng trúng phát nào chết phát đó, thả vào cái sân chỉ rộng 5 đến 15 mét, sẽ thống trị toàn bộ game. Trong VR, tay thật ngắm ở tầm gần rất nhanh. Và khi chết ngay tức thì cộng với độ trễ mạng, người chết sẽ luôn cảm thấy "tôi vừa núp sau tường rồi mà", rất bất công. Nên hai khẩu sniper chỉ được vào game khi đủ cả bốn điều kiện:

1. Bản đồ phải có một trục dài hơn 20 mét. Sniper phải có đất riêng của nó, thay vì mạnh ở khắp nơi.
2. Giá phải cao nhất game (khẩu Hủy Diệt 4500, gần gấp đôi khẩu Heavy đắt nhất còn lại).
3. Phạt cơ động thật nặng: đi chậm hơn người khác 25 đến 30%, mỗi phát phải lên đạn lại, nạp băng lâu nhất game.
4. Bắn mà không ngắm qua ống thì đạn đi tứ tung, gần như vô dụng. Vì thế sniper buộc phải vào cùng đợt với chế độ ngắm.

Có cả ngưỡng theo dõi: nếu sniper chiếm quá 35% tổng số mạng hạ được, tăng thời gian lên đạn hoặc tăng giá.

Bảy điểm cần lập trình viên kiểm tra ở phần vũ khí: cách hòa hướng ngắm khi hai tay cùng cầm một khẩu súng, sao cho khớp với hệ tạo dáng tay đang có; cách cho người chơi khác nhìn thấy đúng khẩu súng mình đang cầm; trạng thái quay nòng và lên đạn chỉ cần tính trên máy người bắn; loạt burst 3 viên nên tính là ba lần dò trúng liên tiếp hay gộp một lần gửi qua mạng; tốc độ chạy theo súng có cần đồng bộ qua mạng không; nhát dao tính là một lần dò trúng tầm gần do người chém quyết định; và câu hỏi ống ngắm đã nói ở trên.

## 7. Ba kỹ năng

Giữ nguyên ba kỹ năng cũ: Quét Xung (dò vị trí địch quanh mình trong chốc lát), Rào Chắn (đặt tấm chắn ánh sáng chặn đạn và tầm nhìn trong 5 giây), Hơi Thở Thứ Hai (tự hồi máu dần trong 2 giây). Luật chi tiết từng kỹ năng không đổi.

Trước trận, người chơi chọn 2 trong 3. Kỹ năng thứ nhất gắn vào nút B ở tay súng, kỹ năng thứ hai gắn vào nút Y ở tay tự do. Bấm kỹ năng không phải hạ súng xuống.

Cách hồi kỹ năng tùy chế độ. Ở Đấu Nhanh và Leo Súng, kỹ năng hồi theo thời gian, dùng xong chờ 15 đến 20 giây là dùng lại được, như sạc pin. Ở Vòng Kinh Tế, kỹ năng phải mua: 300 đồng một lượt, tối đa hai lượt cho mỗi kỹ năng trong một hiệp, dùng hết là thôi, và lượt thừa không mang sang hiệp sau để khỏi tích trữ.

Nguyên tắc không đổi: kỹ năng chỉ nghiêng cán cân, không được là thứ quyết định thắng thua.

Một điểm cần lập trình viên kiểm tra: số lượt kỹ năng còn lại phải được lưu qua mạng ngay trên nhân vật, giống chỉ số máu, để không bị lệch số khi ai đó rớt mạng rồi vào lại.

## 8. Tiền trong chế độ Vòng Kinh Tế

Học từ Valorant nhưng rút gọn còn bốn luật.

Kiếm tiền: thắng hiệp thêm 1000 đồng, thua hiệp thêm 1400 đồng. Bên thua nhận nhiều hơn, và mức cộng cố định chứ không tăng theo chuỗi, để tránh chuyện bên đang thắng càng giàu càng thắng tiếp. Mỗi mạng hạ được thêm 200 đồng. Vào trận ai cũng có sẵn 800 đồng.

Tiêu tiền: mua súng theo bảng giá ở mục 6, mua lượt kỹ năng 300 đồng. Súng đã mua thì giữ đến khi chết trong hiệp; sống sót qua hiệp thì súng còn nguyên cho hiệp sau. Chính chỗ này tạo ra quyết định "hiệp này để dành hay mua luôn".

Ví tiền có trần 6000 đồng, ép phải tiêu chứ không ôm mãi. Chưa có giáp, không bán lại đồ, chết không rơi tiền, chưa mua hộ đồng đội.

Mỗi hiệp mở đầu bằng 20 giây mua đồ qua màn hình shop. Khi chơi thử phải để ý một chuyện: có ai tự nguyện "để dành tiền", tức chấp nhận đánh hiệp đó bằng lục và dao cho rẻ, hay không. Nếu không ai làm vậy bao giờ thì bảng thu nhập đang sai, phải chỉnh.

Điểm cần lập trình viên kiểm tra: tiền, điểm, giai đoạn hiệp và đồng hồ phải do máy chủ phòng quản lý (chủ phòng là máy đang giữ quyền cao nhất trong phòng chơi), theo đúng cách hệ minigame cũ từng làm, và phải sống sót được khi chủ phòng rớt mạng giữa chừng.

## 9. Ba chế độ chơi

Cả ba dùng chung bản đồ, chung điều khiển, chung cảm giác bắn, chung luật kỹ năng. Chỉ khác luật chơi:

| | Leo Súng (học) | Đấu Nhanh (luyện) | Vòng Kinh Tế (thi đấu) |
|---|---|---|---|
| Số người | 2 đến 4, ai bắn ai | 2 đến 4, ai bắn ai | 2 đấu 2 |
| Hồi sinh | Ngay, sau 2 giây | Ngay, sau 2 đến 4 giây | Không, chết là chờ hết hiệp |
| Vũ khí | Thang ép sẵn: Lục Chuẩn, SMG Nhanh, AR Chuẩn, Heavy Máy, Dao. Hạ 3 người thì lên bậc, hạ bằng dao ở bậc cuối là thắng | Tự chọn ở shop khu hồi sinh (nút X) | Mua bằng tiền trong 20 giây đầu hiệp |
| Kỹ năng | Tắt, để tập trung học súng | Chọn 2 trong 3, hồi theo thời gian | Chọn 2 trong 3, mua bằng tiền |
| Thắng | Ai leo hết thang trước | 12 mạng hoặc hết 5 phút | Thắng trước 5 hiệp, tối đa 9 hiệp, mỗi hiệp 90 giây |

Nếu hết giờ một hiệp Vòng Kinh Tế: đội còn nhiều người sống hơn thắng hiệp đó, bằng nhau thì hòa và không ai nhận thưởng thắng. Khi mở sniper ở đợt 3, thang Leo Súng sẽ thêm bậc sniper.

Ba chế độ nối nhau thành một đường học tự nhiên. Leo Súng ép cầm đủ các khẩu nên chơi xong một trận là biết khẩu nào làm gì. Sang Đấu Nhanh, người chơi chọn súng có chủ đích và bắt đầu học kỹ năng. Đến Vòng Kinh Tế, hiểu biết đó thành quyết định tiền bạc thật. Nhờ vậy không cần màn hướng dẫn riêng.

Thứ tự làm game thì ngược với thứ tự người chơi trải nghiệm: Đấu Nhanh làm trước để chứng minh cảm giác bắn, rồi Vòng Kinh Tế, cuối cùng mới Leo Súng vì nó rẻ nhất, chỉ là bộ luật khác đắp lên nền có sẵn.

## 10. Hành trình người chơi, từ mở game đến hết trận

Bước 1: mở game, vào sảnh chờ. Lần đầu chơi phải chọn tay thuận và chỉnh chiều cao, mất khoảng 30 giây. Sảnh là không gian nhỏ, dùng lại đồ họa sảnh có sẵn, nhìn thấy người chơi khác đang đứng đó.

Bước 2: chọn chế độ. Trong sảnh có ba cánh cổng, mỗi cổng một chế độ, bước vào cổng là vào hàng chờ hoặc tạo phòng.

Bước 3: chọn trang bị. Một bảng giao diện điều khiển bằng con trỏ từ tay (chỉ vào rồi bấm, như dùng bút laser, chưa cần cầm nắm vật thật). Đấu Nhanh thì chọn súng chính ở đây, Leo Súng thì xem trước cái thang, Vòng Kinh Tế thì chỉ xác nhận vì tiền tiêu trong trận. Chọn luôn 2 trong 3 kỹ năng nếu chế độ có kỹ năng.

Bước 4: vào trận. Từ giây này mới là vật lý thật và súng dính vào tay. Ở Đấu Nhanh và Leo Súng, khu hồi sinh có shop, bấm X để đổi trang bị giữa trận. Ở Vòng Kinh Tế, shop tự hiện mỗi lần đến 20 giây mua đồ.

Bước 5: hết trận. Bảng điểm, người chơi hay nhất trận, và ba chỉ số cá nhân: độ chính xác, số mạng hạ và bị hạ, mức dùng kỹ năng đúng lúc. Xong thì bỏ phiếu chơi lại (nửa phòng đồng ý là chơi ngay, giữ nguyên phòng) hoặc về sảnh.

Hai luật xuyên suốt. Một, không có màn hình cứng nào cả, mọi bước đều là bảng và cổng trong không gian VR, chỉ phần Cài đặt tách riêng. Hai, từ lúc đeo kính đến phát súng đầu tiên phải dưới 90 giây, đây là thước đo quan trọng nhất của cả hành trình.

## 11. Bản đồ: một sân liền

Trả lời thẳng điều bạn cấn: bản đồ là một sân liền, đi được khắp nơi, không phải hai sân tách rời. Hai phòng hồi sinh chỉ là hai căn phòng kín nằm ở hai đầu sân.

Luật phòng hồi sinh: cửa một chiều, từ trong bước ra được nhưng ngoài không vào được, đạn không xuyên qua. Mỗi phòng có hai cửa mở ra hai hướng khác nhau để không ai rình sẵn một cửa mà bắt hết được. Người vừa hồi sinh được 2 giây không thể bị bắn, và sự bảo vệ đó mất ngay khi họ nổ phát súng đầu tiên.

Luật đường đi: giữa hai nửa sân luôn có ít nhất ba đường. Con số này để một tấm Rào Chắn (chỉ sống 5 giây) không bao giờ bịt được toàn bộ lối đi.

Các yêu cầu giữ từ trước: sân đối xứng, tầm giao tranh 5 đến 15 mét, sàn phẳng, có vật cản cao ngang gối để núp. Thêm vài vật cản cao khoảng 1.2 mét để nhảy lên ló đầu bắn qua, cho nút nhảy có lý do tồn tại. Có ít nhất một hành lang ngắn dưới 8 mét là đất của SMG, và một đường ngắm 12 đến 15 mét là đất của AR và Heavy.

Khi sniper vào game ở đợt 3, bản đồ sẽ nới thêm một trục dài hơn 20 mét, có vật cản rải dọc đường. Đó là đất riêng của sniper và là con đường rủi ro cao cho người khác.

Không có gì trên bản đồ ép người chơi di chuyển ngoài ý muốn.

## 12. Sáu luật cho tình huống lạ

1. Chủ nhân tấm Rào Chắn chết: tấm chắn vẫn đứng đó cho hết 5 giây của nó. Chết không xóa công trình đã đặt. Nhưng nếu chủ nhân rớt mạng thì tấm chắn biến mất ngay.
2. Hai đội chết sạch cùng lúc trong một hiệp Vòng Kinh Tế: hiệp đó hòa, không ai nhận thưởng thắng, cả hai bên nhận thưởng thua.
3. Đổi trang bị giữa trận Đấu Nhanh: đổi súng thoải mái, nhưng thời gian chờ kỹ năng không được làm mới. Không thì người ta cứ chạy vào shop để "rửa" thời gian chờ.
4. Đang nắm báng trước súng hai tay mà gạt đổi vũ khí: tay tự do tự nhả, không bị kẹt.
5. Đổi vũ khí giữa lúc đang nạp đạn: việc nạp bị hủy.
6. Rớt mạng giữa hiệp Vòng Kinh Tế: đội thiếu người vẫn chơi tiếp hiệp đó. Người rớt vào lại cùng trận thì tham gia từ hiệp kế tiếp, ví tiền còn nguyên như trước khi rớt.

## 13. Cân bằng: mục tiêu và các nút chỉnh

Thước đo chính là thời gian hạ gục: từ phát trúng đầu tiên đến lúc đối thủ chết, mục tiêu 0.6 đến 1.0 giây khi bắn vào thân, tính ở đúng sở trường của từng khẩu. Có vài ngoại lệ được phép: AR Mạnh chạm đáy khoảng 0.5 giây nhưng đổi lại giật mạnh khó ghim; Heavy tính cả nửa giây quay nòng thì được đến 1.2 giây; còn mấy khẩu một phát chết (Lục Nặng trúng đầu, hai khẩu sniper) thì không cân bằng bằng thời gian mà bằng giá tiền, sự chậm chạp và nạp đạn lâu.

Các ngưỡng phải giữ: mỗi cặp súng cùng loại phải được chọn tương đối đều nhau, lệch ra ngoài khoảng 35/65 là phải chỉnh khẩu bị lệch. Ít nhất 60% số mạng phải đến từ súng chứ không phải kỹ năng. Không khẩu nào được chọn quá 70% qua sáu trận. Sniper không quá 35% tổng số mạng, Heavy không quá 40%. Ở Vòng Kinh Tế, đội thắng hiệp đầu không được thắng cả trận quá 65%, vượt là tăng tiền cho bên thua.

Những thứ chỉnh được mà không phải thiết kế lại: sát thương, nhịp bắn, băng đạn, mức giảm theo tầm xa của từng khẩu; ba tham số giật; thời gian đổi súng, nạp, quay nòng, lên đạn; tốc độ chạy theo súng; dao; độ cao nhảy; giá tiền; ba mức thu nhập; số mạng và thời gian mỗi trận; độ trễ hồi sinh; thời gian chờ và số lượt kỹ năng.

Phải theo dõi sát: có lối chơi nào lúc nào cũng đúng không (ví dụ ôm Heavy cộng Rào Chắn ngồi giữ góc); có cặp khắc chế nào định đoạt thắng thua ngay từ màn chọn đồ không; người chết có hiểu ngay vì sao mình chết không, đây vẫn là chỉ số quan trọng nhất; tỉ lệ bấm nhầm; và chuyện bên thắng càng lúc càng giàu.

## 14. Dữ liệu cần thu thập

Giữ từ trước: số mạng hạ và bị hạ từng người, thời gian hạ gục thực tế, độ chính xác theo khoảng cách, số lần dùng kỹ năng, thời lượng trận, số lần hồi sinh.

Thêm mới: số mạng theo từng khẩu súng kèm khoảng cách, để biết mỗi khẩu có sống đúng đất của nó không. Tỉ lệ chọn bên trong từng cặp súng cùng loại. Số lần bấm nhầm, số lần đổi súng, số lần chết ngay khi đang nạp hoặc đang đổi. Tần suất nhảy và số ca say VR đếm riêng cho nhảy. Quyết định mua hay để dành từng hiệp, và mối liên hệ giữa tiền với thắng thua. Thời gian từ mở game đến phát bắn đầu, cùng chỗ người chơi hay bỏ ngang trong chuỗi sảnh, chọn đồ, vào trận. Tỉ lệ người chơi xong Leo Súng tự sang chế độ khác ngay trong buổi. Khi có sniper: phần trăm số mạng của sniper và khoảng cách trung bình của các phát hạ gục.

## 15. Ba đợt chơi thử

Đợt 1, lõi Đấu Nhanh với bốn khẩu đại diện: Lục Chuẩn, SMG Nhanh, AR Chuẩn, Dao, cùng nhảy, đổi súng, nạp đạn, hai kỹ năng, tốc độ chạy theo súng. Bốn người, một bản đồ, đấu đến 12 mạng, hiệp đầu ai cũng bị khóa AR Chuẩn cho dễ so sánh. Đạt khi: thời gian hạ gục trung bình nằm trong khoảng 0.6 đến 1.0 giây, không ai say VR, bấm nhầm dưới hai lần một người một trận, ít nhất 60% số mạng do súng, SMG và AR mỗi khẩu ăn mạng ở đúng tầm của mình, và người chơi đòi thêm trận nữa. Trượt thì xử lý theo đúng cầu dao đã định: say vì nhảy thì tắt nhảy, nhầm nhiều thì đổi cách nạp, khẩu nào được chọn quá 70% thì làm yếu một nấc.

Đợt 2, Vòng Kinh Tế 2 đấu 2 với đủ 10 khẩu (chưa có sniper): thêm Lục Nặng, Lục Burst, SMG Burst, AR Mạnh, hai khẩu Heavy, shop, tiền, hiệp đấu, và kỹ năng Hơi Thở Thứ Hai. Đạt khi: có người tự nguyện chơi hiệp để dành tiền, đội thắng hiệp đầu không thắng cả trận quá 65%, 20 giây mua đồ không ai lúng túng, Heavy dưới 40% tổng số mạng, và mỗi cặp súng cùng loại được chọn trong khoảng 35 đến 65%.

Đợt 3, Leo Súng cộng toàn bộ hành trình, bản đồ mở rộng, chế độ ngắm, và hai khẩu sniper. Thử với bốn người chưa từng chơi, bắt đầu từ lúc mở game. Đạt khi: từ mở game đến phát bắn đầu dưới 90 giây, người mới chơi xong một trận Leo Súng nói lại được khẩu nào dùng làm gì, ít nhất nửa số người tự chơi tiếp chế độ khác, và sniper dưới 35% tổng số mạng nhưng có đất riêng ở trục dài.

## 16. Việc nào trước việc nào sau

Làm ngay (đợt 1): chọn tay thuận và nút đảo gương; súng dính tay; ba ô vũ khí với gạt cần để đổi; tốc độ chạy theo súng; bốn khẩu Lục Chuẩn, SMG Nhanh, AR Chuẩn và dao; nạp đạn bằng ấn cần cộng tự nạp; nhảy kèm cầu dao; hai kỹ năng Quét Xung và Rào Chắn hồi theo thời gian; chế độ Đấu Nhanh; shop đơn giản ở khu hồi sinh; bản đồ theo mục 11 (chưa cần trục dài); đo dữ liệu súng, bấm nhầm, nhảy.

Kế tiếp (đợt 2): Lục Nặng, Lục Burst, SMG Burst, AR Mạnh, hai khẩu Heavy; kỹ năng Hơi Thở Thứ Hai; trọn gói Vòng Kinh Tế 2 đấu 2 gồm tiền, mua đồ, hiệp, xử lý chủ phòng rớt mạng; giao diện chọn đồ bản đầy đủ; bảng điểm và bỏ phiếu chơi lại; sáu luật tình huống lạ.

Sau nữa (đợt 3 trở đi): Leo Súng; sảnh với ba cổng; hướng dẫn lần đầu; chế độ ngắm cộng bản đồ mở rộng cộng hai khẩu sniper; nạp đạn bằng cử chỉ thật; khẩu thứ 12 (ứng viên là shotgun); mua hộ đồng đội; menu hệ thống.

Bỏ hẳn: nhặt súng từ xác, giáp, sát thương dao theo tốc độ vung tay, thu nhập tăng theo chuỗi thắng, hệ đạn dự trữ, mọi chuyển động camera ép buộc ngoài nhảy, phần câu chuyện không gắn luật chơi, và toàn bộ cơ chế ném bóng cũ.

## 17. Mười điểm giao cho lập trình viên soi

Đây là đầu vào cho phase technical review. Tất cả là ý định thiết kế, cần đối chiếu với hành vi thật của hệ mạng Fusion, hệ cầm nắm AutoHand và hệ nhập liệu mới:

1. Khóa súng dính tay vĩnh viễn; báng trước của súng hai tay; hòa hướng ngắm hai tay sao cho khớp hệ tạo dáng tay đang giữ.
2. Chia vùng một cần gạt cho ba việc xoay, đổi súng, nạp đạn; đảo gương toàn bộ theo tay thuận.
3. Nút nhảy chạy đúng trên hệ di chuyển hiện có: kiểm tra chạm đất, và vị trí đồng bộ qua mạng khi đang lơ lửng.
4. Cho người khác nhìn thấy đúng khẩu súng mình đang cầm trong ba ô; trạng thái quay nòng và lên đạn chỉ cần tính trên máy người bắn.
5. Cách xác nhận trúng đạn giữ nguyên như trước: người bắn tự dò trúng trên máy mình, gửi một lệnh qua mạng, nạn nhân tự trừ máu. Áp dụng cho mọi khẩu và cả dao. Câu hỏi riêng: loạt burst 3 viên nên là ba lần dò liên tiếp hay gộp một lệnh?
6. Tốc độ chạy theo súng: áp ngay trên máy người chơi, có cần đồng bộ thêm gì không?
7. Tiền, hiệp, giai đoạn trận nằm trên máy chủ phòng theo cách hệ minigame cũ đã làm; chuyển quyền êm khi chủ phòng rớt; giữ ví tiền cho người rớt mạng vào lại.
8. Số lượt kỹ năng lưu qua mạng trên nhân vật.
9. Shop và màn chọn đồ điều khiển bằng con trỏ trong phòng hồi sinh; giữ ranh giới con trỏ ngoài trận, vật lý trong trận; cửa một chiều và 2 giây bất tử.
10. Chế độ ngắm trên kính Quest: ống kính thật (tốn sức máy, phải vẽ thêm cả một khung hình trong ống) hay khe ngắm sắt (không tốn gì)? Câu trả lời này quyết định thiết kế cả hai khẩu sniper.

## 18. Ba câu hỏi còn chờ bạn

1. Vòng Kinh Tế có bắt buộc đúng 2 đấu 2 không, hay chấp nhận 1 đấu 1 khi thiếu người? Câu này ảnh hưởng phòng hồi sinh và bảng thu nhập.
2. Xác nhận lần cuối về nhảy: đồng ý cầu dao "một ca say VR vì nhảy là tắt ngay", hay nhảy là thứ không được đụng tới?
3. Chế độ ngắm: chờ lập trình viên trả lời điểm 10 ở trên rồi mới chốt. Quyết định này định hình cả hai khẩu sniper.

Việc kế tiếp: giao mục 17 cho lập trình viên chạy technical review. Đợt 1 đã đủ chi tiết để soi ngay, không cần chờ ba câu hỏi trên có đáp án.
