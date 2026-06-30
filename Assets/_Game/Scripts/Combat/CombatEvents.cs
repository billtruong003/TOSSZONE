using BillGameCore;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>A player took a hit — fired on EVERY client from <c>PlayerCombat.RPC_TakeHit</c>. Local listeners
    /// drive VFX / haptic / health UI decoupled.</summary>
    public struct PlayerHitEvent : IEvent
    {
        public int Damage;
        public int RemainingHealth;
        public Vector3 Point;
        public bool IsLocalVictim;   // true on the client whose own avatar was hit
    }

    /// <summary>A player's health reached 0 (fired on the victim's client).</summary>
    public struct PlayerDiedEvent : IEvent { public bool IsLocal; }

    /// <summary>The LOCAL player's money changed — for the wrist weapon-selector UI.</summary>
    public struct MoneyChangedEvent : IEvent { public int Money; }
}
