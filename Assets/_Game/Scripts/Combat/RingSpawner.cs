#if PHOTON_FUSION
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

        private void SpawnRingAt(int i)
        {
            if (_ringPrefab == null || i >= SlotCount) return;

            Vector3 c = ZoneCenter, h = ZoneHalfExtents;
            Vector3 pos = new Vector3(
                c.x + Random.Range(-h.x, h.x),
                c.y + Random.Range(-h.y, h.y),
                c.z + Random.Range(-h.z, h.z));

            // Pick a random element and set it in onBeforeSpawned so it is written BEFORE BuffRing.Spawned()
            // runs — otherwise Spawned() resolves its config with Element still = None (0 → null slot),
            // leaving the ring colorless. (Setting Element after Runner.Spawn() is too late.)
            int pick = Random.Range(1, System.Enum.GetValues(typeof(RingElement)).Length);
            NetworkObject obj = Runner.Spawn(_ringPrefab,
                pos, Quaternion.identity, PlayerRef.None,
                (runner, o) =>
                {
                    if (o.TryGetComponent(out BuffRing ring)) ring.Element = (RingElement)pick;
                });
            if (obj == null) return;
            SlotRings.Set(i, obj.Id);
            RespawnTimers.Set(i, default);
        }

        private BuffRingConfig PickConfig()
        {
            if (_catalog == null || _catalog.Length == 0) return null;
            return _catalog[Random.Range(0, _catalog.Length)];
        }
    }
}
#endif
