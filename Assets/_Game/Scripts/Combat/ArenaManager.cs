#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Minigame;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Network authority for the Arena minigame match loop. ONE instance lives as a SCENE NetworkObject;
    /// Fusion (Shared Mode) assigns StateAuthority to the master client automatically.
    ///
    /// Match flow: Warmup → Playing → RoundEnd → (repeat or) MatchEnd.
    /// All [Networked] fields replicate to every client. Events fire on all clients via <c>Bill.Events</c>.
    /// </summary>
    public class ArenaManager : NetworkBehaviour
    {
        public enum MatchPhase { Warmup = 0, Playing = 1, RoundEnd = 2, MatchEnd = 3 }

        [Header("Match rules (authoritative — mirrors MinigameDef)")]
        [SerializeField] private int _bestOf = 3;          // 1, 3, or 5
        [SerializeField] private float _roundDuration = 90f;
        [SerializeField] private float _warmupDuration = 5f;
        [SerializeField] private float _roundEndDuration = 5f;
        [Tooltip("Must match a MinigameDef.id under Resources/Minigames/ — CombatSession reads its weaponCatalog "
                + "from this id. Nothing else in the real join flow (portal / direct-play gate) calls "
                + "MinigameManager.Enter(), so THIS is what turns combat 'on' for weapon equip/fire.")]
        [SerializeField] private string _minigameId = "arena";

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

            // Fire on EVERY client (not just authority) — Bill.Events is a local, per-process bus, and
            // CombatSession (which resolves the weapon catalog for HandWeapon/WristWeaponSelector) needs to
            // react locally on each client. Nothing else in the real join flow fires this: PortalMatchmaker
            // just does a Fusion scene load, and ArenaNetworkLoadGate only fixes up dormant scene objects.
            if (Bill.IsReady) Bill.Events.Fire(new MinigameEnteredEvent { Id = _minigameId });

            if (!HasStateAuthority) return;
            Phase = MatchPhase.Warmup;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _warmupDuration);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Bill.IsReady) Bill.Events.Fire(new MinigameExitedEvent { Id = _minigameId });
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

            PlayerCombat.MaxLives = PlayerCombat.LivesForPlayerCount(CountRealPlayers());
            ResetAllCombat();
            _ringSpawner?.ResetRings();
            ClearLeftoverHazards();

            if (CombatSession.Instance != null) CombatSession.Instance.NotifyRoundStart();
        }

        private void ClearLeftoverHazards()
        {
            foreach (var zone in FindObjectsByType<BuffZone>(FindObjectsSortMode.None))
                if (zone.Object != null && zone.Object.IsValid && zone.Object.HasStateAuthority)
                    Runner.Despawn(zone.Object);
            foreach (var proj in FindObjectsByType<TossZone.Throwing.NetworkProjectile>(FindObjectsSortMode.None))
                if (proj.Object != null && proj.Object.IsValid && proj.Object.HasStateAuthority)
                    Runner.Despawn(proj.Object);
        }

        private int CountRealPlayers()
        {
            int count = 0;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                if (pc.IsPlayer) count++;
            return count;
        }

        private void CheckWinCondition()
        {
            int aliveCount = 0;
            int realPlayerCount = 0;
            PlayerCombat lastAlive = null;

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer) continue;   // bots (DummyAvatar) never count
                realPlayerCount++;
                if (pc.Health > 0)
                {
                    aliveCount++;
                    lastAlive = pc;
                }
            }

            // Decide a round by elimination only once at least 2 real players are in the match. Gating on the
            // bot-inclusive AllInstances.Count let a solo player (or a bot-only arena) end the round every tick,
            // spinning Warmup→Playing→RoundEnd forever and constantly resetting combat health.
            if (realPlayerCount < 2 || aliveCount > 1) return;

            int winnerTeam = lastAlive != null && lastAlive.Object != null
                ? GetTeam(lastAlive.Object.InputAuthority) : -1;
            EndRound(winnerTeam);
        }

        private void OnTimeout()
        {
            int totalA = 0, totalB = 0;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null) continue;
                if (GetTeam(pc.Object.InputAuthority) == 0) totalA += pc.Health;
                else totalB += pc.Health;
            }
            EndRound(totalA > totalB ? 0 : totalB > totalA ? 1 : -1);
        }

        private void EndRound(int winnerTeam)
        {
            Phase = MatchPhase.RoundEnd;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _roundEndDuration);

            if (winnerTeam == 0) ScoreA++;
            else if (winnerTeam == 1) ScoreB++;

            if (Bill.IsReady) Bill.Events.Fire(new RoundEndEvent
            {
                WinnerTeam = winnerTeam,
                Round = Round,
                ScoreA = ScoreA,
                ScoreB = ScoreB
            });

            bool decided = ScoreA >= _winsNeeded || ScoreB >= _winsNeeded;
            bool allRoundsPlayed = Round >= _bestOf;
            if (!decided && !allRoundsPlayed) return;

            Phase = MatchPhase.MatchEnd;
            int matchWinner = ScoreA > ScoreB ? 0 : ScoreB > ScoreA ? 1 : -1;
            if (Bill.IsReady) Bill.Events.Fire(new MatchEndEvent { WinnerTeam = matchWinner });
        }

        private void AdvanceRound()
        {
            if (Phase == MatchPhase.MatchEnd) return;
            StartRound();
        }

        /// <summary>Team of a player: 0 = A, 1 = B (alternating by PlayerId). Single source of truth for team
        /// membership — used by scoring and spawn sides so they can never disagree. No networked field needed:
        /// every client already knows every PlayerId, so this is a pure deterministic function.</summary>
        public static int GetTeam(PlayerRef player) => player.PlayerId % 2;

        private void ResetAllCombat()
        {
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                pc.ResetForRound();
        }

        /// <summary>World spawn position for a player, by team (see <see cref="GetTeam"/>). Used by respawn.
        /// T16: picks randomly among the team's spawn points (a spread-out area, not one fixed spot) so
        /// teammates don't stack on top of each other. Falls back to this object's position when spawn points
        /// aren't wired.</summary>
        public Vector3 GetSpawnPosition(PlayerRef player)
        {
            int side = (GetTeam(player) + Mathf.Max(0, Round - 1)) % 2;
            Transform[] pts = side == 0 ? _spawnPointsA : _spawnPointsB;
            if (pts == null || pts.Length == 0) return transform.position;
            // Deterministic-enough: each client resolving its OWN respawn independently just needs a plausible
            // spot, not perfect cross-client agreement (this only positions the LOCAL rig — see NetworkAvatar.
            // TeleportToSpawn — never networked state).
            int idx = UnityEngine.Random.Range(0, pts.Length);
            return pts[idx] != null ? pts[idx].position : transform.position;
        }
    }
}
#endif
