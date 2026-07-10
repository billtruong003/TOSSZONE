#if PHOTON_FUSION
using BillGameCore;
using BillInspector;
using Fusion;
using TossZone.Minigame;
using TossZone.Player;
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
        [SerializeField] private float _winCheckGraceSeconds = 0.5f;
        [Tooltip("FLOW-04 — seconds after MatchEnd before every client returns itself to the hub. Local timer; "
                + "the replicated Phase is the sync source so no extra RPC is needed.")]
        [SerializeField] private float _matchEndReturnDelay = 10f;
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
        [Networked] public int NetMaxLives { get; private set; }
        [Networked] private int LastWinnerTeam { get; set; }
        [Networked] private NetworkBool RoundHadBothTeams { get; set; }
        [Networked] private TickTimer WinCheckGrace { get; set; }
        [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, int> Teams => default;

        public static ArenaManager Instance { get; private set; }

        private int _winsNeeded;
        private ChangeDetector _changes;
        private float _matchEndAtLocal = -1f;   // Time.time when this client saw Phase flip to MatchEnd
        private bool _returnTriggered;
        private static readonly PlayerRef[] TeamScratch = new PlayerRef[8];

        public override void Spawned()
        {
            Instance = this;
            _winsNeeded = (_bestOf + 1) / 2;
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            // Fire on EVERY client (not just authority) — Bill.Events is a local, per-process bus, and
            // CombatSession (which resolves the weapon catalog for HandWeapon/WristWeaponSelector) needs to
            // react locally on each client. Nothing else in the real join flow fires this: PortalMatchmaker
            // just does a Fusion scene load, and ArenaNetworkLoadGate only fixes up dormant scene objects.
            if (Bill.IsReady)
            {
                Bill.Events.Fire(new MinigameEnteredEvent { Id = _minigameId });
                Bill.Events.Subscribe<FusionPlayerJoinedEvent>(OnPlayerJoined);
                Bill.Events.Subscribe<FusionPlayerLeftEvent>(OnPlayerLeft);
            }

            // Late joiner: the round's lives value was decided before we arrived — the RPC_ResetRound that
            // carried it never reached us, so read the replicated copy instead.
            if (!HasStateAuthority && NetMaxLives > 0) PlayerCombat.MaxLives = NetMaxLives;

            // Late joiner while the match is already over (session reopens at MatchEnd) — OnPhaseChanged
            // never fires for them (no local phase flip), so schedule the return-to-hub countdown here.
            if (Phase == MatchPhase.MatchEnd) _matchEndAtLocal = Time.time;

            if (!HasStateAuthority) return;
            SyncTeams();
            Phase = MatchPhase.Warmup;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _warmupDuration);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            if (hasState && runner != null && Object != null && Object.HasStateAuthority && runner.SessionInfo.IsValid)
                runner.SessionInfo.IsOpen = true;
            if (Bill.IsReady)
            {
                Bill.Events.Fire(new MinigameExitedEvent { Id = _minigameId });
                Bill.Events.Unsubscribe<FusionPlayerJoinedEvent>(OnPlayerJoined);
                Bill.Events.Unsubscribe<FusionPlayerLeftEvent>(OnPlayerLeft);
            }
        }

        public override void Render()
        {
            if (_changes == null) return;
            foreach (string change in _changes.DetectChanges(this))
                if (change == nameof(Phase)) OnPhaseChanged();
        }

        // RoundEnd/MatchEnd events used to fire inside EndRound(), which only runs on the master — remote
        // clients never saw THẮNG/THUA announcements. Detecting the replicated Phase flip fires them locally
        // on every client (including the master, which no longer fires them directly).
        private void OnPhaseChanged()
        {
            if (!Bill.IsReady) return;
            switch (Phase)
            {
                case MatchPhase.Warmup:
                case MatchPhase.Playing:
                    _matchEndAtLocal = -1f;   // rematch — cancel any pending return-to-hub
                    _returnTriggered = false;
                    break;
                case MatchPhase.RoundEnd:
                    FireRoundEnd();
                    break;
                case MatchPhase.MatchEnd:
                    FireRoundEnd();
                    _matchEndAtLocal = Time.time;
                    Bill.Events.Fire(new MatchEndEvent
                    {
                        WinnerTeam = ScoreA > ScoreB ? 0 : ScoreB > ScoreA ? 1 : -1,
                        ScoreA = ScoreA,
                        ScoreB = ScoreB
                    });
                    break;
            }
        }

        private void FireRoundEnd()
        {
            Bill.Events.Fire(new RoundEndEvent
            {
                WinnerTeam = LastWinnerTeam,
                Round = Round,
                ScoreA = ScoreA,
                ScoreB = ScoreB
            });
        }

        private void OnPlayerJoined(FusionPlayerJoinedEvent _) => SyncTeams();
        private void OnPlayerLeft(FusionPlayerLeftEvent _) => SyncTeams();

        /// <summary>Seconds until this client auto-returns to the hub (−1 when no countdown is running).
        /// Read by AnnouncerUI for the MatchEnd countdown display.</summary>
        public float ReturnToHubRemaining =>
            _matchEndAtLocal < 0f ? -1f : Mathf.Max(0f, _matchEndReturnDelay - (Time.time - _matchEndAtLocal));

        // FLOW-04 (Session 17.13): MatchEnd used to be a dead end — Phase flipped, the session reopened and
        // players sat in the arena forever (QMNU hold-B was the only exit). Each client counts down locally
        // off the replicated Phase and leaves via the same tested disconnect-recovery path ArenaQuickMenu
        // uses (FusionNet.Shutdown → FusionShutdownEvent → fade to hub + reconnect).
        private void Update()
        {
            if (_returnTriggered || _matchEndAtLocal < 0f) return;
            if (Object == null || !Object.IsValid || Phase != MatchPhase.MatchEnd) return;
            if (Time.time - _matchEndAtLocal < _matchEndReturnDelay) return;
            _returnTriggered = true;
            FusionNet.Instance?.Shutdown();
        }

        /// <summary>Any client's rematch request (QMNU button at MatchEnd). StateAuthority resets the match;
        /// the Phase flip back to Warmup replicates and cancels every client's return-to-hub countdown.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRematch()
        {
            if (Phase != MatchPhase.MatchEnd) return;
            ScoreA = 0;
            ScoreB = 0;
            Round = 0;
            LastWinnerTeam = -1;
            SyncTeams();
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
                    if (WinCheckGrace.ExpiredOrNotRunning(Runner)) CheckWinCondition();
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
            RoundHadBothTeams = HasBothTeams();
            WinCheckGrace = TickTimer.CreateFromSeconds(Runner, _winCheckGraceSeconds);
            NetMaxLives = PlayerCombat.LivesForPlayerCount(CountRealPlayers());
            if (Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = false;

            RPC_ResetRound(NetMaxLives);
            _ringSpawner?.ResetRings();
            ClearLeftoverHazards();
        }

        // ResetForRound()/NotifyRoundStart() only act on state the CALLING client has authority over — in
        // Shared mode the master has no authority over remote avatars, so calling them directly here reset
        // nothing but the master's own combat. Every client resets its own avatar via this RPC instead.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ResetRound(int maxLives)
        {
            PlayerCombat.MaxLives = maxLives;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
                pc.ResetForRound();
            NetworkAvatar.Local?.ResetForRound();
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
            int realPlayerCount = 0;
            int playersA = 0, playersB = 0, aliveA = 0, aliveB = 0;

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null) continue;   // bots (DummyAvatar) never count
                realPlayerCount++;
                if (GetTeam(pc.Object.InputAuthority) == 0)
                {
                    playersA++;
                    if (pc.Health > 0) aliveA++;
                }
                else
                {
                    playersB++;
                    if (pc.Health > 0) aliveB++;
                }
            }

            // A round that STARTED with both teams populated must not run out the full 90s clock when one
            // team leaves mid-round — award it to whoever is still standing.
            if (RoundHadBothTeams && realPlayerCount >= 1 && (playersA == 0 || playersB == 0))
            {
                EndRound(playersA > 0 ? 0 : playersB > 0 ? 1 : -1);
                return;
            }

            // Decide a round by elimination only once at least 2 real players are in the match. Gating on the
            // bot-inclusive AllInstances.Count let a solo player (or a bot-only arena) end the round every tick,
            // spinning Warmup→Playing→RoundEnd forever and constantly resetting combat health.
            if (realPlayerCount < 2 || playersA == 0 || playersB == 0) return;
            if (aliveA > 0 && aliveB > 0) return;

            EndRound(aliveA > 0 ? 0 : aliveB > 0 ? 1 : -1);
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
            LastWinnerTeam = winnerTeam;

            if (winnerTeam == 0) ScoreA++;
            else if (winnerTeam == 1) ScoreB++;

            bool decided = ScoreA >= _winsNeeded || ScoreB >= _winsNeeded;
            bool allRoundsPlayed = Round >= _bestOf;
            if (!decided && !allRoundsPlayed) return;

            Phase = MatchPhase.MatchEnd;
            if (Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = true;
        }

        private void AdvanceRound()
        {
            if (Phase == MatchPhase.MatchEnd) return;
            StartRound();
        }

        [BillButton("Log State")]
        private void Debug_LogState()
            => Debug.Log("[ArenaManager] Phase=" + Phase + " Round=" + Round + " ScoreA=" + ScoreA + " ScoreB=" + ScoreB);

        [BillButton("Force Round Reset (Play, master only)")]
        private void Debug_ForceRoundReset()
        {
            if (!HasStateAuthority) { Debug.Log("[ArenaManager] Không phải StateAuthority — bỏ qua."); return; }
            StartRound();
        }

        [BillButton("Force Win Team A (Play, master only)")]
        private void Debug_ForceWinTeamA()
        {
            if (!HasStateAuthority) { Debug.Log("[ArenaManager] Không phải StateAuthority — bỏ qua."); return; }
            EndRound(0);
        }

        [BillButton("Force Win Team B (Play, master only)")]
        private void Debug_ForceWinTeamB()
        {
            if (!HasStateAuthority) { Debug.Log("[ArenaManager] Không phải StateAuthority — bỏ qua."); return; }
            EndRound(1);
        }

        /// <summary>Team of a player: 0 = A, 1 = B. Single source of truth for team membership — used by
        /// scoring and spawn sides so they can never disagree. Backed by the replicated Teams dictionary
        /// (master assigns by join-order balance — PlayerId%2 could produce 3v0 once leavers left ID gaps);
        /// falls back to PlayerId%2 outside the arena or before assignment replicates.</summary>
        public static int GetTeam(PlayerRef player)
        {
            ArenaManager am = Instance;
            if (am != null && am.Object != null && am.Object.IsValid && am.Teams.TryGet(player, out int team))
                return team;
            return player.PlayerId % 2;
        }

        private void SyncTeams()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority) return;

            int stale = 0;
            foreach (var kv in Teams)
            {
                bool active = false;
                foreach (PlayerRef p in Runner.ActivePlayers)
                    if (p == kv.Key) { active = true; break; }
                if (!active && stale < TeamScratch.Length) TeamScratch[stale++] = kv.Key;
            }
            for (int i = 0; i < stale; i++) Teams.Remove(TeamScratch[i]);

            int countA = 0, countB = 0;
            foreach (var kv in Teams)
            {
                if (kv.Value == 0) countA++;
                else countB++;
            }

            foreach (PlayerRef p in Runner.ActivePlayers)
            {
                if (Teams.ContainsKey(p)) continue;
                int team = countA <= countB ? 0 : 1;
                Teams.Add(p, team);
                if (team == 0) countA++;
                else countB++;
            }
        }

        private bool HasBothTeams()
        {
            int a = 0, b = 0;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null) continue;
                if (GetTeam(pc.Object.InputAuthority) == 0) a++;
                else b++;
            }
            return a > 0 && b > 0;
        }

        /// <summary>World spawn position for a player, by team (see <see cref="GetTeam"/>). Used by respawn.
        /// T16: spreads teammates across the team's spawn points instead of one fixed spot. Falls back to this
        /// object's position when spawn points aren't wired.</summary>
        public Vector3 GetSpawnPosition(PlayerRef player)
        {
            int side = (GetTeam(player) + Mathf.Max(0, Round - 1)) % 2;
            Transform[] pts = side == 0 ? _spawnPointsA : _spawnPointsB;
            if (pts == null || pts.Length == 0) return transform.position;
            int idx = TeammateRank(player) % pts.Length;
            return pts[idx] != null ? pts[idx].position : transform.position;
        }

        // Stable per-team ordering (PlayerId is unique and doesn't change) so teammates never land on the same
        // spawn point — the old random pick could put two teammates on the same one purely by chance.
        private int TeammateRank(PlayerRef player)
        {
            int team = GetTeam(player);
            int rank = 0;
            foreach (var kv in Teams)
                if (kv.Value == team && kv.Key.PlayerId < player.PlayerId) rank++;
            return rank;
        }
    }
}
#endif
