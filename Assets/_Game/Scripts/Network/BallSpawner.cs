#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Spawns the shared, networked throwable balls ONCE. In Shared Mode only the master client actually
    /// spawns, so every peer sees a single set (not one set per player); late joiners receive the existing
    /// balls via Fusion replication. Mirrors <see cref="PlayerSpawnManager"/>'s ready/connect guarding.
    /// Place at the spawn area; wire the networked ball prefab (NetworkObject + NetworkTransform +
    /// NetworkGrabbable). Balls are laid out in a row centred on this object, offset by <see cref="_origin"/>.
    /// </summary>
    public class BallSpawner : MonoBehaviour
    {
        [Tooltip("Networked ball prefab (NetworkObject + NetworkTransform + Grabbable + NetworkGrabbable).")]
        [SerializeField] private NetworkObject _ballPrefab;
        [SerializeField] private int _count = 3;
        [SerializeField] private float _spacing = 0.35f;
        [Tooltip("Local-space offset of the row centre from this transform.")]
        [SerializeField] private Vector3 _origin = new Vector3(0f, 1.15f, 1.2f);

        private bool _spawned;
        private bool _initialized;

        private void OnEnable() => TryInit();

        // Bootstrap may not be finished when this scene's objects enable, so poll until Bill is ready.
        private void Update()
        {
            if (!_initialized) TryInit();
        }

        private void OnDisable()
        {
            if (!_initialized || !Bill.IsReady) return;
            Bill.Events.Unsubscribe<FusionConnectedEvent>(OnConnected);
            Bill.Events.Unsubscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
        }

        private void TryInit()
        {
            if (_initialized || !Bill.IsReady) return;
            _initialized = true;
            Bill.Events.Subscribe<FusionConnectedEvent>(OnConnected);
            Bill.Events.Subscribe<FusionSceneLoadDoneEvent>(OnSceneLoaded);
            TrySpawn();
        }

        private void OnConnected(FusionConnectedEvent _) => TrySpawn();
        private void OnSceneLoaded(FusionSceneLoadDoneEvent _) => TrySpawn();

        private void TrySpawn()
        {
            if (_spawned || _ballPrefab == null) return;

            FusionNet net = FusionNet.Instance;
            if (net == null || !net.IsRunning) return;
            if (!net.IsSharedModeMasterClient) return; // only one peer spawns the shared set

            for (int i = 0; i < _count; i++)
            {
                float x = (i - (_count - 1) * 0.5f) * _spacing;
                Vector3 pos = transform.TransformPoint(_origin + new Vector3(x, 0f, 0f));
                net.Spawn(_ballPrefab, pos, Quaternion.identity);
            }

            _spawned = true;
            Debug.Log("[BallSpawner] Spawned " + _count + " networked balls (shared master).");
        }
    }
}
#endif
