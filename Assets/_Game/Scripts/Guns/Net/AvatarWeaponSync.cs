#if PHOTON_FUSION
using System.Collections.Generic;
using BillGameCore;
using Fusion;
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>
    /// The single network seam of the gun system, on the NetworkAvatar prefab (Gun_System_Architecture.md
    /// §3/§4.4). Task 1.2.1 scope: ONE [Networked] byte (<see cref="EquippedSlot"/>) + a purely-visual proxy
    /// gun model hung under the replicated right-wrist node on remote clients. Task 1.2.2 adds
    /// <see cref="RPC_ShotFired"/> — the unreliable, cosmetic-only shot relay (§4.2): the owner mirrors each
    /// locally-accepted <see cref="GunFiredEvent"/> to proxies, which re-fire the same event on their local
    /// bus so <see cref="GunFeedback"/> renders local and remote shots through one path. RPC_SubmitShotClaim
    /// + HitValidator (task 1.3.1) land in this same class next.
    /// Strictly additive: reads the avatar only through <see cref="IBillPlayer"/> (wrist transform), never
    /// touches NetworkAvatar's existing logic.
    /// </summary>
    public class AvatarWeaponSync : NetworkBehaviour
    {
        /// <summary>Sentinel for "nothing equipped" — weaponId 0 is a real catalog entry (the P0 AR).</summary>
        public const byte None = byte.MaxValue;

        /// <summary>Local truth published by the equip path (GunInput for P0; WeaponSlots when Phase 2 adds
        /// slots) and mirrored onto <see cref="EquippedSlot"/> by the owner — the same static-mirror pattern
        /// as ThrowController.LocalHoldingBall -> NetworkAvatar.HoldingBall.</summary>
        public static byte LocalEquippedWeaponId = None;

        [Tooltip("Where the proxy gun model hangs on remotes. Leave empty to use the avatar's replicated right-wrist node (IBillPlayer.HandRight).")]
        [SerializeField] private Transform _proxyAnchor;

        /// <summary>P0: slot index == weaponId (one gun; GunCatalog.configs is indexed by weaponId). When
        /// WeaponSlots exists this stays a slot index per the architecture doc — the proxy mapping changes,
        /// the wire format (1 byte, snapshot) does not. Late-joiners read the current value in Render(),
        /// no history RPC needed (§5).</summary>
        [Networked] public byte EquippedSlot { get; set; }

        private int _displayedWeapon = -1;   // what the proxy currently shows; -1 = nothing
        private GameObject _proxyInstance;
        private Transform _proxyMuzzle;      // "MuzzleAnchor" child of the proxy model (GunConfig contract)
        private bool _relayHooked;

        public override void Spawned()
        {
            // 1.3.1: every client registers every sync — the shooter routes claims to the victim's NB and
            // the validator resolves the shooter from this same registry.
            Registry[Object.InputAuthority.PlayerId] = this;
            _combat = GetComponent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>(true);   // 1.3.2: validated-damage write target

            // Owner seeds the initial snapshot. Proxies deliberately do NOTHING here: [Networked] state of
            // other NBs isn't safely readable in Spawned (order not guaranteed — Gotchas), so all proxy
            // rendering keys off Render() below, which also makes late-join correct for free.
            if (HasStateAuthority) EquippedSlot = LocalEquippedWeaponId;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            TryHookLocalRelay();                    // lazy: Bill/EventBus may not be ready at Spawned (same poll as GunFeedback)
            EquippedSlot = LocalEquippedWeaponId;   // 1 byte of networked state — the whole gun system's snapshot cost (§5)
        }

        public override void Render()
        {
            if (HasStateAuthority) return;   // owner sees the real local gun on the toon hand, never the proxy
            int want = EquippedSlot == None ? -1 : EquippedSlot;
            if (want == _displayedWeapon) return;
            _displayedWeapon = want;
            RebuildProxy(want);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Object != null && Registry.TryGetValue(Object.InputAuthority.PlayerId, out AvatarWeaponSync reg)
                && reg == this)
                Registry.Remove(Object.InputAuthority.PlayerId);
            UnhookLocalRelay();
            if (_proxyInstance != null) Destroy(_proxyInstance);
            _proxyInstance = null;
            _proxyMuzzle = null;
            _displayedWeapon = -1;
        }

        private void RebuildProxy(int weaponId)
        {
            if (_proxyInstance != null) { Destroy(_proxyInstance); _proxyInstance = null; }
            _proxyMuzzle = null;
            if (weaponId < 0) return;

            GunCatalog catalog = GunCatalog.Default;                       // logs once itself if missing
            GunConfig cfg = catalog != null ? catalog.Get((byte)weaponId) : null;
            if (cfg == null || cfg.modelPrefab == null) return;            // content/setup error, not a code path

            Transform anchor = ResolveAnchor();
            if (anchor == null) return;

            _proxyInstance = Instantiate(cfg.modelPrefab, anchor);
            _proxyInstance.transform.localPosition = Vector3.zero;
            _proxyInstance.transform.localRotation = Quaternion.identity;
            StripToVisual(_proxyInstance);
            _proxyMuzzle = FindMuzzleAnchor(_proxyInstance.transform);
        }

        // ── Task 1.2.2 — unreliable cosmetic shot relay (Gun_System_Architecture.md §4.2) ──────────

        /// <summary>Owner-side: mirror every locally-accepted shot to the other clients. Subscribed lazily
        /// from FixedUpdateNetwork (owner only) because Bill may not be ready at Spawned.</summary>
        private void TryHookLocalRelay()
        {
            if (_relayHooked || !Bill.IsReady) return;
            _relayHooked = true;
            Bill.Events.Subscribe<GunFiredEvent>(OnLocalShot);
        }

        private void UnhookLocalRelay()
        {
            if (!_relayHooked) return;
            _relayHooked = false;
            if (Bill.IsReady) Bill.Events.Unsubscribe<GunFiredEvent>(OnLocalShot);
        }

        private void OnLocalShot(GunFiredEvent e)
        {
            // Echo guard: the RPC handler below re-fires this SAME event type on receiving clients (the bus
            // is per-process), so relay ONLY shots this machine's own gun produced — otherwise every remote
            // cosmetic re-fire would be re-broadcast (echo storm).
            if (!HasStateAuthority || Object == null || !Object.IsValid) return;
            if (e.Shot.Shooter != Object.InputAuthority) return;
            RPC_ShotFired(e.Shot.ShotId, e.Shot.WeaponId, e.Shot.MuzzlePos, e.Shot.Direction,
                          e.Shot.HitPoint, e.Shot.HitNormal, e.Shot.Victim, (byte)e.Shot.HitPart);

            // Task 1.3.1 — §3 reliable claim path: ONLY player hits (Body/Head) spawn a claim; World hits
            // stay cosmetic-only. Runs alongside (not instead of) the unreliable relay above.
            if (e.Shot.Victim != PlayerRef.None && e.Shot.HitPart != HitPart.World)
                SendShotClaim(e);
        }

        /// <summary>Cosmetic-only, fire-and-forget (§4.2; edge case #11: a dropped packet loses one tracer,
        /// nothing else). Targets Proxies + InvokeLocal=false so the shooter NEVER double-renders its own
        /// shot. Carries NO damage — 1.3.1's ShotClaim is a separate, reliable path.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, InvokeLocal = false, Channel = RpcChannel.Unreliable)]
        private void RPC_ShotFired(uint shotId, byte weaponId, Vector3 muzzlePos, Vector3 direction,
                                   Vector3 hitPoint, Vector3 hitNormal, PlayerRef victim, byte hitPart)
        {
            if (!Bill.IsReady) return;

            // §4.2: muzzle comes from the proxy gun rendered on THIS client (the wrist node already
            // interpolates), so tracers originate from where the remote gun visibly is. The wire position
            // is only the fallback for "shot arrived before Render() built the proxy model".
            Vector3 muzzle = _proxyMuzzle != null ? _proxyMuzzle.position : muzzlePos;

            Bill.Events.Fire(new GunFiredEvent
            {
                Shot = new ShotInfo
                {
                    Shooter   = Object.InputAuthority,
                    ShotId    = shotId,
                    WeaponId  = weaponId,
                    MuzzlePos = muzzle,
                    Direction = direction,
                    HitPoint  = hitPoint,
                    HitNormal = hitNormal,
                    Victim    = victim,
                    HitPart   = (HitPart)hitPart,
                }
            });
        }

        private static Transform FindMuzzleAnchor(Transform root)
        {
            // GunConfig contract: the model prefab exposes a child named exactly "MuzzleAnchor".
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "MuzzleAnchor") return t;
            return null;
        }

        private Transform ResolveAnchor()
        {
            if (_proxyAnchor != null) return _proxyAnchor;
            IBillPlayer player = GetComponent<NetworkAvatar>();
            return player != null ? player.HandRight : null;
        }

        /// <summary>The shared modelPrefab carries the live HitscanGun (+ colliders) for the OWNER's hand.
        /// On a proxy it must be a dumb visual: no Gun logic ticking, and no colliders eating hitscan rays
        /// aimed at the player standing behind it (edge case #17's ignore-list only covers the shooter's
        /// own gun).</summary>
        private static void StripToVisual(GameObject go)
        {
            foreach (Gun gun in go.GetComponentsInChildren<Gun>(true)) Destroy(gun);
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        // ── Task 1.3.1 — ShotClaim + HitValidator (Gun_System_Architecture.md §3/§7, telemetry contract) ──
        //
        // Reliable, validated damage path — deliberately separate from the unreliable cosmetic relay above.
        // The shooter submits a CLAIM (never a damage number — Option A); the victim's State Authority
        // re-validates it and resolves damage from its OWN GunCatalog. 1.3.1 built validate + telemetry;
        // 1.3.2 (D3 locked 2026-07-15: 100 HP) connects accepted claims to the single Health write in
        // PlayerCombat.ApplyValidatedDamage. Score/kill attribution stays out until 1.3.3.

        private const float OriginToleranceMeters = 3f;   // proxy-wrist interp/extrap slack (§7 validator table)
        private const float RangeMarginFactor = 1.1f;     // claimed origin→hit distance may exceed range by 10%
        private const float FireRateWindowSeconds = 1f;   // sliding window for the rpm check (§7)
        private const float FireRateSlackFactor = 1.5f;   // burst/jitter tolerance over nominal rpm
        private const int SeenClaimsCap = 512;            // dedupe memory bound per victim (FIFO eviction)

        /// <summary>Every live sync, keyed by InputAuthority id, maintained on ALL clients (Spawned runs
        /// everywhere): the shooter routes its claim to the VICTIM's instance, and the validator resolves
        /// the SHOOTER's replicated state (EquippedSlot, wrist, combat) from the same map.</summary>
        private static readonly Dictionary<int, AvatarWeaponSync> Registry =
            new Dictionary<int, AvatarWeaponSync>();

        public static AvatarWeaponSync FindByPlayer(PlayerRef player)
            => Registry.TryGetValue(player.PlayerId, out AvatarWeaponSync sync) ? sync : null;

        private readonly HashSet<long> _seenClaims = new HashSet<long>();     // (shooter,shotId) → Duplicate
        private readonly Queue<long> _seenClaimOrder = new Queue<long>();     // FIFO eviction for the set
        private readonly Dictionary<int, Queue<double>> _acceptTimes =
            new Dictionary<int, Queue<double>>();                             // per-shooter accept timestamps
        private PlayerCombat _combat;                                         // 1.3.2: ApplyValidatedDamage sink

        /// <summary>Shooter-side (owner only): turn one locally-accepted player hit into one reliable claim
        /// aimed at the victim's State Authority. Fires `claim_sent` on the local bus (contract §3.4).</summary>
        private void SendShotClaim(GunFiredEvent e)
        {
            AvatarWeaponSync victimSync = FindByPlayer(e.Shot.Victim);
            if (victimSync == null) return;   // victim despawned between raycast and relay — claim is moot

            if (Bill.IsReady) Bill.Events.Fire(new ClaimSentEvent
            {
                Shooter = e.Shot.Shooter,
                Victim = e.Shot.Victim,
                ShotId = e.Shot.ShotId,
                WeaponId = e.Shot.WeaponId,
                HitPart = e.Shot.HitPart,
                ClientTick = Runner.Tick,
            });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Telemetry] claim_sent shooter={e.Shot.Shooter.PlayerId} victim={e.Shot.Victim.PlayerId} " +
                      $"shot={e.Shot.ShotId} weapon={e.Shot.WeaponId} part={e.Shot.HitPart} tick={Runner.Tick}");
#endif
            victimSync.RPC_SubmitShotClaim(e.Shot.ShotId, e.Shot.WeaponId, e.Shot.MuzzlePos, e.Shot.Direction,
                                           e.Shot.HitPoint, (byte)e.Shot.HitPart, Runner.Tick);
        }

        /// <summary>Reliable (default channel) claim transport. Shooter identity comes from the TRANSPORT
        /// (<paramref name="info"/>.Source), never from the payload — a client cannot claim on someone
        /// else's behalf (§7 "shooter identity" row).</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SubmitShotClaim(uint shotId, byte weaponId, Vector3 origin, Vector3 direction,
                                         Vector3 hitPoint, byte hitPart, int clientTick,
                                         RpcInfo info = default)
        {
            ProcessShotClaim(info.Source, shotId, weaponId, origin, direction, hitPoint, hitPart, clientTick);
        }

        /// <summary>Victim-authority validator. Public ONLY as a test seam so the §4 rejection matrix can be
        /// injected in a single editor (guarded: silent no-op off-authority). Emits exactly one
        /// ClaimAccepted/ClaimRejected per claim; the FIRST failing check wins (enum declaration order).</summary>
        public void ProcessShotClaim(PlayerRef shooter, uint shotId, byte weaponId, Vector3 origin,
                                     Vector3 direction, Vector3 hitPoint, byte hitPart, int clientTick)
        {
            if (!HasStateAuthority || Object == null || !Object.IsValid || Runner == null) return;

            PlayerRef victim = Object.InputAuthority;
            long key = ((long)shooter.PlayerId << 32) | shotId;
            float distance = Vector3.Distance(origin, hitPoint);

            ShotRejectReason? reason = ValidateClaim(shooter, weaponId, origin, hitPart, distance, key);
            RememberClaim(key);   // accepted AND rejected both count as seen → replays hit Duplicate (§4)

            if (reason.HasValue)
            {
                if (Bill.IsReady) Bill.Events.Fire(new ClaimRejectedEvent
                {
                    Shooter = shooter, Victim = victim, ShotId = shotId, WeaponId = weaponId,
                    Reason = reason.Value, FusionTick = Runner.Tick,
                });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Telemetry] claim_reject shooter={shooter.PlayerId} victim={victim.PlayerId} " +
                          $"shot={shotId} weapon={weaponId} reason={reason.Value} tick={Runner.Tick}");
#endif
                return;
            }

            NoteAccepted(shooter.PlayerId);
            bool isHead = hitPart == (byte)HitPart.Head;
            int damage = GunCatalog.Default.ResolveDamage(weaponId, distance, isHead);   // victim-side resolve

            // Task 1.3.2 — the ONE Health write of the gun path (D3: 100 HP). Victim authority applies its
            // own catalog-resolved damage; the shooter never supplied a number. Death + respawn ride the
            // existing seams unchanged: PlayerDiedEvent fires on the >0→0 transition inside PlayerCombat,
            // NetworkAvatar.HandleRespawn arms on Health ≤ 0, and RestoreLives re-arms spawn protection so
            // late claims reject as SpawnProtected/VictimDead in ValidateClaim above.
            int healthBefore = _combat != null ? _combat.Health : 0;
            if (_combat != null) _combat.ApplyValidatedDamage(damage, shooter, hitPoint);
            int healthAfter = _combat != null ? _combat.Health : healthBefore;

            if (Bill.IsReady) Bill.Events.Fire(new ClaimAcceptedEvent
            {
                Shooter = shooter, Victim = victim, ShotId = shotId, WeaponId = weaponId,
                ResolvedDamage = damage, HealthBefore = healthBefore, HealthAfter = healthAfter,
                IsHead = isHead, FusionTick = Runner.Tick,
            });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Telemetry] claim_accept shooter={shooter.PlayerId} victim={victim.PlayerId} " +
                      $"shot={shotId} weapon={weaponId} damage={damage} head={isHead} " +
                      $"hb={healthBefore} ha={healthAfter} dist={distance:0.00} tick={Runner.Tick}");
#endif
        }

        /// <summary>Checks run in ShotRejectReason declaration order — one reason per rejection, no
        /// catch-alls (telemetry contract §4).</summary>
        private ShotRejectReason? ValidateClaim(PlayerRef shooter, byte weaponId, Vector3 origin,
                                                byte hitPart, float distance, long key)
        {
            if (_seenClaims.Contains(key)) return ShotRejectReason.Duplicate;

            AvatarWeaponSync shooterSync = FindByPlayer(shooter);
            PlayerCombat shooterCombat = shooterSync != null ? shooterSync._combat : null;
            if (shooterSync == null || shooterSync.Object == null || !shooterSync.Object.IsValid
                || shooterCombat == null || shooterCombat.Health <= 0 || shooterCombat.IsFrozen)
                return ShotRejectReason.InvalidShooter;

            // Same convention as GunInput.cs:60 — a scene without an ArenaManager plays with combat open.
            ArenaManager arena = ArenaManager.Instance;
            if (arena != null && arena.Phase != ArenaManager.MatchPhase.Playing)
                return ShotRejectReason.CombatClosed;

            GunCatalog catalog = GunCatalog.Default;
            GunConfig cfg = catalog != null ? catalog.Get(weaponId) : null;
            if (cfg == null) return ShotRejectReason.InvalidWeapon;

            if (shooterSync.EquippedSlot != weaponId) return ShotRejectReason.EquippedMismatch;

            if (IsOverFireRate(shooter.PlayerId, cfg)) return ShotRejectReason.FireRate;

            Transform wrist = shooterSync.ResolveAnchor();
            Vector3 refPos = wrist != null ? wrist.position : shooterSync.transform.position;
            if ((origin - refPos).sqrMagnitude > OriginToleranceMeters * OriginToleranceMeters)
                return ShotRejectReason.InvalidOrigin;

            if (distance > cfg.range * RangeMarginFactor) return ShotRejectReason.OutOfRange;

            if (hitPart != (byte)HitPart.Body && hitPart != (byte)HitPart.Head)
                return ShotRejectReason.InvalidHitPart;

            if (_combat == null || _combat.Health <= 0) return ShotRejectReason.VictimDead;
            if (_combat.IsInvulnerable) return ShotRejectReason.SpawnProtected;

            return null;
        }

        /// <summary>Sliding-window rpm check against ACCEPTED claims only: allowed =
        /// ceil(rpm/60 × window × slack). Spam of already-rejected claims can't starve honest ones.</summary>
        private bool IsOverFireRate(int shooterId, GunConfig cfg)
        {
            if (!_acceptTimes.TryGetValue(shooterId, out Queue<double> times))
                _acceptTimes[shooterId] = times = new Queue<double>();

            double now = Runner.SimulationTime;
            while (times.Count > 0 && now - times.Peek() > FireRateWindowSeconds) times.Dequeue();

            int allowed = Mathf.Max(1, Mathf.CeilToInt(
                cfg.roundsPerMinute / 60f * FireRateWindowSeconds * FireRateSlackFactor));
            return times.Count >= allowed;
        }

        private void NoteAccepted(int shooterId)
        {
            if (!_acceptTimes.TryGetValue(shooterId, out Queue<double> times))
                _acceptTimes[shooterId] = times = new Queue<double>();
            times.Enqueue(Runner.SimulationTime);
        }

        private void RememberClaim(long key)
        {
            if (!_seenClaims.Add(key)) return;
            _seenClaimOrder.Enqueue(key);
            while (_seenClaimOrder.Count > SeenClaimsCap) _seenClaims.Remove(_seenClaimOrder.Dequeue());
        }
    }
}
#endif
