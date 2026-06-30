#if PHOTON_FUSION
using BillGameCore;
using TossZone.Minigame;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Singleton that survives scene loads and acts as the single source of truth for the active combat round.
    /// Subscribes to <see cref="MinigameEnteredEvent"/> / <see cref="MinigameExitedEvent"/> to gate combat on/off
    /// and expose the current minigame's weapon catalog to <see cref="HandWeapon"/> and <see cref="WristWeaponSelector"/>.
    ///
    /// Separates round timing from <see cref="ArenaManager"/> (authority-only) so ALL clients can read
    /// <see cref="RoundElapsed"/> for unlock-time gates without a network hop.
    /// </summary>
    public class CombatSession : MonoBehaviour
    {
        public static CombatSession Instance { get; private set; }

        /// <summary>True while a minigame is active (between MinigameEntered and MinigameExited).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Seconds elapsed since the current round started (local timer — authority of exact value is TimeRemaining in ArenaManager).</summary>
        public float RoundElapsed { get; private set; }

        /// <summary>The weapon catalog for the currently active minigame (null outside a minigame).</summary>
        public WeaponConfig[] CurrentCatalog { get; private set; }

        private bool _roundRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (!Bill.IsReady) { Bill.Events.Subscribe<GameReadyEvent>(OnReady); return; }
            Subscribe();
        }

        private void OnReady(GameReadyEvent _)
        {
            Bill.Events.Unsubscribe<GameReadyEvent>(OnReady);
            Subscribe();
        }

        private void OnDisable()
        {
            if (!Bill.IsReady) return;
            Bill.Events.Unsubscribe<MinigameEnteredEvent>(OnMinigameEntered);
            Bill.Events.Unsubscribe<MinigameExitedEvent>(OnMinigameExited);
            Bill.Events.Unsubscribe<RoundEndEvent>(OnRoundEnd);
        }

        private void Subscribe()
        {
            Bill.Events.Subscribe<MinigameEnteredEvent>(OnMinigameEntered);
            Bill.Events.Subscribe<MinigameExitedEvent>(OnMinigameExited);
            Bill.Events.Subscribe<RoundEndEvent>(OnRoundEnd);
        }

        private void Update()
        {
            if (_roundRunning) RoundElapsed += Time.deltaTime;
        }

        private void OnMinigameEntered(MinigameEnteredEvent e)
        {
            IsActive = true;
            CurrentCatalog = ResolveMinigameCatalog(e.Id);
        }

        private void OnMinigameExited(MinigameExitedEvent _)
        {
            IsActive = false;
            CurrentCatalog = null;
            _roundRunning = false;
            RoundElapsed = 0f;
        }

        private void OnRoundEnd(RoundEndEvent _) { _roundRunning = false; }

        /// <summary>Called by ArenaManager (authority) when a new round starts — resets local timer.</summary>
        public void NotifyRoundStart()
        {
            RoundElapsed = 0f;
            _roundRunning = true;
        }

        private static WeaponConfig[] ResolveMinigameCatalog(string id)
        {
            // Load catalog from Resources/Minigames/<id>
            MinigameDef def = Resources.Load<MinigameDef>($"Minigames/{id}");
            return def != null ? def.weaponCatalog : null;
        }
    }
}
#endif
