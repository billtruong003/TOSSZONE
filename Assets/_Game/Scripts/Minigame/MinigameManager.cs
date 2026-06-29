using BillGameCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TossZone.Minigame
{
    /// <summary>
    /// Orchestrates the hub → minigame flow. The game is a Gorilla-Tag-style hub (<c>01_TOSSZONE_Main</c>) with
    /// many minigames; the Arena throw mode (<c>02_Arena</c>) is the first. Holds the minigame catalog, loads a
    /// minigame's scene on <see cref="Enter(MinigameDef)"/>, returns to the hub on <see cref="ReturnToHub"/>, and
    /// fires <see cref="MinigameEnteredEvent"/>/<see cref="MinigameExitedEvent"/> so gameplay/UI react decoupled.
    /// Per-minigame gameplay (e.g. the thrower) lives IN that minigame's scene, so it activates only there
    /// (matches <c>Docs/M4_Gameplay_Design.md</c>: "entering the arena gives the local player a thrower").
    ///
    /// Networking: this drives the LOCAL scene flow via <c>Bill.Scene</c>. For Fusion shared mode the scene
    /// change must go through the runner so all players move together — either listen to
    /// <see cref="MinigameEnteredEvent"/> from the Fusion layer and load there, or subclass and override
    /// <see cref="LoadScene"/>. One per session (DontDestroyOnLoad); place it in the hub or bootstrap scene.
    /// </summary>
    public class MinigameManager : MonoBehaviour
    {
        public static MinigameManager Instance { get; private set; }

        [Tooltip("All minigames. If empty, every MinigameDef in a Resources/Minigames folder is loaded.")]
        [SerializeField] private MinigameDef[] _catalog;
        [SerializeField] private string _hubScene = "01_TOSSZONE_Main";

        public MinigameDef Current { get; private set; }
        public MinigameDef[] Catalog => _catalog;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (_catalog == null || _catalog.Length == 0)
                _catalog = Resources.LoadAll<MinigameDef>("Minigames");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Look up a minigame by its <see cref="MinigameDef.id"/> (null if not in the catalog).</summary>
        public MinigameDef Find(string id)
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
                if (_catalog[i] != null && _catalog[i].id == id) return _catalog[i];
            return null;
        }

        public void Enter(string id) => Enter(Find(id));

        public void Enter(MinigameDef def)
        {
            if (def == null) { Debug.LogWarning("[Minigame] Enter: minigame not found / null def."); return; }
            Current = def;
            LoadScene(def.sceneName);
            if (Bill.IsReady) Bill.Events.Fire(new MinigameEnteredEvent { Id = def.id });
            Debug.Log("[Minigame] Enter '" + def.id + "' -> scene " + def.sceneName);
        }

        public void ReturnToHub()
        {
            string prev = Current != null ? Current.id : "";
            Current = null;
            LoadScene(_hubScene);
            if (Bill.IsReady) Bill.Events.Fire(new MinigameExitedEvent { Id = prev });
            Debug.Log("[Minigame] Return to hub (" + _hubScene + ").");
        }

        /// <summary>Local scene load. Override for the Fusion-runner networked load (all players together).</summary>
        protected virtual void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (Bill.IsReady) Bill.Scene.Load(sceneName, TransitionType.Fade, 0.5f);
            else SceneManager.LoadScene(sceneName);
        }
    }
}
