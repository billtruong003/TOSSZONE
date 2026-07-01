#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Network authority for the Arena minigame match loop. ONE instance lives as a SCENE NetworkObject;
    /// Fusion (Shared Mode) assigns StateAuthority to the master client automatically.
    ///
    /// Match flow: Warmup → Playing → RoundEnd → (repeat or) MatchEnd.
    /// Win condition (per round): all real players except one have Health ≤ 0 — that last player wins the round.
    /// Draws (timeout) go to ScoreA if player A has more health, else ScoreB, else draw (no point awarded).
    ///
    /// All [Networked] fields replicate to every client. Events fire on all clients via <c>Bill.Events</c>.
    /// </summary>
    public class ArenaManager : NetworkBehaviour
    {
        public enum MatchPhase { Warmup = 0, Playing = 1, RoundEnd = 2, MatchEnd = 3 }

        [Header("Match rules (authoritative — mirrors MinigameDef)")]
        [SerializeField] private int _bestOf = 1;          // 1, 3, or 5
        [SerializeField] private float _roundDuration = 120f;
        [SerializeField] private float _warmupDuration = 5f;
        [SerializeField] private float _roundEndDuration = 4f;

        [Header("Scene refs")]
        [SerializeField] private Transform[] _spawnPointsA;
        [SerializeField] private Transform[] _spawnPointsB;
        [SerializeField] private RingSpawner _ringSpawner;

        // ── Networked state ───────────────────────────────────────────────────────────
        [Networked] public MatchPhase Phase { get; private set; }
        [Networked] public int Round { get; private set; }
        [Networked] public int ScoreA { get; private set; }
        [Networked] public int ScoreB { get; private set; }
        [Networked] public TickTimer PhaseTimer { get; private set; }

        private int _winsNeeded;

        public override void Spawned()
        {
            _winsNeeded = (_bestOf + 1) / 2;
            if (!HasStateAuthority) return;
            Phase = MatchPhase.Warmup;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _warmupDuration);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (Phase)
            {
                case MatchPhase.Warmup:
                    if (PhaseTimer.Expired(Runner)) StartRound();
                    break;

                case MatchPhase.Playing:
                    CheckWinCondition();
                    if (PhaseTimer.Expired(Runner)) OnTimeout();
                    break;

                case MatchPhase.RoundEnd:
                    if (PhaseTimer.Expired(Runner)) AdvanceRound();
                    break;
            }
        }

        // ── Authority helpers ─────────────────────────────────────────────────────────

        private void StartRound()
        {
            Round++;
            Phase = MatchPhase.Playing;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _roundDuration);

            ResetAllCombat();
            _ringSpawner?.ResetRings();

            if (CombatSession.Instance != null) CombatSession.Instance.NotifyRoundStart();
            if (Bill.IsReady) Bill.Events.Fire(new RoundEndEvent { Round = Round - 1 });
        }

        private void CheckWinCondition()
        {
            PlayerRef winner = PlayerRef.None;
            int aliveCount = 0;
            int realPlayerCount = 0;
            PlayerCombat winnerCombat = null;

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer) continue;   // bots (DummyAvatar) never count
                realPlayerCount++;
                if (pc.Health > 0)
                {
                    aliveCount++;
                    winnerCombat = pc;
                    if (pc.Object != null) winner = pc.Object.InputAuthority;
                }
            }

            // Decide a round by elimination only once at least 2 real players are in the match. Gating on the
            // bot-inclusive AllInstances.Count let a solo player (or a bot-only arena) end the round every tick,
            // spinning Warmup→Playing→RoundEnd forever and constantly resetting combat health.
            if (realPlayerCount >= 2 && aliveCount <= 1)
                EndRound(winner, winnerCombat);
        }

        private void OnTimeout()
        {
            // Award point to the player with higher health; draw = no point.
            PlayerCombat best = null;
            int bestHp = 0;
            bool tied = false;

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer) continue;
                if (pc.Health > bestHp) { bestHp = pc.Health; best = pc; tied = false; }
                else if (pc.Health == bestHp) tied = true;
            }

            PlayerRef winner = (!tied && best != null && best.Object != null)
                ? best.Object.InputAuthority
                : PlayerRef.None;

            EndRound(winner, best);
        }

        private void EndRound(PlayerRef winner, PlayerCombat winnerCombat)
        {
            Phase = MatchPhase.RoundEnd;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _roundEndDuration);

            // Award score (simple 2-team: A = first player, B = second player).
            // For proper team assignment extend with a team registry.
            if (winner != PlayerRef.None) AwardScore(winner);

            if (Bill.IsReady) Bill.Events.Fire(new RoundEndEvent { Winner = winner, Round = Round });

            if (ScoreA >= _winsNeeded || ScoreB >= _winsNeeded)
            {
                Phase = MatchPhase.MatchEnd;
                int winTeam = ScoreA >= _winsNeeded ? 0 : 1;
                if (Bill.IsReady) Bill.Events.Fire(new MatchEndEvent { WinnerTeam = winTeam });
            }
        }

        private void AdvanceRound()
        {
            if (Phase == MatchPhase.MatchEnd) return;
            StartRound();
        }

        private void AwardScore(PlayerRef winner)
        {
            // Naive: check if winner is client 0 (team A) or 1 (team B).
            int idx = 0;
            foreach (PlayerRef pr in Runner.ActivePlayers)
            {
                if (pr == winner) { if (idx == 0) ScoreA++; else ScoreB++; break; }
                idx++;
            }
        }

        private void ResetAllCombat()
        {
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                pc.ResetForRound();
        }
    }
}
#endif
