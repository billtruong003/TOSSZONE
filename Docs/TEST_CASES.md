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

## J. Bug mới owner báo từ playtest thật (2026-07-07 — CHƯA fix)

| ID | Hiện tượng | Nguyên nhân (file:line) | Hướng fix đề xuất | Pri |
|---|---|---|---|---|
| PT-01 | Ring buff spawn quá thấp, sát/cắm đất | **Đã live-check lại (MCP solo 02_Arena):** `ZoneCenter` thực tế = `RingSpawn_1.position.y = 1.8m` (scene arena), ring quan sát được y=1.64-2.09m — KHÔNG chạm đất, ngược với suy đoán ban đầu (code-only). Nghi vấn thật nằm ở **training range hub**: `RingSpawnerHub.prefab` có `_zoneCenter: {fileID: 0}` (NULL) → fallback `transform.position` = vị trí spawn runtime tại `_ringSpawnerAnchor` (local y=2.3 trong scene 01_Main) — CHƯA live-verify được ở hub (chỉ check được arena). Cần verify lại đúng chỗ owner chơi (arena match hay training hub) trước khi kết luận nguyên nhân | Nếu là hub: gán `_zoneCenter` cho prefab `RingSpawnerHub` (đang null) thay vì để fallback theo anchor. Nếu là feel/tune (không phải bug logic): đơn giản nâng zoneCenter cao hơn cho dễ với/ném trong VR | 🟡 |
| PT-02 | Bảng điểm giữa sân 2 mặt đè lên nhau, không đọc được (cả 2 phía) | `ScoreboardUI.cs:17-44` — fix cũ REG-18 chỉ offset 2 mặt ±1cm dọc Z (không đủ ở khoảng cách nhìn thật), KHÔNG có backing panel chắn/culling giữa 2 mặt, và `Update()` gán CÙNG 1 string cho cả `_front`/`_back` — không phải lỗi vị trí đơn thuần mà là thiếu vật chắn + không tách nội dung theo phía | Tách hẳn 2 GameObject bảng riêng (mỗi phía 1 board vật lý, cách nhau đủ xa hoặc quay lưng vào nhau qua 1 tấm chắn dày), hoặc theo đề xuất của owner: chỉ 1 bảng, hiện thông tin theo local-viewer (world-space Canvas billboard riêng mỗi client, không phải geometry chung 2 mặt) | 🔴 |
| PT-03 | Ring "kích hoạt" dù đạn không thực sự chạm/xuyên qua vòng | Đạn đơn (`NetworkProjectile`) qua `BuffRing.OnTriggerEnter` — ĐÚNG, có collider thật. Nhưng đạn MƯA (burst) qua `ProjectileBurstSystem.TryStackThroughRing` (`ProjectileBurstSystem.cs:253-281`) chỉ so khoảng cách phẳng 0.4m tới TÂM ring (`ring.transform.position`), không xét hình học/hướng vòng thật — trúng dù bay ngoài miệng vòng | Đổi check burst sang so với mặt phẳng vòng thật (normal + bán kính trong/ngoài), hoặc raycast/OverlapBox theo hướng vòng thay vì khoảng cách tới tâm | 🟡 |
| PT-04 | Đạn mưa (burst) bay xuyên qua người, không ngưng khi trúng | `ProjectileBurstSystem.cs` — `DeadMaskBits=256` nhưng `MaxProjectilesPerBurst=4096`; `SetDeadBit` no-op cho index ≥256 (`:70-78`), trong khi hit-test scan HẾT `b.Count` (`:222-243`) — pellet index ≥256 vẫn trúng damage (RPC bắn đúng) nhưng không đánh dấu chết được nên **render tiếp tục bay xuyên** + có thể trúng lại nhiều lần | Tăng `DeadMaskBits` lên khớp `MaxProjectilesPerBurst` (hoặc dùng `BitArray`/`NativeBitArray` không giới hạn 256), hoặc giới hạn `scan` theo `DeadMaskBits` thay vì `b.Count` | 🔴 |
| PT-05 | Đạn mưa (burst) hiện hình bóng tròn generic (ThrowBall) thay vì Đá | `ProjectileBurstRenderer.cs:23-59` — 1 `_mesh`/`_material` CỐ ĐỊNH cho toàn bộ renderer, fallback primitive Sphere + vật liệu cam nếu chưa gán tay; `ProjectileBurstSystem`/`Burst` struct không hề mang `WeaponConfig`/visual index — hoàn toàn tách biệt khỏi `WeaponVisuals.SpawnProjectileVisual` mà đường ném đơn dùng | Thêm field visual/prefab vào `Burst` struct (hoặc field mesh/material trên `ProjectileBurstRenderer` set từ `WeaponConfig` của vũ khí gây nổ Multi), fallback mới nên là model Đá thay vì sphere cam | 🟡 |
| PT-06 | Ném Đá — nổ/despawn giữa không trung ngang tầm người, CHƯA chạm đất | `NetworkProjectile.HitFirstVictim()` (đường non-explosive, Đá dùng) — `OverlapSphereNonAlloc(transform.position, _hitRadius*AreaScale=0.8m, ...)` chạy MỌI tick từ lúc thả tay, KHÔNG có arm-gate/khoảng cách tối thiểu (khác đường `_explosive` có `IsArmed()`/`MinArmDistance=0.7m` ở dòng 344) — chỉ cần bay gần bất kỳ ai khác (đồng đội/dummy/người ngoài) trong 0.8m là despawn ngay, giống hệt "nổ" sớm | Thêm arm-gate tương tự `IsArmed()` (khoảng cách tối thiểu từ `_origin`) vào nhánh non-explosive trước khi cho `HitFirstVictim()` chạy, hoặc giảm hit-radius hiệu lực trong N tick đầu | 🔴 |

---

## Ghi chú edge case chưa chốt (hỏi owner)
- **MINE-05**: shooter đứng lên mìn của chính mình — hiện KHÔNG nổ (loại trừ shooter). Owner muốn nổ không?
- **WPN-10 vs clearance**: point-blank súng — clearance tick-đầu có bỏ lỡ địch cực gần không? Cần test thật.
- **Scale sân theo mode** (GDD §III): blockout 14×12 vs chuẩn 1v1 6×5 — chưa scale, ảnh hưởng RING trôi ngang +
  `crossZoneLength` Bom X (47% sâu sân). Là task riêng, không phải bug.
- **Bom X `crossZoneLength=5.64m`** tính theo sân sâu 12m — nếu scale sân thì phải tính lại theo mode.
