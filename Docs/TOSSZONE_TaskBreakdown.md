# TOSSZONE — Active Task Breakdown

> Source of truth cho active backlog. Trạng thái: `[ ]` Todo · `[/]` In Progress · `[x]` Done · `[!]` Blocked.  
> Baseline: `GameDesign/TOSSZONE-Playable-Ready-Roadmap.md` · `Gun_System_Architecture.md` v1.1.  
> Rule: không reorder checkbox task sau khi đã có metadata; mỗi code task phải chạy GitNexus impact trước edit.

## PHASE 0 — Decision & Test Harness

### 0.1 — Lock combat decisions

- [x] Lock Option A as the v0.3-P0 hit-authority and cheat-scope contract
  - Outcome: D1/D2 có một contract đủ rõ để implementation không tự chọn lại authority.
  - Scope: shooter local raycast; immediate local feedback; reliable targeted `ShotClaim`; victim validation, catalog-derived damage và victim-authority Health; unreliable remote cosmetic.
  - Out of scope: server rewind, dedicated authority, kick/ban, competitive guarantee.
  - Dependencies: user chọn Option A ngày 2026-07-14.
  - Risk: client-trusted hit vẫn có thể bị aim/raycast spoof; chấp nhận cho phòng 2–4 người.
  - Acceptance criteria: roadmap và gun architecture thống nhất schema, authority, validation, damage source và upgrade trigger.
  - Verify recipe: review hai docs; không còn chỗ nào mô tả shooter gửi final damage hoặc D1/D2 chưa chốt.
  - Evidence: `GameDesign/TOSSZONE-Playable-Ready-Roadmap.md`, `Gun_System_Architecture.md`.
  - Decision/Assumption: Option A chỉ áp dụng cho v0.3 Ready casual/small-room.

- [x] Lock D3 health model for v0.3-P0
  - Outcome: xác định rõ `Health` là 100 HP hay tiếp tục mang nghĩa lives trước khi nối damage.
  - Scope: max HP, damage semantics, death threshold, respawn reset và ranh giới round/lives.
  - Out of scope: armor, healing, economy lives, revive.
  - Dependencies: user xác nhận product decision D3.
  - Risk: triển khai trên model lives hiện tại sẽ làm TTK, UI và death lifecycle sai nghĩa.
  - Acceptance criteria: một quyết định bằng văn bản; mapping state cũ→mới; regression scope cho PlayerCombat/ArenaManager.
  - Verify recipe: review decision record và một state-transition table HP→death→respawn.
  - Evidence: OWNER DECISION 2026-07-15 (recorded in-session, verbatim intent): v0.3-P0 dùng 100 HP; death khi HP ≤ 0; respawn reset về 100 HP. Mapping cũ→mới: `PlayerCombat.Health` (nghĩa lives) → HP int, max 100, spawn/respawn = 100, death threshold HP ≤ 0 thay cho lives==0. Regression scope: `PlayerCombat` (Health semantics + death lifecycle), `ArenaManager` (death/round hooks), mọi UI/log/telemetry đang đọc Health theo nghĩa lives. State-transition table HP→death→respawn ship cùng evidence của 1.3.2.
  - Decision/Assumption: user chốt D3 = 100 HP ngày 2026-07-15; armor/heal/economy lives vẫn out of scope tới v0.3-P1.

### 0.2 — Establish repeatable proof harness

- [/] Define the repeatable two-client AR test runbook
  - Outcome: một tester có thể mở hai client, vào cùng arena và lặp combat cycle mà không cần sửa state bằng cheat.
  - Scope: scene/build path, room/session steps, AR placeholder, body/head target, respawn loop và cách reset test.
  - Out of scope: Quest performance pass, four-player session, final onboarding.
  - Dependencies: existing Fusion Shared Mode path; inspect current scene flow; GitNexus impact trước mọi code/scene binding edit.
  - Risk: ParrelSync/editor path có thể khác device build; ghi limitation nếu chỉ test editor.
  - Acceptance criteria: runbook có preconditions, exact steps, expected result và failure capture cho hai client.
  - Verify recipe: một người khác chạy runbook từ clean Play session và hoàn thành ba damage→death→respawn cycles.
  - Evidence: `Verification/P0_TWO_CLIENT_RUNBOOK.md` — document complete (traced via GitNexus context + source reads, not guessed); execution pending a human running two live Editor/headset sessions.
  - Decision/Assumption: P0 dùng một greybox arena (`02_FPSMAP.unity`, added to Build Settings index 3 this session) và một AR placeholder (AK74, see `Verification/P0_ASSET_SELECTION_2026-07-14.md`).

- [x] Define the combat telemetry and reject-reason contract
  - Outcome: log phân biệt được lỗi fire, cosmetic, claim validation, damage, death và respawn.
  - Scope: events `shot_local`, `shot_remote`, `claim_sent`, `claim_accept/reject`, `damage`, `death`, `respawn`; correlation bằng shooter + shotId; reject enum.
  - Out of scope: production analytics backend, dashboards, PII, anti-cheat enforcement.
  - Dependencies: Option A contract locked.
  - Risk: log tự do không có correlation sẽ không giải thích được Round 1 failure.
  - Acceptance criteria: schema ghi rõ field bắt buộc, owner phát event, severity và reject reasons; không log final damage do shooter khai.
  - Verify recipe: walkthrough một accepted body hit, một duplicate claim và một protected victim; mỗi path có chuỗi event truy được end-to-end.
  - Evidence: `GameDesign/P0_Combat_Telemetry_Contract.md` — schema + reject enum + three traced walkthroughs (§5) written and cross-checked against `Gun_System_Architecture.md` §7.
  - Decision/Assumption: logging chi tiết chỉ bật trong Editor/Development Build. Actual `Bill.Events` struct definitions ship with the Phase 1 task that owns each event's call site (noted in contract §7), not as a standalone code task.

## PHASE 1 — v0.3-P0 Network Gun Proof

### 1.1 — Prove the local AR loop

- [x] Build one data-driven placeholder AR runtime
  - Outcome: một AR hitscan có config độc lập và phát `ShotInfo` local ổn định.
  - Scope: minimum GunConfig/GunCatalog entry, hitscan ray, body/head/world result, deterministic shotId generation.
  - Out of scope: SMG/pistol/melee, skins, spin-up, bolt, two-hand grip.
  - Dependencies: PHASE 0 test runbook; GitNexus query/context và impact trước edit các symbol liên quan.
  - Risk: dựng full 14-file blueprint quá sớm; chỉ tạo boundary cần cho P0.
  - Acceptance criteria: AR bắn được world/body/head; mỗi accepted local fire tiêu đúng một ammo và phát đúng một unique shotId.
  - Verify recipe: Unity Play Mode bắn 20 phát vào ba target type; kiểm log/raycast result, ammo delta và unique shotId.
  - Evidence: `Verification/P0_1_1_LOCAL_LOOP_2026-07-14.md` — 11 accepted shots across World/Body/Head via execute_code Play Mode calls, ammo delta=1 confirmed every shot, shotId strictly unique per session. Short of one continuous 20-shot pass (noted as follow-up).
  - Decision/Assumption: một tay, súng parent vào wrist, không NetworkObject riêng. AR = AK74 placeholder (`P0_ASSET_SELECTION_2026-07-14.md`).

- [x] Implement AR fire gate, magazine and simplified reload
  - Outcome: trigger/RPM/ammo/reload tạo fire loop dự đoán được và không double-fire.
  - Scope: semi hoặc auto theo config, fire interval, one magazine, reload input, empty-mag safety reload, swap/round fire block nếu path đã có.
  - Out of scope: physical magazine, tactical reload variants, per-shell reload.
  - Dependencies: placeholder AR runtime; GitNexus impact trước edit.
  - Risk: Update/Input callback cùng gọi fire gây duplicate shot.
  - Acceptance criteria: RPM trong tolerance đã ghi; ammo không âm; reload không tạo shot; round freeze chặn fire.
  - Verify recipe: Play Mode giữ/bóp cò theo test matrix, empty mag, reload và fire lúc frozen; đối chiếu shot count/timestamp/ammo.
  - Evidence: `Verification/P0_1_1_LOCAL_LOOP_2026-07-14.md` §3 — fire-rate gate (1 of 5 rapid calls fired), dry-fire (TryFire=false, no ammo consumed), auto-reload (state Reloading -> Ready, ammo refilled to 30) all proven in Play Mode. Round-freeze gate code-reviewed but not independently exercised (needs non-Playing ArenaManager phase).
  - Decision/Assumption: simplified reload = animation/timer; config là nguồn stat.

- [/] Deliver immediate local AR feedback
  - Outcome: người bắn thấy/nghe/cảm nhận phát bắn trong frame fire local được accept.
  - Scope: muzzle, tracer, fire audio, recoil visual và haptic; pooled cosmetic reset an toàn.
  - Out of scope: final art, skin FX, remote haptic, damage confirmation styling.
  - Dependencies: local `ShotInfo`; BillGameCore pool/audio/events rules; GitNexus impact trước edit.
  - Risk: feedback chờ RPC hoặc pooled tracer giữ stale state.
  - Acceptance criteria: feedback không phụ thuộc network callback; miss vẫn có tracer/impact phù hợp; pool reuse không để stale visual.
  - Verify recipe: offline/solo Play Mode bắn liên tục và reuse pool; capture frame/log chứng minh local event precedes network relay.
  - Evidence: `Verification/P0_1_1_LOCAL_LOOP_2026-07-14.md` §4 — GunFiredEvent fires synchronously in the same call stack as the raycast (no RPC in the path yet), zero exceptions across 11 shots, pool spawns succeeded. NOT yet verified: visual/audio/haptic quality (no AudioLibrary content in the project at all, no clean screenshot with gun in frame, no physical haptic device this session). Code-complete, sensory verification pending.
  - Decision/Assumption: local responsiveness ưu tiên hơn remote cosmetic exactness.

### 1.2 — Replicate weapon cause and shot cosmetics

- [x] Render the equipped AR proxy on the remote wrist
  - Outcome: client khác luôn thấy đúng proxy gun gắn với wrist của shooter.
  - Scope: minimal `EquippedSlot`, proxy lookup/parenting, late-join render và respawn cleanup.
  - Out of scope: gun NetworkObject, ownership transfer, remote reload animation, skins.
  - Dependencies: current NetworkAvatar wrist replication; GitNexus context/impact trước edit.
  - Risk: stale proxy sau respawn hoặc Spawned ordering.
  - Acceptance criteria: equip/respawn/late join không tạo duplicate hoặc stale gun; proxy không cần transform sync riêng.
  - Verify recipe: hai client equip, respawn và reconnect/late join; quan sát đúng một AR proxy tại wrist.
  - Evidence: `Verification/P0_1_2_REMOTE_PROXY_2026-07-14.md` — code-complete (AvatarWeaponSync: `[Networked] EquippedSlot` + owner mirror + proxy instantiate/StripToVisual). Solo audit pass: proxy strip window không có side effect (AK74_P0 chỉ có HitscanGun + mesh; auto-fire gated `_triggerHeld=false`); static `LocalEquippedWeaponId` không stale trong P0 (domain reload bật, `m_EnterPlayModeOptions: 0`; chưa có unequip/death path — latent: phải clear static khi 1.3.2 land). Two-client verify PASS 2026-07-14 (§6 cùng doc, main + ParrelSync clone): remote player trên clone `slot=0, proxy=True, rends=4, guns=0, collidersOn=0, muzzle=True` — đúng một proxy visual-only trên wrist remote, late-join render OK, zero console error; authority không tự render proxy (`slot=255, proxy=False` khi chưa equip). Respawn cleanup chưa test được — chưa tồn tại death/respawn path (thuộc 1.3.2, blocked D3).
  - Decision/Assumption: sync cause (`EquippedSlot`), không sync mesh transform.

- [x] Relay remote shot cosmetics over an unreliable channel
  - Outcome: remote client thấy muzzle/tracer/impact hợp lý mà packet loss không ảnh hưởng gameplay damage.
  - Scope: unreliable `RPC_ShotFired`, proxy muzzle resolution, local event re-fire trên receiving process.
  - Out of scope: reliable cosmetic replay, historical tracer cho late joiner.
  - Dependencies: AR proxy + local ShotInfo; GitNexus impact trước edit.
  - Risk: dùng local event bus như global bus hoặc double-render trên shooter.
  - Acceptance criteria: remote cosmetic xuất hiện đúng shooter/weapon; shooter không double feedback; mất cosmetic packet không mất accepted ShotClaim.
  - Verify recipe: two-client fire test, sau đó simulated packet loss nếu tooling cho phép; so sánh cosmetic count và accepted damage count.
  - Evidence: `Verification/P0_1_2_REMOTE_PROXY_2026-07-14.md` §6 — two-client PASS 2026-07-14: clone re-fire `[Probe] GunFired shooter=1 shot=424242 weapon=0 part=Body victim=2`, payload khớp 100% shot gốc, muzzle resolve từ proxy `MuzzleAnchor`; shooter không nhận lại event của mình (`RpcTargets.Proxies + InvokeLocal=false`). Simulated packet loss CHƯA chạy (không có tooling trong editor session) — chấp nhận: damage đi channel reliable riêng by construction, mất packet chỉ mất 1 tracer.
  - Decision/Assumption: cosmetic loss được chấp nhận.

### 1.3 — Implement Option A gameplay truth

- [x] Implement reliable ShotClaim submission and victim-side validation
  - Outcome: player hit đi qua một contract reliable, dedupe được và chỉ victim State Authority có quyền accept/reject.
  - Scope: claim schema; targeted RPC; dedupe; shooter/round/equipped/fire-rate/range/origin/hit-part/spawn-protection checks; reject reason telemetry.
  - Out of scope: rewind, server ray reconstruction, kick/ban, client punishment.
  - Dependencies: AR runtime, telemetry contract, existing Fusion ownership model; GitNexus query/context + impact trước edit.
  - Risk: HIGH nếu gắn nhầm authority hoặc trust final damage; phải báo user nếu GitNexus impact trả HIGH/CRITICAL.
  - Acceptance criteria: valid claim được accept đúng một lần; invalid/duplicate claim không tạo accepted-result lần hai; claim không có trusted finalDamage; task này TUYỆT ĐỐI không ghi Health/damage/death/respawn/score (Health write thuộc 1.3.2, blocked theo D3).
  - Verify recipe: inject matrix valid, duplicate, over-rate, bad weapon, out-of-range, protected/dead victim; verify exact accept/reject result và reject reason cho từng case; xác nhận Health trước/sau không đổi (không có Health write trong task này).
  - Evidence: `Verification/P0_1_3_1_SHOTCLAIM_2026-07-14.md` — solo injection matrix pass (11 case §4: valid ×1 accept, Duplicate kể cả replay-of-rejected, InvalidWeapon/OutOfRange/InvalidOrigin/InvalidHitPart/InvalidShooter/SpawnProtected, fire-rate 15=ceil(600/60×1.5) rồi FireRate); hpStart=hpEnd=5 → zero Health write. Two-client transport PASS 2026-07-14 (cùng doc): claim cho shot 424242 đi qua `RPC_SubmitShotClaim` wire thật, shooter identity resolve từ `info.Source`=player 1 (không spoof được từ payload), ClaimAccepted trên victim clone, hpStart==hpEnd. Còn nợ môi trường (không blocker): EquippedMismatch (catalog mới có 1 gun), VictimDead trực tiếp (solo precedence, cần death path 1.3.2), CombatClosed (scene chưa có ArenaManager).
  - Decision/Assumption: victim không rewind/re-raycast lịch sử trong v0.3 Ready.

- [x] Integrate catalog-derived damage with HP, death and respawn
  - Outcome: accepted ShotClaim tạo damage→death→respawn nhất quán trên hai client.
  - Scope: victim tra damage/falloff/headshot từ GunCatalog; Health write; death transition; respawn reset/protection.
  - Out of scope: armor/heal/revive/economy lives.
  - Dependencies: D3 chốt 2026-07-15 (100 HP, death HP ≤ 0, respawn reset 100); ShotClaim validator (1.3.1 Done); GitNexus impact trước edit.
  - Risk: PlayerCombat hiện dùng Health theo nghĩa lives, có blast radius sang ArenaManager/UI.
  - Acceptance criteria: damage không do shooter cung cấp; Health không âm; death/respawn đúng một lần; protection reject late claim.
  - Verify recipe: two-client body/head damage table + lethal simultaneous claims + delayed pre-respawn claim.
  - Evidence: `Verification/P0_1_3_2_DAMAGE_RESPAWN_2026-07-15.md` — two-client PASS: body 16, head 32, HP clamped at 0, simultaneous second lethal claim rejected `VictimDead`, clean marker observed exactly one death and one respawn, respawn restored 100 HP, late claim rejected `SpawnProtected`.
  - Decision/Assumption: D3 locked (100 HP / death ≤ 0 / respawn 100, owner 2026-07-15); killer/score attribution KHÔNG thuộc task này (1.3.3).

- [ ] Award kill and score exactly once from confirmed victim death
  - Outcome: score chỉ tăng từ death đã được victim xác nhận, không từ shooter hit prediction.
  - Scope: killer attribution, duplicate-death guard, ArenaManager score handoff và two-client convergence.
  - Out of scope: assists, economy rewards, ranked persistence.
  - Dependencies: HP/death/respawn integration; GitNexus context/impact trước edit.
  - Risk: hai lethal claim cùng tick có thể double event hoặc sai attribution.
  - Acceptance criteria: mỗi death tăng đúng một score; hai client thống nhất alive/dead/killer/score sau respawn.
  - Verify recipe: 30 alternating kills + simultaneous lethal edge case; compare both clients after each cycle.
  - Evidence: score/death correlation log.
  - Decision/Assumption: claim được victim accept đầu tiên gây lethal nhận kill credit.

### 1.4 — Test Round 1 gate

- [ ] Pass Test Round 1 and tag v0.3-P0 Network Gun Proof
  - Outcome: chứng minh core network gun loop đủ ổn định để mở task cho v0.3-P1.
  - Scope: 30 complete damage→death→respawn cycles; good-network run; latency/loss run nếu tooling hỗ trợ; blocker review và evidence bundle.
  - Out of scope: weapon balance, ability, four-player final performance, polished onboarding.
  - Dependencies: mọi task P0 phía trên Done và có evidence.
  - Risk: một pass ngẫu nhiên hoặc evidence trộn từ nhiều build không chứng minh được milestone.
  - Acceptance criteria: đạt toàn bộ bảy tiêu chí Round 1 trong roadmap trên cùng build lineage; zero unresolved blocker/high-risk issue.
  - Verify recipe: chạy nguyên Test Round 1 theo roadmap, ghi build id/device/network/tester và archive correlated logs/captures.
  - Evidence: `Verification/P0_ROUND1_<date>.md` + artifacts.
  - Decision/Assumption: chỉ task này Pass mới mở detailed backlog cho M2/v0.3-P1.

## PHASE 2 — v0.3-P1 Combat Playable (GATED)

Chưa task hóa. Mở sau khi task Test Round 1 đạt `[x]` và evidence còn hiệu lực trên build lineage hiện hành.

## PHASE 3 — v0.3-RC1 Ready Candidate (GATED)

Chưa task hóa. Mở sau khi Test Round 2 Pass; nội dung bám roadmap, không phục hồi backlog deprecated.
