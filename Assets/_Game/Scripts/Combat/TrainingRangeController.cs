#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Minigame;
using TossZone.UI;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// T25 — hub training range ("Khu Khởi Động / Warm-up Target", GDD §VII). Scene object in
    /// <c>01_TOSSZONE_Main</c>: rows of <see cref="PokeButton3D"/> cubes that (a) equip any weapon FREE and
    /// (b) spawn specific buff rings on demand, plus dummy targets — so the owner can test every weapon and
    /// ring without entering a match or earning money.
    ///
    /// On boot it fires <see cref="MinigameEnteredEvent"/> with the arena's catalog id (CombatSession only
    /// resolves the catalog inside a minigame otherwise) and raises <see cref="CombatSession.TrainingMode"/>,
    /// which bypasses unlock-time and purchase gates in HandWeapon / WristWeaponSelector. Both end when this
    /// object dies with the hub scene — entering the arena re-fires the event with normal rules.
    /// Ring/dummy spawning is master-only (solo hub testing is the main use).
    /// </summary>
    public class TrainingRangeController : MonoBehaviour
    {
        [Header("Catalog")]
        [Tooltip("MinigameDef id whose weapon catalog the range uses (Resources/Minigames/<id>).")]
        [SerializeField] private string _minigameId = "arena";

        [Header("Weapon buttons (index = catalog index)")]
        [SerializeField] private PokeButton3D[] _weaponButtons;

        [Header("Ring buttons (order = RingElement 1..5: Ice, Fire, Multi, Speed, Area)")]
        [SerializeField] private PokeButton3D[] _ringButtons;
        [Tooltip("Stress button: spawns _burstCount random rings at once (capacity test).")]
        [SerializeField] private PokeButton3D _ringBurstButton;
        [Tooltip("RingSpawner PREFAB, runtime-spawned at _ringSpawnerAnchor — hub scene NetworkObjects stay " +
                 "dormant (no Fusion scene load here), so a scene-placed spawner never attaches.")]
        [SerializeField] private NetworkObject _ringSpawnerPrefab;
        [SerializeField] private Transform _ringSpawnerAnchor;
        [BillInspector.BillSlider(1, 5)]
        [SerializeField] private int _ringTier = 3;
        [SerializeField] private int _burstCount = 8;

        [Header("Targets")]
        [SerializeField] private NetworkObject _dummyPrefab;
        [SerializeField] private Transform[] _dummySpawns;

        private bool _entered;
        private bool _spawnsDone;
        private bool _spawnerSpawned;
        private int _nextDummy;

        private void OnDestroy()
        {
            if (CombatSession.Instance != null) CombatSession.Instance.TrainingMode = false;
            UnsubscribeButtons();
        }

        private void Update()
        {
            if (!Bill.IsReady) return;
            if (!_entered) Enter();
            if (!_spawnsDone) TrySpawnNetworkPieces();
        }

        private void Enter()
        {
            _entered = true;
            // The hub has no CombatSession scene object (it lives in 02_Arena) — without one, nobody resolves
            // the catalog from the event below. Create the DDOL singleton here on first use.
            if (CombatSession.Instance == null) new GameObject("CombatSession(hub)").AddComponent<CombatSession>();
            Bill.Events.Fire(new MinigameEnteredEvent { Id = _minigameId });
            CombatSession.Instance.TrainingMode = true;
            SubscribeButtons();
            Debug.Log("[TrainingRange] active — catalog '" + _minigameId + "', free equip + ring buttons live.");
        }

        private void SubscribeButtons()
        {
            if (_weaponButtons != null)
                for (int i = 0; i < _weaponButtons.Length; i++)
                {
                    if (_weaponButtons[i] == null) continue;
                    int index = i;   // capture per button
                    _weaponButtons[i].Poked += _ => EquipFree(index);
                }
            if (_ringButtons != null)
                for (int i = 0; i < _ringButtons.Length; i++)
                {
                    if (_ringButtons[i] == null) continue;
                    RingElement element = (RingElement)(i + 1);
                    _ringButtons[i].Poked += _ => SpawnRing(element);
                }
            if (_ringBurstButton != null) _ringBurstButton.Poked += OnBurstPoked;
        }

        private void UnsubscribeButtons()
        {
            // Buttons die with the same scene — lambdas need no bookkeeping; only the named handler does.
            if (_ringBurstButton != null) _ringBurstButton.Poked -= OnBurstPoked;
        }

        private void EquipFree(int catalogIndex)
        {
            PlayerCombat combat = PlayerCombat.Local;
            if (combat == null) return;
            combat.EquipWeapon(catalogIndex);   // EquipWeapon has no cost gate; TrainingMode covers the rest
        }

        private void SpawnRing(RingElement element)
        {
            if (RingSpawner.Instance == null) return;
            RingSpawner.Instance.SpawnSpecific(element, _ringTier);
        }

        private void OnBurstPoked(Autohand.Hand hand)
        {
            if (RingSpawner.Instance == null) return;
            for (int i = 0; i < _burstCount; i++)
                RingSpawner.Instance.SpawnSpecific((RingElement)Random.Range(1, 6), Random.Range(1, 6));
        }

        /// <summary>Master-only, one piece per frame with a hard "runner really ready" gate: Runner.IsRunning
        /// flips true a few frames BEFORE the simulation can allocate ids — Runner.Spawn in that window throws
        /// from Simulation.GetNextId and leaves a half-instantiated corpse. The local avatar spawning (same
        /// Runner.Spawn path, PlayerSpawnManager) is the proof the window has passed, so gate on it.</summary>
        private void TrySpawnNetworkPieces()
        {
            NetworkRunner runner = FusionNet.Instance != null ? FusionNet.Instance.Runner : null;
            if (runner == null || !runner.IsRunning || PlayerCombat.Local == null) return;
            if (!runner.IsSharedModeMasterClient) { _spawnsDone = true; return; }   // master's spawns replicate over

            if (!_spawnerSpawned)
            {
                _spawnerSpawned = true;
                if (_ringSpawnerPrefab != null)
                {
                    Vector3 pos = _ringSpawnerAnchor != null ? _ringSpawnerAnchor.position : transform.position + Vector3.up * 2.3f;
                    runner.Spawn(_ringSpawnerPrefab, pos, Quaternion.identity, PlayerRef.None);
                }
                return;   // one spawn per frame keeps each attempt cheap to retry
            }

            if (_dummyPrefab == null || _dummySpawns == null || _nextDummy >= _dummySpawns.Length)
            {
                _spawnsDone = true;
                Debug.Log("[TrainingRange] network pieces spawned (ringSpawner + " + _nextDummy + " dummies).");
                return;
            }
            Transform spawn = _dummySpawns[_nextDummy];
            _nextDummy++;
            if (spawn != null)
            {
                NetworkObject o = runner.Spawn(_dummyPrefab, spawn.position, spawn.rotation, PlayerRef.None);
                // Warm-up TARGETS, not attackers — live test: active bots killed the browsing player in
                // seconds, and every death resets EquippedIndex. Re-enable per-dummy via DevCombatPanel (F1).
                DummyBotDriver bot = o != null ? o.GetComponent<DummyBotDriver>() : null;
                if (bot != null) bot.enabled = false;
            }
        }
    }
}
#endif
