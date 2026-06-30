# TOSSZONE — Minigame Combat & Weapon Design (v2 — bản tổng hợp)

> **Status:** DESIGN (chưa code). **Consolidates** vision cũ + mới: `M4_Gameplay_Design.md` §2-5 (throw/ammo/teams),
> `tasks.json` §2.2 (buff rings) + §3.x (arsenal + economy), và toàn bộ buổi bàn game-design 2026-06-30
> (catch, kiếm, dash/jump, readability). Patterns: **Shmackle** + **BillGameCore**.
> Sister: `Network_Architecture_Lessons.md` · `AutoHand_Grab_Notes.md` · `Fusion_Shared_Mode_Gotchas.md`.

---

## 0. Thể loại & vòng lặp

**VR Dodgeball có RPS** (không phải "shooter có ném"). Ném/né/**bắt** là lõi; súng/bom/kiếm/buff-ring là gia vị.

```
Đánh trúng → +Tiền (theo thời gian + bonus mỗi hit; ví RESET $0 mỗi hiệp)
   → đủ tiền + vũ khí đã unlock theo thời gian → chọn trên carousel cổ tay
      → BuyOnce (giữ tới hết hiệp) / PayPerUse (mỗi phát tốn tiền)
         → ném/bắn → luồn qua VÒNG BUFF để cường hoá → trúng → trừ cục máu
            → đối thủ BẮT được (buff theo màu) hoặc CHÉM (kiếm) hoặc DASH né
```

---

## 1. Pillars / ràng buộc

1. **Configurable trên hết** — mọi vũ khí = 1 `WeaponConfig` (ScriptableObject + BillInspector). Thêm = tạo asset.
2. **Chỉ trong minigame** — hub sạch; bật/tắt theo `MinigameEnteredEvent`/`MinigameExitedEvent`.
3. **Fusion 2 Shared Mode, NO Physics Addon** — hit = trigger/raycast; damage = `Rpc(StateAuthority, All)` từ authority.
4. **BillGameCore-native** — data=SO; event=`Bill.Events`; VFX/text local=`Bill.Pool`; networked spawn=`Runner.Spawn`.
5. **AutoHand-native** — vũ khí là Grabbable; grab-from-hologram = `DispenserPoint`.
6. **2 tay** — ném/bắt/cầm được cả hai tay.
7. **Readability là luật** — nhiều modifier chồng lên 1 quả thì phải đọc được tức thì (§15).

---

## 2. Tam giác RPS (counter rõ ràng)

| Đòn | Bị khắc bởi |
|---|---|
| **Ném thường** | **Bắt** (grab giữa không) hoặc **Dash** né |
| **Ném-đã-bắt / Power throw** (tím, không bắt lại được) | **Kiếm chém** / **Dash** |
| **Súng (đạn)** | **Dash / núp** (KHÔNG bắt, KHÔNG chém được) |
| **Kiếm** (cận chiến) | kiting tầm xa; **chém hụt = ăn đòn** |
| **Bom AoE / vùng lửa/băng** | vị trí, tránh vùng |

Hai điểm-nóng KHÔNG-GIAN khác kiểu nhau: **buff-ring** (luồn đạn qua để buff — kỹ năng nhắm) vs **kiếm pickup** (1 vật phẩm tranh chấp trên map).

---

## 3. Scoping — "chỉ trong minigame"

| Cách | Dùng cho | Cơ chế |
|---|---|---|
| **Scene-scoped** | object riêng arena (portal, ring spawner, kiếm pickup, target) | đặt trong `02_Arena` → tự sống khi load |
| **Event-gated** | thứ trên **LocalPlayer (persistent)**: carousel cổ tay, UI máu, loadout | `CombatSession` nghe Entered→bật / Exited→tắt + reset |

---

## 4. Trạng thái combat (networked) — `PlayerCombat : NetworkBehaviour` (cùng object NetworkAvatar)

```csharp
[Networked] int Health;        // 5 cục; hit → -1
[Networked] int Money;         // +thời gian +hit; RESET 0 mỗi hiệp
[Networked] int OwnedMask;     // bitmask vũ khí BuyOnce đã mua (trong hiệp)
[Networked] int EquippedIndex; // vũ khí đang trang bị
[Networked] int Ammo;          // PayPerUse có magazine (optional)
```
Sống qua scene-load nhờ `NetworkAvatar.Local` static (Fusion mất player-registry khi load — xem gotchas).

---

## 5. Weapon system

### 5.1 `WeaponConfig : ScriptableObject` (mở rộng `ThrowConfig`)
```
id · displayName · icon
cost · acquireMode {BuyOnce|PayPerUse} · costPerUse · unlockTime(s) · cooldown · magazine
handSource {AppearInHand|GrabFromHologram} · heldPrefab
fireMode {ThrowBallistic|ProjectileLaunch|Hitscan|Melee} · throwConfig · projectilePrefab · muzzleSpeed · damage · aoeRadius
```
Map trực tiếp data 3.x: giá→cost, hồi đạn→magazine/ammo, AoE→aoeRadius, mốc unlock→unlockTime, internal cd 0.4s→cooldown.

### 5.2 Roster THẬT — `Assets/_Game/Models/` (M_Pallet đã gán ✅) — **7 vũ khí chiến đấu**

| Model `MS_WP_` | Vai trò | tasks.json 3.1 |
|---|---|---|
| **Rock** | Đá — starter $0, vô hạn (cũng dùng ở khán đài) | = Đá |
| **Gun** (+`Gun_Bullet`) | súng nhanh, đạn **uncatchable** | = Súng Viên $2 |
| **Grenade** | bom ném AoE | = Bom Nhỏ $5 |
| **RocketLaucher** (+`Rocket`) | bazooka, AoE lớn | = Bazooka $8 |
| **BigBoom** | bom diện rộng (finale) | = Bom Nguyên Tử $20 |
| **LandMine** | đặt mìn — zoning | ≈ Bom Chữ X $13 (chốt stats/role) |
| **⚔️ Kiếm** | pickup map, chém bóng (§9) | mới — **CHƯA có model** → import/tạo |

**KHÔNG phải vũ khí chiến đấu:**
- **Egg, Tomato** (+Rock) = **người ĐÃ CHẾT ném từ KHÁN ĐÀI** chọc người sống (heckle — §18.1).
- **Poop** = **cosmetic / trang phục mua ở shop** (Phase 3), không phải vũ khí.

> 6 model + Kiếm = **7 vũ khí**. 5 món map sạch tasks.json → **chỉ LandMine cần chốt stats** + **Kiếm cần model**.

### 5.3 Hai cách sở hữu (AcquireMode)
- **BuyOnce**: trả `cost` 1 lần → set bit `OwnedMask` → dùng free tới hết hiệp (ví reset → mua lại hiệp sau).
- **PayPerUse**: mỗi phát trừ `costPerUse`/`Ammo` → hết → **biến mất khỏi tay** + cue "hết đạn".
- *(Cần chốt: 6 món hiện tại cái nào BuyOnce, cái nào PayPerUse — §19.)*

---

## 6. Throw + **grab/catch arbitration** (chống overlap)

Vấn đề: hiện `ThrowBallHolder` cứ grip là spawn ammo → bóp grip để BẮT sẽ đẻ ammo đè lên bóng địch = overlap.
**Fix — grip context-sensitive, ammo là FALLBACK:**

```
Bóp grip:
 ├─ có bóng CATCHABLE trong catch-zone tay? → BẮT (despawn bóng địch, snap bóng-buff vào tay)   [ưu tiên]
 └─ không có gì để bắt + tay trống          → spawn ammo                                          [fallback]
```
**State tay:** `Empty → (grip+bóng tới) HoldingCaught | (grip+trống) HoldingAmmo → swing=ném → refill ammo (giữ grip) → release=ẩn`.
**Bóng bắt được THAY THẾ ammo** → luôn 1 quả tại 1 thời điểm → **không overlap**.
Refactor: `GrabBall()` tách `TryCatch()` (chạy trước) + `SpawnAmmo()` (chỉ khi `!caught && handEmpty`).

---

## 7. Catch — buff **random theo MÀU** (telegraphed)

Catch-zone = trigger sphere trên tay; bắt = overlap `NetworkProjectile` + grip. Mọi outcome **đều có lợi**, đọc qua màu:

| Màu bóng tới | Bắt được → |
|---|---|
| ⚪ Trắng | +tiền, bóng thường |
| 🔴 Đỏ | ném ra **nổ 3 chùm** |
| 🟡 Vàng | +**nhiều tiền** |
| 🟢 Lục | hồi **1 cục máu** |
| 🟣 Tím | **Power throw**: mạnh + **không bắt lại được** |

**Catch streak**: bắt liên tiếp không rớt → buff leo thang. Bảng màu→outcome = config (`CatchBuff` table).
**Networking:** quả tới là `NetworkProjectile` của người ném (authority họ) → bắt = despawn quả đó (RPC/authority) + spawn bóng-bắt-được trong tay catcher kèm buff màu (async authority — gotchas).

---

## 8. Uncatchable + ngôn ngữ hình ảnh (readability)

Không-bắt: **đạn súng**, **quả tím (Power throw)**, **spike-ball**.
- **Bắt-được** = xanh/mềm/xoay chậm/glow dịu. **KHÔNG-bắt** = **đỏ + gai + nhanh + trail rực**.
- Chạm tay quả uncatchable = **ăn đòn** (không catch). Đọc sai = ức chế → ưu tiên art rõ ràng.

---

## 9. ⚔️ Kiếm — pickup tranh chấp trên map (high risk/reward)

- **Đặt trên map**, ai cũng lụm được → **điểm nóng** cả sân tranh nhau (pattern "power-weapon spawn"). 1 vật phẩm, KHÔNG phải hệ-pickup-vũ-khí (nên không clash buff-ring).
- **Chém**: vung trúng bóng đang bay → **phá huỷ** (làm trước) → nâng cấp **deflect bật lại** (sau). Counter quả **không-bắt-được** (bắt không được thì chém). Cận chiến: chém người.
- **Chém hụt = dính đòn** → kỹ năng có rủi ro, không phải nút-block. Chém được **bóng** (kể cả Power throw), **KHÔNG chém đạn súng**.

---

## 10. 💍 Buff Rings (từ `tasks.json` 2.2) — lớp KỸ NĂNG NHẮM

Vòng **trôi trái↔phải** qua sân; **ném đạn XUYÊN qua vòng → buff đạn đó** (không nhặt). **Stack tối đa 3 vòng/viên.** Tier 1-5 (đường kính/tốc-độ-trôi/giá-trị/rarity theo cửa sổ 0-30/31-60/61-90s); Tier 4-5 hiếm + trôi nhanh.

**Model dùng chung** `MS_Circle_Lv1..Lv5` (5 tier — đã có + đã gán M_Pallet); **5 loại buff phân biệt qua config/màu**, KHÔNG phải 5 model riêng (đúng ý owner "vòng dùng chung, buff khác nhau").

| Vòng | Hiệu ứng | Dải |
|---|---|---|
| **Số Lượng (Multiplier)** | nhân đạn "mưa đạn" | x2/x4/x8/x12/x15 |
| **Tốc Độ (Velocity)** | đạn nhanh hơn | +20%→+100% |
| **Vùng (Area)** | bán kính nổ to | x1.25→x2.25 |
| **Băng (Ice)** | đóng băng + **tường băng** (đến hết Tier; dính dmg thì tan) | theo Tier |
| **Lửa (Fire)** | sau nổ tạo **vùng lửa** ai qua mất 1 mạng (đến hết Tier) | theo Tier |

**Compose với catch:** catch = **trục thuộc-tính** (chùm/tiền/máu); ring = **trục nguyên-tố/nhân-số** → bắt quả đỏ (3 chùm) **rồi** luồn vòng Lửa = **3 quả lửa**. Combo đã đời mà vẫn đọc được.

---

## 11. Carousel cổ tay (chọn vũ khí) — reconcile M4 ammo-selector

M4 §3 đã có **"ammo selector tay trái: carousel hologram, scroll lên/xuống, grab vào hologram để chọn"** + "nạp vũ khí khi **đưa tay ra sau đầu**". = chính cái wrist selector.
- **v1 (làm trước):** nút tới/lui (hoặc scroll thumbstick) cycle catalog trên watch curved (`CurvedUI.shader`); hiện icon+giá+sở-hữu/đủ-tiền+trạng-thái-unlock. Confirm = mua/trang bị/arm.
- **v2 (sau):** đưa tay vào tầm nhìn → bung hologram → **pinch/grab** chọn (`DispenserPoint`).
- Stick đang bận (move/turn/dash) → radial phải **mở bằng giữ nút**; ít vũ khí thì nút tới/lui đơn giản hơn.

---

## 12. UI máu — 5 cục curved

`HealthUI` (local, cổ tay/ngực), 5 segment rời (Shmackle dùng fill liên tục → mình đổi 5 cục), billboard về cam, bind `[Networked] Health`, mỗi hit −1 cục + flash. 0 cục = loại → respawn.

---

## 13. Economy

`PlayerCombat.Money` (`[Networked]`, **reset $0 mỗi hiệp**):
- **+ thời gian** (FixedUpdateNetwork authority) + **+ bonus mỗi hit** (trong RPC_OnHit).
- **Gate:** mua BuyOnce (−cost), bắn PayPerUse (−costPerUse/−Ammo). Vũ khí còn gate theo **unlockTime** (mở dần trong hiệp → escalation).
- **Feedback:** text tiền/damage bay (Bill.Pool local mỗi client khi RPC hit) — pattern `RewardShowerController`.

---

## 14. Controls (full scheme)

| Input | Hành động | Lưu ý |
|---|---|---|
| Stick trái | Move | |
| Stick phải L/R | Turn | |
| **Stick phải ↓** | **Dash** | deadzone chặt (y<−0.8 & |x| nhỏ) kẻo dash nhầm; *hoặc double-tap stick move* |
| **A** | **Jump** (hop nhỏ) | jump VR nhân tạo dễ say → hop nhỏ; dash > jump cho fantasy né |
| Grip | grab vũ khí + **bắt** | §6 arbitration |
| Trigger | bắn súng | (ném = cử chỉ, không nút) |
| Nút tới/lui (hoặc radial-giữ-nút) | chọn vũ khí trên carousel | §11 |

---

## 15. Luật readability (modifier chồng nhau)

1 viên có thể mang: màu-catch + ≤3 buff-vòng + loại-vũ-khí = nhiều thứ. Giữ sạch:
- **2 trục khác nhau**: catch=thuộc-tính lõi, ring=nguyên-tố/nhân-số → compose không đè.
- **Visual tách**: catch=màu-lõi/glow quả; ring=FX nguyên-tố quay quanh. **Cap** tổng modifier hiển thị.

---

## 16. 2-hand throw
Refactor `_rightHand` bool → mỗi tay 1 `HandWeapon` (+ `ThrowController`). Grip/cò/bắt đọc riêng từng tay. Loadout gán vũ khí theo tay.

## 17. Bot test MP
`DummyAvatar` (Runner.Spawn): NetworkAvatar giả + driver (đi + ném) → test S2/S3 + hit + health/economy + **catch** SOLO, khỏi 2 Quest.

## 18. Điều kiện thắng / round — **CHỐT 2026-06-30**
**3 chế độ:**
- **1v1 (duel):** ai **chết trước = THUA** (đối thủ win). Hiệp ngắn, sống-chết 1 mạng.
- **BO3 (Best of 3):** **team-based, last-man-standing** — team còn người sống cuối cùng **thắng hiệp**; thắng **2/3** hiệp = thắng trận.
- **BO5 (Best of 5):** như BO3, thắng **3/5** hiệp.

**Mỗi hiệp:** ví **reset $0**; vũ khí unlock dần theo thời gian → leo thang tới **finale bom**. `MinigameManager` (networked, MG2) giữ `[Networked] Mode/Round/ScoreA/ScoreB/Phase`.
*(Còn cần chốt: respawn trong hiệp hay **one-life/round**? Team size BO3/BO5 = 3v3 / 5v5?)*

### 18.1 Người chết → KHÁN ĐÀI (heckle) — engagement cho người đã loại
Chết trong hiệp → ra **khán đài** → ném **Egg / Tomato / Rock** vào người còn sống để chọc (vui, social — đúng chất Gorilla-Tag). Giữ người đã loại bận rộn thay vì ngồi không. *(Chốt: heckle chỉ **visual splat che màn 1 nhịp**, hay có **nuisance nhẹ** — che tầm nhìn/làm chậm?)*

---

## 19. Quyết định cần chốt
| # | Quyết định | Default |
|---|---|---|
| 1 | LandMine stats + Kiếm model + Egg/Tomato heckle-effect | 5 vũ khí map sạch; Poop=cosmetic, Egg/Tomato=khán đài |
| 2 | 6 món hiện tại: cái nào BuyOnce / PayPerUse? | Đá=free; còn lại nghiêng PayPerUse (ví reset/hiệp) |
| 3 | Tiền+máu networked? | **Networked** |
| 4 | Đồng hồ tay nào + nút | Tay không thuận; tới/lui = 2 nút mặt |
| 5 | Catalog per-minigame/global | **Per-minigame** |
| 6 | Hết tiền/đạn: biến mất/báo | **Biến mất + cue** |
| 7 | Win condition / round (§18) | ✅ **CHỐT: 1v1 (chết trước thua) / BO3 / BO5 team last-standing; ví reset/hiệp** |

---

## 20. Task breakdown (phase, khớp build-order)

**Phase A — Foundation** ⭐: A1 transfer throw vào `02_Arena` · A2 `DummyAvatar` bot · A3 throw 2 tay.
**Phase B — Combat core**: B1 `PlayerCombat`+`CombatSession` · B2 hit/damage RPC (=MG1)→máu-1+reward+juice · B3 HealthUI 5 cục · B4 economy (thời-gian+hit+reset/hiệp+unlockTime).
**Phase C — Weapon + catch + ring**: C1 `WeaponConfig`+catalog+roster (ball=#0) · C2 carousel cổ tay v1 (buy/equip/arm, reconcile M4) · C3 **catch** (màu+arbitration+catch-zone+net) · C4 súng/bom (PayPerUse, AoE, uncatchable) · C5 **buff rings** (5 loại, xuyên-vòng, stack 3).
**Phase D — Depth/polish**: D1 **kiếm** pickup (chém bóng, whiff-punish) · D2 **win-condition/round** (§18) · D3 carousel v2 pinch · D4 thêm vũ khí · D5 juice T3 + VFX nguyên-tố + readability pass.

## 21. File map
**Mới (TossZone.Combat):** `WeaponConfig` · `PlayerCombat`(NB) · `CombatSession` · `HandWeapon` · `CatchController`/catch-zone · `WristWeaponSelector` · `WeaponDispenser` · `SwordPickup` · `BuffRing`+`RingSpawner` · `HealthUI` · `RewardText` · `DummyAvatar`.
**Sửa:** `ThrowController`/`ThrowBallHolder` (per-hand + arbitration) · `NetworkAvatar.prefab` (+PlayerCombat) · `MinigameDef` (+WeaponCatalog) · `NetworkProjectile` (+trigger hit→RPC_OnHit, +catchable color).
**Tái dùng:** ThrowController/Config/Projectile · PlayerRig · NetworkAvatar · CurvedUI/CurvedTMP shader · MinigameManager/Def/Events/Portal.
**Shmackle ref:** WeaponDataObject · BasePlayerWeapon · DispenserPoint · ShmackleQuickSlot · HealthBarController · RewardShowerController · PlayerHealthSimple (damage RPC).
