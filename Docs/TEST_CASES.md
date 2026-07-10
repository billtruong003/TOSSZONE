# TOSSZONE — Test Cases & Edge Cases (rà bug hệ chiến đấu)

> Danh sách test case đầy đủ (normal + edge) cho hệ chiến đấu, cập nhật sau Session 14 (2026-07-05).
> Dùng cùng **prompt kiểm tra** ở đầu mục Session 14 trong `HANDOFF.md`. Nguồn hành vi: GDD_Core_Reference.md
> + code hiện tại. Khi số liệu lệch GDD → GDD thắng (báo là bug).

## Cách dùng
- Cột **Verify**: `MCP` = kiểm được headless qua Unity MCP (execute_code + recorder EditorPrefs) · `VR` = chỉ
  cảm/kiểm được trong headset · `2P` = cần 2 client thật (ParrelSync). Test `VR`/`2P` chỉ đọc code + note, đừng
  cố repro headless.
- Cột **Pri**: 🔴 nặng (crash/sai luật/stuck) · 🟡 vừa · ⚪ nhẹ/cosmetic.
- Setup chuẩn mỗi lần play 02_Arena: tắt `DummyBotDriver`, `RingSpawner._slotCount=0` + despawn hết ring
  (ring nuốt đạn test), `PlayerCombat.Local.HealCheat()`, check `Bill.IsReady` (half-state im lặng).
- ✅ = đã verify Session 14 · ⬜ = cần rà lại.

---

## A. Đạn & vũ khí (NetworkProjectile / HandWeapon / ThrowController)

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| WPN-01 | Bắn/ném từng vũ khí (8 món) | Bay đúng model, damage đúng bảng, không lỗi console | MCP | 🔴 |
| WPN-02 | Đá / Súng trúng mục tiêu | Damage + despawn, **KHÔNG cầu lửa** (non-explosive), `Exploded=false` | MCP | 🔴 ✅ |
| WPN-03 | Grenade/Bazooka/Nuke/BomX trúng | `Exploded=true` + cầu lửa + AoE damage mọi người trong bán kính | MCP | 🔴 ✅ |
| WPN-04 | **[EDGE] Ném/bắn — projectile nổ ngay điểm bắt đầu** | KHÔNG BAO GIỜ nổ ở start (throw-snap fix `_prevPosValid`) | MCP | 🔴 ✅ |
| WPN-05 | **[EDGE] Sau khi nổ, đếm NetworkProjectile** | Về 0 sau ~0.5s, KHÔNG để lại projectile stuck (drawcall thừa) | MCP | 🔴 ✅ |
| WPN-06 | **[EDGE] Ném explosive xuống chân mình** | Chủ nhân KHÔNG mất máu (Shooter loại trừ + arm-gate 0.7m) | MCP | 🔴 ✅ |
| WPN-07 | **[EDGE] Bắn thẳng vào tường sát mặt** | Nổ ở tường (sau clearance), không nổ ở nòng | MCP | 🟡 |
| WPN-08 | **[EDGE] Ném hụt (không trúng ai, không chạm đất)** | Despawn khi hết `_lifetime` (5s), không leak | MCP | 🟡 |
| WPN-09 | **[EDGE] Bắn nhanh liên tục (súng 0.1s × 20 phát)** | Pool không leak, không stuck, mỗi viên despawn đúng | MCP | 🟡 |
| WPN-10 | **[EDGE] Point-blank (địch cách <0.3m)** | Súng vẫn trúng từ tick 2 (không bỏ lỡ vì clearance) | MCP | 🟡 |
| WPN-11 | Nổ chạm ĐẤT (grenade thả) | Nổ tại điểm chạm sàn, không bay xuyên | MCP | 🔴 ✅ |
| WPN-12 | **[EDGE] Ném thẳng lên trời** | Rơi xuống, nổ khi chạm đất (không nổ trên đỉnh) | MCP | 🟡 |
| WPN-13 | isUncatchable (Súng/Power) | CatchController KHÔNG bắt được | MCP | 🟡 ✅ |
| WPN-14 | Ice shot trúng người | ĐÓNG BĂNG theo giây, **KHÔNG damage**, không thưởng kill | MCP | 🔴 |
| WPN-15 | Fire shot trúng → vùng lửa | Vùng lửa sống 1-3s, đi qua **mất 1 mạng/lần** | MCP | 🔴 |
| WPN-16 | Bom Chữ X nổ | 2 BuffZone HỘP xoay 45°/135°, 1.1m×5.64m, sống 3s, mất 1 mạng | MCP | 🟡 ✅ |
| WPN-17 | **[EDGE] Đá có aoeRadius 0.8 (GDD)** | Splash 0.8m nhưng KHÔNG explosive (không fireball) | MCP | 🟡 ✅ |
| WPN-18 | CatchController đọc Element trước Spawned | Guard `Object.IsValid` — không `InvalidOperationException` | MCP | 🟡 ✅ |
| WPN-19 | Laser sight (Gun/Bazooka) | LineRenderer đỏ từ nòng theo raycast; tắt khi đổi vũ khí khác | MCP | ⚪ ✅ |
| WPN-20 | **[EDGE] Đổi vũ khí giữa lúc đạn đang bay** | Đạn cũ vẫn resolve đúng, không ảnh hưởng | MCP | 🟡 |

### A2. Chuỗi mìn (LandMine)
| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| MINE-01 | Ném mìn → chạm đất | NẰM (kinematic, unlink twin, lifetime giãn 60s), KHÔNG nổ ngay | MCP | 🔴 ✅ |
| MINE-02 | Mìn nằm → chờ fuseDelay | ARM sau đúng fuseDelay (đo ~1s) | MCP | 🔴 ✅ |
| MINE-03 | Người KHÁC-shooter đạp lên mìn armed | Nổ AoE (đo -3 dmg) | MCP | 🔴 ✅ |
| MINE-04 | **[EDGE] Mìn đang bay (chưa chạm đất)** | KHÔNG proximity-nổ trên không | MCP | 🔴 ✅ |
| MINE-05 | **[EDGE] Chính shooter đứng lên mìn của mình** | Chốt luật: hiện tại loại trừ shooter → không nổ. Owner xác nhận có muốn nổ không | MCP | 🟡 |
| MINE-06 | **[EDGE] Mìn không ai đạp** | Despawn khi hết 60s lifetime | MCP | 🟡 |
| MINE-07 | **[EDGE] 2+ mìn cùng lúc** | Mỗi mìn arm/nổ độc lập, không leak | MCP | 🟡 |
| MINE-08 | **[EDGE] Mìn ném ra ngoài sân (không có đất bên dưới)** | Bay tới hết lifetime rồi despawn (không nằm lơ lửng) | MCP | 🟡 |

### A3. Effect nổ (ExplosionFx)
| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| FX-01 | Nổ 20 lần liên tiếp | Pool ≤8 fireball + ≤3 flash + **1 material chia sẻ**, không leak | MCP | 🟡 ✅ |
| FX-02 | Nuke (radius ≥3.5m) | Thêm flash point-light + haptic 0.5s | VR | ⚪ |
| FX-03 | **[EDGE] Nhiều nổ đồng thời (>8)** | Pool round-robin tái dùng, fireball cũ bị cắt sớm — chấp nhận | MCP | ⚪ |
| FX-04 | Fireball có collider không? | KHÔNG (destroy ngay) — không dính hit detection | MCP | 🟡 ✅ |
| FX-05 | **[EDGE] Đổi scene giữa lúc fireball đang chạy** | Root DontDestroyOnLoad — không lỗi MissingReference | MCP | ⚪ |

---

## B. Ring Buff (BuffRing / RingSpawner / BuffZone) — GDD §VI

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| RING-01 | Spawn 5 element | Đúng config/màu/label theo `config.element` (không index) | MCP | 🔴 ✅ |
| RING-02 | Tier 1-5 đường kính | Scale theo GDD (T1 1.8m → T5 0.6m), `DiameterForTier` | MCP | 🟡 ✅ |
| RING-03 | Tier 1-5 giá trị buff | Theo `valuePerTier` (Multi 2/4/8/12/15, Speed 1.2→2, Area 1.25→2.25...) | MCP | 🔴 ✅ |
| RING-04 | Quỹ đạo trôi ngang | PingPong X mép↔mép theo tốc tier, dY=dZ=0 | MCP | 🟡 ✅ |
| RING-05 | **Băng = FREEZE** | Đóng băng người, **KHÔNG damage**; **dính damage GIẢI băng** | MCP | 🔴 ✅ |
| RING-06 | **Lửa = mất 1 mạng/lần đi qua** | Vùng sống 1-3s theo tier (không phải 90s), per-entry | MCP | 🔴 ✅ |
| RING-07 | Speed → vận tốc bay THẬT | Đạn nhân tốc độ (đo 6→14.4 m/s), cả throw twin lẫn RB | MCP | 🔴 ✅ |
| RING-08 | Area → bán kính nổ | AreaScale nhân vào hit radius | MCP | 🟡 |
| RING-09 | **Stack cộng dồn tối đa 3 vòng/viên** | Xuyên 3 ring = 3 buff nhân dồn; ring thứ 4 pass-through không consume | MCP | 🔴 ✅ |
| RING-10 | **[EDGE] Đạn đã đủ 3 buff xuyên ring** | Ring KHÔNG bị consume (bay xuyên) | MCP | 🟡 ✅ |
| RING-11 | Anti-dup T4-T5 | Tối đa 1×T4 + 1×T5 đồng thời (bất kể element) | MCP | 🟡 ✅ |
| RING-12 | Weight spawn 3 cửa sổ | (65,25,8,2,0)/(38,26,20,10,5)/(20,25,25,20,10) — thống kê 5000 roll | MCP | 🟡 ✅ |
| RING-13 | Multi → burst mưa đạn | 1 viên → N viên data-oriented GPU instance | MCP | 🔴 ✅ |
| RING-14 | **[EDGE] Burst xuyên Multi ring (stack)** | Nhân Count, KHÔNG re-stack qua chính ring đang shrink (guard `IsConsumed`) | MCP | 🔴 ✅ |
| RING-15 | **[EDGE] Ring consume 1 lần duy nhất** | Trong 0.25s shrink không bị consume lại | MCP | 🔴 ✅ |
| RING-16 | Ring respawn theo slot | Slot trống → respawn sau `respawnDelay` | MCP | 🟡 |
| RING-17 | **[EDGE] x8 ring cùng lúc (training)** | Spawn 8 ring không lỗi/leak | MCP | 🟡 ✅ |
| RING-18 | Tường băng (BuffZone Ice) chạm | Freeze-on-touch 1 lần/người; đạn khác-spawner melt tường | MCP | 🟡 |
| RING-19 | Đạn nhuộm màu element sau xuyên ring | Sphere network + trail local đổi màu theo element | MCP | ⚪ ✅ |

---

## C. Match & Economy (ArenaManager / PlayerCombat) — GDD §II/IV

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| ECO-01 | Thu nhập thụ động | +$2/giây (đo ~1.97) | MCP | 🔴 ✅ |
| ECO-02 | Hạ 1 mạng đối thủ | Shooter +$5/mạng + bounty nạn nhân | MCP | 🔴 ✅ |
| ECO-03 | Shutdown bounty | Mỗi mạng lấy được +$2 vào bounty MÌNH; ai hạ mình được $5+bounty | MCP | 🟡 ✅ |
| ECO-04 | Mất 1 mạng | +$10 đền bù + **3s BẤT TỬ** | MCP | 🔴 ✅ |
| ECO-05 | **[EDGE] Hit lần 2 trong 3s bất tử** | Bị chặn hoàn toàn (không mất máu) | MCP | 🔴 ✅ |
| ECO-06 | **[EDGE] Bounty reset khi mất mạng** | Về 0 sau khi bị hạ | MCP | 🟡 ✅ |
| ECO-07 | Dummy (bot) mất mạng | KHÔNG nhận 3s bất tử (training bắn liên tục được) | MCP | 🟡 ✅ |
| ECO-08 | Ví reset $0 đầu HIỆP | ResetForRound = 0 | MCP | 🟡 ✅ |
| ECO-09 | **[EDGE] Respawn giữa hiệp** | `RestoreLives` — GIỮ ví + vũ khí (không reset $0) | MCP | 🔴 ✅ |
| MATCH-01 | Thời gian hiệp | 90s (không phải 120) | MCP | 🔴 ✅ |
| MATCH-02 | Bo3 | Thắng 2 hiệp = thắng trận | MCP | 🔴 ✅ |
| MATCH-03 | Nghỉ giữa hiệp | 5s | MCP | 🟡 ✅ |
| MATCH-04 | Đổi bên mỗi hiệp | Spawn side xoay theo parity Round (z +9→-9) | MCP | 🟡 ✅ |
| MATCH-05 | Mạng theo chế độ | 1v1=7, 2v2/3v3=5, 4v4/5v5=4 (`LivesForPlayerCount`) | MCP | 🔴 ✅ |
| MATCH-06 | Timeout → phân định | So **TỔNG MẠNG ĐỘI** (không phải cá nhân) | MCP | 🔴 ✅ |
| MATCH-07 | **[EDGE] Timeout hòa (tổng mạng bằng)** | Round Draw, không điểm | MCP | 🟡 ✅ |
| MATCH-08 | **[EDGE] Match 1-1-1** | Hòa Chung Cuộc (`MatchEndEvent.WinnerTeam=-1`) | MCP | 🟡 ✅ |
| MATCH-09 | Wipe Out | Quét sạch mạng đội địch → thắng hiệp ngay | MCP | 🔴 |
| MATCH-10 | **[EDGE] Solo player (1 người thật)** | KHÔNG spin Warmup→Playing→RoundEnd vô hạn | MCP | 🔴 ✅ |
| MATCH-11 | **[EDGE] Tất cả người thật chết cùng tick** | Xử lý đúng (không crash, phân định hợp lý) | MCP | 🟡 |
| MATCH-12 | Leftover hazard đầu hiệp | `ClearLeftoverHazards` despawn mìn/zone sót từ hiệp trước | MCP | 🟡 |

---

## D. PPU Ammo & Mua bán (PlayerCombat / WristWeaponSelector / CatchController) — T31

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| PPU-01 | Grab hologram PPU (Súng) | Mua 1 băng (-$2), ammo=10 | MCP | 🔴 ✅ |
| PPU-02 | Bắn hết băng | ammo 10→0 sau 10 phát | MCP | 🟡 ✅ |
| PPU-03 | Bắn khi hết đạn + đủ tiền | Tự nạp băng mới (-$2), ammo=9 | MCP | 🔴 ✅ |
| PPU-04 | **[EDGE] Bắn khi hết đạn + HẾT tiền** | Tịt, không bắn | MCP | 🔴 |
| PPU-05 | BuyOnce (Bazooka/Kiếm) | Mua 1 lần, đạn vô hạn tới hết hiệp | MCP | 🟡 |
| PPU-06 | Catch bóng | Thưởng đạn vào **slot đang cầm** (thường +1, power +2) | MCP | 🟡 |
| PPU-07 | **[EDGE] Ammo per-slot độc lập** | Đổi vũ khí giữ ammo riêng từng slot, quay lại còn nguyên | MCP | 🟡 |
| PPU-08 | TrainingMode | Bỏ qua toàn bộ cost + unlock | MCP | 🟡 ✅ |
| PPU-09 | **[EDGE] Grab hologram thiếu tiền** | "KHÔNG ĐỦ $" đỏ + haptic buzz, không equip | VR | 🟡 |
| PPU-10 | Unlock time | Slot khóa tới đúng giây `unlockTime`; hiện đếm ngược 🔒Xs | MCP | 🟡 |
| PPU-11 | **[EDGE] Reset hiệp** | AmmoSlots + Bounty + OwnedMask về 0 | MCP | 🟡 |

---

## E. HUD & Feedback (T28)

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| HUD-01 | Scoreboard | Tỉ số XANH-ĐỎ + hiệp + đồng hồ mm:ss/phase, live 2 mặt | MCP | 🟡 ✅ |
| HUD-02 | Announcer thắng/thua hiệp | Đúng text/màu theo team local + tỉ số | MCP | 🟡 ✅ |
| HUD-03 | Announcer match/hòa chung cuộc | THẮNG/THUA TRẬN / HÒA CHUNG CUỘC | MCP | 🟡 ✅ |
| HUD-04 | Announcer bị hạ / hồi sinh | BẠN BỊ HẠ / HỒI SINH | MCP | 🟡 |
| HUD-05 | Announcer đóng băng | BỊ ĐÓNG BĂNG Xs + haptic 2 tay | MCP | 🟡 ✅ |
| HUD-06 | Announcer bắt bóng / deflect | BẮT ĐƯỢC+đạn / DEFLECT! + haptic | MCP | 🟡 ✅ |
| HUD-07 | Wrist HUD ví + ammo | $ ví + đạn x/băng (PPU) / ∞ (BuyOnce/Đá) | VR | ⚪ |
| HUD-08 | **[EDGE] HUD khi chưa có rig/camera** | Null-safe, không lỗi (ẩn/skip) | MCP | 🟡 |
| HUD-09 | HealthUI 5 cục | Đổi theo Health, billboard camera | VR | ⚪ |
| HUD-10 | **[EDGE] Announcer nhiều event dồn dập** | Tween/fade không chồng lỗi | MCP | ⚪ |

---

## F. Ném (ThrowController) — CHỦ YẾU HEADSET

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| THR-01 | **Đẩy joystick tới lui (locomotion)** | KHÔNG ném (mốc head cancel + min swing 0.25m) | VR | 🔴 |
| THR-02 | Vung tay ra thật (>25cm) | Ném ra, hướng/lực theo cú vung | VR | 🔴 |
| THR-03 | **[EDGE] Flick nhẹ (<25cm)** | KHÔNG ném | VR | 🟡 |
| THR-04 | Default cầm = Đá (không phải bóng) | `MS_WP_Rock` trong tay | MCP | 🔴 ✅ |
| THR-05 | **[EDGE] Đổi Đá→Súng/Kiếm** | Không sót bóng generic trong tay | VR | 🔴 ✅(logic) |
| THR-06 | Vung tay lúc đang di chuyển | Ném đúng hướng vung (đã trừ locomotion) | VR | 🟡 |
| THR-07 | Player đóng băng | KHÔNG ném được (frozen gate) | MCP/VR | 🟡 |
| THR-08 | Grab pose vũ khí | Súng đã có pose tay phải; bazooka/kiếm chờ owner | VR | ⚪ |
| THR-09 | **[EDGE] Grip nhả liên tục nhanh** | State machine không kẹt (Empty↔Loaded) | VR | 🟡 |
| THR-10 | Cooldown ném | Refill sau `cooldown`, không spam quá tốc | VR | 🟡 |

---

## G. Networking 2-client (Shared Mode) — CẦN 2 MÁY (ParrelSync)

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| NET-01 | 2 player thấy nhau | Avatar sync, IK tay đúng | 2P | 🔴 |
| NET-02 | Bắn trúng nhau | Máu đồng bộ 2 phía | 2P | 🔴 |
| NET-03 | Đạn visual sync | VisualIndex → đúng model mọi client | 2P | 🟡 |
| NET-04 | **[EDGE] Non-master ném xuyên ring** | Buff áp qua RPC về authority đạn (cross-authority) | 2P | 🔴 |
| NET-05 | Đổi bên nhìn từ 2 phía | Cả 2 thấy mình đổi bên đúng | 2P | 🟡 |
| NET-06 | Round-end/respawn 2 máy | Đúng sân, đồng bộ | 2P | 🔴 |
| NET-07 | Freeze sync | Người bị băng khóa move ở cả 2 máy | 2P | 🟡 |
| NET-08 | PPU cross-client | Mua/nạp/catch đạn đúng phía mình | 2P | 🟡 |
| NET-09 | Hòa Chung Cuộc 1-1-1 thật | 2 máy đều thấy kết quả hòa | 2P | 🟡 |
| NET-10 | **[EDGE] Photon rate-limit** | Chạy 1 phiên dài thay vì connect/disconnect nhiều | 2P | ⚪ |
| NET-11 | **[EDGE] Dummy passive khi ≥2 người thật** | Bot tự tắt tấn công | 2P | 🟡 |

---

## H. Frozen / Status (PlayerCombat)

| ID | Kịch bản | Kỳ vọng | Verify | Pri |
|---|---|---|---|---|
| FRZ-01 | Ice trúng → freeze | `IsFrozen=true` theo giây tier | MCP | 🔴 ✅ |
| FRZ-02 | Freeze hết giờ | Tự giải đúng thời điểm (đo 2.0s cho 2s) | MCP | 🔴 ✅ |
| FRZ-03 | **Dính damage khi đang băng** | GIẢI băng ngay (RPC_TakeHit clear timer) | MCP | 🔴 ✅ |
| FRZ-04 | Freeze khóa locomotion | AutoHandPlayer.Move(0) khi frozen | VR | 🟡 |
| FRZ-05 | Freeze khóa ném/bắn/deflect | Gate ở ThrowController/HandWeapon | MCP/VR | 🟡 |
| FRZ-06 | **[EDGE] Freeze khi Health=0** | Không freeze (chết rồi) | MCP | 🟡 |
| FRZ-07 | **[EDGE] Freeze chồng nhiều lần** | Lấy Max thời gian, không cộng dồn vô hạn | MCP | ⚪ |

---

## I. Regression (bug ĐÃ fix — đảm bảo không tái phát)

| ID | Bug cũ | Commit fix | Test tái kiểm |
|---|---|---|---|
| REG-01 | Đạn tự nổ vào tay lúc buông | `35be6b2` | WPN-06 |
| REG-02 | Rock ra cầu lửa | `35be6b2` | WPN-02, WPN-17 |
| REG-03 | Ném ra bóng vàng generic | `00ee5eb` | THR-04 |
| REG-04 | Đổi vũ khí sót bóng | `00ee5eb` | THR-05 |
| REG-05 | Joystick tới lui = ném | `f94cfcd` | THR-01 |
| REG-06 | ExplosionFx tạo object/material mỗi nổ | `4e4b50b` | FX-01 |
| REG-07 | **Nổ tại điểm bắt đầu + projectile stuck** | `4c68117` | WPN-04, WPN-05 |
| REG-08 | Config ring element 5 index out-of-bounds | `3c5ceac` | RING-01 |
| REG-09 | Burst re-stack qua ring đang shrink → 4096 | `3c5ceac` | RING-14 |
| REG-10 | InvalidOperationException đọc Element trước Spawned | `83fac0c` | WPN-18 |
| REG-11 | RewardHit $10/hit (sai luật GDD) | `4629bcc` | ECO-02 |
| REG-12 | Announcer hô "thắng hiệp" nhầm đầu mỗi hiệp | `674e021` | HUD-02 |
| REG-13 | Catch không despawn NetworkProjectile — đạn "đã bắt" bay tiếp | `9b0774c` | WPN-13 + catch |
| REG-14 | Mìn nổ log Error "kinematic body" mỗi lần nổ | `e2c3524` | MINE-03 |
| REG-15 | Đạn nổ proximity/fire event cạnh XÁC đã chết | `e2c3524` | FRZ-06 |
| REG-16 | Freeze ngắn tới sau GIẢM thời gian băng còn lại (không Max) | `f4c013b` | FRZ-07 |
| REG-17 | Wipe Out đếm gộp 2 đội — 2v2+ quét sạch đội địch không kết hiệp | `a7e7b12` | MATCH-09 (2P còn nợ) |
| REG-18 | Scoreboard 2 mặt chồng khít cùng tọa độ (z-fight) | `e7d7a30` | HUD-01 |
| REG-19 | **Đạn ném treo lơ lửng vĩnh viễn** — Tween pool tái cấp + `.Kill()` ref cũ | `a4d6e0d` | WPN-05 + repro aliasing |
| REG-20 | Ném liên tiếp giết chéo twin network (slot đơn + event vô danh) | `a4ddceb` | NET-03 (2P còn nợ) |
| REG-21 | Label training range đè chữ + tên "Chắn" sót | `7361eb8` | scene hub |

---

## J. Bug mới owner báo từ playtest thật (2026-07-07 — ĐÃ FIX + VERIFY, commit `a587a93`)

| ID | Hiện tượng | Nguyên nhân | Fix đã áp dụng | Verify sau fix | Pri |
|---|---|---|---|---|---|
| PT-01 | Ring buff spawn quá thấp, sát/cắm đất | Zone box random Y không biết gì về bán kính ring — tier to (đường kính 1.8m) mép dưới có thể chỉ ~0.56m | `RingSpawner._minRingBottomY` (mới, =1m) — `RollSpawnPos()` kẹp `pos.y = max(pos.y, minBottomY + radius)` | 15× tier1 (bán kính 0.9m) spawn liên tiếp → minCenterY quan sát = **1.90** = đúng khớp `1 + 0.9` | ✅ |
| PT-02 | Bảng điểm giữa sân 2 mặt đè lên nhau, không đọc được (cả 2 phía) | TMP fontMaterial `_CullMode=0` (double-sided) — chữ mặt xa xuyên qua mặt gần dạng gương | `_CullMode=2` (Cull Back) set trên fontMaterial cả 2 mặt trong `CreateFace()` | Đọc lại `_CullMode` cả 2 mặt sau khi tạo = **2** | ✅ |
| PT-03 | Ring "kích hoạt" dù đạn không thực sự chạm/xuyên qua vòng | ① Đạn đơn: mesh `ColliderRing` convex hull sai scale ~100× (bounds gốc 211m) = đĩa đặc trùm map. ② Burst: so khoảng cách phẳng 0.4m tới TÂM, không xét hình học vòng | ① `Spawned()` phát hiện `MeshCollider` → thay bằng `BoxCollider` đúng kích thước prefab (`_prefabDiameter`), cộng double-check bán kính local-space trong `OnTriggerEnter`. ② `TryStackThroughRing` đổi sang plane-crossing thật (segment cắt mặt phẳng vòng + trong `OpeningRadius`) | Đạn bay cắt mặt phẳng cách tâm >10m → ring KHÔNG còn bị ăn (trước: 3 ring/1 phát). Bắn thẳng qua tâm → vẫn consume đúng (1→0). Burst spread=0 bay ngoài mặt phẳng → không stack; bay xuyên tâm thật → stack đúng | ✅ |
| PT-04 | Đạn mưa (burst) bay xuyên qua người, không ngưng khi trúng | `DeadMaskBits=256` < `MaxProjectilesPerBurst=4096` — pellet index ≥256 trúng damage nhưng `SetDeadBit` no-op nên không chết được | `SpawnBurst`/`TryStackThroughRing` clamp `Count` theo `DeadMaskBits` (256) thay vì 4096 — mọi pellet giờ luôn trong vùng trackable | `SpawnBurst(count=4096)` → `Count` thực tế = **256** (đúng clamp mới) | ✅ |
| PT-05 | Đạn mưa (burst) hiện hình bóng tròn generic thay vì Đá | `ProjectileBurstRenderer._mesh`/`_material` chưa từng được gán → fallback Sphere + material cam runtime | Gán `MS_WP_Rock.fbx` + `M_Pallet.mat` vào 2 field serialized trên scene object `[ProjectileBurstSystem]` (02_Arena) | Đọc lại field sau khi Play: `mesh=MS_WP_Rock material=M_Pallet instancing=True` | ✅ |
| PT-06 | Ném Đá — nổ/despawn giữa không trung ngang tầm người, CHƯA chạm đất | `HitFirstVictim()` dùng CHUNG 1 bán kính (`_hitRadius*AreaScale`=0.8m) cho cả "có chạm không" lẫn "damage AoE bao xa" — không arm-gate | Tách CONTACT check (bán kính gốc `_hitRadius`, cỡ viên đá) khỏi SPLASH apply (bán kính AoE, chỉ chạy SAU khi contact=true) | Bay cách dummy 0.6m (case y hệt lúc repro bug) → **bay xuyên qua, KHÔNG despawn** (trước: despawn + dummy mất máu oan). Bắn thẳng trúng → vẫn despawn + dummy mất máu (regression OK) | ✅ |

**Setup repro/verify chung (MCP):** tắt bot dummy, `RingSpawner._slotCount=0` + despawn ring cũ, spawn `DummyAvatar` làm mục tiêu (nhớ `HealCheat()` sau spawn — spawn ra Health=0), projectile spawn qua `Runner.Spawn(NetworkProjectile.prefab)` + `SetAoe(0.8)` + **`Shooter = LocalPlayer`** (để None sẽ trùng InputAuthority của dummy → bị skip như "chính chủ").

---

## Ghi chú edge case chưa chốt (hỏi owner)
- **MINE-05**: shooter đứng lên mìn của chính mình — hiện KHÔNG nổ (loại trừ shooter). Owner muốn nổ không?
- **WPN-10 vs clearance**: point-blank súng — clearance tick-đầu có bỏ lỡ địch cực gần không? Cần test thật.
- **Scale sân theo mode** (GDD §III): blockout 14×12 vs chuẩn 1v1 6×5 — chưa scale, ảnh hưởng RING trôi ngang +
  `crossZoneLength` Bom X (47% sâu sân). Là task riêng, không phải bug.
- **Bom X `crossZoneLength=5.64m`** tính theo sân sâu 12m — nếu scale sân thì phải tính lại theo mode.


---

## Session 17.14 — Bộ test case GAMEPLAY/UX mới (khác T1-T32 & các nhóm cũ; ưu tiên chơi thật 2 người)

> Nguồn: quét code CombatSession / ArenaManager / RingSpawner / BuffRingConfig / PlayerCombat / CatchController / WristWeaponSelector / WeaponHolder / MinigamePortal / RoomCodeConsole / ArenaQuickMenu / TrainingRangeController / HealthUI. Các case đánh dấu ⚠️ = nghi có bug/design-flaw thật từ code, test trước.

### REFF — Vòng buff có THỰC SỰ effective không? (đo lường, không chỉ pass/fail)
- [ ] REFF-01 ⚠️ **Hit-rate theo tier**: ném 10 quả mỗi tier T1→T5 (đường kính 1.8/1.5/1.2/0.9/0.6m, drift 1/1.5/2/2.5/3.5 m/s). Nếu T4-T5 <10% trúng → T5 (0.6m + 3.5m/s) gần như vô dụng, cần cân bằng lại.
- [ ] REFF-02 **T5 gần như không tồn tại**: weights 0-30s = {65,25,8,2,0} → T5 = 0%; 31-60s = 5%; 61-90s = 10%. Round 90s → kỳ vọng gặp T5 cực thấp. Chơi 3 match, đếm số lần thấy T5. Nếu ~0 → design hay bug?
- [ ] REFF-03 ⚠️ **Ice/Fire KHÔNG BAO GIỜ spawn trong arena**: `RingSpawner.AllowedElements = {Multi, Speed, Area}` — 2/5 element chỉ có trong training range. Xác nhận với owner đây là chủ đích GDD §VI hay sót.
- [ ] REFF-04 **Multi T5 = 15 đạn**: ném xuyên Multi T5 → 15 đạn burst: FPS 2 client, đạn có damage đúng, có catch được từng viên (CatchController poll)?
- [ ] REFF-05 **Xuyên 2 ring liên tiếp** (Speed rồi Area cùng đường bay): buff stack, ghi đè, hay chỉ nhận ring đầu? Ghi rõ hành vi thực tế.
- [ ] REFF-06 **Ném lob chậm xuyên ring**: bóng bay cầu vồng chậm qua ring — có nhận buff như ném thẳng mạnh không?
- [ ] REFF-07 **Ring drift ra mép zone**: quan sát 5 phút — có ring nào lơ lửng vị trí không ai ném tới được từ 2 phía spawn không (zone 8×1×4m)?
- [ ] REFF-08 **Độ cao ring vs người thấp**: `_minRingBottomY=1.6` + bán kính → tâm ring T1 ≈ 2.5m. Người chơi VR cao 1m5 đứng gần có góc ném xuyên nổi không?
- [ ] REFF-09 **2 người cùng xuyên 1 ring cùng lúc** (2 hướng ngược nhau): cả 2 được buff? Ring despawn 1 lần, respawn timer đúng 1 slot?
- [ ] REFF-10 **Ring respawn trong Warmup/RoundEnd**: dùng ring cuối round → 10s sau đang RoundEnd — ring mới có spawn và bị "ăn chùa" trước khi round sau bắt đầu?

### CTCH — Cơ chế bắt bóng
- [ ] CTCH-01 **Ammo cộng vào slot đang cầm**: bắt bóng thường +1 ammo vào `EquippedIndex` hiện tại — kể cả đang cầm vũ khí KHÁC loại bóng vừa bắt. Đúng design?
- [ ] CTCH-02 ⚠️ **EXPLOIT tự bắt bóng mình**: ném thẳng lên trời rồi tự bắt — `CatchController` không check shooter → +1 ammo (power +2) miễn phí vô hạn?
- [ ] CTCH-03 **Power catch 2 client**: bắt bóng element (tím) → +2 ammo, cả 2 máy thấy bóng biến mất êm (không nổ, không ghost).
- [ ] CTCH-04 **Race bắt đạn burst**: 2 người cùng đưa tay vào 1 viên đạn mưa (`RPC_RequestCatch`) — chỉ 1 người được ammo? Viên đạn chết trên cả 2 máy?
- [ ] CTCH-05 **Tunneling catch**: bóng 20m/s xuyên qua zone 0.15m trong ~1 frame — bắt được không hay lọt tay? (giống bug #3 collider vừa fix nhưng cho catch)
- [ ] CTCH-06 **Bắt khi frozen/invuln**: đang đóng băng hoặc vừa mất mạng (invuln 3s) — có được phép bắt bóng không? Hành vi mong muốn là gì?

### SEL — Wrist weapon shop (UX mua đồ giữa trận)
- [ ] SEL-01 **Mở nhầm panel giữa combat**: cúi nhìn xuống né bóng — cổ tay lọt cone 22°/1m → panel bật che tầm nhìn đúng lúc nguy hiểm?
- [ ] SEL-02 **Panel nhấp nháy ở mép cone**: giữ cổ tay đúng rìa góc mở/đóng (22°/38°, delay 0.8s) — có flicker không?
- [ ] SEL-03 ⚠️ **Conflict grip mua vs grip cầm bóng**: tay phải đang GRIP giữ bóng (WeaponHolder force-grab) → với sang SQUEEZE GRIP hologram để mua — có bị nhả bóng / mua nhầm / grab cả hai?
- [ ] SEL-04 **Không đủ tiền**: hologram denied — status label có nói rõ thiếu bao nhiêu $ không, hay người chơi không hiểu vì sao grab bị từ chối?
- [ ] SEL-05 **Spam prev/next**: poke 20 lần nhanh — index wrap đúng, hologram model cũ có bị leak/chồng không?
- [ ] SEL-06 **BuyOnce qua mạng sống & round**: mua weapon → chết → còn own (OwnedMask)? Sang round mới money reset $0 — mất own hay giữ?
- [ ] SEL-07 **Người thuận tay trái**: panel gắn cứng cổ tay TRÁI, throw tay phải — flow đảo tay có dùng được shop không?
- [ ] SEL-08 **Đang mở panel thì bị Ice/hit**: panel có kẹt mở / hologram kẹt giữa không?

### PRTL / FLOW — Vào-ra trận & các điểm người chơi dễ KẸT
- [ ] PRTL-01 **Vào portal do vô ý**: dwell 0.6s là ngắn — đi ngang qua portal có bị hút vào không? Có hiệu ứng/progress báo "đang vào" trong 0.6s đó không? (UX: cần telegraph)
- [ ] PRTL-02 **2 người cùng dwell 1 portal**: cả 2 vào cùng session arena hay tách phòng?
- [ ] PRTL-03 **Vào portal khi đang reconnect** (ConnectionFlow busy): kẹt màn đen / hub trống? Có message gì?
- [ ] FLOW-01 **Late join giữa round**: người thứ 3 vào match 1v1 đang đánh — team gán đúng, `NetMaxLives` đọc từ replicated (không phải RPC đã lỡ), HealthUI đúng số mạng?
- [ ] FLOW-02 ⚠️ **Master thoát giữa round**: StateAuthority migrate — Phase/PhaseTimer/Score giữ nguyên? RingSpawner còn respawn? (Shared Mode gotcha kinh điển)
- [ ] FLOW-03 **Cả team địch thoát giữa round**: `WinCheckGrace` 0.5s — round kết thúc THẮNG ngay hay treo? Người còn lại có bị kẹt trong arena một mình vô hạn?
- [ ] FLOW-04 **MatchEnd rồi sao nữa?**: hết best-of-3 — có auto về hub / rematch không, hay người chơi đứng trong arena chết không biết làm gì? (nghi thiếu flow)
- [ ] FLOW-05 **AFK suốt Warmup 5s**: đứng im — có rơi map / bị đẩy khỏi spawn point không?

### QMNU / ROOM — Menu thoát & console phòng
- [ ] QMNU-01 ⚠️ **Thao tác 2 tay bắt buộc**: panel VỀ HUB chỉ hiện KHI ĐANG GIỮ B (tay phải) → phải poke bằng tay TRÁI. Tay trái đang cầm/bắt bóng thì sao? Nhả B là panel biến mất — người chơi có kịp hiểu không?
- [ ] QMNU-02 **Panel sau lưng**: giữ B, xoay người 180° — panel cố định world-space nằm sau lưng, người chơi tưởng menu không hoạt động?
- [ ] ROOM-01 **Nhập mã sai**: gõ mã 5 chữ không tồn tại → VÀO PHÒNG — báo lỗi gì? Có bị treo "Đang kết nối..." vô hạn không?
- [ ] ROOM-02 **Nhập mã thiếu** (<5 ký tự) → VÀO PHÒNG: validate hay gửi luôn?
- [ ] ROOM-03 **Host chờ mãi không ai vào**: có nút hủy/quay lại quick play? Status "MÃ PHÒNG: XXXXX n/m" cập nhật đúng khi bạn vào/ra?
- [ ] ROOM-04 **Join phòng khi host đã vào arena**: bạn nhập mã lúc host đang giữa match — vào thẳng arena, chờ ở hub, hay lỗi?

### TRNG — Training range rò rỉ sang match
- [ ] TRNG-01 ⚠️ **TrainingMode leak**: equip free ở training range → đi thẳng vào portal arena — `TrainingMode` chỉ clear ở `OnDestroy` của hub object. Nếu không clear kịp: MUA ĐỒ 0đ TRONG MATCH. Verify giá trong match ngay sau khi rời training.
- [ ] TRNG-02 **2 CombatSession**: TrainingRange tự tạo `CombatSession(hub)` DDOL — vào arena (có thể có instance riêng) → 2 singleton đá nhau? Catalog resolve đúng?
- [ ] TRNG-03 **Ring burst tồn dư**: bấm nút burst 8 rings rồi vào match ngay — rings training có còn sống / đi theo vào session arena không?
- [ ] TRNG-04 **Client 2 trong hub bấm nút ring**: spawn là master-only — client bấm không có gì xảy ra và KHÔNG có feedback → tưởng game hỏng. Cần message?

### UXH — Hiển thị trạng thái khi chơi
- [ ] UXH-01 ⚠️ **7 mạng nhưng chỉ 5 pip**: 1v1 → `LivesForPlayerCount` trả 7, HealthUI chỉ có 5 pip sphere — máu 7 và 6 hiển thị y hệt 5? Người chơi không biết mình còn bao nhiêu mạng. (nghi bug hiển thị thật)
- [ ] UXH-02 **Bắn người đang invuln 3s**: người bắn có thấy hiệu ứng "MIỄN THƯƠNG" không hay tưởng hit không ăn (giống bug collider cũ) → report bug ảo?
- [ ] UXH-03 **Bị Ice đóng băng**: người bị băng thấy gì (overlay? timer?), có biết còn bao lâu tan không, có cách giãy thoát sớm không?
- [ ] UXH-04 **Bounty trên đầu**: người kill streak có bounty +2/kill — có hiển thị để người khác biết mà săn không? Nếu không hiển thị thì bounty vô nghĩa về mặt gameplay.
- [ ] UXH-05 **Money realtime**: income thụ động 2$/s — HUD tiền có nhảy liên tục không, hay chỉ update khi mua/kill khiến người chơi không biết mình đang giàu lên?
- [ ] UXH-06 **Biết tỉ số bằng cách nào**: giữa round, muốn xem ScoreA/ScoreB + round mấy/best-of — nhìn đâu trong VR? Đo thời gian người mới tìm ra scoreboard.

### ECO — Kinh tế trong round (nối tiếp ECO-01..04)
- [ ] ECO-05 **Income trong Warmup/RoundEnd**: `FixedUpdateNetwork` cộng 2$/s mọi lúc — xác nhận tiền có tick ngoài Playing không; reset $0 đúng thời điểm nào (mua trong warmup bằng tiền round trước được không)?
- [ ] ECO-06 **Trúng 1 quả mất 2 mạng** (damage 2): compensation nhận 20$ (10×2) đúng không? Kill reward người bắn tính 1 hay 2 lần?
- [ ] ECO-07 **Kill reward cộng local**: `RewardShooterLocal` chạy trên mọi client — verify shooter chỉ được +$ MỘT lần (không nhân đôi khi 3+ client cùng fire event).
