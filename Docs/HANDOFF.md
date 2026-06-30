# TOSSZONE — Session Handoff

> Cập nhật mỗi session. Đọc file này đầu tiên khi bắt đầu session mới.

---

## Session 9c — 2026-07-01 (session vừa xong) — Ref audit + hotfix pass

### Vấn đề cốt lõi session này
Session 9b tạo toàn bộ code/assets/prefabs nhưng **rất nhiều ref null** do MCP verify chạy nhầm scene (Bootstrap thay vì 02_Arena) và bỏ sót kiểm tra ThrowController. Session này dump toàn bộ serialized fields của mọi component, phát hiện và fix hết.

### Tất cả fixes đã làm

| Fix | Vấn đề | Kết quả |
|---|---|---|
| ThrowController 4 refs | `_config`, `_projectilePrefab`, `_heldBallPrefab`, `_netProjectilePrefab` đều null | ✅ wired |
| HandWeapon._bladeTip | null — tạo child `BladeTip` dưới WristR tại (0,0,0.35) | ✅ wired |
| BillBootstrapConfig.defaultPools | hoàn toàn trống — `Bill.Pool.Spawn("throwprojectile")` fail silently | ✅ registered 2 keys |
| RewardText.prefab | chưa tồn tại — code có nhưng prefab chưa build | ✅ created |
| CatchController SphereCollider | isTrigger=false | ✅ set true |
| NetworkProjectile Rigidbody | thiếu — bot dùng `rb.linearVelocity`, đạn đứng im | ✅ added |
| NetworkProjectile hit detection | early-return khi `_localProjectile==null` → bot throw không hit được ai | ✅ fixed |
| ArenaManager spawn points | `_spawnPointsA/B` trống | ✅ wired to [SpawnA]/[SpawnB] |
| BuffRing SphereCollider | isTrigger=false | ✅ set true |

---

## ══ DANH SÁCH TASK NGÀY MAI ══

> Đọc kỹ phần này trước khi bắt đầu. Làm theo thứ tự: verify trước, fix bug, rồi mới build thêm.

### BƯỚC 0 — Môi trường (5 phút)

```
1. Mở Unity project ThrowingShot
2. Kiểm console: 0 compile error (expected)
3. Mở scene: 02_Arena
4. Bật Meta XR Simulator: Meta > Meta XR Simulator > Activate
5. Play mode → kiểm [Bill] Ready trong console
```

---

### BƯỚC 1 — Test M1: Ném vào DummyAvatar

**Mục tiêu**: xác nhận hit detection + health system hoạt động.

```
Trong Play mode (02_Arena):
  1. Chờ Warmup 5s → ArenaManager chuyển sang Playing
  2. Kiểm: 3 BuffRing xuất hiện tại 3 spawn points và drift lên xuống
  3. Nhấn G (giữ) → HeldBallVisual xuất hiện ở wrist
  4. Nhấn T → ball bay thẳng về phía DummyAvatar
  5. Kiểm: DummyAvatar HealthUI pip giảm từ 5→4
  6. Nhấn T thêm 4 lần → DummyAvatar chết (pips=0, body chuyển màu xám)
  7. Kiểm: sau 3s → DummyAvatar respawn (pips về 5, màu bình thường)
```

**Nếu không hit được** (pips không giảm):
- Kiểm layer của `[Hitbox]` trên DummyAvatar prefab → phải là `Hittable` (15)
- Kiểm `NetworkProjectile._hittableMask` → phải include layer 15
- Kiểm console: có log "Spawned" từ Fusion không → nếu không có nghĩa Runner chưa start

**Nếu ball không spawn** (T key không làm gì):
- Kiểm console: `[Bill] Pool 'throwprojectile' not found` → BillBootstrapConfig chưa boot đúng
- Kiểm: scene 02_Arena có `CombatSession` GO không (phải tồn tại để Fusion start)
- Kiểm: `PlayerSpawnManager` trong scene có spawn NetworkAvatar không

---

### BƯỚC 2 — Test M2: Bot ném ngược lại

**Mục tiêu**: DummyBotDriver tự throw NetworkProjectile và trúng player.

```
Tiếp tục trong Play mode:
  1. Chờ 2-3.5s → DummyBotDriver throw (interval random)
  2. Kiểm: thấy NetworkProjectile bay từ DummyAvatar về phía player
  3. Kiểm: Player HealthUI pip giảm khi trúng đạn
  4. Nếu player bị hết pips → không có gì xảy ra (chưa implement player respawn)
```

**Nếu đạn bot không bay** (đứng im):
- Vấn đề: Rigidbody.isKinematic = true khi không phải authority
- Kiểm: `NetworkProjectile.Spawned()` → `rb.isKinematic = !HasStateAuthority` có chạy không
- Debug: thêm `Debug.Log("Spawned isKinematic=" + rb.isKinematic)` tạm thời

**Nếu đạn bay nhưng không hit player**:
- Kiểm: Player body có collider trên layer Hittable (15) không
- `[Hitbox]` trên NetworkAvatar prefab → layer = 15, trigger = true, isTrigger = true

---

### BƯỚC 3 — Test BuffRing

**Mục tiêu**: vòng buff xuất hiện đúng màu, ball xuyên qua apply buff, "EFFECTIVE!" hiện.

```
  1. Kiểm: 3 vòng xuất hiện sau Playing start
  2. Vòng có màu khác nhau (Ice=xanh, Fire=đỏ, ...) và drift lên xuống
  3. Label hiện tên ring (fade in sau 0.35s)
  4. Ném ball xuyên qua ring → "EFFECTIVE!" flash trên label → ring thu nhỏ và despawn
  5. Sau respawnDelay giây → ring mới xuất hiện tại slot đó
```

**⚠️ Known bug khả năng cao**: BuffRing.Spawned() chạy trước khi RingSpawner set Element → ring hiện màu trắng/không có màu.

**Cách fix nếu gặp**:
```csharp
// Trong BuffRing.cs — thêm [OnChangedRender] cho Element
[Networked, OnChangedRender(nameof(ApplyVisuals))]
public RingElement Element { get; set; }

private void ApplyVisuals()
{
    _config = ResolveConfig();
    if (_config == null) return;
    ApplyColor();
    ApplyLabel();
}

// Và trong Spawned() bỏ ApplyColor/ApplyLabel, chỉ giữ:
public override void Spawned()
{
    GetComponent<SphereCollider>().isTrigger = true;
    _originPos = transform.position;
    PlayBounceIn();
    StartDrift();
    // ApplyVisuals() sẽ được gọi tự động khi Element thay đổi
    if (Element != RingElement.None) ApplyVisuals(); // fallback nếu đã set trước Spawned
}
```

---

### BƯỚC 4 — Test ArenaManager flow

**Mục tiêu**: Warmup → Playing → RoundEnd → MatchEnd cycle hoạt động.

```
  1. Vào Play: Phase=Warmup (5s)
  2. Sau 5s: Phase=Playing, timer 120s bắt đầu
  3. Giết DummyAvatar 5 lần (không phải IsPlayer=true) → win condition không trigger (đúng)
  4. Đợi timer hết 120s → Phase=RoundEnd (4s) → Phase=Warmup lại
  5. Kiểm: RoundEndEvent fire trong console (Debug.Log hoặc Bill.Events)
```

**⚠️ Note**: ArenaManager.CheckWinCondition chỉ đếm `AllInstances where IsPlayer==true`. Chỉ có NetworkAvatar (player thật) count. DummyAvatar không count vì `IsPlayer=false`.

---

### BƯỚC 5 — Build tasks còn thiếu (theo thứ tự ưu tiên)

#### 5.1 Player respawn sau chết (QUAN TRỌNG nhất)
Hiện tại player chết (Health=0) nhưng không có gì xảy ra — DummyAvatar có auto-respawn nhưng NetworkAvatar thì không.

```
Cần thêm vào PlayerCombat.cs:
  - Khi Health hit 0 → fire PlayerDiedEvent
  - ArenaManager.EndRound() → teleport player tới spawn point
  - Hoặc: NetworkAvatar.Render() → hide body khi Health=0, show ghost/spectate
```

#### 5.2 WristWeaponSelector prefab
Code có (`WristWeaponSelector.cs`) nhưng chưa có prefab. Cần:
```
Trong NetworkAvatar.prefab:
  - Add child GO "WristSelector" parented to WristL
  - Add World Space Canvas (Canvas + CanvasScaler)
  - Add 3 slots (WeaponSlotUI): prev / current / next
  - Add WristWeaponSelector component, wire _slots[3]
  - Wire trong NetworkAvatar.Spawned(): wws.Initialize(combat)
```

#### 5.3 RewardText pool registration trong Resources/Pools
BillGameCore auto-loads từ `Resources/Pools/<key>` nhưng folder trống. Đã đăng ký qua BillBootstrapConfig rồi nên OK — nhưng cần verify pool hoạt động khi `RewardHit()` call.

#### 5.4 2-player verify với ParrelSync
```
Setup:
  1. ParrelSync: Tools > ParrelSync > Clones Manager > Add Clone
  2. Open Clone → Play cả 2 editor
  3. Verify: thấy avatar kia di chuyển
  4. Verify: ném trúng nhau → pip giảm đúng bên
  5. Verify: HeldBallVisual hiện ở tay đúng khi đang hold
```

---

### TRẠNG THÁI TOÀN BỘ HỆ THỐNG

| Layer | Code | Prefab/Assets | Scene | Test |
|---|---|---|---|---|
| **Throw mechanic** | ✅ | ✅ | ✅ | ⬜ verify tomorrow |
| **NetworkProjectile** | ✅ | ✅ | N/A | ⬜ verify tomorrow |
| **PlayerCombat** (health/economy) | ✅ | ✅ | ✅ | ⬜ verify tomorrow |
| **HealthUI** (5 pip) | ✅ | ✅ | N/A | ⬜ verify tomorrow |
| **DummyAvatar** (static target) | ✅ | ✅ | ✅ | ⬜ verify tomorrow |
| **DummyBotDriver** (AI throw) | ✅ | ✅ | ✅ | ⬜ verify tomorrow |
| **BuffRing** (drift + apply) | ✅ | ✅ | N/A | ⬜ likely bug on color |
| **RingSpawner** | ✅ | N/A | ✅ | ⬜ verify tomorrow |
| **ArenaManager** (loop) | ✅ | N/A | ✅ | ⬜ verify tomorrow |
| **CombatSession** | ✅ | N/A | ✅ | ⬜ verify tomorrow |
| **HandWeapon** (dispatch) | ✅ | ✅ wired | N/A | ⬜ |
| **CatchController** | ✅ | ✅ wired | N/A | ⬜ |
| **WeaponConfig** (7 weapons) | ✅ | ✅ SOs | N/A | ⬜ |
| **WristWeaponSelector** | ✅ code | ⬜ NO prefab | N/A | ⬜ |
| **RewardText** | ✅ | ✅ | N/A | ⬜ |
| **Player respawn** | ⬜ NO code | N/A | N/A | N/A |
| **2-player verify** | N/A | N/A | N/A | ⬜ |

---

### ⚠️ GOTCHAS QUAN TRỌNG NHẤT

1. **BuffRing.Element timing bug**: `Spawned()` chạy trước khi RingSpawner set Element → ring không có màu/label. Fix: dùng `[OnChangedRender]` (xem Bước 3 ở trên).

2. **Buff ring Shared Mode conflict**: `BuffRing.OnTriggerEnter` có guard `proj.Object.HasStateAuthority` — chỉ apply buff khi ring authority == projectile authority. Bot projectile: master owns both → OK. Player throw từ non-master: bị block. Fix sau khi verify cơ bản.

3. **Player chết nhưng không respawn**: chưa implement player-side death/respawn. Arena round end reset tất cả combat, nhưng không teleport player về spawn point.

4. **[SpawnA] và [SpawnB] position**: chưa kiểm tra tọa độ thực trong scene — cần đặt đúng 2 phía sân.

5. **execute_code guard**: `Application.dataPath.Contains("ThrowingShot")` — không phải "TOSSZONE".

6. **Mở 02_Arena trước khi verify scene**: MCP FindObjectOfType fail nếu scene sai.

---

### FILES THAY ĐỔI SESSION 9b + 9c (đầy đủ)

```
NEW SCRIPTS:
+ Assets/_Game/Scripts/Combat/CombatSession.cs
+ Assets/_Game/Scripts/Combat/ArenaManager.cs
+ Assets/_Game/Scripts/Combat/BuffRing.cs
+ Assets/_Game/Scripts/Combat/BuffRingConfig.cs
+ Assets/_Game/Scripts/Combat/CatchController.cs
+ Assets/_Game/Scripts/Combat/DummyBotDriver.cs
+ Assets/_Game/Scripts/Combat/RingSpawner.cs
+ Assets/_Game/Scripts/Throwing/HandWeapon.cs
+ Assets/_Game/Scripts/UI/WristWeaponSelector.cs
+ Assets/_Game/Scripts/UI/RewardText.cs

MODIFIED SCRIPTS:
M Assets/_Game/Scripts/Combat/PlayerCombat.cs       — economy + AllInstances + IsPlayer
M Assets/_Game/Scripts/Combat/DummyAvatar.cs        — IsPlayer=false
M Assets/_Game/Scripts/Minigame/MinigameDef.cs      — weaponCatalog + bestOf + roundDuration
M Assets/_Game/Scripts/Player/TossLocomotionInput.cs — Dash + Jump
M Assets/_Game/Scripts/Player/NetworkAvatar.cs       — HandWeapon+WristSelector init
M Assets/_Game/Scripts/Throwing/ThrowProjectile.cs  — IsCatchable/IsPower/OnCaught
M Assets/_Game/Scripts/Throwing/NetworkProjectile.cs — bot hit-detection + Rigidbody kinematic

NEW ASSETS:
+ Assets/_Game/Data/Rings/RC_Ice.asset ... RC_Shield.asset   (5 BuffRingConfig)
+ Assets/_Game/Data/Weapons/WC_Rock.asset ... WC_Sword.asset (7 WeaponConfig)
+ Assets/Resources/Minigames/arena.asset                     (MinigameDef)
+ Assets/_Game/Prefabs/BuffRing.prefab
+ Assets/_Game/Prefabs/RewardText.prefab

MODIFIED ASSETS:
M Assets/_Game/Prefabs/NetworkAvatar.prefab    — HandWeapon+CatchController+BladeTip+ThrowController refs
M Assets/_Game/Prefabs/DummyAvatar.prefab      — DummyBotDriver added
M Assets/_Game/Prefabs/NetworkProjectile.prefab — Rigidbody added
M Assets/_Game/Prefabs/BuffRing.prefab          — SphereCollider.isTrigger=true
M Assets/_Game/Scenes/02_Arena.unity           — ArenaManager+CombatSession+RingSpawner placed + refs
M Assets/Resources/BillBootstrapConfig.asset   — pools registered

DEPRECATED (moved to Docs/deprecated/):
- M0_Activation_Design.md
- M2_M3_Design.md
- M3_Avatar_Redesign.md
- M4_Gameplay_Design.md
- PHASE1_BUILD_PLAN.md
- Networking_Architecture.md
- AutoHand_Grab_Notes.md
```

---

## Session 9b — 2026-07-01 — Scene + prefab + data asset setup

### Đã làm

**Data assets tạo qua MCP execute_code:**
- `Assets/_Game/Data/Rings/RC_Ice.asset` ... `RC_Shield.asset` (5 BuffRingConfig SO)
- `Assets/_Game/Data/Weapons/WC_Rock.asset` ... `WC_Sword.asset` (7 WeaponConfig SO)
- `Assets/Resources/Minigames/arena.asset` (MinigameDef: weaponCatalog wired 7 WC_*)

**Prefab setup:**
- `BuffRing.prefab`: NetworkObject + NetworkTransform + BuffRing + SphereCollider(trigger, r=0.35); RingMesh=MS_Circle_Lv1; Label=TMP3D child; _catalog[1-5] wired RC_Ice→RC_Shield
- `DummyAvatar.prefab`: DummyBotDriver added; _netProjPrefab=NetworkProjectile; _throwOrigin=root; Fusion bake
- `NetworkAvatar.prefab`: HandWeapon on root (rightHand=true, _muzzle=WristR/Muzzle child); CatchController on WristL (_catchRadius=0.15)

**Scene 02_Arena:**
- ArenaManager GO: bestOf=1, roundDuration=120
- CombatSession GO: DontDestroyOnLoad singleton
- RingSpawner GO: _ringPrefab=BuffRing, _catalog[0-4]=RC_Ice→RC_Shield, 3 spawn points ở (-3,1.8,0)/(0,1.8,0)/(3,1.8,0)

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
