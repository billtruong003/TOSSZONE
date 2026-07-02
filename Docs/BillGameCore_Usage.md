# BillGameCore trong TOSSZONE — bản đồ sử dụng + phần đã đắp thêm cho framework

> Mục đích: biết framework cung cấp gì, game đang dùng ở đâu, và phần nào Session 10-11 ĐẮP THÊM vào
> framework (tái dùng được cho project khác). Cập nhật 2026-07-02.

## 1. Services framework + chỗ game đang dùng

| Service (`Bill.*`) | Game dùng ở đâu |
|---|---|
| `Bill.Events` (EventBus) | Xương sống decoupling: `MinigameEnteredEvent` (ArenaManager→CombatSession), `PlayerHitEvent` (PlayerCombat→CombatJuice/HealthUI), `BallThrown/Landed/CaughtEvent`, `RoundEndEvent`/`MatchEndEvent`, `MoneyChangedEvent`, `WeaponResetEvent`, `RingConsumedEvent`, Fusion*Event (connect/scene-load/player-join…) |
| `Bill.Pool` | `throwprojectile`, `rewardtext`, `releaseflash`, `impactburst`, `bouncenumber` (đăng ký trong `BillBootstrapConfig.defaultPools` + runtime `Register`) |
| `Bill.Tween` (BillTween — KHÔNG DOTween) | Arc bay ThrowProjectile, ring bounce/shrink/label, juice scale-pop, RewardText/BounceNumber rise-fade |
| `Bill.Audio` | `throw`/`impact` SFX; **`PlayPitched`** (pitch theo lực ném — API mới S11) |
| `Bill.Timer` | Refill cooldown ném (`Bill.Timer.Delay`) |
| `Bill.Scene` | Bootstrap return-to-edit-scene, dev load |
| `Bill.Net` / `FusionNet` | Toàn bộ networking (xem mục 2) |
| `Bill.Players` | Registry player local/remote (mới S11 — xem mục 2) |
| `CheatConsole` (dev) | Lệnh game tự đăng ký: `money/unlockall/equip/heal` (CombatCheats.cs) + spawn `DevCombatPanel` |
| `BillInspector` | Attribute trên mọi ScriptableObject config (WeaponConfig, BuffRingConfig, ThrowConfig) |

Bootstrap: `BillBootstrap` tự chạy qua `RuntimeInitializeOnLoadMethod`, config tại
`Assets/Resources/BillBootstrapConfig.asset` (pools, audio library, network mode, cheat/overlay flags).
Guard mọi chỗ: `Bill.IsReady` trước khi đụng service.

## 2. Phần ĐẮP THÊM vào framework (S10-S11) — tái dùng cho project sau

| Thêm gì | File | Ghi chú |
|---|---|---|
| `FusionNet` | `Runtime/Network/Fusion/FusionNet.cs` | Controller Fusion 2 trọn gói: connect/session/scene/spawn/authority/events. **S11 thêm:** `LoadSceneAdditive` / `UnloadSceneAdditive` |
| `PooledNetworkObjectProvider` + `NetworkPoolable` | `Runtime/Network/Fusion/` | Pool NetworkObject (S10) — thay Instantiate/Destroy của Fusion |
| `BillPlayers` / `Bill.Players` | `Runtime/Network/Fusion/BillPlayers.cs` | Registry `IBillPlayer` (Local/Get/All + events). Game implement trên NetworkAvatar (S11/T13) |
| `IAudioService.PlayPitched(key, pitchMul, vol)` | `Runtime/Services/Audio/` | Pitch multiplier per-call (juice theo lực) — 4 overload Play cũ giữ nguyên |
| Fix `CheatConsole` TextField crash | `DevTools/DevTools.cs` | Defer clear sau KeyDown dispatch (bug UIToolkit selection stale) |

Quy ước khi đắp thêm: chỉ nhét vào BillGameCore thứ **game-agnostic** (không tham chiếu TossZone.*);
phần Fusion để trong `Runtime/Network/Fusion/` + guard `#if PHOTON_FUSION`.

## 3. Gotchas framework hay dính (đúc kết S10-S11)

- **Domain-reload giữa Play** reset mọi static (`Bill`, `FusionNet.Instance` = null) nhưng `isPlaying` vẫn true
  → half-state. Stop → Play lại là sạch. Compile xong hãy vào Play.
- `Bill.Events` là bus **local per-process** — event networking phải tự fire trên MỖI client (xem
  ArenaManager.Spawned fire MinigameEnteredEvent cho mọi client, không chỉ authority).
- `Bill.Pool.Register` trùng key = no-op im lặng (không lỗi, không thay prefab).
- `PooledObject.OnSpawnedFromPool/OnReturnedToPool` PHẢI reset state thủ công — pool không reset field thường
  (chỉ Fusion reset [Networked]).
- BillTween: **`KillTarget(x)` trước khi Destroy(x)** — tween sống lâu hơn object là MissingReferenceException
  (dính 2 lần: BuffRing consume, held-ball swap).
- `CheatConsole`/`DebugOverlay`/`DevCombatPanel` chỉ compile `UNITY_EDITOR || DEVELOPMENT_BUILD` — đừng đặt
  chúng làm scene object (release build sẽ ra missing script); spawn runtime từ code cùng guard.
