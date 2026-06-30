# TOSSZONE — Kiến trúc mạng: bài học & thiết kế tiếp theo

> Tài liệu này được viết sau 5 session phát triển, tổng hợp những gì học được từ thực tế — bao gồm
> cả những chỗ sai, lý do sai, và cách thiết kế lại cho đúng. Đây không phải lý thuyết chay —
> mỗi mục gắn với một bug thật hoặc một quyết định kiến trúc có hậu quả rõ ràng.
>
> **Stack:** Fusion 2.0.12 · Shared Mode · NO Physics Addon · BillGameCore · AutoHand · Unity 6000.3 · Quest/Android

---

## Phần 1 — Kiến trúc hiện tại (sau 5 session)

### 1.1 Tổng quan luồng scene

```
00_Bootstrap (Scene 0)
  └─ BillBootstrap khởi động toàn bộ services
  └─ BillStartup splash → load 01_TOSSZONE_Main

01_TOSSZONE_Main  (Hub — Social Space, luôn networked)
  └─ FusionNet.StartShared("TOSSZONE_DEMO") ngay khi vào → players gặp nhau ở đây
  └─ [PlayerSpawnManager] → spawn NetworkAvatar cho mỗi player
  └─ [ArenaPortal] → walk-through → FusionNet.LoadScene(arenaIndex)

02_Arena  (Gameplay)
  └─ [PlayerSpawnManager] → reuse / respawn NetworkAvatar
  └─ [ThrowSystem] → ThrowController + ThrowBallHolder
  └─ [MinigameManager] → quản lý game state
```

### 1.2 Tách biệt Local vs Networked — quyết định kiến trúc quan trọng nhất

Đây là pattern trung tâm của toàn bộ hệ thống, học từ Gorilla Tag / VRChat:

```
LocalPlayer  (DDOL, KHÔNG networked, tồn tại xuyên suốt mọi scene)
  ├─ PlayerRig           ← expose 4 tracking points: Head, WristL, WristR, Root
  ├─ AutoHandPlayer      ← physics body + joystick locomotion
  ├─ TossLocomotionInput ← drive locomotion từ new InputSystem (không dùng legacy XR API)
  └─ TrackerOffsets/
       ├─ Camera (head)       ← TrackedPoseDriver (HMD pose)
       ├─ RobotHand (L/R)     ← toon hands, AutoHand grab/physics
       └─ ...

NetworkAvatar  (Fusion NetworkBehaviour — 1 per player, rất mỏng)
  ├─ NetworkTransform trên: Root + HeadNode + WristLNode + WristRNode
  ├─ [Networked] int ColorIndex
  ├─ AvatarArmPoser   ← IK tính LOCAL từ synced nodes, zero network cost
  ├─ AvatarLegPoser   ← procedural legs, cũng tính LOCAL, zero network cost
  └─ Stickman model (AvatarModel)
```

**Nguyên tắc nền tảng:** `LocalPlayer` KHÔNG bao giờ được spawn bởi Fusion. `NetworkAvatar` KHÔNG có AutoHand / Camera / Physics. Hai thứ communicate qua `PlayerRig` — cầu nối duy nhất.

**Những gì đi qua dây mỗi tick:** 4 transforms (position + rotation cho Root, Head, WristL, WristR) + ColorIndex. Không có bone rotation, không có finger state, không có IK data.

**Những gì KHÔNG đi qua dây (tính local mỗi client):** toàn bộ IK (arm + leg), VFX, haptics, âm thanh, finger pose, foot stepping, capsule height.

---

## Phần 2 — Bài học cụ thể từ 5 session

### Bài 1 — PlayerRig phải có mặt trong scene và được wire đầy đủ

**Vấn đề xảy ra:** `PlayerRig` component bị thiếu hoàn toàn trên `LocalPlayer`. Kết quả: `PlayerRig.Local = null`, `NetworkAvatar.FixedUpdateNetwork()` skip mọi tracking copy, avatar đứng im lặng tại spawn point.

**Tại sao nguy hiểm:** console chỉ in `[PlayerSpawn] No local PlayerRig found` — là warning màu vàng, dễ bỏ qua. Không có exception đỏ. IK vẫn "chạy" (không crash) nhưng nhận toàn bộ zero data → cánh tay collapse về origin.

**Quy tắc:** PlayerRig là điều kiện tiên quyết của toàn bộ avatar + networking. Mỗi khi avatar không follow → kiểm tra `PlayerRig.Local != null` TRƯỚC TIÊN.

**Wire bắt buộc:**

| Field | Target |
|---|---|
| `_head` | `LocalPlayer/TrackerOffsets/Camera (head)` |
| `_wristL` | `LocalPlayer/TrackerOffsets/Robot Hands/RobotHand (L)` |
| `_wristR` | `LocalPlayer/TrackerOffsets/Robot Hands/RobotHand (R)` |
| `_root` | `LocalPlayer/AutoHandPlayer` (cái có Rigidbody + locomotion) |

---

### Bài 2 — Legacy XR.InputDevices chết với OpenXR; phải dùng new InputSystem

**Vấn đề xảy ra:** AutoHand's `XRHandPlayerControllerLink` dùng `UnityEngine.XR.InputDevices.primary2DAxis`. Trên OpenXR runtime (Meta Sim + Quest thật), feature Vector2 này trả về `(0, 0)` liên tục trong khi button/float features vẫn hoạt động bình thường. Kết quả: grab bóng OK nhưng joystick locomotion chết hoàn toàn, không có bất kỳ lỗi nào.

**Tại sao nguy hiểm:** silent-fail — return zero thay vì throw exception. Rất dễ nhầm với bug vật lý hoặc bug animation.

**Fix:** `TossLocomotionInput.cs` — đọc thumbstick từ `UnityEngine.InputSystem` (new), disable `XRHandPlayerControllerLink`, drive `AutoHandPlayer.Move/Turn` trực tiếp. File này cần có mặt trong scene và được wire vào `AutoHandPlayer`.

**Quy tắc:** Với OpenXR trên Unity 6, **luôn dùng new InputSystem** cho mọi XR input. Legacy `UnityEngine.XR.*` API không đáng tin với OpenXR runtime. Nguyên tắc này áp dụng cho cả joystick, button, trigger, và grip.

---

### Bài 3 — `FromToRotation` để roll bone tự do → xương bị vặn xoắn

**Vấn đề xảy ra:** Cả `AvatarArmPoser` (cẳng tay / forearm) lẫn `AvatarLegPoser` (ống chân / shin) dùng pattern:

```csharp
// Pattern CŨ — gây twist
lower.rotation = Quaternion.FromToRotation(old_dir, new_dir) * lower.rotation;
```

`FromToRotation` chỉ constraint **hướng** của xương (mà xương trỏ về đâu), không constraint **roll** (twist quanh trục dài của xương đó). Kết quả: roll tích lũy qua frame hoặc flip ngẫu nhiên khi arm/leg gần thẳng. Nhìn thấy: cẳng tay hoặc ống chân bị xoắn vặn trông rất kỳ.

**Fix đã áp dụng cho cả AvatarArmPoser.cs và AvatarLegPoser.cs:**

```csharp
// Capture một lần lúc Awake — ghi nhớ roll của bind pose
private static Quaternion CaptureRoll(Transform bone, Vector3 endWorldPos)
{
    Vector3 fwd = (endWorldPos - bone.position).normalized;
    return Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bone.rotation;
}

// Mỗi frame — stateless, không phụ thuộc frame trước
Vector3 shinFwd = (target - lower.position).normalized;
lower.rotation = Quaternion.LookRotation(shinFwd) * lowerRest;
```

**Cơ chế:** `LookRotation(fwd)` cho một neutral reference (dùng world up làm axis thứ 2). `lowerRest` lưu sự khác biệt giữa neutral đó và bind pose thật của xương. Kết hợp lại → roll luôn đúng, không bao giờ drift.

**Quy tắc:** Bất kỳ two-bone IK nào dùng `FromToRotation` cho lower bone đều sẽ bị vấn đề roll. Luôn dùng `LookRotation * capturedRest` cho lower bone. Upper bone có thể dùng delta `FromToRotation` vì elbow/knee hint đã constrain đủ.

---

### Bài 4 — `AllowStateAuthorityOverride` bắt buộc cho mọi object có thể bị grab/interact

**Vấn đề xảy ra:** `NetworkBall` không có flag này → `RequestStateAuthority()` bị denied im lặng với mọi client không phải spawner → chỉ client spawn bóng (thường là host) mới grab được. Client còn lại thấy bóng nhưng không cầm được.

**Cơ chế:** Trong Shared Mode, `RequestStateAuthority()` là async và có thể bị denied. Nó chỉ được chấp nhận nếu object có `AllowStateAuthorityOverride = true` HOẶC authority cũ đã `ReleaseStateAuthority()`. Default flags `262145` KHÔNG include bit override này.

**Quy tắc:** Mọi `NetworkObject` mà player có thể interact (grab, đẩy, nhặt, throw, kích hoạt) **bắt buộc phải tick `Allow State Authority Override`** trong Inspector. Kiểm tra flag này là bước đầu tiên khi debug "chỉ host mới làm được X".

Chi tiết đầy đủ: `Docs/Fusion_Shared_Mode_Gotchas.md §2`.

---

### Bài 5 — Fusion Runner phải là ROOT DontDestroyOnLoad

**Vấn đề:** Runner được tạo là child của `[FusionNet]` → Unity log DDOL warning, nguy cơ runner bị destroy khi parent bị destroy trong Single-mode scene load.

**Fix:** `FusionNet.EnsureRunner()` tạo runner là root `GameObject` trước khi gọi `DontDestroyOnLoad`. Không bao giờ parent runner vào bất cứ thứ gì.

---

### Bài 6 — Player-object registry của Fusion KHÔNG tồn tại qua networked scene load

**Vấn đề:** Sau khi load Arena từ Main (Single-mode scene load), `Runner.TryGetPlayerObject()` trả về null dù `NetworkAvatar` vẫn còn sống → spawn guard fail → tạo avatar thứ 2 → 2 avatar cùng lúc.

**Fix:** Static `NetworkAvatar.Local` set trong `Spawned()` khi `HasStateAuthority`. Field này là instance reference thật, không phụ thuộc registry → tồn tại qua scene load. `PlayerSpawnManager` check `NetworkAvatar.Local != null` TRƯỚC `TryGetPlayerObject`.

**Quy tắc:** Đừng tin vào Fusion's player-object registry qua scene load. Tự manage static reference. Sau load thành công, re-call `SetPlayerObject` để sync lại registry nếu cần cho logic khác.

---

### Bài 7 — `AutoHandPlayer` tự override capsule height mỗi FixedUpdate

**Vấn đề:** `AutoHandPlayer.autoAdjustColliderHeight = true` (default) → mỗi FixedUpdate chạy:

```csharp
playerHeight = Mathf.Clamp(headCamera.position.y - transform.position.y,
                           minMaxHeight.x, minMaxHeight.y);
bodyCapsule.height = playerHeight;
```

Bất kỳ giá trị nào set thủ công trong Inspector đều bị ghi đè frame tiếp theo.

**Fix:** Set `minMaxHeight.y` để clamp max height (crouch vẫn hoạt động), hoặc tắt `autoAdjustColliderHeight` nếu muốn fixed height hoàn toàn. Không cần set capsule height trực tiếp.

---

### Bài 8 — Layer management cho first-person và mirror

**Quyết định (locked):** Avatar của owner → layer `RemoteVisual` (14) → main cam cull → bạn không nhìn thấy bản thân (first-person). Mirror cam include layer 14 → bạn thấy mình trong gương. Proxy avatars → layer `Default` (0) → main cam render.

**Khi debug IK:** Uncheck `_hideOwnVisuals` trên `NetworkAvatar` prefab → main cam render avatar của bạn → có thể nhìn thấy IK của mình đang hoạt động hay không.

---

## Phần 3 — Kiến trúc mạng cần build tiếp theo

### 3.1 Networked Throw System (S2 → S3 → S8 → S9)

Layer networking tiếp theo, xây trên throw mechanic local đã có (peak-velocity trigger, ThrowBallHolder, BillTween arc — đều đã done).

**S2 — Networked held ball: mọi người thấy bóng bạn đang cầm** ✅ DONE (Session 6)

Vấn đề: `ThrowBall` Grabbable hiện chỉ là local visual. Các player khác không thấy gì.

**Thiết kế gốc** muốn tách `NetworkHeldBall` prefab riêng + `RequestStateAuthority`. **Thực tế implement** đơn giản hơn và đúng hơn vì wrist đã sync sẵn qua NT:

```
NetworkAvatar
  ├─ [Networked] bool HoldingBall  ← mới thêm
  └─ WristR (NetworkTransform)
       └─ HeldBallVisual (Sphere, ThrowBall.mat, scale 0.09)  ← mới thêm trong prefab

ThrowController
  └─ static bool LocalHoldingBall  ← set trong ShowHeld(on), cleared trong OnDisable()

NetworkAvatar.FixedUpdateNetwork() [authority only]:
  HoldingBall = TossZone.Throwing.ThrowController.LocalHoldingBall;

NetworkAvatar.Render() [proxies only]:
  _heldBallVisual.enabled = HoldingBall;
  // WristR NT đã interpolate vị trí → sphere theo wrist tự động
```

**Tại sao không cần `RequestStateAuthority`?** Vì `HoldingBall` là state của chính `NetworkAvatar` — object mà player luôn là authority. Không có object nào "đổi chủ" → không cần request authority. Pattern này zero-overhead và không có async edge case nào.

**Tại sao sphere con của WristR thay vì prefab riêng?** WristR đã có NT sync position → child sphere follow automatically, không cần thêm NT, không cần thêm NetworkObject, không thêm bandwidth.

**S3 — Networked flying projectile: thrower drives, NT replicates** ✅ DONE (Session 6)

**Thiết kế gốc** muốn make `ThrowProjectile` là NetworkBehaviour — không làm vì conflict với `PooledObject` inheritance. Thay vào đó dùng thin wrapper:

```
NetworkProjectile.cs (new — NetworkBehaviour)
  ├─ NetworkObject + NetworkTransform
  ├─ Visual sphere (ThrowBall.mat, no collider) — chỉ proxies thấy
  └─ LinkTo(Transform localProj)  ← authority gọi ngay sau Spawn

NetworkProjectile.FixedUpdateNetwork() [authority only]:
  transform.SetPositionAndRotation(localProjectile.position, localProjectile.rotation);
  // LocalProjectile là ThrowProjectile đang chạy BillTween — copy pos mỗi tick

NetworkProjectile.Spawned():
  _mr.enabled = !HasStateAuthority;  // authority thấy ThrowProjectile thật, proxy thấy cái này

ThrowController.SpawnProjectile():
  Bill.Pool.Spawn(PoolKey, ...)         // local ThrowProjectile như cũ
  SpawnNetworkProjectile(pos, rot, go.transform)  // spawn NetworkProjectile + link

ThrowController.OnBallLanded():
  DespawnNetworkProjectile()  // Runner.Despawn(_activeNetProj)
```

**Tại sao tách `NetworkProjectile` khỏi `ThrowProjectile`?** Giữ `ThrowProjectile` sạch (không Fusion dependency), Bill.Pool vẫn hoạt động offline. `NetworkProjectile` là thin adapter — nó không cần biết BillTween là gì, chỉ cần copy transform.

Điểm quan trọng: không sync velocity hay tween params — chỉ sync world position mỗi tick. BillTween arc chạy local trên authority; proxies nhận position kết quả qua NT.

**S8 — Hit detection + cross-player haptics**

```csharp
// Trên authority (thrower), khi BallLandedEvent nhắm vào collider của player:
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_OnHit(PlayerRef hitPlayer, Vector3 hitPoint)
{
    // Tất cả client: play impact VFX tại hitPoint (shockwave, burst, bounce number)
    SpawnImpactVFX(hitPoint);

    // Chỉ client là hitPlayer: haptic rumble mạnh
    if (Runner.LocalPlayer == hitPlayer)
        TriggerHaptic(HapticStrength.Strong, duration: 0.3f);
}
```

Juice hoàn toàn cosmetic → không cần gatekeeper authority. Mỗi client tự tính local.

**S9 — 2-player verify: checklist**

```
[x] Player A thấy Player B đang giữ bóng (HoldingBall + HeldBallVisual — S2 done)
[x] Player A thấy bóng từ B bay về phía mình (NetworkProjectile spawns + moves — S3 done)
[ ] Player A nhận haptic khi bị trúng (RPC_OnHit targeting correct PlayerRef — S8)
[ ] Visual split-ball swap ẩn bởi launch flash (timing đúng giữa hide held + spawn projectile)
[ ] Throw-to-impact lag < 100ms cảm nhận được trên local network
[ ] Bóng không bị stuck khi thrower disconnect (NetworkProjectile despawn on disconnect)
[ ] Score update đúng sau hit (nếu MinigameManager đã có)
```

---

### 3.2 Minigame Networked State

Đã có scaffold (`MinigameDef`, `MinigameEvents`, `MinigameManager`, `MinigamePortal`). Cần thêm networking:

```csharp
// MinigameManager : NetworkBehaviour
// MasterClientObject = true → authority tự chuyển khi master disconnect

[Networked] public NetworkDictionary<PlayerRef, int> Scores => default;
[Networked] public MinigamePhase Phase { get; set; }
[Networked] public float TimeRemaining { get; set; }

public override void FixedUpdateNetwork()
{
    if (!HasStateAuthority) return; // chỉ Master Client update

    TimeRemaining -= Runner.DeltaTime;
    if (TimeRemaining <= 0f && Phase == MinigamePhase.Playing)
        Phase = MinigamePhase.GameOver;
}

// Score update khi bị hit (authority → FUN, không dùng RPC để tránh conflict):
public void RegisterHit(PlayerRef hitPlayer)
{
    if (!HasStateAuthority) return;
    Scores.Set(hitPlayer, Scores.Get(hitPlayer) - 1);
}
```

**Tại sao `MasterClientObject`?** Master Client thay đổi khi master disconnect — flag này tự chuyển authority mà không cần code thêm. Phù hợp cho game state manager vì nó không "thuộc về" ai.

**Tại sao update score trong FUN thay vì RPC?** RPC có thể conflict nếu 2 hit xảy ra cùng lúc (network race). FUN chạy theo tick order xác định → không conflict.

---

### 3.3 Team System

Tags đã có: `TeamA`, `TeamB`, `Projectile`, `Throwable`.

```csharp
// Trong NetworkAvatar:
[Networked] public int TeamIndex { get; set; } // 0 = A, 1 = B

// PlayerSpawnManager.OnSpawn():
avatar.TeamIndex = Object.InputAuthority.PlayerId % 2; // alternating

// Spawn point theo team:
Transform[] points = avatar.TeamIndex == 0 ? spawnPointsA : spawnPointsB;
```

Khi cần balanced teams (không phải alternating): `NetworkList<PlayerRef>` per team trên `MinigameManager`, assign khi player join bằng cách pick team nhỏ hơn.

---

## Phần 4 — Nguyên tắc thiết kế cốt lõi

### 4.1 Juice ở local, state mới đi dây

Chỉ sync những gì cần đồng thuận thật sự. Mọi thứ còn lại tính local mỗi client.

| Phải sync qua Fusion | Giữ local — tính lại mỗi client |
|---|---|
| Transform: Root, Head, WristL, WristR | Toàn bộ IK (arm + leg) |
| Game state: score, phase, timer | VFX: particle, trail, shockwave |
| Authority ownership của grabbable | Haptics |
| Spawn / Despawn event | Audio |
| Hit event (RPC) | Capsule height, foot stepping |
| Team index, color index | Camera eye offset, hand recoil tween |

**Lý do:** VR cần latency < 100ms để không gây motion sickness. Mỗi thứ sync qua dây thêm potential jitter. Juice là thứ client nào cũng có thể tính độc lập — nếu lệch 1-2 frame thì không ai nhận ra và không ảnh hưởng game logic.

---

### 4.2 Authority = ai cần write state frame này, không phải ai "sở hữu" object

Trong Shared Mode, authority là dynamic. Bất kỳ client nào cần modify state → request authority:

| Object | Authority là ai | Có đổi không |
|---|---|---|
| `NetworkAvatar` | Player đó | Không đổi bao giờ |
| `NetworkHeldBall` | Người đang cầm | Đổi khi grab/release |
| `NetworkProjectile` | Người throw | Không đổi (chỉ thrower drive arc) |
| `MinigameManager` | Master Client | Tự đổi khi master disconnect |
| `NetworkBall` (sandbox) | Người đang cầm | Đổi khi grab |

Bất kỳ object nào có thể đổi authority → bắt buộc `AllowStateAuthorityOverride = true`.

---

### 4.3 Viết đúng lifecycle method

| Method | Dùng cho |
|---|---|
| `FixedUpdateNetwork()` | Logic networked: copy tracking, update game state, move held object theo hand |
| `Render()` | Visual smoothing, proxy posing, extrapolation trong authority transfer window |
| `LateUpdate()` | IK: `AvatarArmPoser`, `AvatarLegPoser` — chạy SAU FUN và Render |
| `FixedUpdate()` | Rigidbody physics: `AutoHandPlayer`, grab physics |
| `Update()` | Local input reading: `TossLocomotionInput`, `ThrowController` |

Không đọc/write `[Networked]` property ngoài `FixedUpdateNetwork()` khi là authority. Nếu cần display giá trị networked trong Update, cache vào field local.

---

### 4.4 New InputSystem — không có ngoại lệ với OpenXR

```csharp
// ĐÚNG — new InputSystem, hoạt động với mọi OpenXR runtime
var devices = InputSystem.devices;
foreach (var d in devices)
    foreach (var u in d.usages)
        if (u == CommonUsages.LeftHand) { /* match và đọc thumbstick */ }

// SAI — legacy API, trả về (0,0) silently trên OpenXR
UnityEngine.XR.InputDevices
    .GetDeviceAtXRNode(XRNode.LeftHand)
    .TryGetFeatureValue(CommonUsages.primary2DAxis, out vec2);
// → vec2 = (0,0) mọi lúc trên Meta Sim và Quest OpenXR runtime
```

---

### 4.5 Scene loading — checklist an toàn

```
1. Chỉ Master Client gọi FusionNet.LoadScene(arenaIndex)
2. Trước khi spawn → check NetworkAvatar.Local != null (static, tồn tại qua load)
3. Nếu avatar tồn tại nhưng registry mất → re-SetPlayerObject, KHÔNG spawn mới
4. PlayerRig.Local tồn tại qua load nhờ DDOL — không cần recreate
5. ThrowSystem: check PlayerRig.Local != null trước khi init (chỉ hoạt động trong Arena)
```

---

### 4.6 IK phải stateless — không accumulate error qua frame

Áp dụng cho mọi two-bone IK lower bone (shin, forearm):

```csharp
// Capture một lần, Awake():
private Quaternion CaptureRoll(Transform bone, Vector3 endPos)
{
    Vector3 fwd = (endPos - bone.position).normalized;
    return Quaternion.Inverse(Quaternion.LookRotation(fwd)) * bone.rotation;
}

// Mỗi frame — không phụ thuộc frame trước, không tích lũy lỗi:
lower.rotation = Quaternion.LookRotation((target - lower.position).normalized) * lowerRest;
```

Upper bone (`FromToRotation * upper.rotation`) vẫn OK vì elbow/knee hint đã constrain đủ và upper bone không bị twisted như lower.

---

## Phần 5 — Thứ tự ưu tiên build tiếp

```
[ NGAY BÂY GIỜ — throw networking ]
  S2  NetworkHeldBall: mọi người thấy bóng bạn đang cầm
  S3  NetworkProjectile: thrower drives BillTween arc, NT replicates

[ SAU KHI 2-player throw verify OK ]
  S5  Haptics 3-tier: wind-up / release / impact (local, test feel on device)
  S6  Juice T2: release flash, squash-stretch, glow trail
  S8  Hit detection + cross-player haptic RPC
  S9  Full 2-player checklist (6 items trên — xem §3.1)

[ SAU KHI THROW SYSTEM HOÀN CHỈNH ]
  M4  MinigameManager networked state: score, phase, countdown timer
  M4  Team assignment + spawn point per team
  M5  Hub polish: dress Main scene như social space thật
  M5  Multiple game portals (Arena 2, Arena 3...)
```

---

## Nguồn tham khảo trong project

| File | Nội dung |
|---|---|
| `Docs/Fusion_Shared_Mode_Gotchas.md` | Verified Fusion 2 Shared Mode behavior — đọc trước khi viết bất kỳ networking code nào |
| `Docs/AutoHand_Grab_Notes.md` | AutoHand grab physics patterns học từ project Shmackle |
| `Docs/Throw_Mechanic_Spec.md` | Throw mechanic spec đầy đủ (peak-velocity, locked) + implementation order |
| `Assets/_Game/Scripts/Player/PlayerRig.cs` | Local rig bridge — expose tracking points |
| `Assets/_Game/Scripts/Player/NetworkAvatar.cs` | Thin networked avatar — sync + IK drive |
| `Assets/_Game/Scripts/Player/AvatarArmPoser.cs` | Arm IK — stateless roll fix |
| `Assets/_Game/Scripts/Player/AvatarLegPoser.cs` | Leg IK — stateless roll fix |
| `Assets/_Game/Scripts/Player/TossLocomotionInput.cs` | new InputSystem locomotion driver |
| `Assets/_Game/Scripts/Throwing/ThrowController.cs` | Peak-velocity throw state machine |
