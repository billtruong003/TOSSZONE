# TOSSZONE — Master Task List (audit + hướng đã sửa)

> Cập nhật: Session 10 (2026-07-01). Đây là danh sách tổng đã audit theo THỰC TẾ chạy được, thay cho
> `Docs/deprecated/tasks.json` (tracker cũ 122 task, over-report "Done"). Quy ước: ✅ xong+verify · 🟡 code xong chưa
> verify · ⬜ chưa làm · ❌ bug đang mở.

## Ghi chú audit (đọc trước)

- **tasks.json over-report Done.** Nhiều task "Done" chỉ là code hoặc config viết xong, chưa chạy thật. Ví dụ:
  5 loại buff ring đánh Done nhưng chỉ là 5 file config; buff xuyên ring mãi hôm nay mới thật sự chạy sau khi
  fix collider. Weapons "Done" = có 7 WeaponConfig, chưa bắn được viên nào.
- **Hướng đạn đã đổi (locked).** Task cũ "Multi nhân bản projectile thành mưa đạn" + "stacking tối đa 3 vòng"
  bị thay bằng **Burst System** (data-oriented, ×12 stack nhiều tầng). Xem `Docs/Burst_Projectile_System_Design.md`.
  Các task 2.2.5 / 2.2.11 / 2.2.12 trong tasks.json coi như superseded.
- **Nền đã chắc:** vòng chơi lõi (hit → death → respawn dummy → ring buff đơn → bot) đã verify chạy session 10.

---

## A. Đã xong và verify (Session 10)

- ✅ Fix 4 bug minigame: ring màu, CombatSession NRE, arena spin round, dummy respawn (`c769ae4`)
- ✅ Network Phase 1: gate load scene cho play thẳng arena (1a) + fix 2 body player overlap (1b) (`2e710e6`)
- ✅ BuffRing collider MissingComponentException (`730e3b5`)
- ✅ Bot ném trúng player (fix gravity) + bóng xuyên ring dán buff (thêm collider) (`d232f61`)
- ✅ Chốt thiết kế Burst System (`327b79b`)
- ✅ Verify chạy: ném trúng dummy tụt máu, chết, respawn; ring có màu/spawn/trôi; ring buff dán vào đạn +
  consume + respawn; arena loop Warmup→Playing không spin; play thẳng arena; portal Main→Arena 1 avatar; bot hit player.

## B. Fix và dọn ngay (follow-up phát sinh session 10)

- ❌ **NetworkProjectile leak** — bắn xong không despawn, bay thẳng mãi, chất đống. Fix bằng network pool (mục D-A).
- ⬜ **Font glyph** — 5 RC_*.asset để emoji (🔥❄⚡🛡💨), font không vẽ được nên ra ô vuông. Bỏ emoji khỏi displayName.
- ⬜ **AutoHand CollisionTracker spam** — MissingReferenceException khi chuyển scene (bóng grabbable bị despawn mà tracker còn giữ ref). Untrack trước khi load hoặc null-check.
- ⬜ **BuffRing consume tween** — MissingReferenceException nhẹ khi ring despawn mà tween còn chạy. Kill tween trước Despawn.
- ⬜ **Fusion TickRate** — cảnh báo "Invalid TickRate", Fusion tự override 32/16. Chỉnh NetworkProjectConfig cho hết warning.
- ⬜ **2 audio listeners** trong scene — bỏ bớt một.

## C. Gameplay lõi còn thiếu

- ⬜ **Player respawn** (ưu tiên cao nhất) — player chết Health=0 hiện đứng luôn. NetworkAvatar death → respawn về spawn point.
- 🟡 **HandWeapon bắn các vũ khí khác** — code có, 7 WeaponConfig có, nhưng mới có ném đá. Equip + fire theo fireMode (gun/grenade/bazooka/bigboom/landmine).
- 🟡 **WristWeaponSelector** — code có, chưa có prefab canvas world-space ở cổ tay trái.
- 🟡 **CatchController** — code có (bắt viên đơn qua collider, +ammo), chưa verify; và cần nhánh burst-aware (mục D-B).
- ⬜ **Sword deflect (cắt)** — chém đạn bật ngược. Viên đơn dùng collider; đạn burst dùng query (mục D-B). Sword rút sau lưng, deflect-only.
- ⬜ **Team assignment + spawn 2 phía** — gán Team A/B khi join, spawn đúng phía. (tasks 1.3.9/1.3.10)
- ⬜ **Win-condition thật** — hiện cần ≥2 real players mới xử; wire BO1/BO3/BO5, ví reset $0/hiệp.

## D. Kiến trúc đạn (locked — xem Burst_Projectile_System_Design.md)

- ⬜ **Bước A — Network pool** (`INetworkObjectProvider` override trong FusionNet, kiểu PoolNetworkObjectProvider).
  Fix leak, gọn draw call. Nền cho tất cả. Làm trước.
- ⬜ **Bước B — Burst System** cho đạn mưa:
  - ⬜ Struct `Burst` networked (origin, dir, count, seed, gravity, tick, element, dead-mask)
  - ⬜ Sinh viên deterministic từ seed + tween phân tích
  - ⬜ `ProjectileInstanceRenderer` GPU instance (RenderMeshIndirect + compute cull, phỏng ShadersLab InstanceGPU)
  - ⬜ Authority hit test theo công thức + RPC-on-hit (không sync vị trí)
  - ⬜ Catch query (tìm-gần-điểm) + deflect query (tìm-đoạn-cắt) → RPC lẻ; viên deflect tách ra đơn lẻ
  - ⬜ Trần cứng tổng số viên (~4096)
  - ⬜ Bỏ cap `Multiplier=3` trong BuffRing.ApplyBuff → count nhân dồn
  - ⬜ Shader instanced đúng single-pass stereo (VR)

## E. Buff content — vùng hiệu ứng (mới là config, chưa có gameplay)

- 🟡 5 loại ring config (Ice/Fire/Multi/Speed/Shield) — có asset, buff cơ bản (velocity/area/element) chạy.
- ⬜ **Băng**: đóng băng + tường băng tồn tại tới hết Tier, dính damage thì tan.
- ⬜ **Lửa**: sau nổ tạo vùng lửa, ai đi qua mất mạng.
- ⬜ **Speed/Area**: đã set field, cần thấy hiệu ứng thật trên đạn (velocity/AoE).
- ⬜ Spawn theo cửa sổ thời gian + rarity Tier 4-5 hiếm hơn + chống trùng.

## F. Network và flow

- ⬜ **Phase 2 audit** — rà PlayerSpawnManager, PortalMatchmaker, MinigameManager, ThrowController spawn/despawn cho khớp network layer mới (sau pool).
- ⬜ **2-player verify (ParrelSync)** — thấy nhau di chuyển, held ball đúng tay, ném trúng nhau pip giảm đúng bên. (cả loạt task 1.x "Pending 2-player verify")
- ⬜ **Buff-ring Shared Mode RPC** — ring dán buff qua RPC về authority của đạn, cho chạy đúng multi-client (giờ chỉ đúng solo master).
- ⬜ **Bill.Players** (hệ player cấp cao) + **additive scene loading** — CHƯA build. Thiết kế đầy đủ nằm ở `deprecated/Networking_Architecture.md` §2/§7 (playbook Shmackle). Networking core + pool đã xong; hai cái này là phần networking còn thiếu, để sau cùng.

## G. Polish / juice (Throw spec S4-S8, đều Todo)

- ⬜ S4 feel: tune ThrowConfig curve
- ⬜ S5 haptic 3 tầng (wind/release/impact) + SFX pitch theo lực
- ⬜ S6 VFX bay: release flash + squash-stretch + trail glow
- ⬜ S7 impact: burst + shockwave + bounce number
- ⬜ Arm-swing run locomotion (Gorilla-Tag)

## H. Ship

- ⬜ Sân demo hoàn chỉnh (floor + ranh giới 2 đội)
- ⬜ Build Quest + test 2 device thật
- ⬜ Fix lag/jitter/trajectory giữa 2 client
- ⬜ VR splash world-space (splash hiện đang screen-space, không thấy trong headset)

---

> Chi tiết "làm gì" từng task (mục tiêu / file / verify / deps): **`TASKS_DETAIL.md`**.

## Thứ tự đề xuất

1. **Player respawn** (C) — khép vòng chơi solo.
2. **Bước A network pool** (D) — fix leak, nền cho đạn.
3. **Dọn nhanh B** (font, AutoHand, tween, TickRate) — console sạch.
4. **Bước B Burst System** (D) — phần lớn nhất, đạn mưa + catch + deflect.
5. **Weapons + selector + team + win-condition** (C).
6. **Buff zones** (E) + **Shared Mode RPC** (F).
7. **Juice** (G) → **2-player verify** (F) → **ship** (H).
