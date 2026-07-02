# TOSSZONE — Chi tiết từng task còn lại

> Bản chi tiết "làm gì" cho mỗi task chưa xong. Danh sách tổng + trạng thái: `TASKS_MASTER.md`.
> Trạng thái tổng: `HANDOFF.md`. Thiết kế combat: `Combat_Minigame_Design.md`. Đạn mưa: `Burst_Projectile_System_Design.md`.
> Mỗi task ghi: **Mục tiêu · Làm gì (file/cách) · Verify · Deps/ghi chú.** Thứ tự dưới đây = thứ tự đề xuất làm.

Ký hiệu: 🟡 code có sẵn chưa hoàn chỉnh · ⬜ chưa làm.

---

## NHÓM A — Combat gameplay (ưu tiên: làm vòng chơi phong phú)

### T1 — Weapons fire (bắn các vũ khí khác) 🟡
**Mục tiêu:** Ngoài ném đá (ThrowBallistic), các vũ khí khác bắn được theo `fireMode` của chúng.
**Làm gì:**
- `HandWeapon.cs` (đã có, per-hand dispatcher) — hoàn thiện 3 nhánh fire theo `WeaponConfig.fireMode`:
  - `ProjectileLaunch` (Grenade/Bazooka/BigBoom): spawn projectile networked theo arc/tốc độ của config, dùng lại `NetworkProjectile` (đã pooled). Bazooka có arc + laser sight; BigBoom AoE lớn.
  - `Hitscan` (Gun): raycast tức thì từ muzzle, trúng thì `PlayerCombat.RPC_TakeHit`. Bắn nhanh (cooldown ~0.1s).
  - `Melee`/deflect (Sword): xem T5.
- Poll `PlayerCombat.EquippedIndex`; khi equip vũ khí non-ThrowBallistic thì `ThrowController` disable (đã có logic), re-enable khi về Rock (index -1).
- Trừ đạn qua `PlayerCombat.UseAmmo()` cho vũ khí PayPerUse; trừ tiền/unlock theo `WeaponConfig`.
- Stats + hành vi từng món: theo `Combat_Minigame_Design.md §5.4`.
**Verify (MCP):** equip từng weapon (set `EquippedIndex`), fire → đúng loại đạn/raycast bay; Gun bắn nhanh; Grenade/Bazooka arc; trúng dummy → máu giảm; hết ammo → không bắn.
**Deps:** dùng NetworkProjectile pool (Task 2 xong). Muzzle transform trên NetworkAvatar prefab (WristR/Muzzle — đã có).

### T2 — WristWeaponSelector prefab 🟡
**Mục tiêu:** UI cổ tay chọn/mua/equip vũ khí.
**Làm gì:**
- `WristWeaponSelector.cs` đã có (3-slot carousel, palm-up detect, left-stick flick nav, grip confirm, unlock time gate). Thiếu **prefab**.
- Dựng trong `NetworkAvatar.prefab`: child GO "WristSelector" parent vào WristL node → world-space Canvas + CanvasScaler → 3 slot UI (prev/current/next) mỗi slot có icon + giá + trạng thái (locked/owned/buyable) → wire `_slots[3]`.
- `NetworkAvatar.Spawned()` (authority) đã gọi `WristWeaponSelector.Initialize(combat)` — chỉ cần prefab tồn tại.
- Flick left-stick đổi slot; ngửa tay (palm-up) hiện panel; grip confirm → `PlayerCombat.TryBuyWeapon`/`EquipWeapon`.
**Verify:** ngửa tay trái → panel hiện 3 slot; flick đổi; grip mua (trừ tiền) + equip (đổi EquippedIndex → HandWeapon đổi vũ khí).
**Deps:** T1 (để equip có tác dụng). MoneyChangedEvent/WeaponResetEvent đã có.

### T3 — Team A/B + win-condition ⬜
**Mục tiêu:** Trận 1v1 (mở rộng BO3/BO5 team) đúng luật, ví reset mỗi hiệp.
**Làm gì:**
- **Team assign:** `NetworkAvatar` thêm `[Networked] int TeamIndex` (0=A,1=B), set trong `PlayerSpawnManager` khi spawn = `InputAuthority.PlayerId % 2` (alternating; balanced sau). Spawn point theo team (ArenaManager đã có `GetSpawnPosition` + `_spawnPointsA/B`).
- **Win-condition:** `ArenaManager.CheckWinCondition` đã gate `realPlayerCount >= 2` — mở rộng: đội nào còn người sống thắng hiệp; `AwardScore` theo team; `_bestOf` = 1/3/5 → `_winsNeeded`; hết BO → MatchEnd (đã có khung).
- **Ví reset:** `PlayerCombat.ResetForRound` đã reset Money=0/hiệp (đã có) — verify chạy mỗi StartRound.
- Cần thêm cờ mạng cho TeamIndex → re-bake NetworkAvatar prefab.
**Verify:** 2 player (ParrelSync) → mỗi người 1 phía; giết đối thủ → hiệp kết + score team; đủ BO → MatchEnd; đầu hiệp ví về $0.
**Deps:** cần 2 client thật để verify đúng (win-condition ≥2 real players).

---

## NHÓM B — Burst System follow-up (xây trên MVP Task 4)

### T4 — Catch viên trong burst ⬜
**Mục tiêu:** Tay bắt được đạn mưa (không chỉ viên đơn), +ammo.
**Làm gì:**
- `ProjectileBurstSystem` thêm query `TryConsumeNear(Vector3 point, float radius, out burstSlot, out projIndex)` — mỗi frame local duyệt viên gần tay (tính vị trí bằng công thức), trả viên gần nhất trong bán kính.
- `CatchController` (đã có, đang collider-based cho viên đơn) thêm nhánh: mỗi frame hỏi burst system quanh vị trí tay bắt; có viên → RPC về authority "catch (slot,i)" → authority set dead-mask[i] (xem T8) + `+Ammo` cho người bắt + fire `BallCaughtEvent`.
- Determinism: index viên ổn định (cùng seed) nên authority xác nhận vị trí trước khi cho bắt (anti-cheat nhẹ).
**Verify:** đưa tay vào cơn mưa → 1 viên biến mất khỏi visual (dead-mask) + Ammo +1.
**Deps:** T8 (dead-mask) để xóa viên khỏi visual; không có T8 thì viên vẫn hiện dù đã "bắt".

### T5 — Sword deflect (chém đạn) ⬜
**Mục tiêu:** Kiếm quét chém đạn (đơn + burst) → bật ngược về người ném.
**Làm gì:**
- Kiếm là HandWeapon fireMode Melee: BladeTip (đã có child dưới WristR) quét đoạn mỗi frame.
- **Viên đơn:** đoạn lưỡi vs collider NetworkProjectile → đảo hướng bay (đổi v0), đổi Shooter về người chém.
- **Burst:** `ProjectileBurstSystem.TryDeflectAlong(segment, out list)` — viên bị đoạn cắt qua → **tách ra**: set dead-mask trong burst + spawn 1 NetworkProjectile đơn (pooled) tại vị trí viên, v0 = hướng deflect. Viên mới đi đường đơn lẻ, trúng lại người ném gốc.
- Sword rút sau lưng, deflect-only (không gây damage trực tiếp) — theo `Combat_Minigame_Design §9`.
**Verify:** vung kiếm qua đạn đơn → bật ngược; vung qua cơn mưa → vài viên tách ra bay ngược.
**Deps:** T8 (dead-mask). NetworkProjectile pool (xong).

### T6 — Burst dead-mask (networked) ⬜
**Mục tiêu:** Cho phép xóa từng viên khỏi burst (trúng/bắt/chém) đồng bộ mọi client.
**Làm gì:**
- `ProjectileBurstSystem.Burst` struct thêm dead-mask: fixed bitmask (vd 4×uint = 128 bit; hoặc chọn cap viên/burst nhỏ hơn cho vừa). Với cap 4096 thật thì mask lớn → cân nhắc chia burst thành nhiều slot nhỏ hơn, hoặc chấp nhận replicate delta.
- Authority set bit khi viên trúng/bắt/chém; renderer + hit-test bỏ qua viên có bit set.
- Hiện MVP: authority tránh double-hit bằng **local HashSet** (không networked) → viên "chết" vẫn render. T6 thay bằng networked mask để visual + catch/deflect đồng bộ.
**Verify:** viên trúng/bắt → biến mất khỏi visual trên mọi client; số viên còn lại giảm.
**Deps:** re-bake ProjectileBurstSystem (đổi struct networked). Là tiền đề cho T4/T5 hoàn chỉnh.

### T7 — Burst stacking qua nhiều ring ⬜
**Mục tiêu:** Cơn mưa đi xuyên ring Multi tiếp → nhân count (12×12×12).
**Làm gì:**
- Burst là data (không collider) nên không tự trigger ring OnTriggerEnter. Cần: authority mỗi tick check vị trí viên burst vs vị trí các ring Multi còn sống → nếu burst "đi qua" ring Multi thì nhân `Count` (kẹp trong trần 4096) + tiêu ring đó.
- Hoặc đơn giản hơn: khi spawn burst, tính trước xem hướng bay có xuyên ring Multi nào không → nhân luôn. (Ít chính xác hơn nhưng rẻ.)
**Verify:** burst bay xuyên ring Multi thứ 2 → count nhân lên (vd 40→ nhiều hơn), vẫn ≤ trần.
**Deps:** T6 nên có trước (quản viên). Trần cứng bắt buộc.

### T8 — RenderMeshIndirect + compute cull ⬜
**Mục tiêu:** Nâng render từ DrawMeshInstanced (≤1023/batch, CPU matrices) lên indirect + GPU cull, cho số viên rất lớn + tiết kiệm.
**Làm gì:**
- `ProjectileBurstRenderer` chuyển sang `Graphics.RenderMeshIndirect` + GraphicsBuffer instance data + compute shader tính vị trí/cull (phỏng `DynamicInstancingManager` trong ShadersLab InstanceGPU).
- **Bắt buộc:** shader instanced xử lý đúng single-pass stereo (eye index) cho Quest, không thì lệch mắt.
**Verify:** trên Quest thật (không tin sim) — cơn mưa lớn render đúng cả 2 mắt, draw call thấp.
**Deps:** chỉ cần khi số viên thật sự lớn (sau stacking). VR test bắt buộc.

---

## NHÓM C — Ring & buff content

### T9 — Ring spawn random + trôi trong vùng giữa ⬜
**Mục tiêu:** Ring spawn ngẫu nhiên vị trí + trôi lang thang trong VÙNG GIỮA sân (thay 3 điểm cố định + trôi lên xuống).
**Làm gì:**
- `RingSpawner` nhận **bounds vùng giữa** (BoxCollider hoặc 1 Transform + Vector3 size) thay `_spawnPoints[]`. Spawn tại vị trí random trong box.
- `BuffRing` drift: đổi từ sin lên-xuống sang **wander random trong bounds** (đổi hướng theo thời gian, clamp trong box).
- Khi dựng map thật, chỉ cần đặt + chỉnh size box giữa — không sửa code lại.
**Verify:** ring spawn rải rác trong vùng giữa, trôi loanh quanh, không ra ngoài box.
**Deps:** liên quan map (xem HANDOFF/dưới). Code làm trước, map size box sau.

### T10 — Buff zones: tường băng + vùng lửa ⬜
**Mục tiêu:** Ice tạo tường băng; Fire tạo vùng lửa tồn tại tới hết Tier.
**Làm gì:**
- **Ice:** đạn dính Element=Ice khi nổ → spawn tường băng (networked object) tồn tại N giây; ai chạm mất lượt/máu; dính damage thì tan. 
- **Fire:** đạn Element=Fire nổ → spawn vùng lửa (area) = bán kính nổ; ai đi qua mất máu; tồn tại tới hết Tier.
- **Speed/Area:** hiện chỉ set field trên projectile — verify velocityScale/areaScale áp dụng thật lên flight + hit radius.
- Config đã có 5 `BuffRingConfig` (RC_*), thêm field zone nếu cần.
**Verify:** ném đạn buff Ice/Fire → thấy tường/vùng xuất hiện + gây hiệu ứng đúng.
**Deps:** cần hit/explosion path rõ (NetworkProjectile OnHit). Combat_Minigame_Design §10.

### T11 — Ring spawn rules ⬜
**Mục tiêu:** Spawn theo cửa sổ thời gian + rarity + chống trùng.
**Làm gì:** RingSpawner theo `BuffRingConfig` rate + window (0-30s/31-60s/61-90s); Tier 4-5 hiếm hơn + trôi nhanh hơn; không cho 2 ring cùng tên+Tier 4-5 đồng thời.
**Verify:** quan sát phân bố ring theo thời gian đúng thiết kế.
**Deps:** T9 (spawn system).

---

## NHÓM D — Network còn lại

### T12 — Buff-ring Shared Mode RPC (multi-client) ⬜
**Mục tiêu:** Ring dán buff đúng khi ring authority ≠ projectile authority (multi-client).
**Làm gì:** `BuffRing.OnTriggerEnter` hiện chỉ apply khi `proj.Object.HasStateAuthority` (đúng solo master). Sửa: gửi RPC về authority của projectile để apply buff, thay vì đọc/ghi trực tiếp. Xem `Burst_Projectile_System_Design` + Fusion gotchas.
**Verify:** 2 client — player non-master ném xuyên ring → buff vẫn áp.
**Deps:** cần 2 client. Đọc Fusion_Shared_Mode_Gotchas trước.

### T13 — Bill.Players + additive scene ⬜ (để sau cùng)
**Mục tiêu:** Hệ player cấp cao + load nhiều scene cùng lúc (hub + minigame).
**Làm gì:** Theo playbook `deprecated/Networking_Architecture.md §2/§7`. Chỉ làm khi cần hub-as-shared-space thật sự coexist với minigame.
**Deps:** không gấp. Networking core + pool đã đủ cho hiện tại.

---

## NHÓM E — Dọn, juice, ship

### T14 — Dọn còn lại ⬜
- AutoHand `CollisionTracker` spam lúc chuyển scene (bóng grabbable despawn mà tracker giữ ref) — untrack trước load hoặc null-check. Benign, third-party.
- Audio listener dư ở rig lobby — bỏ bớt 1 (arena scene đã sạch).

### T15 — Juice (Throw spec S4-S7) ⬜
- S4 tune ThrowConfig feel · S5 haptic 3 tầng (wind/release/impact) + SFX pitch theo lực · S6 VFX bay (flash + squash-stretch + trail) · S7 impact (burst + shockwave + bounce number) · arm-swing run locomotion (Gorilla-Tag). Chi tiết: `Throw_Mechanic_Spec.md`.

### T16 — Map thật ⬜
**Mục tiêu:** 2 sân hai bên cách nhau + vùng giữa cho ring trôi.
**Làm gì:** dựng geometry 2 sân + khoảng hở giữa; đặt + size **box vùng giữa** (cho T9); mở rộng [SpawnA]/[SpawnB] thành vùng spawn 2 đội; tường vô hình chặn player băng sân nhưng cho bóng bay qua.
**Khi nào:** trước T9 (ring drift zone) hoặc trước playtest/build — cái nào tới trước. Không gấp lúc này.

### T17 — 2-player verify + build Quest ⬜
**Mục tiêu:** Chốt cuối trên máy thật.
**Làm gì:** ParrelSync 2 client — thấy nhau di chuyển, held ball đúng tay, ném trúng nhau pip giảm đúng bên, win-condition; rồi build Quest 2 máy, fix lag/jitter, chuyển VR splash sang world-space.

---

## Phụ thuộc chính (đọc nhanh)
- T4/T5 cần **T6 (dead-mask)** để xóa viên khỏi visual.
- T7 (stacking) nên có T6 trước.
- T9 (ring drift zone) + T17 (build) cần **T16 (map)**.
- T3/T12/T17 cần **2 client thật** để verify đúng.
- Mọi networking: đọc `Fusion_Shared_Mode_Gotchas.md` trước. File .cs mới / thêm [Networked] → refresh scope=all + re-bake prefab.
