using BillGameCore;

namespace TossZone.Network
{
    /// <summary>High-level matchmaking phase, surfaced to UI via <see cref="MatchmakingStatusEvent"/>.</summary>
    public enum MatchPhase
    {
        Idle,
        Connecting,
        Connected,
        Failed,
        TimedOut
    }

    /// <summary>Fired whenever the matchmaking flow changes phase. A world-space UI can subscribe.</summary>
    public struct MatchmakingStatusEvent : IEvent
    {
        public MatchPhase Phase;
        public string Message;
    }
}
