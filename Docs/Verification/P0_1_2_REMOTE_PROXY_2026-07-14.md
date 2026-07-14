# P0 1.2.1 — Remote equipped AR proxy: implementation + solo audit (2026-07-14)

Status: **Code-complete, NOT verified.** Two-client verify (equip / respawn / late join, zero-error console) chưa chạy — giữ task mở trên board. Branch: `codex/phase1-prep`.

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

## 4. Còn nợ (blocking Done)

1. Two-client verify theo recipe board: equip / respawn / late join → đúng một AR proxy trên remote wrist, zero console error.
2. 1.2.2 (unreliable shot cosmetic RPC) chưa bắt đầu — vẫn Todo.
