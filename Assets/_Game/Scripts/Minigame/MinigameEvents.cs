using BillGameCore;

namespace TossZone.Minigame
{
    /// <summary>Fired when the local player enters a minigame (as the manager kicks off the scene load).
    /// Gameplay/UI/audio subscribe decoupled via <c>Bill.Events</c>.</summary>
    public struct MinigameEnteredEvent : IEvent { public string Id; }

    /// <summary>Fired when the local player exits a minigame back to the hub.</summary>
    public struct MinigameExitedEvent : IEvent { public string Id; }
}
