# TOSSZONE! — GDD Core Reference (chép từ PDF chính chủ, 2026-07-02)

> **ĐÂY LÀ NGUỒN CHÂN LÝ (canonical).** Khi `Combat_Minigame_Design.md` hay code mâu thuẫn với file này
> → file này thắng. Chép nguyên số liệu từ GDD PDF owner đưa; phần ⚠️ là ghi chú lệch so với code hiện tại.

## I. Concept
VR Party Game. 2 đội 2 bên sân, ngăn bởi khoảng hở trung tâm. Tích tiền mua vũ khí, dùng lực tay thật ném
xuyên **Vòng Buff** bay lơ lửng để nhân số lượng / bọc nguyên tố, dội bom AoE xuống sân đối phương trừ mạng.

## II. Trận đấu & luật
- **Bo3** (thắng 2 hiệp). Hiệp = **90 giây** đếm ngược cứng ⚠️(code: 120s). Nghỉ **5s** giữa hiệp, đổi bên +
  bảng điểm ⚠️(code: 4s, không đổi bên, không bảng điểm).
- Ví reset **$0** đầu mỗi hiệp ✅ code có.
- **Mạng (Life Pool) theo chế độ:** 1v1 = **7 mạng**/người · 2v2 & 3v3 = 5 · 4v4 & 5v5 = 4
  ⚠️(code: `MaxHealth=5` cố định mọi chế độ).
- **Khán đài (Linh hồn):** hết mạng → bay lên khán đài, ném đồ vô hại (cà chua, trứng thối) gây nhiễu
  ⚠️(chưa build — prefab MS_WP_Egg/Poop/Tomato có sẵn).
- **Phân định:** Wipe Out (quét sạch mạng) · Time Out Win (hết 90s, **tổng mạng ĐỘI** cao hơn thắng
  ⚠️code so máu CÁ NHÂN cao nhất) · Round Draw (bằng nhau) · Match: thắng 2 hiệp; 3 hiệp hòa kiểu 1-1-1 =
  **Hòa Chung Cuộc** ⚠️(code không có khái niệm hòa trận).

## III. Kích thước sân (MỖI BÊN, theo chế độ)
| Chế độ | Ngang (W) | Sâu (D) | Diện tích |
|---|---|---|---|
| 1v1 | 6m | 5m | 30m² |
| 2v2 | 9m | 7m | 63m² |
| 3v3 | 12m | 9m | 108m² |
| 4v4 | 15m | 11m | 165m² |
| 5v5 | 18m | 13m | 234m² |

⚠️ Blockout T16 hiện tại: 14×12/bên = to hơn sân 1v1 GDD (6×5) rất nhiều — cần scale lại theo chế độ.

## IV. Kinh tế
- Thụ động: **+$2/giây** ⚠️(code +$1/s).
- **Hạ đối thủ: +$5** ⚠️(code: +$10 MỖI PHÁT TRÚNG — sai cả giá trị lẫn điều kiện).
- **Đền bù:** mất 1 mạng → **+$10 và 3 giây BẤT TỬ** ⚠️(code: không có cả hai).
- **Shutdown:** mỗi mạng giết được, giá trị mạng người đó +$2 (bounty) ⚠️(chưa có).

## V. Vũ khí (6 món — Internal Cooldown 0.4s)
| Vũ khí | Giá | Hồi đạn | AoE | Mở bán |
|---|---|---|---|---|
| Đá (Stone) | $0 | 0.4s | 0.8m | 0s |
| Súng Viên (Pellet Gun) | $2 | 0.1s | 0.35m | 1s |
| Bom Nhỏ (Small Grenade) | $5 | 1.0s | 1.5m | 5s |
| Bazooka | $8 | 1.2s | 2.5m | 10s |
| **Bom Chữ X (Cross-Bomb)** | $13 | 2.3s | vệt lửa chữ X: rộng 1.1m, dài 47% chiều sâu sân | 20s |
| Bom Nguyên Tử (Nuke) | $20 | 3.0s | 4.5m | 45s |

⚠️ Lệch code: giá/mở bán khác hết (Gun $15@0s, Bazooka $20@30s, BigBoom $25@60s...). **Bom Chữ X chưa
tồn tại trong code.** Code có thêm **Sword + LandMine** KHÔNG có trong GDD này (có trong
Combat_Minigame_Design cũ — ❓owner chốt: giữ như extension hay bỏ?). Đá/Súng cũng có AoE nhỏ (0.8m/0.35m).

Vai trò (tóm): Đá = warm-up/free · Súng = cấu rỉa 0.1s (+ Vòng Tốc Độ = tia không né được) · Bom Nhỏ = quấy
nhiễu · Bazooka = phá thế thủ góc khuất · Bom X = cắt đường chạy (khắc chế nhấp nhả) · Nuke = dứt điểm
(+ Vòng Kích Thước x2.25 = phủ gần cả sân).

## VI. Vòng Buff — 5 LOẠI (KHÔNG CÓ SHIELD!)
1. **Số Lượng (Multiplier)** — nhân đạn thành mưa.
2. **Tốc Độ (Velocity)** — đạn bay nhanh hơn, ép phản xạ.
3. **Băng (Stunner)** — **ĐÓNG BĂNG người dính (KHÔNG sát thương)**; tạo tường băng, ai chạm bị đóng băng
   theo thời gian tier; **dính sát thương thì băng GIẢI TRỪ**; tường tồn tại hết thời gian Tier.
4. **Lửa (Zoning)** — sau khi gây sát thương tạo vùng lửa = phạm vi vụ nổ; **ai đi qua MẤT 1 MẠNG**; tồn tại
   hết thời gian Tier.
5. **Tăng Kích Thước (Area Expansion)** — nhân bán kính nổ (x1.25 → x2.25).

⚠️ Code sai: element 5 đặt tên **Shield** (`shieldSelf`) — không tồn tại trong GDD, phải là **Area**.
⚠️ Băng trong code (BuffZone) đang GÂY DAMAGE khi chạm — GDD nói Băng KHÔNG damage, chỉ đóng băng.
⚠️ Lửa trong code = DoT tick mỗi giây + sống 90s — GDD = mất 1 mạng/lần đi qua, chỉ sống 1-3s theo Tier.

### Quy tắc vận hành vòng
- **Quỹ đạo: trôi liên tục từ mép sân TRÁI sang PHẢI (hoặc ngược lại)** ⚠️(code T9: wander Perlin ngẫu nhiên
  trong box — ❓owner từng mô tả miệng "di chuyển random ở giữa", GDD nói trôi ngang: cần chốt 1 trong 2).
- **Stacking: tối đa 3 vòng** áp lên cùng 1 viên nếu ném xuyên cả 3 ⚠️(code dùng Max() = không cộng dồn).
- **Số vòng tối đa:** `S_max = 3 + floor((diện_tích_một_bên − 30)/35)` → 1v1=3 ✅ khớp config hiện tại, 5v5=8.
- **Chống trùng:** tối đa **1 vòng Tier 4** và **1 vòng Tier 5** cùng lúc (2 vòng T4, hay 2 vòng T5 cùng lúc là
  cấm — cùng TÊN khác tier thì được) ⚠️(code T11 đang chặn theo cùng-element-tier≥4 — sai rule).

### MA TRẬN TIER (giá trị buff DO TIER quyết định)
| Tier | Đường kính | Tốc độ trôi | Số Lượng | Kích Thước | Tốc độ đạn | Băng (đóng băng/tường) | Lửa | Tỷ lệ (0-30s \| 31-60s \| 61-90s) |
|---|---|---|---|---|---|---|---|---|
| 1 | 1.8m | 1.0 m/s | x2 | x1.25 | +20% | 1s / 1s | 1s | 65% \| 38% \| 20% |
| 2 | 1.5m | 1.5 m/s | x4 | x1.5 | +40% | 1.5s / 1.5s | 1.5s | 25% \| 26% \| 25% |
| 3 | 1.2m | 2.0 m/s | x8 | x1.75 | +60% | 2s / 2s | 2s | 8% \| 20% \| 25% |
| 4 | 0.9m | 2.5 m/s | x12 | x2 | +80% | 2.5s / 2.5s | 2.5s | 2% \| 10% \| 20% |
| 5 | 0.6m | 3.5 m/s | x15 | x2.25 | +100% | 3s / 3s | 3.0s | 0% \| 5% \| 10% |

⚠️ Code: BuffRingConfig 1 giá trị/element (RC_Multi.multiplier=40 — GDD max x15!); tier không scale giá trị,
không scale đường kính; bảng weight T11 là placeholder ≠ số GDD.

## VII. Lobby / Out-game (GDD phần 2 — backlog epic)
- Hub = **không gian 3D tương tác** (đấm/ném/kéo vật thể thay vì bấm UI phẳng).
- **Matchmaking:** đấm nút HOST → Room Code 5 chữ · Join bằng ném khối chữ cái / bàn phím hologram ·
  Quick Play = đứng vào Teleport Pad.
- **Waiting Room:** Host Control Panel (cần gạt/núm xoay: mode 1v1-5v5, size sân tự khóa theo mode, map
  theme) · **chia đội = đứng vào vùng màu** (Xanh trái / Đỏ phải / Trung lập giữa) · bảng đếm số người.
- **Ready:** đập nút hologram trước mặt → avatar sáng/tick xanh. START của host chỉ mở khi: đội cân bằng +
  100% người trong vùng đội đã READY. Transition: blackout/sàn mở → load 2-3s → vào sân → $0 + 90s.
- **Pre-match:** voice chat proximity, đập tay high-five · Wardrobe (gương + kéo thả skin) ·
  **Khu Khởi Động (Warm-up Target): máy bắn bóng + vòng buff vô hại** ← T25 training range trùng khớp ý này.
