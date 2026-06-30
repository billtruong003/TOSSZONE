#if PHOTON_FUSION
using Fusion;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Authority-managed spawner for <see cref="BuffRing"/>s. Maintains one ring per spawn-point slot.
    /// When a ring is consumed (despawned by <see cref="BuffRing"/>), the slot is detected as empty in
    /// <see cref="FixedUpdateNetwork"/> and a <see cref="TickTimer"/> starts; on expiry a new ring
    /// (random type from <see cref="_ringPrefabs"/>) spawns in that slot.
    /// </summary>
    public class RingSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject[] _ringPrefabs;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private float _defaultRespawnDelay = 10f;

        [Networked, Capacity(8)] private NetworkArray<NetworkId> SlotRings => default;
        [Networked, Capacity(8)] private NetworkArray<TickTimer> RespawnTimers => default;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            for (int i = 0; i < _spawnPoints.Length; i++)
                SpawnRingAt(i);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _spawnPoints == null) return;

            for (int i = 0; i < _spawnPoints.Length && i < SlotRings.Length; i++)
            {
                NetworkId slotId = SlotRings.Get(i);
                bool hasRing = slotId != default(NetworkId)
                               && Runner.FindObject(slotId) != null;

                if (hasRing) continue;

                // Ring is gone: start timer if not started; spawn if timer elapsed.
                TickTimer timer = RespawnTimers.Get(i);
                if (timer.Expired(Runner))
                {
                    // Elapsed → spawn a new ring.
                    RespawnTimers.Set(i, default);
                    SpawnRingAt(i);
                }
                else if (timer.ExpiredOrNotRunning(Runner))
                {
                    // Not started yet → begin countdown.
                    RespawnTimers.Set(i, TickTimer.CreateFromSeconds(Runner, _defaultRespawnDelay));
                }
            }
        }

        private void SpawnRingAt(int slotIndex)
        {
            if (_ringPrefabs == null || _ringPrefabs.Length == 0) return;
            if (_spawnPoints == null || slotIndex >= _spawnPoints.Length) return;

            NetworkObject prefab = _ringPrefabs[Random.Range(0, _ringPrefabs.Length)];
            if (prefab == null) return;

            NetworkObject ring = Runner.Spawn(prefab,
                _spawnPoints[slotIndex].position,
                _spawnPoints[slotIndex].rotation,
                PlayerRef.None);

            if (ring != null)
            {
                SlotRings.Set(slotIndex, ring.Id);
                // Reset respawn timer now that slot is filled.
                RespawnTimers.Set(slotIndex, default);
            }
        }

        /// <summary>Called by ArenaManager at round start to clear and respawn all rings.</summary>
        public void ResetRings()
        {
            if (!HasStateAuthority) return;
            // Despawn existing rings.
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
            // Spawn fresh set.
            for (int i = 0; i < _spawnPoints.Length; i++)
                SpawnRingAt(i);
        }
    }
}
#endif
