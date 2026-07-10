#if PHOTON_FUSION
using BillInspector;
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Spawns one shared <see cref="BuffRing"/> prefab per slot at a random position inside a shared "vùng giữa"
    /// box (replaces the old 3 fixed spawn points — T9), randomizes its <see cref="RingElement"/> after spawn,
    /// then respawns after <see cref="BuffRingConfig.respawnDelay"/> when a slot goes empty. Every client resolves
    /// <see cref="ZoneCenter"/>/<see cref="ZoneHalfExtents"/> from this same scene object's serialized fields (no
    /// networking needed — see <see cref="BuffRing"/>'s deterministic wander, which reads them via
    /// <see cref="Instance"/>).
    /// </summary>
    public class RingSpawner : NetworkBehaviour
    {
        public static RingSpawner Instance { get; private set; }

        [SerializeField] private NetworkObject _ringPrefab;
        [SerializeField] private BuffRingConfig[] _catalog = new BuffRingConfig[5];

        [Header("Vùng giữa (T9) — box quanh _zoneCenter (hoặc quanh RingSpawner nếu để trống). Map thật (T16) chỉ cần chỉnh 2 field này.")]
        [SerializeField] private Transform _zoneCenter;
        [SerializeField] private Vector3 _zoneSize = new Vector3(8f, 1f, 4f);
        [SerializeField] private int _slotCount = 3;
        [Tooltip("Mép DƯỚI của ring không bao giờ thấp hơn cao độ này (world Y) — tier to (đường kính 1.8m) từng lết sát đất vì zone box không biết gì về bán kính ring (PT-01; owner feedback 2026-07-07: vẫn thấy thấp, nâng 1→1.6).")]
        [SerializeField] private float _minRingBottomY = 1.6f;

        private static readonly RingElement[] AllowedElements = { RingElement.Multi, RingElement.Speed, RingElement.Area };

        // REFF (Session 17.13): band B was {38,26,20,10,5} = 99 (typo) → bumped T2 to 27 for a clean 100.
        // Added a 90s+ band so long rounds keep ramping pressure instead of freezing at the 60-90s curve.
        private static readonly float[] TierWeights0to30 = { 65f, 25f, 8f, 2f, 0f };
        private static readonly float[] TierWeights31to60 = { 38f, 27f, 20f, 10f, 5f };
        private static readonly float[] TierWeights61to90 = { 20f, 25f, 25f, 20f, 10f };
        private static readonly float[] TierWeights90Plus = { 12f, 20f, 28f, 25f, 15f };

        [Networked, Capacity(8)] private NetworkArray<NetworkId> SlotRings    => default;
        [Networked, Capacity(8)] private NetworkArray<TickTimer> RespawnTimers => default;

        public Vector3 ZoneCenter => _zoneCenter != null ? _zoneCenter.position : transform.position;
        public Vector3 ZoneHalfExtents => _zoneSize * 0.5f;

        private int SlotCount => Mathf.Clamp(_slotCount, 0, SlotRings.Length);

        public override void Spawned()
        {
            Instance = this;   // every client (not just authority) needs zone bounds for BuffRing's wander
            if (!HasStateAuthority) return;
            for (int i = 0; i < SlotCount; i++) SpawnRingAt(i);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            for (int i = 0; i < SlotCount; i++)
            {
                NetworkId id = SlotRings.Get(i);
                bool hasRing = id != default(NetworkId) && Runner.FindObject(id) != null;
                if (hasRing) continue;

                TickTimer timer = RespawnTimers.Get(i);
                if (timer.Expired(Runner))
                {
                    RespawnTimers.Set(i, default);
                    SpawnRingAt(i);
                }
                else if (timer.ExpiredOrNotRunning(Runner))
                {
                    float delay = PickConfig()?.respawnDelay ?? 10f;
                    RespawnTimers.Set(i, TickTimer.CreateFromSeconds(Runner, delay));
                }
            }
        }

        /// <summary>T25 — spawn one SPECIFIC ring on demand (training range), outside the slot system so it
        /// never auto-respawns. Master only; returns null otherwise. Position rolls inside the zone box.</summary>
        public NetworkObject SpawnSpecific(RingElement element, int tier)
        {
            if (_ringPrefab == null || Object == null || !Object.IsValid || !HasStateAuthority) return null;
            int clamped = Mathf.Clamp(tier, 1, 5);
            Vector3 pos = RollSpawnPos(clamped);
            return Runner.Spawn(_ringPrefab, pos, Quaternion.identity, PlayerRef.None,
                (runner, o) =>
                {
                    if (o.TryGetComponent(out BuffRing ring)) { ring.Element = element; ring.Tier = clamped; }
                });
        }

        private Vector3 RollSpawnPos(int tier)
        {
            Vector3 c = ZoneCenter, h = ZoneHalfExtents;
            Vector3 pos = new Vector3(
                c.x + Random.Range(-h.x, h.x),
                c.y + Random.Range(-h.y, h.y),
                c.z + Random.Range(-h.z, h.z));
            float radius = BuffRingConfig.DiameterForTier(tier) * 0.5f;
            pos.y = Mathf.Max(pos.y, _minRingBottomY + radius);
            return pos;
        }

        public void ResetRings()
        {
            if (!HasStateAuthority) return;
            int slots = SlotCount;
            for (int i = 0; i < slots; i++)
            {
                NetworkId id = SlotRings.Get(i);
                if (id != default(NetworkId))
                {
                    NetworkObject obj = Runner.FindObject(id);
                    if (obj != null) Runner.Despawn(obj);
                }
                SlotRings.Set(i, default(NetworkId));
                RespawnTimers.Set(i, default);
            }
            for (int i = 0; i < slots; i++) SpawnRingAt(i);
        }

        [BillButton("Reset Rings (Play, master only)")]
        private void Debug_ResetRings()
        {
            if (!HasStateAuthority) { Debug.Log("[RingSpawner] Không phải StateAuthority — bỏ qua."); return; }
            ResetRings();
        }

        [BillButton("Spawn Multi Tier 3 (Play, master only)")]
        private void Debug_SpawnMultiTier3()
        {
            if (!HasStateAuthority) { Debug.Log("[RingSpawner] Không phải StateAuthority — bỏ qua."); return; }
            SpawnSpecific(RingElement.Multi, 3);
        }

        private void SpawnRingAt(int i)
        {
            if (_ringPrefab == null || i >= SlotCount) return;

            // Pick a random element (from AllowedElements — Ice/Fire tạm khoá) + a rarity-weighted tier (T11),
            // and set both in onBeforeSpawned so they are written BEFORE BuffRing.Spawned() runs — otherwise
            // Spawned() resolves its config with Element still = None (0 → null slot), leaving the ring
            // colorless. (Setting these after Runner.Spawn() is too late.)
            var element = AllowedElements[Random.Range(0, AllowedElements.Length)];
            int tier = RollTier();   // T4/T5 cap handled inside RollTier by renormalizing (REFF, Session 17.13)
            Vector3 pos = RollSpawnPos(tier);

            NetworkObject obj = Runner.Spawn(_ringPrefab,
                pos, Quaternion.identity, PlayerRef.None,
                (runner, o) =>
                {
                    if (o.TryGetComponent(out BuffRing ring)) { ring.Element = element; ring.Tier = tier; }
                });
            if (obj == null) return;
            SlotRings.Set(i, obj.Id);
            RespawnTimers.Set(i, default);
        }

        private int RollTier()
        {
            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;
            float[] weights = elapsed < 30f ? TierWeights0to30
                            : elapsed < 60f ? TierWeights31to60
                            : elapsed < 90f ? TierWeights61to90
                            : TierWeights90Plus;
            if (weights == null || weights.Length == 0) return 1;

            // Tiers 4-5 are capped at one active instance each. The old downgrade (roll T4/T5 while one is
            // active → uniform Random.Range(1,4)) dumped the whole high-tier probability mass into a flat
            // 33/33/33 — inflating T3 far above its designed weight. Renormalizing over the tiers that are
            // actually available keeps the curve's ratios intact.
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                int t = i + 1;
                if (t >= 4 && HasActiveTier(t)) continue;
                total += Mathf.Max(0f, weights[i]);
            }
            if (total <= 0f) return 1;

            float roll = Random.Range(0f, total);
            float accum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                int t = i + 1;                     // index 0 = Tier1 .. index 4 = Tier5
                if (t >= 4 && HasActiveTier(t)) continue;
                accum += Mathf.Max(0f, weights[i]);
                if (roll <= accum) return t;
            }
            return 1;
        }

        private bool HasActiveTier(int tier)
        {
            int slots = SlotCount;
            for (int i = 0; i < slots; i++)
            {
                NetworkId id = SlotRings.Get(i);
                if (id == default(NetworkId)) continue;
                NetworkObject obj = Runner.FindObject(id);
                if (obj == null || !obj.TryGetComponent(out BuffRing ring)) continue;
                if (ring.Tier == tier) return true;
            }
            return false;
        }

        private BuffRingConfig PickConfig()
        {
            if (_catalog == null || _catalog.Length == 0) return null;
            return _catalog[Random.Range(0, _catalog.Length)];
        }
    }
}
#endif
