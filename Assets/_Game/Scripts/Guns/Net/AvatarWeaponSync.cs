#if PHOTON_FUSION
using BillGameCore;
using Fusion;
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
    }
}
#endif
