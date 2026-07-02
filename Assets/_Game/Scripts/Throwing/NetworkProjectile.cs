#if PHOTON_FUSION
using Fusion;
using TossZone.Combat;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Thin Fusion-replicated wrapper around the locally-simulated <see cref="ThrowProjectile"/>. The authority
    /// client copies its local projectile's world transform into this NetworkObject every tick; NetworkTransform
    /// replicates that to proxies. Proxies show the attached mesh renderer (a sphere) interpolated by NT —
    /// they never run the BillTween arc themselves, they just display what the NT feed gives them.
    ///
    /// On the authority the local ThrowProjectile renderer is visible while this NetworkProjectile renderer is
    /// HIDDEN (set in <see cref="Spawned"/>), avoiding a doubled ball.
    /// Despawn is driven from <see cref="ThrowController.DespawnNetworkProjectile"/> when
    /// <see cref="BallLandedEvent"/> fires on the authority client.
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkProjectile : NetworkBehaviour
    {
        private Transform _localProjectile;
        private Renderer _mr;

        [Header("Hit + damage")]
        [SerializeField] private int _baseDamage = 1;
        [SerializeField] private float _hitRadius = 0.3f;
        [Tooltip("Layers the projectile can hit (the networked avatar bodies).")]
        [SerializeField] private LayerMask _hittableMask = ~0;
        [Tooltip("Authority despawns the projectile after this many seconds (backstop; player throws also " +
                 "despawn early on BallLanded). Prevents bot/orphan projectiles from leaking forever.")]
        [SerializeField] private float _lifetime = 5f;

        [Tooltip("T10 — shared BuffZone prefab, spawned at the hit point when Element is Ice/Fire.")]
        [SerializeField] private NetworkObject _zonePrefab;

        /// <summary>Who fired this — excluded from its own hits + rewarded on a landed hit.</summary>
        [Networked] public PlayerRef Shooter { get; set; }

        // ── Buff hooks (buff-aware from the start): buff rings + catch SET these; default = no buff. ──────────
        [Networked] public int Multiplier { get; set; }      // 1 = single; >1 = "đạn mưa" (spawns via ring system later)
        [Networked] public float VelocityScale { get; set; } // 1 = base flight speed
        [Networked] public float AreaScale { get; set; }     // 1 = base hit/explosion radius
        [Networked] public int Element { get; set; }         // 0 None · 1 Ice · 2 Fire

        private bool _hasHit;
        private bool _isAoe;
        private float _age;
        private float _customGravity;
        private int _damageOverride;
        private Rigidbody _rb;
        private static readonly Collider[] _overlap = new Collider[8];

        /// <summary>
        /// Called by the authority immediately after <see cref="Fusion.NetworkRunner.Spawn"/> so every
        /// FixedUpdateNetwork tick can copy the local projectile's position into the replicated transform.
        /// </summary>
        public void LinkTo(Transform localProj) => _localProjectile = localProj;

        /// <summary>
        /// Direct-fire path (HandWeapon: Gun/Bazooka/Grenade/BigBoom) — no local BillTween projectile involved.
        /// Sets an initial Rigidbody velocity and a manual per-tick gravity (0 = straight line, e.g. Gun;
        /// &gt;0 = arcs down, e.g. Bazooka/Grenade). Gravity is integrated by hand rather than
        /// <c>Rigidbody.useGravity</c> so it stays authority-only and deterministic (project has no Physics Addon).
        /// </summary>
        public void Launch(Vector3 velocity, float gravity, int damage = 0)
        {
            _customGravity = gravity;
            if (damage > 0) SetDamage(damage);
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = false;
                _rb.linearVelocity = velocity;
            }
        }

        /// <summary>Override this shot's damage (e.g. the ThrowBallistic path — Grenade/BigBoom/LandMine — sets
        /// this from the currently equipped WeaponConfig; the prefab's own <see cref="_baseDamage"/> stays the
        /// default for Rock / anything that doesn't call this).</summary>
        public void SetDamage(int damage) => _damageOverride = damage;

        /// <summary>Explosive weapons (BigBoom/Grenade): damage EVERY player in <paramref name="radiusMeters"/>,
        /// not just the first found. Expressed via the existing AreaScale hook (also used by buff rings).</summary>
        public void SetAoe(float radiusMeters)
        {
            _isAoe = true;
            if (_hitRadius > 0.0001f) AreaScale = Mathf.Max(AreaScale, radiusMeters / _hitRadius);
        }

        public override void Spawned()
        {
            // Reset per-life plain state — a pooled instance keeps stale fields from its previous life
            // (Fusion resets [Networked] state, but not these). Without this a reused projectile carries
            // _hasHit=true (never hits again) or a leftover _localProjectile link.
            _hasHit = false;
            _isAoe = false;
            _age = 0f;
            _customGravity = 0f;
            _damageOverride = 0;
            _localProjectile = null;

            _mr = GetComponentInChildren<Renderer>();
            // Authority sees the real local ThrowProjectile; hide the network copy to avoid doubling.
            // Proxies keep it enabled — they have no local projectile, so this IS the ball for them.
            if (_mr != null) _mr.enabled = !HasStateAuthority;
            if (HasStateAuthority)
            {
                // Default = no buff (rings / catch overwrite these before + while flying).
                if (Multiplier < 1) Multiplier = 1;
                if (VelocityScale <= 0f) VelocityScale = 1f;
                if (AreaScale <= 0f) AreaScale = 1f;
            }
            // Physics-driven path (DummyBotDriver / HandWeapon.Launch): authority runs Rigidbody, proxies use
            // kinematic NT.
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.isKinematic = !HasStateAuthority;
                _rb.useGravity = false;             // gravity integrated manually (see FixedUpdateNetwork)
                _rb.linearVelocity = Vector3.zero;   // clear stale velocity on pooled reuse
                _rb.angularVelocity = Vector3.zero;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Lifetime backstop → despawn (recycled by the pool). Fixes bot/orphan projectiles leaking forever.
            _age += Runner.DeltaTime;
            if (_age >= _lifetime) { Runner.Despawn(Object); return; }

            // Mirror BillTween-driven position when linked to a local ThrowProjectile (player throw path).
            if (_localProjectile != null)
            {
                transform.SetPositionAndRotation(_localProjectile.position, _localProjectile.rotation);
            }
            else if (_rb != null && _customGravity != 0f)
            {
                // Direct-fire path (HandWeapon.Launch): manual per-tick gravity integration, not
                // Rigidbody.useGravity — keeps the arc authority-only/deterministic (no Physics Addon).
                _rb.linearVelocity += Vector3.down * _customGravity * Runner.DeltaTime;
            }

            // Hit detection runs on the authority regardless of how the projectile moves.
            if (_hasHit) return;
            int dmg = _damageOverride > 0 ? _damageOverride : _baseDamage;
            int n = Physics.OverlapSphereNonAlloc(transform.position, _hitRadius * AreaScale, _overlap, _hittableMask, QueryTriggerInteraction.Collide);
            bool hitAny = false;
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null) continue;
                // Guard: InputAuthority identifies the PLAYER who owns the avatar.
                // Scene objects (DummyAvatar) have InputAuthority = None, so they are never excluded
                // even when the master client is the shooter — fixing solo-test blocking.
                if (victim.Object.InputAuthority == Shooter) continue;
                victim.RPC_TakeHit(dmg, transform.position, Shooter);
                hitAny = true;
                if (!_isAoe) break;   // single-target weapons stop at the first victim; AoE hits everyone in range
            }
            if (hitAny)
            {
                _hasHit = true;
                if (Element == (int)RingElement.Ice || Element == (int)RingElement.Fire) SpawnElementZone();
                if (PlayerCombat.Local != null) PlayerCombat.Local.RewardHit();
            }
        }

        /// <summary>T10: Ice/Fire shots leave a persistent <see cref="BuffZone"/> hazard at the hit point — see
        /// Combat_Minigame_Design.md §10.</summary>
        private void SpawnElementZone()
        {
            if (_zonePrefab == null) return;
            int element = Element;
            float radius = _hitRadius * Mathf.Max(1f, AreaScale);
            NetworkId selfId = Object.Id;
            Runner.Spawn(_zonePrefab, transform.position, Quaternion.identity, PlayerRef.None,
                (runner, o) =>
                {
                    if (o.TryGetComponent(out BuffZone zone)) zone.Configure(element, radius, selfId);
                });
        }
    }
}
#endif
