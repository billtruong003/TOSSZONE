# TOSSZONE — Session Handoff

> Cập nhật mỗi session. Đọc file này đầu tiên khi bắt đầu session mới.

---

## Session 9 — 2026-07-01 (session vừa xong)

### Đã làm được — full minigame implementation pass

**PlayerCombat.cs ✅ (bổ sung)**
- `[Networked] OwnedMask`, `EquippedIndex` (init -1), `Ammo`
- `TryBuyWeapon(slotIndex, cost)` — deduct money + set bit
- `OwnsWeapon(slotIndex)` — bitmask check
- `EquipWeapon(slotIndex)` — authority write EquippedIndex
- `UseAmmo()` — decrement, return false if 0
- `ResetForRound()` — full wipe: Health/Money/OwnedMask/EquippedIndex(-1)/Ammo + fires `WeaponResetEvent`
- `AllInstances` static list (add Spawned, remove Despawned)
- `IsPlayer` bool (default true; bots set false)

**MinigameDef.cs ✅ (bổ sung)**
- `WeaponConfig[] weaponCatalog` + `bestOf` + `roundDuration`

**New scripts (all compiled clean):**
| File | Role |
|---|---|
| `Combat/CombatSession.cs` | DontDestroyOnLoad singleton; resolves catalog từ `Resources/Minigames/<id>`; `RoundElapsed` timer; `NotifyRoundStart()` |
| `Throwing/HandWeapon.cs` | Per-hand weapon dispatcher; polls `EquippedIndex`; disables ThrowController for non-ThrowBallistic; ProjectileLaunch/Hitscan/Melee fire; Initialize() gọi từ NetworkAvatar.Spawned() |
| `UI/WristWeaponSelector.cs` | 3-slot wrist carousel; palm-up detect; left-stick flick nav; grip confirm buy/equip; unlock time gate |
| `UI/RewardText.cs` | `PooledObject` floating text; pool key `"rewardtext"`; BillTween rise+fade |
| `Combat/CatchController.cs` | SphereCollider trigger; catches ThrowProjectile (IsCatchable) + NetworkProjectile; Normal catch +1 Ammo, Power catch +2 Ammo; fires `BallCaughtEvent` |
| `Combat/BuffRingConfig.cs` | SO: element (Ice/Fire/Multi/Speed/Shield), color, multiplier, velocityScale, areaScale, shieldSelf, respawnDelay |
| `Combat/BuffRing.cs` | NetworkBehaviour; BillTween drift; OnTriggerEnter → ApplyBuff + Despawn; authority-only write; ⚠️ Shared Mode note: only applies if ring authority == projectile authority |
| `Combat/RingSpawner.cs` | NetworkBehaviour; `NetworkArray<NetworkId>` SlotRings + `NetworkArray<TickTimer>` RespawnTimers; FixedUpdateNetwork polls slot presence via `Runner.FindObject(id)`; `ResetRings()` |
| `Combat/ArenaManager.cs` | NetworkBehaviour scene object; `[Networked]` Phase/Round/ScoreA/ScoreB/PhaseTimer; Warmup→Playing→RoundEnd→MatchEnd; checks AllInstances (IsPlayer) for alive count; `ResetAllCombat()` |
| `Combat/DummyBotDriver.cs` | NetworkBehaviour; `[Networked] TickTimer ThrowTimer`; authority finds nearest real player + fires NetworkProjectile via `Runner.Spawn`; 2–3.5s random interval |

**ThrowProjectile.cs ✅ (bổ sung)**
- `IsCatchable`, `IsPower` properties; `SetPower(bool)`, `SetUncatchable()`, `OnCaught()` (kills tween + pools)
- Reset in `OnSpawnedFromPool()`

**DummyAvatar.cs ✅ (1 dòng)**
- `_combat.IsPlayer = false` trong Spawned() — loại khỏi win-condition count

**TossLocomotionInput.cs ✅ (bổ sung)**
- Dash: right stick click → burst (`_dashStrength=3.5`, `_dashDuration=0.18s`, `_dashCooldown=0.8s`)
- Jump: A button → `_player.Jump()`

**NetworkAvatar.cs ✅ (bổ sung)**
- Trong Spawned() (authority): `foreach HandWeapon → hw.Initialize(combat, Runner)` + `WristWeaponSelector?.Initialize(combat)`
- `using TossZone.Throwing` thêm

### Trạng thái sau session 9
| Layer | Status |
|---|---|
| PlayerCombat economy (buy/equip/ammo/reset) | ✅ |
| CombatSession singleton + catalog | ✅ code; ⬜ prefab chưa đặt trong scene |
| HandWeapon (per-hand fire dispatch) | ✅ code; ⬜ chưa add vào NetworkAvatar prefab |
| WristWeaponSelector | ✅ code; ⬜ chưa build prefab |
| RewardText (floating damage text) | ✅ code; ⬜ chưa build prefab + đăng ký pool |
| CatchController | ✅ code; ⬜ chưa add vào player prefab |
| BuffRingConfig (5 ring SO assets) | ✅ code; ⬜ chưa tạo SO assets |
| BuffRing + RingSpawner | ✅ code; ⬜ chưa build prefabs + đặt trong scene |
| ArenaManager | ✅ code; ⬜ chưa đặt NetworkObject trong 02_Arena |
| DummyBotDriver | ✅ code; ⬜ chưa add vào DummyAvatar prefab |
| Dash + Jump | ✅ code |

### Việc cần làm tiếp (thứ tự)
1. **M1 verify**: bắn vào DummyAvatar → pips giảm → respawn sau 3s (không cần gì mới, chỉ verify)
2. **MCP scene setup**: đặt `CombatSession` prefab (DontDestroyOnLoad) + `ArenaManager` NetworkObject trong `02_Arena`
3. **MCP prefab additions**: add `HandWeapon` + `CatchController` vào `NetworkAvatar` prefab; add `DummyBotDriver` vào `DummyAvatar` prefab
4. **WeaponConfig assets**: tạo 7 SO assets (Rock, Gun, Grenade, Bazooka, BigBoom, LandMine, Sword) + đăng ký vào MinigameDef arena catalog
5. **BuffRing prefabs**: tạo 5 prefabs (Ice/Fire/Multi/Speed/Shield) + đặt `RingSpawner` với spawn points trong scene
6. **WristWeaponSelector prefab**: world-space Canvas child của wristL node
7. **RewardText prefab**: WorldSpace TextMeshPro + đăng ký pool key `"rewardtext"`
8. **2-player verify**: S2 HeldBall + S3 Projectile + hit detection với 2 clients thật

### ⚠️ Gotchas (mới thêm)
- **CombatSession.ResolveMinigameCatalog** load từ `Resources/Minigames/<id>` — MinigameDef asset phải đặt ở `Resources/Minigames/arena.asset` (hoặc id tương ứng).
- **DummyBotDriver dùng `rb.linearVelocity`** (Unity 6 API) — projectile prefab cần Rigidbody.
- **BuffRing authority conflict**: trong Shared Mode, ring chỉ apply buff khi ring authority == projectile authority. Fix đúng là RPC gọi về phía projectile's state authority.
- **HandWeapon.Initialize() gọi authority-only** — non-authority clients không có combat/runner → null guard đủ.
- **ThrowController disabled** khi HandWeapon equip non-ThrowBallistic weapon — re-enable khi switch về rock (index -1).
- Mọi gotcha Session 7+8 vẫn còn hiệu lực.

### Files thay đổi (session 9)
```
M Assets/_Game/Scripts/Combat/PlayerCombat.cs          — economy methods + AllInstances + IsPlayer
M Assets/_Game/Scripts/Minigame/MinigameDef.cs         — weaponCatalog + bestOf + roundDuration
+ Assets/_Game/Scripts/Combat/CombatSession.cs         — new
+ Assets/_Game/Scripts/Throwing/HandWeapon.cs          — new
+ Assets/_Game/Scripts/UI/WristWeaponSelector.cs       — new
+ Assets/_Game/Scripts/UI/RewardText.cs                — new
+ Assets/_Game/Scripts/Combat/CatchController.cs       — new
+ Assets/_Game/Scripts/Combat/BuffRingConfig.cs        — new
+ Assets/_Game/Scripts/Combat/BuffRing.cs              — new
+ Assets/_Game/Scripts/Combat/RingSpawner.cs           — new
+ Assets/_Game/Scripts/Combat/ArenaManager.cs          — new
+ Assets/_Game/Scripts/Combat/DummyBotDriver.cs        — new
M Assets/_Game/Scripts/Throwing/ThrowProjectile.cs     — IsCatchable/IsPower/OnCaught
M Assets/_Game/Scripts/Combat/DummyAvatar.cs           — IsPlayer=false
M Assets/_Game/Scripts/Player/TossLocomotionInput.cs   — Dash + Jump
M Assets/_Game/Scripts/Player/NetworkAvatar.cs         — HandWeapon+WristSelector init
M Docs/HANDOFF.md                                      — this file
```

---

## Session 8 — 2026-06-30 (session vừa xong)

### Đã làm được

**HealthUI ✅**
- `Assets/_Game/Scripts/UI/HealthUI.cs` — MonoBehaviour: 5 pip renderers, billboard LateUpdate, poll `[Networked]` Health mỗi frame (cheap field read, visible mọi client). `Bind(PlayerCombat)` gọi từ Spawned().
- **NetworkAvatar prefab** — child `HealthUI` (localPos `0,2,0`): 5 sphere pip con xếp arc nhẹ (`±0.20/±0.10/0` x, `0/0.04/0.06` y), scale `0.055`, collider stripped. `_pipRenderers` wired via SerializedObject.
- `NetworkAvatar.Spawned()` → `healthUI.Bind(combat)` (thêm `using TossZone.Combat/UI`).

**DummyAvatar (Bot) ✅**
- `Assets/_Game/Scripts/Combat/DummyAvatar.cs` — NetworkBehaviour: `[Networked] TickTimer RespawnTimer`, tự reset health sau 3s khi chết. `Render()` thay màu grey khi dead (cached `_wasDeadLastRender`). `Spawned()` → `HealthUI.Bind(combat)`.
- **DummyAvatar prefab** — Capsule body (height 1.8m) + `CapsuleCollider` trigger layer `Hittable=15` (center 0,0.9,0) + `NetworkObject` + `PlayerCombat` + `DummyAvatar` + `HealthUI` child. **Fusion baked: 2 NetworkedBehaviours** (PlayerCombat + DummyAvatar). Đặt trong `02_Arena` tại `(0,0,4)` quay `180°`.

**Guard fix trong NetworkProjectile ✅**
- `victim.Object.StateAuthority == Shooter` → `victim.Object.InputAuthority == Shooter`.
- **Lý do**: scene objects (DummyAvatar) có `InputAuthority = PlayerRef.None`, không bao giờ match Shooter → solo test không bị block. Player avatar vẫn đúng vì `InputAuthority == StateAuthority` với owner trong Shared Mode.

### Trạng thái
| Layer | Status |
|---|---|
| HealthUI (5 pip curved, billboard) | ✅ built + wired |
| DummyAvatar (bot target, auto-respawn) | ✅ built + placed in Arena |
| Hit detection guard (solo test) | ✅ fixed |
| HandWeapon (equip + fire) | ⬜ next |
| Buff-ring system | ⬜ next |
| Wrist selector | ⬜ next |

### Việc cần làm tiếp (thứ tự)
1. **Verify hit detection** — bắn vào DummyAvatar, pips giảm, respawn sau 3s. Check `NetworkObject.NetworkedBehaviours` trên DummyAvatar prefab nếu hit không register.
2. **HandWeapon** — equip `WeaponConfig` lên tay + fire theo `fireMode` (các vũ khí khác bắn đạn).
3. **Buff-ring system** — `RingSpawner` 5 vòng MS_Circle trôi + xuyên-qua SET modifier-hook của projectile.
4. **Wrist selector** — UI chọn vũ khí + behaviors (mine fuse / bazooka arc / sword deflect / catch).

### ⚠️ Gotchas (cập nhật)
- **execute_code guard ĐÚNG**: `Application.dataPath.Contains("ThrowingShot")` — KHÔNG phải "TOSSZONE" (folder tên ThrowingShot, không phải TOSSZONE).
- **Hittable layer = 15** — DummyAvatar body + player Hitbox đều cần layer này để `NetworkProjectile.OverlapSphere` detect.
- **HealthUI là MonoBehaviour** (không phải NetworkBehaviour) → không cần Fusion bake khi thêm vào prefab.
- **DummyAvatar cần bake** (có DummyAvatar.cs NetworkBehaviour): force-reimport đã chạy, verify `NetworkedBehaviours.Length == 2`.
- Mọi gotcha Session 7 vẫn còn hiệu lực (xem bên dưới).

### Files thay đổi
```
+ Assets/_Game/Scripts/UI/HealthUI.cs                — new, 5-pip curved billboard
+ Assets/_Game/Scripts/Combat/DummyAvatar.cs         — new, bot target + TickTimer respawn
M Assets/_Game/Scripts/Player/NetworkAvatar.cs       — HealthUI.Bind() in Spawned() + using
M Assets/_Game/Scripts/Throwing/NetworkProjectile.cs — guard InputAuthority fix
M Assets/_Game/Prefabs/NetworkAvatar.prefab          — HealthUI child (5 pips)
+ Assets/_Game/Prefabs/DummyAvatar.prefab            — new, Fusion-baked
M Assets/_Game/Scenes/02_Arena.unity                 — DummyAvatar instance at (0,0,4)
M Docs/HANDOFF.md                                    — this file
```

---

## Session 7 — 2026-06-30 (session vừa xong)

### Đã làm được

**Dọn merge (S5+S6) + verify**
- Xóa **duplicate `PlayerRig`** (bug chí mạng: 2 PlayerRig cùng GameObject → `Awake` singleton gọi `Destroy(LocalPlayer)` → phá rig lúc start; trạng thái merge này chưa từng chạy) + duplicate `TossLocomotionInput`. Giữ PlayerRig `Root=AutoHandPlayer`.
- `headCamera` đã gán (root-cause locomotion S5). Dọn **debug HUD** khỏi `TossLocomotionInput`/`ThrowController`. Reconcile 2 doc networking.

**Fix leg knee-twist**
- `AvatarLegPoser`: đùi (upper leg) → **stateless `LookRotation` + captured rest** (trước dùng `FromToRotation` tích lũy → roll-drift vặn gối khi di chuyển nhiều). Shin đã fix S6, đùi bị sót.

**Palette tooling**
- `Assets/_Game/Editor/PaletteAssigner.cs` — tool gán M_Pallet hàng loạt (prefab renderer + **FBX default remap**), right-click `TOSSZONE ▸ Assign Palette` hoặc menu `TOSSZONE/Palette`. Tìm M_Pallet theo TÊN (move file thoải mái). Đã gán cho toàn kit MS_ (20 prefab) + FBX defaults + lightsword. M_Pallet đã move sang `Materials/`.

**Design combat — `Docs/Combat_Minigame_Design.md` v2 (đọc kỹ trước khi build tiếp)**
- Roster THẬT: **7 vũ khí chiến đấu** (Rock/Gun/Grenade/Bazooka/BigBoom/LandMine/Sword). Egg/Tomato = người CHẾT ném từ khán đài; Poop = cosmetic.
- Win-condition: **1v1** (chết trước thua) / **BO3** / **BO5** (team last-standing); ví reset $0/hiệp.
- Hành vi từng vũ khí (§5.4) · Sword **rút sau lưng, DEFLECT-ONLY** (§9) · catch random-theo-màu (§7) · buff-ring áp **mọi đạn ném** (§10).

**Weapon system ✅**
- `WeaponConfig.cs` (SO configurable: AcquireMode/HandSource/FireMode + economy/unlock/fire).
- **7 WeaponConfig asset** (`Weapons/`), wired heldPrefab/projectile/throwConfig (5 món stats thật, LandMine/Sword placeholder).
- 7 weapon prefab → **AutoHand grabbable** (Rigidbody+collider+Grabbable+layer Grabbable).

**Combat foundation ✅ (built + wired + Fusion-baked)**
- `PlayerCombat` (NetworkBehaviour): `[Networked]` Health(5)/Money · `RPC_TakeHit` (**victim-authority-writes** đúng Shared Mode) · income theo thời gian + `RewardHit` + `ResetForRound` · static `Local`.
- `CombatEvents`: PlayerHit/PlayerDied/MoneyChanged (Bill.Events).
- `NetworkProjectile`: **buff-hook sẵn** (`Multiplier/VelocityScale/AreaScale/Element`, default no-buff) + `Shooter` + hit detection (authority OverlapSphere → `RPC_TakeHit` + reward).
- `ThrowController`: set Shooter khi spawn networked projectile.
- **Wired**: layer `Hittable=15` · PlayerCombat trên NetworkAvatar prefab (**baked** — verify `NetworkedBehaviours` chứa PlayerCombat) · Hitbox trigger capsule · NetworkProjectile mask=Hittable/dmg=1/r=0.3.

### Trạng thái
| Layer | Status |
|---|---|
| Throw + IK + NetworkAvatar + S2/S3 | ✅ (S6) |
| Leg knee-twist | ✅ fixed |
| Weapon data (7 config + grabbable prefab) | ✅ |
| Combat foundation (PlayerCombat + hit + buff-hook) | ✅ built+wired+baked |
| HealthUI / Bot / HandWeapon / buff-ring / selector | ⬜ next |

### Việc cần làm tiếp (thứ tự)
1. **HealthUI (5 cục curved)** — bind `PlayerCombat.Health`, billboard. Visible nhanh nhất.
2. **Bot `DummyAvatar`** — NetworkAvatar giả (Hitbox+PlayerCombat) làm target test hit/damage **solo** (ném không tự trúng mình).
3. **HandWeapon** — equip WeaponConfig + fire theo `fireMode` (các vũ khí khác bắn).
4. **Buff-ring system** — RingSpawner (5 vòng MS_Circle trôi) + xuyên-qua **SET modifier-hook** (đã buff-aware → zero rework).
5. **Wrist selector** + behaviors (mine fuse / bazooka arc / sword deflect / catch). Thêm field hành vi vào WeaponConfig khi build (fuseDelay/armsOnGround/projectileGravity/laserSight/attacksPlayers/canDeflect).

### ⚠️ Gotchas (QUAN TRỌNG)
- **Multi-editor routing CỰC kỳ flip** khi mở 2 editor ("Teabag - Copy" + TOSSZONE): `read_console`/`refresh_unity`/`manage_scene` nhảy qua lại → từng lưu nhầm scene Overview + đọc console Teabag. **ĐÓNG Teabag** trước khi MCP. `execute_code` **tự an toàn** (Teabag từ chối nó) → dùng cho mutation, self-guard `Application.dataPath.Contains("TOSSZONE")`.
- New folder/file → `refresh_unity scope=all` (scope=scripts bỏ sót file trong folder mới).
- Thêm NetworkBehaviour vào prefab → cần **Fusion bake** (force-reimport `ImportAssetOptions.ForceUpdate` → verify `NetworkObject.NetworkedBehaviours`).
- **Projectile buff-aware**: ring/catch chỉ SET 4 hook → không sửa lại projectile/damage.
- WeaponConfig stats = **placeholder** (nhất là LandMine/Sword) → tune Inspector.

### Commits (đã push hết)
`f25f786` cleanup+leg+palette+kit · `2053480` weapon system + combat foundation code · `8762d1e` wire foundation.

### Cần chốt (design opens)
Respawn 1-mạng/hiệp? · Team size BO3/BO5 (3v3/5v5)? · Egg/Tomato heckle (nuisance hay chỉ splat)? · LandMine stats thật? · Sword model (đã có `lightsword`).

---

## Session 6 — 2026-06-30

### Đã làm được

**Fix IK + rig (carry-over từ S5 verify)**
- `AvatarArmPoser.cs` — forearm roll fix: `LookRotation(forearmFwd) * lowerRest` thay `FromToRotation`
- `AvatarLegPoser.cs` — shin roll fix: cùng pattern, field `_lLowerLegRest / _rLowerLegRest`
- Camera eye offset: `TrackedPoseDriver.originPose = (0, -0.05, 0.07)` + `UseRelativeTransform = true`
- `PlayerRig` + `TossLocomotionInput` thêm vào scene (thiếu từ S5), wire đầy đủ
- `AutoHandPlayer.minMaxHeight.y = 1.6f` — fix capsule tự reset về 1.7

**S2 — Networked Held Ball** ✅
- `NetworkAvatar.cs`: thêm `[Networked] bool HoldingBall`, field `_heldBallVisual (Renderer)`
  - `FixedUpdateNetwork()`: đọc `ThrowController.LocalHoldingBall`
  - `Render()` (proxies): `_heldBallVisual.enabled = HoldingBall`
- `ThrowController.cs`: thêm `public static bool LocalHoldingBall`, set trong `ShowHeld()`
- Prefab `NetworkAvatar.prefab`: thêm sphere `HeldBallVisual` con của `WristR` (0.09 scale, ThrowBall.mat), `_heldBallVisual` wired

**S3 — Networked Projectile** ✅
- `NetworkProjectile.cs` (new): thin NetworkBehaviour — `LinkTo(Transform)`, FUN copy pos từ local proj, Spawned ẩn renderer cho authority
- `ThrowController.cs`: field `_netProjectilePrefab (Fusion.NetworkObject)`, methods `SpawnNetworkProjectile()`, `DespawnNetworkProjectile()`, `TryGetRunner()`, tất cả trong `#if PHOTON_FUSION`
- Prefab `NetworkProjectile.prefab` (new): sphere + NetworkObject + NetworkTransform + NetworkProjectile, wired vào ThrowController trong scene

---

### Trạng thái hiện tại

| Layer | Status |
|---|---|
| Local throw (grab → swing → fire) | ✅ Done |
| IK avatar (arm + leg) | ✅ Done (roll fix session 6) |
| NetworkAvatar sync (4 transforms) | ✅ Done |
| S2 — held ball visible to others | ✅ Done session 6 |
| S3 — projectile visible to others | ✅ Done session 6 |
| S5 — Haptics 3-tier (wind/release/impact) | ⬜ Not started |
| S6 — Juice T2 (flash, squash-stretch, trail glow) | ⬜ Not started |
| S8 — Hit detection + cross-player haptic RPC | ⬜ Next priority |
| S9 — 2-player full verify | ⬜ Blocked on S8 |
| MinigameManager networking | ⬜ Not started |
| Team assignment + spawn points | ⬜ Not started |

---

### Direction pivot (2026-06-30)

**Không còn đi theo P2 (buff rings) → P3 (shop/weapon) → P4 (lobby) nữa.**
Hướng mới: **Minigame loop đầu tiên** — hit detection → score → round end → reset. Trải nghiệm chơi được trước khi làm thêm content.

tasks.json đã cập nhật:
- P2 title: `[DEFERRED]` — buff rings không ưu tiên
- P3 title: `[DEFERRED]` — vũ khí/shop/skin để sau
- P4 title: `[DEFERRED]` — full lobby để sau
- Section `1.MG` mới trong P1: 6 task minigame (MG1-MG6)

---

### Việc cần làm tiếp (theo thứ tự ưu tiên)

**1. Verify S2 + S3 với 2 player thật** (nền tảng — làm trước khi build MG)
```
[ ] Player A thấy HeldBallVisual ở wrist của B khi B đang hold
[ ] Player A thấy NetworkProjectile bay khi B throw
[ ] Không có double-ball trên authority (HeldBallVisual.enabled = false cho owner)
[ ] NetworkProjectile despawn đúng khi ball land
```

**2. MG1 — Hit detection** (= S8 cũ, đổi tên để fit minigame context)
```csharp
// NetworkProjectile: trigger collider detect overlap với NetworkAvatar body collider
// → authority fires RPC to all:
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_OnHit(PlayerRef hitPlayer, Vector3 hitPoint)
{
    SpawnImpactVFX(hitPoint);
    if (Runner.LocalPlayer == hitPlayer)
        TriggerHaptic(HapticStrength.Strong, 0.3f);
}
```

**3. MG2 — MinigameManager networking**
- `[Networked] NetworkDictionary<PlayerRef, int> Scores`
- `[Networked] MinigamePhase Phase`
- `[Networked] float TimeRemaining`
- `RegisterHit(hitPlayer)` trong `FixedUpdateNetwork()` (không RPC)

**4. MG3 → MG6** — Portal trigger → scoreboard → round end → spawn points
Chi tiết: xem section `1.MG` trong `Docs/tasks.json`

**5. S5 — Haptics 3-tier** (có thể song song với MG1-2)
- Wind-up, release, impact — tất cả local, không cần thêm networking

---

### Gotchas cần nhớ

- **Runner.Spawn() trong Shared Mode**: synchronous return, caller thành State Authority. Gọi từ MonoBehaviour OK nếu dùng `NetworkRunner.Instances[0]`.
- **NetworkProjectile.LinkTo()** phải gọi ngay sau Spawn (trong cùng frame). FixedUpdateNetwork tick đầu sẽ copy pos. Không có race condition vì FUN chạy sau frame hiện tại.
- **HeldBallVisual** là con của WristR trong `NetworkAvatar` prefab — không phải scene object. Khi test solo thì không thấy (owner bị ẩn); cần 2 client mới verify được.
- **Meta XR Simulator**: kích hoạt mỗi session qua `Meta > Meta XR Simulator > Activate`. Process-level env var, không persist.
- **AssetImportWorker windows**: nếu thấy nhiều cửa sổ nhỏ 136×39px khi compile → là AssetImportWorker bình thường, không phải bug. Fix: `Edit > Preferences > Asset Pipeline > Standby Import Worker Count = 0`.

---

### Files thay đổi trong session 6

```
M  Assets/_Game/Scripts/Player/AvatarArmPoser.cs        — forearm roll fix
M  Assets/_Game/Scripts/Player/AvatarLegPoser.cs        — shin roll fix
M  Assets/_Game/Scripts/Player/NetworkAvatar.cs         — HoldingBall + HeldBallVisual
M  Assets/_Game/Scripts/Throwing/ThrowController.cs     — LocalHoldingBall + S3 Runner.Spawn
+  Assets/_Game/Scripts/Throwing/NetworkProjectile.cs   — new, thin Fusion wrapper
M  Assets/_Game/Prefabs/NetworkAvatar.prefab            — HeldBallVisual sphere child of WristR
+  Assets/_Game/Prefabs/NetworkProjectile.prefab        — new, NetworkObject+NT+NetworkProjectile
M  Assets/_Game/Scenes/01_TOSSZONE_Main.unity           — PlayerRig + TossLocomotionInput + cam offset
M  Docs/Network_Architecture_Lessons.md                 — S2/S3 marked done, actual impl documented
+  Docs/HANDOFF.md                                      — this file
```
