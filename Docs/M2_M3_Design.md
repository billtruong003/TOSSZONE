# M2 + M3 — Scene Flow / Matchmaking + Player Presence (Design)

> Plan-of-record cho 2 milestone tiếp theo của Phase 1. Bám [PHASE1_BUILD_PLAN.md](PHASE1_BUILD_PLAN.md) + framework [BillGameCore](../.claude/skills/billgamecore/SKILL.md). Style: doc → implement qua MCP (đã được phép thao tác trực tiếp từ M0).
> Trạng thái nền: M0 ✅, Section 1.1 (folders + Fusion + App ID + tags/layers) ✅. Define `PHOTON_FUSION` active (Android).

## Quyết định đã chốt (theo HANDOFF + đề xuất mặc định)
- **3 scene**: `00_Bootstrap(0)` → `01_TOSSZONE_Main(1)` (sảnh) → `02_Arena(2)` (combat).
- **Vào trận = đi xuyên `[ArenaPortal]`** trong Main (VR-native, dùng scaffold sẵn có) — KHÔNG nút 2D.
- **Session cố định** `"TOSSZONE_DEMO"` (2 người tự gặp nhau; solo-test được).
- **Networking**: `FusionNet.Instance.StartShared(session, arenaSceneIndex=2)` — master load scene 2, client follow.
- **Local rig** (`XRPlayer` AutoHand) KHÔNG networked; **avatar** (`NetworkPlayer`) là object Fusion spawn, copy pose từ rig.

---

## M2 — Scene Flow + Matchmaking

### Flow
```
01_TOSSZONE_Main: player đi vào trigger [ArenaPortal]
   → PortalMatchmaker.StartMatch()
   → FusionNet.GetOrCreate() + StartShared("TOSSZONE_DEMO", sceneIndex=2)
   → phase Connecting (status UI) ; Bill.Timer.Delay(30s) timeout guard
   → OnConnected → Fusion (master) LoadScene(2) → 02_Arena
   → timeout/fail → Shutdown + status "Không kết nối được" + cho đi lại vào portal
```

### Script (`_Game/Scripts/Network/`)
- **`PortalMatchmaker`** (MonoBehaviour, gắn lên `[ArenaPortal]`):
  - SerializeField: `_sessionName="TOSSZONE_DEMO"`, `_arenaSceneIndex=2`, `_connectTimeoutSeconds=30`, `_playerLayers` (LayerMask → HandPlayer), `_statusText` (TMP, optional world-space).
  - `OnTriggerEnter` → nếu collider thuộc `_playerLayers` và chưa đang connect → `StartMatch()`.
  - `#if PHOTON_FUSION`: `FusionNet.GetOrCreate()` → `StartShared(...)`; subscribe `NetworkPhaseChangedEvent` + `FusionConnectFailedEvent` → cập nhật status; `Bill.Timer.Delay(30s, OnTimeout)`. `#else`: fallback `Bill.Scene.Load` thẳng Arena (PC test không Fusion).
  - Luôn `Unsubscribe` ở `OnDisable`; hủy timer khi connect xong.
- Status hiển thị: tái dùng 1 TMP world-space cạnh portal (M2 tối giản; UI tìm trận đẹp = sau).

### Done khi
2 build đi vào portal trong 30s → cùng vào `02_Arena` 1 session; nếu timeout → quay lại trạng thái chờ ở Main, không kẹt.

---

## M3 — Player Presence (network-synced)

### Prefab `NetworkPlayer` (`_Game/Prefabs/`)
```
NetworkPlayer (root)   NetworkObject + NetworkTransform + NetworkPlayerAvatar
├─ Body                Capsule mesh (no collider) — thân, follow head XZ + cao theo head Y
├─ Head                NetworkTransform — copy từ camera rig
├─ HandL               NetworkTransform — copy từ tay trái rig
└─ HandR               NetworkTransform — copy từ tay phải rig
```
- 1 `NetworkObject` ở root; `NetworkTransform` nested cho Head/HandL/HandR (Fusion hỗ trợ nested behaviours dưới 1 NetworkObject).
- **Pass 1 (làm ngay):** thấy nhau di chuyển + giơ tay, đứng đúng phía đội. **Pass 2 (refine):** team color (`[Networked]`), leg IK 2 chân procedural.

### Scripts (`_Game/Scripts/Player` + `Network`)
- **`LocalPlayerRig`** (MonoBehaviour, gắn lên `XRPlayer` rig) — decouple khỏi AutoHand:
  - SerializeField `Transform _head, _handL, _handR;` + `static LocalPlayerRig Instance` (set ở `OnEnable`, clear `OnDisable`).
  - Avatar đọc qua `Instance` → không reference trực tiếp type AutoHand (asset gitignored).
- **`NetworkPlayerAvatar`** (`NetworkBehaviour`, `#if PHOTON_FUSION`):
  - Refs (SerializeField) `Transform _body, _head, _handL, _handR;`.
  - `Render()`: nếu `HasStateAuthority` (local) → copy pose từ `LocalPlayerRig.Instance` vào Head/HandL/HandR (world) + Body follow (XZ theo head, Y = nửa chiều cao head). Remote → NetworkTransform tự nội suy.
  - Pass 2: `[Networked] byte TeamId` + `ChangeDetector` → đổi màu material.
- **`NetworkPlayerSpawner`** (MonoBehaviour, trong `02_Arena`, `#if PHOTON_FUSION`):
  - SerializeField `NetworkObject _playerPrefab; Transform _spawnA, _spawnB;`.
  - Khi runner sẵn sàng trong Arena (`FusionSceneLoadDoneEvent` / local `PlayerJoined`) → `FusionNet.Instance.Spawn(_playerPrefab, spawnPos, rot, localPlayer)` (Shared: mỗi client spawn player của mình giữ StateAuthority).
  - Team = `LocalPlayerId % 2` (0→A trái, 1→B phải) → chọn spawn point + (pass 2) set TeamId.

### Done khi
A thấy B là capsule có 2 tay (+ đầu), di chuyển/giơ tay realtime, spawn đúng 2 phía. (Team color + leg IK = pass 2.)

---

## Thứ tự thực thi
1. Scripts M2 + M3 (`PortalMatchmaker`, `LocalPlayerRig`, `NetworkPlayerAvatar`, `NetworkPlayerSpawner`) → compile sạch (`read_console`).
2. Prefab `NetworkPlayer` (capsule + head + 2 hand + NetworkObject/NetworkTransform).
3. Wire scene: `[ArenaPortal]` ← `PortalMatchmaker` (Main); spawner + 2 spawn point (Arena); `LocalPlayerRig` lên `XRPlayer` (Main + Arena).
4. Verify: compile 0 error → Play smoke (Main → portal → Arena, spawn local avatar). Test 2-người = trên device (bạn).

## Cần bạn quyết / lưu ý
- `00_Bootstrap` đang có **unsaved changes** → cần save/discard trước khi mình load scene khác để wire (mình sẽ hỏi).
- `_playerLayers` mặc định = `HandPlayer`(9); chỉnh nếu rig dùng layer khác.
- Pass 2 (team color + leg IK) làm sau khi pass 1 chạy được trên device.
