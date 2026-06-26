using BillGameCore;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>EventBus events for the throw mechanic — fired so juice (audio/haptic/VFX) layers decouple from
    /// the controller/projectile. All are <c>struct : IEvent</c> (zero-GC). Subscribe via <c>Bill.Events</c>.</summary>

    /// <summary>Fired the instant a throw launches (FIRE), at the spawn point, with the resolved power 0..1.</summary>
    public struct BallThrownEvent : IEvent
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Power;   // 0..1 resolved through the power curve
    }

    /// <summary>Fired when a flying projectile lands / ends its arc (no hit).</summary>
    public struct BallLandedEvent : IEvent
    {
        public Vector3 Position;
        public float Power;
    }

    /// <summary>Fired when a projectile hits a player (damage deferred — this drives juice + cross-player haptic).</summary>
    public struct BallHitEvent : IEvent
    {
        public Vector3 Position;
        public float Power;
        public int VictimPlayerId;   // -1 if unknown / local-only test
    }
}
