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
    /// gun model hung under the replicated right-wrist node on remote clients. RPC_ShotFired (task 1.2.2) and
    /// RPC_SubmitShotClaim + HitValidator (task 1.3.1) land in this same class next.
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
            if (_proxyInstance != null) Destroy(_proxyInstance);
            _proxyInstance = null;
            _displayedWeapon = -1;
        }

        private void RebuildProxy(int weaponId)
        {
            if (_proxyInstance != null) { Destroy(_proxyInstance); _proxyInstance = null; }
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
