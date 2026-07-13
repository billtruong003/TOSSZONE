# TOSSZONE — Kiến trúc Gun System (VR-FPS pivot)

> **Status:** DESIGN — v1.1 · 2026-07-14 · Option A hit contract locked · Blueprint implement cho GDD v0.3 (`Docs/GameDesign/VR-FPS-Playable-GDD-v0.3.vi.md`)
> **Đọc kèm:** `Docs/Fusion_Shared_Mode_Gotchas.md` · `Docs/BillGameCore_Usage.md` · `Docs/Network_Architecture_Lessons.md`
> **Quyết định nền:** KHÔNG dùng gun code của AutoHand. KHÔNG dùng AutoHand Grabbable cho súng.
> AutoHand chỉ còn giữ vai trò locomotion + physics body của LocalPlayer.

---

## 0. Tóm tắt cho người vội

- Súng của TOSSZONE là **loại dễ**: hitscan, không thao tác băng đạn vật lý, reload = animation + timer.
  Vì vậy KHÔNG cần grab-physics, KHÔNG cần NetworkObject cho súng, KHÔNG cần AutoHand.
- **1 class cha `Gun`** phát ra event stream chuẩn hóa (`ShotInfo`); mọi consumer (VFX, SFX, tracer,
  haptics, network relay) chỉ ăn event — không biết loại súng. Subclass CHỈ khi hình dạng fire-loop
  đổi (spin-up, bolt). Semi/auto/burst là **data trong config, không phải subclass**.
- **Networked state cho toàn bộ gun system = 1 byte** (`EquippedSlot`) + 1 unreliable RPC per shot
  (cosmetic) + 1 reliable targeted `ShotClaim` per player hit. Không có gì khác đi dây.
- Master client **không có vai trò gì** trong gun system. Mọi logic bắn/đạn/reload chạy trên client
  của người bắn; State Authority của nạn nhân validate claim, tự tra damage từ catalog rồi write máu.
- Skin FX = swap 1 asset `FxSet`, zero code.

---

## 1. Mục tiêu & Non-goals

### Mục tiêu
1. Thêm 1 súng mới (stats + model + FX + sound) trong ≤ 15 phút, không viết code nếu không có
   fire-behavior mới.
2. Fire-feel local **0 ms**: mọi feedback (muzzle, tracer, recoil, haptic, sound) bắn ngay frame bóp cò,
   không chờ mạng (Lessons §4.1 — juice ở local, state mới đi dây).
3. Bandwidth tối thiểu: worst case 4 player × Heavy full-auto vẫn chỉ là chuỗi RPC unreliable cosmetic.
4. Không phụ thuộc AutoHand: code súng đọc thẳng InputSystem, gắn thẳng vào wrist.

### Non-goals (chốt để khỏi tranh cãi lại)
- **Không physical reload** (rút băng, đút băng, kéo slide). GDD đã chốt reload = nút + anim.
- **Không server-authoritative anti-cheat.** Shared Mode + phòng bạn bè → sanity-check là đủ (mục 7).
- **Không projectile bay thật** cho vũ khí GDD hiện tại (toàn hitscan). Nếu sau này có grenade/rocket
  → dùng lại pattern NetworkProjectile của throw system (Lessons S3), không nhét vào Gun.
- **Không two-hand grip** ở v1. Chừa hook (mục 10) nhưng không build.

---

## 2. Nguyên tắc mạng — áp gotchas vào gun system

| Nguyên tắc | Nguồn | Hệ quả thiết kế |
|---|---|---|
| Sync nguyên nhân, không sync kết quả | Lessons S2 (HoldingBall + child visual của WristR NT) | Súng trên tay remote = **child tĩnh của WristR node** trên NetworkAvatar. Chỉ sync `EquippedSlot` (byte); model/anim/muzzle proxy tự render local. Không thêm NetworkObject, không thêm NT. |
| Authority = ai cần write frame này | Lessons §4.2 | Ammo/reload/spread/spin-up = state của riêng người bắn → **field thường, không [Networked]**. Health = state của nạn nhân → nạn nhân write (RPC targeted). |
| Object đổi chủ mới cần RequestStateAuthority | Gotchas §2, Lessons Bài 4 | Súng KHÔNG bao giờ đổi chủ (không rơi, không nhặt của nhau ở v1) → **không đụng RequestStateAuthority/AllowStateAuthorityOverride** ở bất kỳ đâu. Cả một lớp async-bug biến mất. |
| Juice local từng client | Lessons §4.1, S8 | Impact VFX/hit-sound cho mọi người đi kèm RPC cosmetic; haptic chỉ chạy trên client trúng đạn. |
| Pooled object phải tự reset | Gotchas (pool giữ stale field), BillGameCore §3 | Tracer/muzzle/impact đều pooled qua `Bill.Pool` → bắt buộc reset trong `OnSpawnedFromPool`; tween luôn `SetTarget(owner)` + `KillTarget(owner)`, không giữ `Tween` ref qua frame. |
| [Networked] chỉ đọc sau Spawned; thứ tự Spawned giữa các NB không đảm bảo | Gotchas | `AvatarWeaponSync` không đọc state của NB khác trong `Spawned()`; late-joiner render súng trong `Render()` theo `EquippedSlot` hiện hành. |
| Bill.Events là bus local per-process | BillGameCore §3 | Event gameplay (`GunFiredEvent`…) phải được fire trên MỖI client từ RPC handler, không chỉ trên shooter. |

**Master client:** vai trò duy nhất trong game là phase/round (ArenaManager). Gun system không đọc,
không ghi, không chờ bất kỳ thứ gì từ master. Master rớt mạng giữa fight → không ai hụt phát đạn nào.

---

## 3. Bản đồ class

```
GunConfig : ScriptableObject          ── stats: damage, rpm, fireMode(Semi/Auto/Burst), magSize,
  │                                      reloadTime, spread/bloom curve, recoil pattern, range,
  │                                      falloff curve, headshotMul, tier/price (shop),
  │                                      behaviour = Hitscan | SpinUp | Bolt
  ├─ modelPrefab (view-only, có GunView + muzzle anchor)
  ├─ FxSet      : ScriptableObject    ── muzzleKey, tracerKey, impactKeys per-surface, shellKey?
  └─ SoundSet   : ScriptableObject    ── fire, dryFire, reloadStart/End, equip (Bill.Audio keys)

IWeapon                               ── Equip/Unequip, TriggerDown/Up, TickLocal(dt)
  │
Gun : MonoBehaviour, IWeapon (abstract)
  │   sở hữu: ammo, state machine (Ready/Firing/Reloading/Swapping/Blocked),
  │   fire-gate, spread state, phát event OnShot(ShotInfo)/OnDryFire/OnReloadStart/End
  ├─ HitscanGun        ── default cho Pistol/SMG/AR/Shotgun-nếu-có. Semi/auto/burst đọc từ config.
  │    ├─ SpinUpGun    ── Heavy: thêm spinup state (thời gian quay nòng trước khi fire-loop mở)
  │    └─ BoltActionGun── Sniper: sau mỗi phát chèn bolt-cycle state (khóa fire, anim kéo khóa nòng)
  │
  └─ (MeleeWeapon — sibling cùng IWeapon, spec riêng, ngoài phạm vi doc này)

WeaponSlots  (local)                  ── 3 ô theo GDD; giữ instance Gun; xử lý swap; nguồn sự thật
                                         local về "đang cầm gì"
GunInput     (local)                  ── InputSystem (KHÔNG XR legacy — Lessons Bài 2): trigger,
                                         nút reload, swap slot
GunFeedback  (local, per-client)      ── subscribe GunEvents → muzzle/tracer/recoil-kick/haptic/sound
TracerPool / ImpactFx (pooled)        ── Bill.Pool; thuần cosmetic
AvatarWeaponSync : NetworkBehaviour   ── trên NetworkAvatar: [Networked] byte EquippedSlot,
                                         RPC_ShotFired (cosmetic, unreliable), RPC_SubmitShotClaim (reliable),
                                         proxy gun visual dưới WristR node
HitValidator (trong AvatarWeaponSync) ── sanity-check phía nạn nhân (mục 7)
```

**Vì sao subclass ít vậy?** Semi/auto/burst khác nhau đúng 1 hàm gate thời gian → config.
Spin-up và bolt làm **đổi hình dạng state machine** (thêm state chắn trước/sau shot) → mới đáng subclass.
Nếu sau này súng mới chỉ khác số → không class mới. Đây là điều kiện để pipeline 15-phút sống được.

**ShotInfo (struct cosmetic, truyền qua event + unreliable RPC):**
`weaponId (byte), muzzlePos, dir, hitPoint, hitNormal, surface (byte), victimRef (PlayerRef|None), isHead (bool)`
— nuôi tracer, impact, sound và cosmetic relay. VFX theo skin chỉ cần lookup FxSet theo
`weaponId + skinId` (mục 8).

**ShotClaim (struct gameplay, reliable targeted RPC):**
`shotId, weaponId, origin, direction, hitPoint, hitPart, clientTick` — chỉ là bằng chứng shooter khai báo.
Không gửi hoặc tin `finalDamage`; victim tự resolve damage/falloff/headshot từ `GunCatalog` sau validation.

---

## 4. Luồng dữ liệu

### 4.1 Bóp cò (client người bắn — tất cả trong 1 frame)

```
GunInput.trigger ──► WeaponSlots.Current.TriggerDown()
  └─► Gun.TryFire()
        gate: state==Ready? ammo>0? fireInterval ok? matchPhase cho phép? spawn-protect?
        │  fail vì hết đạn → OnDryFire (click + haptic nhẹ) + auto-reload nếu config bật
        ▼
      ComputeSpread(bloom state) → Physics.Raycast (mask: Player|World, bỏ qua chính mình,
        QueryTriggerInteraction.Ignore; hit collider tag Head/Body để ra isHead)
        ▼
      build ShotInfo
        ├─► Bill.Events.Fire(GunFiredEvent{shot})        ← GunFeedback ăn: muzzle, tracer,
        │                                                   recoil kick, PlayPitched fire sfx, haptic
        ├─► AvatarWeaponSync.RPC_ShotFired(shot)          ← cosmetic cho người khác (unreliable)
        └─► nếu victimRef != None:
              victimAvatar.WeaponSync.RPC_SubmitShotClaim(shooter, ShotClaim)
                                                          ← targeted đến StateAuthority nạn nhân
```

Shooter quyết định ray đã trúng ai, nhưng không quyết định số damage cuối. Victim không dựng lại ray lịch sử;
victim sanity-check claim, tính khoảng cách từ dữ liệu claim đã kiểm tra và tự tra damage/falloff/headshot từ
`GunCatalog`. Đây là Option A: responsiveness cao, đủ cho phòng nhỏ, nhưng không có competitive guarantee.

### 4.2 Phía các client khác (cosmetic)

```
RPC_ShotFired(shot) đến mọi client ≠ shooter:
  proxyGun = gun visual đang render dưới WristR node của avatar shooter
  muzzle   = proxyGun.muzzleAnchor.position   ← LẤY TỪ PROXY, không dùng shot.muzzlePos
  tracer: muzzle → shot.hitPoint (hitPoint là thật, muzzle là proxy → tracer luôn "dính tay")
  impact VFX + sound 3D tại shot.hitPoint theo shot.surface
  fire sound 3D tại muzzle
```

`shot.muzzlePos` vẫn gửi kèm — chỉ dùng làm fallback khi proxy chưa kịp render súng (late-join đúng
lúc súng nổ) và để HitValidator đo khoảng cách. Vì WristR đã có NT interpolate (Lessons S2), muzzle
proxy tự đúng — không sync gì thêm.

### 4.3 Phía nạn nhân (duy nhất chỗ write networked state)

```
RPC_SubmitShotClaim(...) chỉ chạy trên StateAuthority của avatar nạn nhân:
  HitValidator.CheckAndDedupe(...)  → fail: log reason + drop, không trừ máu
  dmg = GunCatalog.ResolveDamage(claim.weaponId, checkedDistance, checkedHitPart)
  Health -= dmg            ← victim là chỗ duy nhất write [Networked] Health
  Bill.Events.Fire(PlayerHitEvent{...})   ← HealthUI, CombatJuice, haptic rumble local
  Health <= 0 → luồng death/respawn hiện có của CombatSession (không thuộc doc này)
```

Mọi client khác thấy máu tụt qua `[Networked] Health` như hiện tại. Kill-credit: nạn nhân biết
`shooter` từ RPC → fire `PlayerDiedEvent{killer}` (đi qua RPC All có sẵn của CombatSession) —
tiền/score xử lý ở hệ economy, không ở gun system.

### 4.4 Swap / equip

```
GunInput.swap(slotIdx) ──► WeaponSlots:
  Current.Unequip()   (cancel reload, reset spin-up/bolt, TriggerUp cưỡng bức)
  Current = slots[idx]; Current.Equip()  (anim rút súng, equip sound, khóa fire equipTime ngắn)
  AvatarWeaponSync.EquippedSlot = idx    ← 1 byte [Networked]
proxy: Render() thấy EquippedSlot đổi → swap model dưới WristR + equip sound local
```

Reload đổi chỗ tương tự: local state machine thuần, **không networked** — người khác không cần biết
bạn đang reload (nếu sau này muốn anim reload trên proxy: thêm 1 bit `IsReloading` là xong, quyết
định lúc đó, đừng sync trước).

---

## 5. Bảng networked state (toàn bộ gun system)

| State | Kiểu | Ai write | Cơ chế | Ghi chú |
|---|---|---|---|---|
| `EquippedSlot` | `[Networked] byte` | Owner avatar | snapshot | Late-joiner tự thấy đúng súng qua `Render()` — không cần RPC lịch sử |
| Shot (cosmetic) | RPC unreliable, All | Shooter | fire-and-forget | Mất gói = mất 1 tracer, chấp nhận. Worst case Heavy ~15 msg/s/player × 4 = ổn với Fusion |
| `ShotClaim` | RPC **reliable**, targeted StateAuthority nạn nhân | Shooter gửi, victim validate | reliable | Claim không được phép rớt; không chứa final damage đáng tin cậy |
| `Health` | `[Networked]` (đã có — PlayerCombat) | Victim | snapshot | Không đổi hệ hiện tại |

Hết. Không có gì khác. Ammo, reload, spread, spin-up, recoil, skin — tất cả local.

**Ghi chú nâng cấp (chỉ làm khi profiler kêu):** nếu RPC per-shot thành vấn đề (không kỳ vọng với
2-4 player), thay bằng `[Networked] NetworkBool TriggerHeld + int FireStartTick` và proxy tự chạy
fire-loop cosmetic với seed = tick. Đắt hơn về code, rẻ hơn về msg. Ghi lại đây để khỏi re-design mù.

---

## 6. VFX / SFX / Haptics

- **Một consumer duy nhất:** `GunFeedback` subscribe `GunFiredEvent` (local) — và RPC handler của
  `RPC_ShotFired` fire đúng event đó trên các client khác (nhớ luật bus local per-process). Kết quả:
  local và remote đi **chung một đường render**, chỉ khác nguồn phát. Không có code VFX nào phân biệt
  "của tôi hay của nó" ngoài chỗ haptic/recoil (chỉ local).
- **Pooling:** đăng ký `Bill.Pool` keys từ `FxSet` lúc equip lần đầu (lazy). Nhớ `Register` trùng key
  là no-op im lặng (BillGameCore §3) → key phải là **tên asset FxSet + loại** (vd `fx_ak_tracer`)
  để hai skin không giẫm key nhau.
- **Tracer hitscan:** stretched-quad/LineRenderer bay muzzle→hitPoint trong ~50-80 ms bằng BillTween.
  Tuân luật tween: `SetTarget(tracer)` khi tạo, `KillTarget(tracer)` trong `OnReturnedToPool` —
  đây đúng loại bug S15 (đạn treo lơ lửng), đừng tái phạm.
- **Impact theo surface:** `surface` byte lấy từ tag/PhysicMaterial của collider trúng; `FxSet` map
  surface→impact prefab key. Thiếu key → fallback impact chung, không lỗi.
- **Âm thanh:** `Bill.Audio.PlayPitched(fireKey, 1f + microRandom)` local; remote play 3D tại proxy
  muzzle. Dry-fire, reload, equip đều đi SoundSet. Không AudioSource rời trên prefab súng — mọi thứ
  qua Bill.Audio để còn pool + mixer chung.
- **Haptics:** chỉ local: bóp cò (nhẹ, theo weapon weight trong config), trúng đạn (mạnh — đã có từ
  S8 pattern), dry-fire (tick nhẹ). Không bao giờ gửi haptic qua mạng.

---

## 7. Anti-cheat — sanity layer, không phải pháo đài

Mô hình trust-client (shooter quyết định hit) là **chấp nhận có chủ đích** cho Shared Mode phòng nhỏ.
Validator chạy trên nạn nhân, rẻ, chỉ để chặn cheat lộ liễu + bug khuếch đại:

| Check | Luật | Hành động khi fail |
|---|---|---|
| Dedupe | `(shooter, shotId)` chưa từng được accept/reject | drop duplicate, log |
| Fire-rate window | sliding window per-shooter: số hit ≤ `rpm/60 × window × 1.5` | drop hit, log |
| Range/origin | origin gần shooter proxy trong tolerance; claim distance ≤ catalog range × margin | drop hit, log |
| Trạng thái shooter | shooter chết / chưa vào round / đang frozen → không được gây damage | drop hit |
| WeaponId/equipped | có trong catalog và khớp equipped weapon đã replicate | drop hit, log |
| Hit part | chỉ nhận body/head enum hợp lệ; head multiplier lấy từ catalog | drop hit, log |
| Victim state | đang sống, combat mở và không còn spawn protection | drop hit, log |

Log qua DebugOverlay/CheatConsole (guard `UNITY_EDITOR || DEVELOPMENT_BUILD` — nhớ gotcha đừng đặt
làm scene object). **Không kick, không phạt** — sai số mạng thật sẽ dính check này, phạt là phạt oan.

---

## 8. Skin FX — thiết kế sẵn, build sau

- `GunConfig` giữ `FxSet defaultFx`. Skin = bảng `skinId → (FxSet, Material[], modelOverride?)`.
- `GunFeedback` resolve FxSet **một lần lúc equip** (không lookup per-shot).
- Skin của người khác: v1 KHÔNG sync skin (proxy dùng defaultFx). Khi economy cần khoe skin:
  thêm `[Networked] byte SkinId` cạnh EquippedSlot — 1 byte, xong. Ghi vào đây để không ai
  "tiện tay" sync cả FxSet reference qua mạng.
- Pool key theo FxSet asset name → hai skin nặng FX khác nhau không tranh pool.

---

## 9. Edge cases — bảng bắt buộc test

| # | Tình huống | Hành vi chốt |
|---|---|---|
| 1 | Swap khi đang reload | Cancel reload, đạn giữ nguyên số trước reload |
| 2 | Giữ cò khi swap xong | Phải NHẢ ra bóp lại — `Equip()` không kế thừa trigger state |
| 3 | Hết đạn bóp cò | Dry-click + haptic nhẹ; auto-reload nếu config bật |
| 4 | Chết giữa reload / giữa spin-up | `Unequip()` cưỡng bức → mọi state về Ready; respawn cầm slot cũ |
| 5 | Heavy: nhả cò giữa spin-up | Spin-down (tham số config), không bắn phát nào |
| 6 | Sniper: swap giữa bolt-cycle | Cancel cycle; equip lại → súng ở trạng thái đã lên đạn |
| 7 | Bắn đúng lúc round-end freeze | Gate `matchPhase` trong `TryFire` (đọc từ event phase local — không hỏi master) |
| 8 | Spawn-protection | GDD: hết protect khi bắn phát đầu → `TryFire` thành công thì clear flag local + hệ combat hiện có |
| 9 | Nạn nhân chết bởi 2 người cùng tick | Hai `ShotClaim` đều đến; check `Health > 0` trước khi trừ; kill-credit cho claim được accept trước — chấp nhận không công bằng tuyệt đối |
| 10 | Nạn nhân vừa respawn thì RPC hit cũ mới tới | Validator check state (đang protect/mới respawn) → drop |
| 11 | RPC_ShotFired rớt gói | Mất tracer 1 phát — cosmetic, kệ |
| 12 | Late-joiner vào giữa fight | Thấy đúng súng qua EquippedSlot; không thấy tracer quá khứ — đúng kỳ vọng |
| 13 | Master client rời phòng giữa fight | Không ảnh hưởng — gun system không đụng master (mục 2) |
| 14 | Scene load (hub↔arena) | WeaponSlots là local component trên LocalPlayer (DDOL) — sống qua scene; AvatarWeaponSync set lại EquippedSlot trong Spawned của avatar mới (nhớ: registry Fusion không sống qua scene load — Lessons Bài 6, dùng `NetworkAvatar.Local`) |
| 15 | Domain reload nửa chừng trong Editor | `Bill.IsReady` guard mọi entry point (BillGameCore §3); Stop→Play lại |
| 16 | Tracer pooled dính state cũ | Reset toàn bộ trong `OnSpawnedFromPool`; tween theo luật SetTarget/KillTarget |
| 17 | Raycast trúng chính collider mình / collider súng | Mask + ignore list build lúc Equip |
| 18 | Bắn xuyên khe cửa mà proxy shooter đứng lệch (interp) | Không xử lý — hitscan tính trên client bắn, đó là sự thật duy nhất; nạn nhân chỉ sanity-check |

---

## 10. Danh sách script (một file một việc)

```
Assets/_Game/Scripts/Guns/
  GunConfig.cs          SO stats + behaviour enum + refs (BillInspector attribute như config khác)
  FxSet.cs              SO khóa VFX
  SoundSet.cs           SO khóa SFX
  GunCatalog.cs         SO danh mục weaponId → GunConfig (shop + validator + proxy visual tra chung)
  GunEvents.cs          struct ShotInfo + GunFiredEvent/GunDryFireEvent/GunReloadEvent (Bill.Events)
  Gun.cs                abstract: state machine + fire gate + ammo + reload + phát event
  HitscanGun.cs         raycast + spread + falloff; semi/auto/burst từ config
  SpinUpGun.cs          : HitscanGun — thêm spin-up/spin-down state
  BoltActionGun.cs      : HitscanGun — thêm bolt-cycle state sau mỗi phát
  GunView.cs            trên modelPrefab: muzzleAnchor, animator hooks, KHÔNG logic
  WeaponSlots.cs        3 slot local, swap, nguồn sự thật local (trên LocalPlayer, DDOL)
  GunInput.cs           InputSystem actions: trigger/reload/swap (tuyệt đối không UnityEngine.XR legacy)
  GunFeedback.cs        event → muzzle/tracer/impact/sound/haptic/recoil-kick; resolve FxSet lúc equip
  TracerFx.cs           pooled tracer (OnSpawnedFromPool reset + tween đúng luật)
  Net/
    AvatarWeaponSync.cs [Networked] EquippedSlot (+SkinId sau này), RPC_ShotFired, RPC_SubmitShotClaim,
                        proxy gun visual dưới WristR node, HitValidator
```

Ước lượng: ~14 file, không file nào nên quá ~250 dòng. `PlayerCombat`/`Health`/economy **giữ nguyên** —
gun system chỉ là nguồn `ShotClaim` mới; victim vẫn là nơi duy nhất xác nhận và write damage.

**Hook để dành (không build ở v1):** two-hand grip = 1 component `ForeGripAnchor` trên GunView, khi
tay trái vào vùng anchor thì hướng súng = trung bình 2 wrist — thuần local + cosmetic, không đổi
kiến trúc mạng. Grenade/rocket = quay lại NetworkProjectile pattern của throw system.

---

## 11. Workflow: thêm súng mới trong 15 phút

1. Duplicate `GunConfig` gần nhất → sửa stats (damage/rpm/mag/spread/falloff/tier).
2. Gán `modelPrefab` (có GunView + muzzleAnchor), `FxSet`, `SoundSet` (tái dùng set cũ được).
3. Thêm vào `GunCatalog` → tự có mặt trong shop + validator + proxy visual.
4. `behaviour = Hitscan` trừ khi cần spin-up/bolt. **Không class mới cho súng chỉ khác số.**
5. Test checklist tối thiểu: bắn semi + auto, hết đạn, reload, swap giữa reload, 2-client thấy
   tracer + máu tụt, edge #1/#2/#5 nếu là behaviour đặc biệt.

---

## 12. Thứ tự implement đề xuất

1. `GunConfig/FxSet/SoundSet/GunCatalog/GunEvents` + `Gun` + `HitscanGun` — bắn được local, log hit.
2. `GunFeedback` + `TracerFx` + pool — feel local hoàn chỉnh (đây là gate "gun feel" của
   TECH VALIDATION trong GDD — đo feel TRƯỚC khi network).
3. `WeaponSlots` + `GunInput` + swap/reload.
4. `AvatarWeaponSync`: EquippedSlot + RPC_ShotFired + proxy visual — 2 client thấy nhau bắn.
5. `RPC_SubmitShotClaim` nối `HitValidator` + `GunCatalog` vào Health hiện có.
6. `SpinUpGun`/`BoltActionGun` + bảng edge case mục 9.
