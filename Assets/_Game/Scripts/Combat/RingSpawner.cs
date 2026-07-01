#if PHOTON_FUSION
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Spawns one shared <see cref="BuffRing"/> prefab per slot, randomizes its <see cref="RingElement"/> after
    /// spawn, then respawns after <see cref="BuffRingConfig.respawnDelay"/> when a slot goes empty.
    /// </summary>
    public class RingSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject _ringPrefab;
        [SerializeField] private BuffRingConfig[] _catalog = new BuffRingConfig[5];
        [SerializeField] private Transform[] _spawnPoints;

        [Networked, Capacity(8)] private NetworkArray<NetworkId> SlotRings    => default;
        [Networked, Capacity(8)] private NetworkArray<TickTimer> RespawnTimers => default;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            for (int i = 0; i < _spawnPoints.Length; i++) SpawnRingAt(i);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _spawnPoints == null) return;

            for (int i = 0; i < _spawnPoints.Length && i < SlotRings.Length; i++)
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
            for (int i = 0; i < SlotRings.Length && i < _spawnPoints.Length; i++)
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
            for (int i = 0; i < _spawnPoints.Length; i++) SpawnRingAt(i);
        }

        private void SpawnRingAt(int i)
        {
            if (_ringPrefab == null || _spawnPoints == null || i >= _spawnPoints.Length) return;

            // Pick a random element and set it in onBeforeSpawned so it is written BEFORE BuffRing.Spawned()
            // runs — otherwise Spawned() resolves its config with Element still = None (0 → null slot),
            // leaving the ring colorless. (Setting Element after Runner.Spawn() is too late.)
            int pick = Random.Range(1, System.Enum.GetValues(typeof(RingElement)).Length);
            NetworkObject obj = Runner.Spawn(_ringPrefab,
                _spawnPoints[i].position, _spawnPoints[i].rotation, PlayerRef.None,
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
