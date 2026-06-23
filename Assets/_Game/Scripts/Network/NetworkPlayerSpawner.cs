#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Lives in 02_Arena. Once the Fusion runner is up in the arena, spawns the local player's
    /// NetworkPlayer avatar at a team spawn point. Shared Mode: each client spawns its own avatar
    /// and keeps state authority over it. Team = LocalPlayerId parity (even → A, odd → B).
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Transform _spawnTeamA;
        [SerializeField] private Transform _spawnTeamB;

        private bool _spawned;

        private void OnEnable()
        {
            Bill.Events.Subscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
            TrySpawn(); // arena may already be running when this object enables
        }

        private void OnDisable()
        {
            Bill.Events.Unsubscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
        }

        private void OnSceneLoaded(FusionSceneLoadDoneEvent _)
        {
            TrySpawn();
        }

        private void TrySpawn()
        {
            if (_spawned || _playerPrefab == null) return;

            FusionNet net = FusionNet.Instance;
            if (net == null || !net.IsRunning) return;

            bool teamA = (net.LocalPlayerId & 1) == 0;
            Transform spawn = teamA ? _spawnTeamA : _spawnTeamB;
            Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

            net.Spawn(_playerPrefab, pos, rot, net.LocalPlayer);
            _spawned = true;
            Debug.Log("[Spawner] Spawned local player on team " + (teamA ? "A" : "B"));
        }
    }
}
#endif
