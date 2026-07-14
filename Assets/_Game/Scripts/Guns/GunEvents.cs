#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>Which part of a body a hitscan ray struck.</summary>
    public enum HitPart { World = 0, Body = 1, Head = 2 }

    /// <summary>
    /// Cosmetic description of one shot — feeds VFX/SFX/tracer/cosmetic-relay. Never carries a trusted final
    /// damage number (Option A, see Docs/Gun_System_Architecture.md §3): damage is resolved victim-side from
    /// <see cref="GunCatalog"/> once a <see cref="ShotClaim"/> is validated.
    /// </summary>
    public struct ShotInfo
    {
        public PlayerRef Shooter;
        public uint ShotId;
        public byte WeaponId;
        public Vector3 MuzzlePos;
        public Vector3 Direction;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public PlayerRef Victim;
        public HitPart HitPart;
    }

    /// <summary>Fired the instant a shot is locally accepted (before any RPC) — drives local muzzle/tracer/
    /// haptic/sound (task 1.1.3). Also re-fired on receiving clients from the cosmetic RPC (task 1.2.2), so a
    /// single feedback listener serves both without knowing which case it is.</summary>
    public struct GunFiredEvent : IEvent
    {
        public ShotInfo Shot;
    }

    /// <summary>Trigger pulled but the gun had no ammo — dry-fire click + light haptic, no shot.</summary>
    public struct GunDryFireEvent : IEvent
    {
        public byte WeaponId;
    }

    public struct GunReloadStartEvent : IEvent
    {
        public byte WeaponId;
        public float Duration;
    }

    public struct GunReloadEndEvent : IEvent
    {
        public byte WeaponId;
    }
}
#endif
