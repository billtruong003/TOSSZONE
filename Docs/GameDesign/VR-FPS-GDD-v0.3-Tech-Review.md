# Technical Review — GDD v0.3 mục 17 (10 điểm)

> Reviewer: expert-developer · 2026-07-13
> Đối chiếu: `VR-FPS-Playable-GDD-v0.3.md` vs codebase thật (`Assets/_Game/Scripts`), Fusion 2 Shared Mode, AutoHand, Unity Input System.
> Files đã đọc: `TossLocomotionInput.cs`, `PlayerCombat.cs`, `NetworkProjectile.cs`, `HandWeapon.cs`, `WeaponHolder.cs`, `WristWeaponSelector.cs`, `ArenaManager.cs` (grep), `CombatSession.cs` (grep), `NetworkAvatar.cs` (grep), `Docs/Fusion_Shared_Mode_Gotchas.md`, `Docs/Network_Architecture_Lessons.md`.

**Kết luận chung: 8/10 điểm nằm trên rails có sẵn. Hai chỗ phải quyết lại trước khi code: (A) bỏ AutoHand Grabbable cho súng, (B) sniper dùng iron sight chứ không dùng scope render-texture. Một mismatch lớn GDD chưa gọi tên: hệ máu hiện tại là "mạng" (5 lives), GDD cần 100 HP.**

---

## Điểm 1 — Súng khóa dính tay, báng trước, hòa hướng ngắm hai tay

**Verdict: LÀM ĐƯỢC, nhưng nên đổi cách làm — bỏ AutoHand Grabbable cho súng.**

Hiện trạng: `WeaponHolder.cs` (T19) giữ súng bằng AutoHand `Grabbable` + `ForceGrab`. Chính code này đã phải tự vá sự flaky của AutoHand: `EnsureAttached()` chạy vòng re-grab mỗi 0.25s vì "AutoHand's grab is a multi-frame coroutine and can silently bail", kèm ép layer, ép kinematic, `Physics.SyncTransforms()` + raycast mồi. Đó là bốn workaround cho một việc GDD không còn cần: GDD v0.3 nói súng **không bao giờ rời tay** — không nhặt, không thả, không cướp.

Khi item không bao giờ rời tay thì grab-physics là chi phí thuần, không mua được gì. Codebase đã có sẵn đường thứ hai tốt hơn: `HandWeapon.SpawnHeldVisual()` — instantiate model, strip toàn bộ MonoBehaviour/Collider/Rigidbody, parent thẳng vào wrist node của `PlayerRig` với offset per-weapon. Đường này đang dùng cho cosmetic + proxy, chạy ổn.

**Đề xuất:** dùng cosmetic-parenting cho CẢ local lẫn remote. Hướng ngắm = transform `_muzzle` (đã có). Xóa được: vòng regrab, layer forcing, grip-to-hold. `WeaponHolder` gần như nghỉ hưu.

**Báng trước / hai tay:** đừng dùng two-hand grab vật lý của AutoHand (độ flaky nhân đôi). Làm giả: mỗi súng hai tay có một điểm `foregrip`; khi tay tự do vào trong bán kính ~12cm quanh điểm đó → coi là đang nắm, hướng ngắm đổi từ "hướng muzzle theo cổ tay" sang "vector từ tay sau đến tay trước". Một script nhỏ, không physics, dễ tune. Nhả tự động khi gạt đổi súng (luật #4 mục 12) = một dòng.

Effort: **nhỏ-vừa**, và là effort *thay thế* code cũ chứ không đắp thêm.

## Điểm 2 — Chia vùng cần gạt + đảo tay thuận

**Verdict: CODE DỄ, nhưng handedness là việc mới thật sự.**

Hiện trạng `TossLocomotionInput.cs`: đọc thumbstick qua New Input System (fallback legacy — chú ý comment trong file: legacy `primary2DAxis` đọc (0,0) trên OpenXR, đừng ai "dọn" cái fallback đó). Turn hiện chỉ dùng trục X của stick phải (`ReadTurn().x`) → **trục Y stick phải đang trống, đúng chỗ GDD muốn đặt weapon swap**. Chia vùng 80%-dọc / ngang / deadzone-giữa là toán thuần trong class này, không đụng hệ input.

Hai việc thật:
1. **Dash phải chết.** Right-stick-click hiện là dash (`HandleDashInput`, `_dashStrength = 3.5f`). GDD v0.3 không có dash, và stick-click được GDD đặt cho reload. Xóa dash trước khi map reload, đừng để hai hành vi chồng một nút.
2. **Không tồn tại hệ handedness.** Mọi thứ hardcode tay phải: `HandleJumpInput` đọc `XRNode.RightHand` cứng, `_rightHand` serialize sẵn trên `HandWeapon`/`WeaponHolder`, shop gắn cứng cổ tay TRÁI (`WristWeaponSelector` "Lives on a child of the LEFT wrist bone"). Cần một `HandednessSetting` (static + PlayerPrefs) và 4–5 chỗ đọc nó. Không khó, nhưng phải làm từ đợt 1 vì retrofit sau sẽ rải if khắp nơi.

## Điểm 3 — Nhảy

**Verdict: NHẢY ĐÃ CÓ TRONG CODE. Việc còn lại là visual proxy.**

`TossLocomotionInput.HandleJumpInput()` đã gọi `_player.Jump()` (AutoHandPlayer, nút A tay phải) — GDD ghi "làm mới" là sai, đây là tune chứ không phải build (chỉnh jumpPower cho đỉnh 0.8m).

Gap thật nằm ở mạng: `NetworkAvatar` sync head/hands qua NetworkTransform, nhưng thân proxy được dựng bằng "Stand the body under the head's ground position" (comment dòng ~151). Người nhảy sẽ hiện ra ở máy khác như đầu bay lên còn thân dính đất. Vì hit dùng collider theo node đã sync nên gameplay không sai — chỉ sai hình. Fix nhỏ: sync một bit `IsAirborne` (hoặc clamp khoảng head-body), cho vào checklist đợt 1.

Vignette mờ viền khi rời đất (yêu cầu comfort mục 5): chưa có gì, việc mới nhỏ.

## Điểm 4 — Người khác thấy đúng súng; spin-up/bolt local-only

**Verdict: ĐÃ GIẢI XONG TỪ TRƯỚC. Reuse nguyên si.**

`EquippedIndex` là `[Networked]` trên `PlayerCombat`; proxy tự dựng model qua `HandWeapon.UpdateProxyHeldModel()` — "sync the cause, not the mesh", cùng pattern `VisualIndex` của projectile. Ba ô vũ khí = map thẳng vào catalog index. Spin-up nòng xoay / bolt sniper local-only như GDD đề xuất là đúng: không ai ở xa cần biết, đừng sync.

## Điểm 5 — Xác nhận trúng đạn + câu hỏi burst

**Verdict: PATTERN CÓ SẴN VÀ ĐÚNG. Trả lời burst: 3 lần dò riêng, KHÔNG gộp.**

Đường đạn hiện tại khớp GDD từng chữ: shooter raycast local (`FireHitscan`) → `victim.RPC_TakeHit(damage, point, shooter)` → chỉ StateAuthority của nạn nhân ghi Health, mọi client bắn juice event. Melee (`FireMelee`, OverlapSphere + cooldown) phủ luôn con dao. Giữ nguyên.

**Burst 3 viên:** mỗi viên là một raycast, viên nào trúng thì viên đó gửi RPC riêng. Ở 4 người chơi, tiết kiệm 2 RPC/loạt không mua được gì, còn gộp thì phá đường kill-attribution và juice per-hit đang chạy. Đừng batch — đó là tối ưu cho vấn đề tưởng tượng.

Việc mới thật của combat (đây là cục code mới lớn nhất toàn dự án, dồn hết vào một chỗ):
- `FireHitscan` hiện là 1 ray, edge-trigger. Cần: full-auto (giữ cò + interval), spread/bloom 3 tham số, damage falloff theo khoảng cách, headshot ×2.
- Headshot cần hitbox đầu riêng — `NetworkAvatar` đã sync head node riêng, chỉ việc gắn collider tag "Head" lên node đó. Nền có sẵn.

**Mismatch GDD chưa gọi tên — máu:** `PlayerCombat.Health` hiện là MẠNG (`MaxLives = 5`, `CompensationPerLife`, HealthUI 5 pip). GDD cần 100 HP. Cùng là `[Networked] int` nên đổi được sạch, nhưng kéo theo `LivesForPlayerCount`, `ResetForRound`, `RestoreLives`, HealthUI (pip → thanh), và economy cũ (`IncomePerSecond`, `KillReward = 5`) phải thay bằng bảng tiền mục 8 của GDD. Làm việc này ĐẦU TIÊN của đợt 1 — mọi con số súng đều đứng trên nó.

## Điểm 6 — Tốc độ chạy theo súng

**Verdict: TẦM THƯỜNG. Không cần sync gì.**

`AutoHandPlayer.Move()` chạy local, vị trí đã replicate qua NetworkTransform — máy khác chỉ thấy vị trí, không cần biết tốc độ. Một multiplier áp khi `OnEquipChanged`. Kiến trúc này vốn trust client hoàn toàn (shooter tự phán trúng), nên khỏi bàn anti-cheat riêng cho move speed.

## Điểm 7 — Tiền/hiệp/phase trên chủ phòng

**Verdict: KHUNG CÓ SẴN NGUYÊN BỘ. Gap duy nhất: trả ví khi vào lại.**

`ArenaManager` đã đúng hình GDD muốn: `[Networked] Phase / Round / ScoreA / ScoreB / PhaseTimer` trên scene object, mọi ghi guard bằng `HasStateAuthority` (= shared-mode master client). Ví tiền đã `[Networked]` per-player trên `PlayerCombat` với owner-authority — khớp GDD. Đổi luật kinh tế (800 khởi điểm, 1000/1400, 200/kill, trần 6000, 20s mua) là đổi số + thêm phase, không đổi kiến trúc.

Gap thật: **restore ví khi reconnect** (luật #6 mục 12). Avatar despawn là mất state, và `PlayerRef` đổi khi vào lại — không key bằng PlayerRef được. Cần ledger trên ArenaManager key theo identity ổn định (userId/tên). Việc mới nhỏ nhưng lắt nhắt; GDD đã xếp Vòng Kinh Tế vào đợt 2, để nó ở đó.

Host migration: shared mode tự chuyển StateAuthority scene object khi master rớt, nhưng **PhaseTimer có liền mạch qua migration không** phải nằm trong test list đợt 2 — đừng tin, hãy đo.

## Điểm 8 — Lượt kỹ năng networked

**Verdict: KHÔNG CÓ GÌ ĐỂ REVIEW.** `[Networked] int SkillCharges` cạnh `Money`/`OwnedMask`; biến thể cooldown dùng `TickTimer` như `FrozenTimer`/`InvulnTimer` đang có. Copy pattern, xong.

## Điểm 9 — Shop con trỏ, cửa một chiều, 2s bất tử

**Verdict: ĐỀ NGHỊ LỆCH GDD — dùng lại wrist shop thay vì build hệ con trỏ mới.**

Codebase KHÔNG có ray-interactor nào đang dùng (grep sạch). Thứ đang có và đã qua hai vòng rework (T18/T19) là `WristWeaponSelector`: nhìn cổ tay để mở, poke để duyệt, nắm hologram để mua — đã nối sẵn vào `Money`/`OwnedMask`/`EquipWeapon`. Build hệ laser-pointer UI mới cho đợt 1 là viết lại một thứ đã tồn tại chỉ để đổi cách bấm. Đề xuất: đợt 1 dùng wrist shop; chỉ cân nhắc pointer nếu playtest cho thấy 20 giây mua đồ không đủ thao tác. Ghi quyết định này ngược vào GDD.

Cửa một chiều: collider một mặt + layer, việc dựng map. 2s bất tử: `InvulnTimer` đã có sẵn (3s) — đổi hằng số thành 2, và thêm luật "mất khi nổ súng" (một check trong `OnTriggerPressed`).

## Điểm 10 — ADS trên Quest

**Verdict: CHỐT LUÔN ĐƯỢC: IRON SIGHT / HOLO DOT. Không render-texture scope.**

URP confirmed (code tự tìm shader `Universal Render Pipeline/Unlit`). Scope render-texture = camera thứ hai mỗi mắt — thứ đắt nhất có thể thêm vào Quest, và map GDD giao tranh 5–15m, trục sniper >20m: **không có nhu cầu phóng đại thật**. "Ngắm" thiết kế thành: đưa sight lên gần mắt → hết spread + hiện dot. Zero camera phụ.

Điều này trả lời luôn câu hỏi mở #3 mục 18 của GDD: sniper dùng iron/holo sight, thiết kế hai khẩu sniper theo hướng đó, không chờ nữa.

---

## Tổng effort & thứ tự đề nghị (đợt 1)

| # | Việc | Cỡ | Ghi chú |
|---|---|---|---|
| 1 | Health lives→100HP + gỡ economy cũ | M | Làm đầu tiên, mọi thứ đứng trên nó |
| 2 | Xóa dash; chia vùng stick; HandednessSetting | S–M | `TossLocomotionInput` + 4 file đọc setting |
| 3 | Súng = cosmetic parenting, bỏ Grabbable; foregrip aim-blend | M | Thay code, không đắp thêm |
| 4 | FireHitscan → full-auto + spread + falloff + headshot | **L** | Cục code mới thật sự duy nhất |
| 5 | Hitbox đầu trên head node | S | |
| 6 | Move speed multiplier theo equip | XS | |
| 7 | Jump: tune 0.8m + sync IsAirborne + vignette | S | |
| 8 | 3 ô vũ khí trên EquippedIndex + swap 0.5s | S | Pattern có sẵn |
| 9 | Skill charges networked | XS | |
| 10 | Wrist shop giữ nguyên cho đợt 1 | 0 | Quyết định lệch GDD, cần bạn duyệt |

**Ba rủi ro tôi muốn được nghe phản biện:**
1. Bỏ AutoHand cho súng — mất auto-pose ngón tay (fingers wrap). Nếu art ngón tay quan trọng, phải author pose tĩnh per-weapon.
2. Wrist shop thay pointer shop — nếu GDD coi "con trỏ như bút laser" là trải nghiệm cốt lõi chứ không phải phương tiện, nói lại.
3. PhaseTimer qua host migration — chưa ai kiểm chứng, tôi xếp nó là unknown lớn nhất của đợt 2.
