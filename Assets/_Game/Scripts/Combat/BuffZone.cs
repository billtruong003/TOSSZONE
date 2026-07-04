#if PHOTON_FUSION
using System.Collections.Generic;
using BillGameCore;
using Fusion;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    [RequireComponent(typeof(SphereCollider))]
    public class BuffZone : NetworkBehaviour
    {
        [SerializeField] private float _fallbackLifetime = 3f;
        [SerializeField] private int _fireDamagePerPass = 1;
        [SerializeField] private Renderer _visualRenderer;

        [Networked] public int Element { get; set; }
        [Networked] public NetworkId SpawnerProjectileId { get; set; }
        [Networked] public float EffectSeconds { get; set; }
        [Networked] public Vector3 BoxHalfExtents { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private SphereCollider _col;
        private MaterialPropertyBlock _block;
        private readonly HashSet<PlayerRef> _iceFrozenPlayers = new HashSet<PlayerRef>();
        private readonly HashSet<PlayerRef> _fireInside = new HashSet<PlayerRef>();
        private bool _subscribed;

        public void Configure(int element, float radius, NetworkId spawnerProjectileId, float effectSeconds)
        {
            Element = element;
            SpawnerProjectileId = spawnerProjectileId;
            EffectSeconds = effectSeconds;
            if (_col == null) _col = GetComponent<SphereCollider>();
            if (_col != null && radius > 0f) _col.radius = radius;
        }

        public void ConfigureBox(int element, Vector3 halfExtents, NetworkId spawnerProjectileId, float effectSeconds)
        {
            Element = element;
            SpawnerProjectileId = spawnerProjectileId;
            EffectSeconds = effectSeconds;
            BoxHalfExtents = halfExtents;
        }

        public override void Spawned()
        {
            _col = GetComponent<SphereCollider>();
            if (_col != null) _col.isTrigger = true;
            _iceFrozenPlayers.Clear();
            _fireInside.Clear();

            ApplyColor();
            ApplyBoxVisual();
            if (HasStateAuthority)
                LifeTimer = TickTimer.CreateFromSeconds(Runner, EffectSeconds > 0f ? EffectSeconds : _fallbackLifetime);

            if (HasStateAuthority && Bill.IsReady && !_subscribed)
            {
                Bill.Events.Subscribe<RoundEndEvent>(OnRoundEnd);
                _subscribed = true;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_subscribed && Bill.IsReady) Bill.Events.Unsubscribe<RoundEndEvent>(OnRoundEnd);
            _subscribed = false;
        }

        private void OnRoundEnd(RoundEndEvent e)
        {
            if (!HasStateAuthority || Runner == null || Object == null || !Object.IsValid) return;
            Runner.Despawn(Object);
        }

        private void ApplyColor()
        {
            if (_visualRenderer == null) return;
            _block ??= new MaterialPropertyBlock();
            Color c = Element == (int)RingElement.Fire ? new Color(1f, 0.35f, 0.05f) : new Color(0.3f, 0.85f, 1f);
            _block.SetColor(_colorId, c);
            _visualRenderer.SetPropertyBlock(_block);
        }

        private void ApplyBoxVisual()
        {
            if (_visualRenderer == null || BoxHalfExtents == default) return;
            _visualRenderer.transform.localScale = new Vector3(BoxHalfExtents.x * 2f, 0.4f, BoxHalfExtents.z * 2f);
        }

        private bool Contains(Vector3 point, float radiusSq)
        {
            if (BoxHalfExtents == default)
                return (point - transform.position).sqrMagnitude <= radiusSq;
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= BoxHalfExtents.x
                && Mathf.Abs(local.y) <= BoxHalfExtents.y
                && Mathf.Abs(local.z) <= BoxHalfExtents.z;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (LifeTimer.Expired(Runner)) { Runner.Despawn(Object); return; }

            float radiusSq = _col != null ? _col.radius * _col.radius : 0f;
            if (Element == (int)RingElement.Fire) TickFireZone(radiusSq);
            else if (Element == (int)RingElement.Ice) TickIceZone(radiusSq);
        }

        private void TickFireZone(float radiusSq)
        {
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null || pc.Health <= 0) continue;
                PlayerRef pr = pc.Object.InputAuthority;
                bool inside = Contains(pc.transform.position, radiusSq);

                if (!inside)
                {
                    _fireInside.Remove(pr);
                    continue;
                }
                if (_fireInside.Contains(pr)) continue;
                _fireInside.Add(pr);
                pc.RPC_TakeHit(_fireDamagePerPass, pc.transform.position, PlayerRef.None);
            }
        }

        private void TickIceZone(float radiusSq)
        {
            NetworkProjectile[] projs = FindObjectsByType<NetworkProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projs.Length; i++)
            {
                NetworkProjectile p = projs[i];
                if (p == null || p.Object == null || !p.Object.IsValid) continue;
                if (p.Object.Id == SpawnerProjectileId) continue;
                if (!Contains(p.transform.position, radiusSq)) continue;
                Runner.Despawn(Object);
                return;
            }

            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null || pc.Health <= 0) continue;
                if (!Contains(pc.transform.position, radiusSq)) continue;

                PlayerRef pr = pc.Object.InputAuthority;
                if (_iceFrozenPlayers.Contains(pr)) continue;
                _iceFrozenPlayers.Add(pr);
                pc.RPC_Freeze(EffectSeconds > 0f ? EffectSeconds : 1f);
            }
        }
    }
}
#endif
