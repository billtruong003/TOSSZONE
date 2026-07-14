# P0 1.2.1 + 1.2.2 — Remote equipped AR proxy & shot cosmetic relay: implementation + solo audit (2026-07-14)

Status: **Cả 1.2.1 và 1.2.2 code-complete; two-client verify PASS (xem §6, cùng ngày).** Packet-loss simulation chưa chạy (không có tooling trong editor session). Branch: `codex/phase1-prep`.

## 1. Scope của session này

- `AvatarWeaponSync` (mới, `Assets/_Game/Scripts/Guns/Net/AvatarWeaponSync.cs`): `[Networked] EquippedSlot` trên `NetworkAvatar`, owner mirror từ `GunInput.LocalEquippedWeaponId`, proxy render bằng cách instantiate model prefab từ `GunCatalog` rồi `StripToVisual` (destroy `Gun` component, disable collider) và parent vào wrist.
- `GunInput.cs` (+4 dòng): expose `LocalEquippedWeaponId` static, set trong `TryEquip`.
- `NetworkAvatar.prefab` (+16 dòng): gắn `AvatarWeaponSync`.

## 2. Audit A — Instantiate-then-strip lifecycle (proxy path)

Câu hỏi: giữa `Instantiate(modelPrefab)` và deferred `Destroy(gunComponent)`, component sống ~1 frame có side effect nào không?

Prefab inventory (đọc YAML trực tiếp, Unity Editor không chạy):

- `Assets/_Game/Art/Weapons/P0/AK74_P0.prefab`: root GameObject + Transform + **một** MonoBehaviour (`HitscanGun`, guid `bc344bd5dab687043b4f73b201e909d7`) + nested prefab instance.
- Nested visual `Assets/Low Poly Weapons VOL.1/Prefabs/AK74.prefab` (guid `ee329c3cd08e58645834d20702d7f6c6`): chỉ GameObject/Transform/MeshFilter/MeshRenderer (u!1/u!4/u!33/u!23). **Không có collider, không AudioSource, không script nào khác.**

Trace `Gun.cs` (base của `HitscanGun`; `HitscanGun` không override lifecycle nào):

- `Awake`: set ammo từ config — instance-local, chết cùng component.
- `OnEnable`: `State = Ready` — instance-local.
- `Update` (dòng 60–63): auto-fire gate bởi `_triggerHeld` (dòng 56, private, mặc định `false`; chỉ set `true` qua `TriggerDown` dòng 47). Fresh instance không có input route → **không thể fire trong cửa sổ 1 frame**.
- Không subscribe event, không đụng static, không `OnDestroy`, không coroutine. `GunFeedback` là scene-level service, không nằm trên prefab → không có double feedback.

Kết luận: cửa sổ deferred-Destroy **không có side effect**; collider-disable trong `StripToVisual` là defensive-only (prefab không có collider). **Không cần đổi code.**

## 3. Audit B — Static `GunInput.LocalEquippedWeaponId` stale-state

- Single writer: `GunInput.TryEquip` (Start path). Không có nơi nào clear.
- `ProjectSettings/EditorSettings.asset`: `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0` → **domain reload KHÔNG bị disable** → static reset mỗi Play session. Không có stale cross-session.
- In-session: P0 chưa có unequip/death/despawn path (1.3.2 chưa implement), local rig sống suốt session, single weapon luôn equip lại ở `Start` → không tồn tại trace ra stale thực. **Không đổi code** (đúng rule: chỉ sửa khi trace ra staleness thật).
- ⚠️ Latent risk ghi nhận cho tương lai: khi 1.3.2 (death/respawn) hoặc weapon switching land, **phải** clear/update static này trên unequip/despawn, nếu không owner mirror sẽ đẩy weaponId chết vào `EquippedSlot`.

## 4. Task 1.2.2 — Unreliable shot cosmetic relay (code-complete, cùng session)

Thêm vào chính `AvatarWeaponSync` (đúng "single network seam" §3/§4.4 của Gun_System_Architecture.md):

- **Owner relay**: lazy-subscribe `GunFiredEvent` từ `FixedUpdateNetwork` (owner-only; cùng pattern poll `Bill.IsReady` như `GunFeedback` — Spawned có thể chạy trước khi Bill ready). Unhook trong `Despawned`.
- **Echo guard**: chỉ relay khi `e.Shot.Shooter == Object.InputAuthority`. Bắt buộc vì receiver re-fire **cùng event type** trên bus per-process; thiếu guard này thì mỗi cosmetic re-fire từ remote lại bị broadcast tiếp (echo storm) — đúng risk đã ghi trên board ("dùng local event bus như global bus").
- **Wire**: `[Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, InvokeLocal = false, Channel = RpcChannel.Unreliable)] RPC_ShotFired(shotId, weaponId, muzzlePos, direction, hitPoint, hitNormal, victim, hitPart)`. Cosmetic-only, fire-and-forget (§4.2, edge case #11: mất packet = mất một tracer, không mất gì khác). **Không mang damage** — 1.3.1 ShotClaim là đường reliable riêng.
- **No double-render trên shooter**: `RpcTargets.Proxies` + `InvokeLocal = false` — shooter không bao giờ nhận lại shot của mình. Haptic phía remote cũng tự loại: `GunFeedback` gate haptic bằng `shot.Shooter == LocalShooterRef()`.
- **Proxy muzzle resolution (§4.2)**: receiver ưu tiên `_proxyMuzzle` — child tên đúng `"MuzzleAnchor"` của proxy model (contract ghi trong `GunConfig`), cache lúc `RebuildProxy`, clear khi rebuild/despawn. Wire `MuzzlePos` chỉ là fallback cho "shot đến trước khi Render() build proxy".
- **One render path**: receiver re-fire `GunFiredEvent` trên bus local → `GunFeedback` vẫn là the ONE consumer, local và remote shot đi chung một đường render.

Impact surface (grep ground truth sau khi `--repair-fts`; FTS index trước đó degraded):

- Publisher duy nhất của `GunFiredEvent`: `Gun.cs:89`.
- Consumer duy nhất: `GunFeedback` (subscribe dòng 39, unsubscribe 47).
- Kiểm chứng build: Unity compile sạch (chỉ 2 warning CS0414 pre-existing ở `NetworkProjectile`/`DummyBotDriver`), `validate_script` = 0 errors, 0 warnings.

## 5. Còn nợ (blocking Done)

1. ~~Two-client verify 1.2.1 theo recipe board~~ → PASS, xem §6.
2. ~~Two-client verify 1.2.2 theo recipe board~~ → PASS, xem §6. Simulated packet loss: **chưa chạy** — không có packet-loss tooling trong editor session này; rủi ro thấp vì channel Unreliable là cosmetic-only by construction (mất packet = mất 1 tracer, damage đi đường reliable riêng).

## 6. Two-client verify (2026-07-14, main editor + ParrelSync clone `TOSSZONE_clone_0`)

Setup: Fusion shared session 2 client (main = player 1, clone = player 2), scene `02_FSPMAP`, probes hook `GunFiredEvent`/`ClaimAcceptedEvent`/`ClaimRejectedEvent` trên bus của **clone** qua `execute_code`.

**1.2.1 — proxy state trên clone (probe reflection `_proxyInstance`/`_proxyMuzzle`):**

- Remote player 1: `slot=0, proxy=True, rends=4, guns=0, collidersOn=0, muzzle=True` → đúng một proxy visual-only trên wrist remote: có renderer + `MuzzleAnchor`, **không** Gun logic, **không** collider enabled.
- Local avatar clone (authority, chưa equip): `slot=255, proxy=False` → authority không tự render proxy. Zero console error kèm theo.

**1.2.2 — cosmetic relay qua wire thật:** shot phát trên main (shooter=1, weapon=0, victim=2, part=Body), clone nhận và re-fire trên bus local:

```
[Probe] GunFired shooter=1 shot=424242 weapon=0 part=Body victim=2 muzzle=(0.25, 1.10, -1.37)
```

Payload khớp 100% shot gốc; muzzle resolve phía receiver hoạt động. Shooter (main) không nhận lại event của mình (`RpcTargets.Proxies + InvokeLocal=false` — không có log double-render phía main).

**Cách phát shot:** `Gun` component không tồn tại trong play scene P0 (producer thật đã có EditMode/solo evidence riêng); shot được publish trực tiếp `Bill.Events.Fire(new GunFiredEvent{...})` trên main với `ShotInfo` well-formed (`Shooter = Object.InputAuthority`, origin = `ResolveAnchor()` wrist, `WeaponId = EquippedSlot`). Đây đúng seam mà Gun.TryFire dùng, nên toàn bộ đường **OnLocalShot → echo guard → RPC wire → clone re-fire** được exercise thật.
