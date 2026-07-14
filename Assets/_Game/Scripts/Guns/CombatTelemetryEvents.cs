#if PHOTON_FUSION
using BillGameCore;
using Fusion;

namespace TossZone.Guns
{
    /// <summary>
    /// Why a victim-side validator dropped a ShotClaim. Mirrors Docs/GameDesign/P0_Combat_Telemetry_Contract.md
    /// §4 1:1 — exactly ONE reason per rejection (first failing check in declaration order wins). Evolve by
    /// adding named values, never by overloading an existing one into a catch-all.
    /// </summary>
    public enum ShotRejectReason
    {
        Duplicate,          // (shooter, shotId) already accepted or rejected before
        InvalidShooter,     // shooter doesn't resolve to a live avatar, or shooter is dead/frozen/not in round
        CombatClosed,       // ArenaManager.Phase isn't Playing when the claim arrived
        InvalidWeapon,      // weaponId has no entry in GunCatalog
        EquippedMismatch,   // weaponId doesn't match the shooter's currently-replicated EquippedSlot
        FireRate,           // claims arrive faster than the weapon's rpm sliding window allows (§7 arch doc)
        InvalidOrigin,      // origin implausibly far from the shooter's replicated wrist/root position
        OutOfRange,         // claimed origin→hitPoint distance exceeds catalog range × tolerance margin
        InvalidHitPart,     // hitPart isn't Body/Head
        VictimDead,         // victim Health already <= 0 when the claim arrived
        SpawnProtected,     // victim is within its post-respawn invulnerability window
    }

    /// <summary>`claim_sent` (contract §3.4) — fired on the SHOOTER's client the moment a reliable
    /// RPC_SubmitShotClaim leaves for the victim's State Authority. Carries NO damage field by design
    /// (Option A: the shooter never sends a trusted damage number).</summary>
    public struct ClaimSentEvent : IEvent
    {
        public PlayerRef Shooter;
        public PlayerRef Victim;
        public uint ShotId;
        public byte WeaponId;
        public HitPart HitPart;
        public int ClientTick;
    }

    /// <summary>`claim_accept` (contract §3.4/§3.6) — fired ONLY on the victim's State Authority after every
    /// validator check passed. This is the validated outcome 1.3.2 will consume to write Health once D3
    /// unblocks; in 1.3.1 nothing consumes it for state, so HealthBefore == HealthAfter always (read-only
    /// observation, zero Health writes).</summary>
    public struct ClaimAcceptedEvent : IEvent
    {
        public PlayerRef Shooter;
        public PlayerRef Victim;
        public uint ShotId;
        public byte WeaponId;
        public int ResolvedDamage;   // victim-side GunCatalog.ResolveDamage — never shooter-supplied
        public int HealthBefore;     // read-only snapshot; 1.3.1 performs no Health write
        public int HealthAfter;      // == HealthBefore until 1.3.2 lands (D3-blocked)
        public bool IsHead;
        public int FusionTick;
    }

    /// <summary>`claim_reject` (contract §3.5) — fired ONLY on the victim's State Authority with exactly one
    /// reason (the first failing check). Never silently swallowed.</summary>
    public struct ClaimRejectedEvent : IEvent
    {
        public PlayerRef Shooter;
        public PlayerRef Victim;
        public uint ShotId;
        public byte WeaponId;
        public ShotRejectReason Reason;
        public int FusionTick;
    }
}
#endif
