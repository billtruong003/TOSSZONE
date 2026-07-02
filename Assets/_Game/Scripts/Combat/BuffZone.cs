#if PHOTON_FUSION
using System.Collections.Generic;
using BillGameCore;
using Fusion;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Persistent networked area hazard spawned by <see cref="NetworkProjectile"/> when an Ice/Fire element shot
    /// lands (Combat_Minigame_Design.md §10). One shared prefab, behavior branches on <see cref="Element"/>
    /// (mirrors <see cref="BuffRing"/>'s "one prefab, Element decides" pattern):
    /// <list type="bullet">
    ///   <item>Ice — a wall/obstacle blob. First player to touch it takes <see cref="_iceDamage"/> once (the
    ///         design doc's "mất lượt" doesn't map to anything in this real-time hit-point game — no turn-order
    ///         concept exists anywhere in the codebase — so a real hit substitutes for "losing a turn"). A
    ///         projectile OTHER than the one that spawned it hitting the wall melts it early ("dính dmg thì tan").</item>
    ///   <item>Fire — a damage-over-time area. Any player standing inside takes <see cref="_fireDamagePerTick"/>
    ///         on a per-player cooldown for as long as they linger.</item>
    /// </list>
    /// Both persist until <see cref="RoundEndEvent"/> fires or <see cref="_maxLifetime"/> elapses as a failsafe
    /// (covers the case a round never cleanly ends, e.g. solo testing outside the real match flow).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class BuffZone : NetworkBehaviour
    {
        [SerializeField] private float _maxLifetime = 90f;
        [SerializeField] private float _fireTickInterval = 1f;
        [SerializeField] private int _fireDamagePerTick = 1;
        [SerializeField] private int _iceDamage = 1;
        [SerializeField] private Renderer _visualRenderer;

        [Networked] public int Element { get; set; }                  // matches NetworkProjectile.Element (1 Ice, 2 Fire)
        [Networked] public NetworkId SpawnerProjectileId { get; set; } // the exploding projectile that spawned this — never melts its own wall
        [Networked] private TickTimer LifeTimer { get; set; }

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private SphereCollider _col;
        private MaterialPropertyBlock _block;
        private readonly HashSet<PlayerRef> _iceHitPlayers = new HashSet<PlayerRef>();
        private readonly Dictionary<PlayerRef, float> _fireNextTick = new Dictionary<PlayerRef, float>();
        private bool _subscribed;

        /// <summary>Called by the spawner (NetworkProjectile) via onBeforeSpawned, before Spawned() runs — same
        /// timing requirement as BuffRing.Element (must be written before the first snapshot goes out).</summary>
        public void Configure(int element, float radius, NetworkId spawnerProjectileId)
        {
            Element = element;
            SpawnerProjectileId = spawnerProjectileId;
            if (_col == null) _col = GetComponent<SphereCollider>();
            if (_col != null && radius > 0f) _col.radius = radius;
        }

        public override void Spawned()
        {
            _col = GetComponent<SphereCollider>();
            if (_col != null) _col.isTrigger = true;
            _iceHitPlayers.Clear();
            _fireNextTick.Clear();

            ApplyColor();
            if (HasStateAuthority) LifeTimer = TickTimer.CreateFromSeconds(Runner, _maxLifetime);

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

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (LifeTimer.Expired(Runner)) { Runner.Despawn(Object); return; }

            float radiusSq = _col != null ? _col.radius * _col.radius : 0f;
            if (Element == (int)RingElement.Fire) TickFireDamage(radiusSq);
            else if (Element == (int)RingElement.Ice) TickIceZone(radiusSq);
        }

        private void TickFireDamage(float radiusSq)
        {
            double now = Runner.SimulationTime;
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null || pc.Health <= 0) continue;
                if ((pc.transform.position - transform.position).sqrMagnitude > radiusSq) continue;

                PlayerRef pr = pc.Object.InputAuthority;
                if (_fireNextTick.TryGetValue(pr, out float next) && now < next) continue;
                _fireNextTick[pr] = (float)now + _fireTickInterval;
                pc.RPC_TakeHit(_fireDamagePerTick, pc.transform.position, PlayerRef.None);
            }
        }

        /// <summary>Manual per-tick distance scan instead of OnTriggerEnter — matches
        /// <see cref="TickFireDamage"/> and the rest of the codebase's hit-testing (ProjectileBurstSystem,
        /// NetworkProjectile all use OverlapSphere/distance checks, not Unity trigger EVENTS, which need a
        /// Rigidbody + matching physics-layer setup on both sides to fire reliably — verified via MCP that
        /// OnTriggerEnter silently never fired here even with exactly-overlapping colliders).</summary>
        private void TickIceZone(float radiusSq)
        {
            // A projectile OTHER than the one that spawned this wall melts it early ("dính dmg thì tan").
            NetworkProjectile[] projs = FindObjectsByType<NetworkProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projs.Length; i++)
            {
                NetworkProjectile p = projs[i];
                if (p == null || p.Object == null || !p.Object.IsValid) continue;
                if (p.Object.Id == SpawnerProjectileId) continue;
                if ((p.transform.position - transform.position).sqrMagnitude > radiusSq) continue;
                Runner.Despawn(Object);
                return;
            }

            // First player to touch takes one hit ("mất lượt" substituted with a real hit — see class doc).
            foreach (PlayerCombat pc in PlayerCombat.AllInstances)
            {
                if (!pc.IsPlayer || pc.Object == null || pc.Health <= 0) continue;
                if ((pc.transform.position - transform.position).sqrMagnitude > radiusSq) continue;

                PlayerRef pr = pc.Object.InputAuthority;
                if (_iceHitPlayers.Contains(pr)) continue;
                _iceHitPlayers.Add(pr);
                pc.RPC_TakeHit(_iceDamage, transform.position, PlayerRef.None);
            }
        }
    }
}
#endif
