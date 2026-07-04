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
        private ThrowProjectile _localThrowProj;
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

        public const int MaxRingStack = 3;

        // ── Buff hooks (buff-aware from the start): buff rings + catch SET these; default = no buff. ──────────
        [Networked] public int Multiplier { get; set; }      // 1 = single; >1 = "đạn mưa" (spawns via ring system later)
        [Networked] public float VelocityScale { get; set; } // 1 = base flight speed
        [Networked] public float AreaScale { get; set; }     // 1 = base hit/explosion radius
        [Networked] public int Element { get; set; }         // 0 None · 1 Ice · 2 Fire
        [Networked] public int RingsApplied { get; set; }
        [Networked] public float EffectSeconds { get; set; }

        /// <summary>T20 — which weapon's shot this projectile LOOKS like: 0 = default sphere, i+1 = weapon
        /// catalog index i. Set by the shooter in onBeforeSpawned (so proxies see it in their first snapshot);
        /// every client dresses the projectile from its own catalog copy — sync the cause, not the mesh.</summary>
        [Networked] public int VisualIndex { get; set; }

        private bool _hasHit;
        private bool _isAoe;
        private float _age;
        private float _customGravity;
        private int _damageOverride;
        private Rigidbody _rb;
        // T20 visual cache — survives pool lives on purpose (rebuilt only when VisualIndex changes).
        private GameObject _visualHolder;
        private int _appliedVisual;
        private static readonly Collider[] _overlap = new Collider[8];

        /// <summary>
        /// Called by the authority immediately after <see cref="Fusion.NetworkRunner.Spawn"/> so every
        /// FixedUpdateNetwork tick can copy the local projectile's position into the replicated transform.
        /// </summary>
        public void LinkTo(Transform localProj)
        {
            _localProjectile = localProj;
            _localThrowProj = localProj != null ? localProj.GetComponent<ThrowProjectile>() : null;
            RefreshVisibility();   // linked authority renders its LOCAL twin — hide this network copy entirely
        }

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

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplyRingBuff(float velocityScale, float areaScale, int element, float effectSeconds)
        {
            if (RingsApplied >= MaxRingStack) return;
            RingsApplied++;
            if (velocityScale > 1f)
            {
                VelocityScale = (VelocityScale <= 0f ? 1f : VelocityScale) * velocityScale;
                ApplySpeedMultiplier(velocityScale);
            }
            if (areaScale > 1f) AreaScale = (AreaScale <= 0f ? 1f : AreaScale) * areaScale;
            if (element != 0) Element = element;
            if (effectSeconds > 0f) EffectSeconds = Mathf.Max(EffectSeconds, effectSeconds);
        }

        private void ApplySpeedMultiplier(float mul)
        {
            if (mul <= 1f) return;
            if (_localThrowProj != null) _localThrowProj.ApplySpeedMultiplier(mul);
            else if (_rb != null && !_rb.isKinematic) _rb.linearVelocity *= mul;
        }

        /// <summary>T12 — same authority rule: only this projectile's own State Authority may despawn it.
        /// BuffRing (Multi ring) calls this after spawning the burst rain that replaces this single ball.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestSelfDespawn()
        {
            if (Runner != null && Object != null && Object.IsValid) Runner.Despawn(Object);
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
            _localThrowProj = null;

            _mr = null;   // re-resolve, EXCLUDING the T20 visual holder's own renderers
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (_visualHolder == null || !r.transform.IsChildOf(_visualHolder.transform)) { _mr = r; break; }
            // T20: visible by default for EVERYONE — including the shooter on the direct-fire path
            // (Gun/Bazooka have no local twin; the old unconditional !HasStateAuthority hid the shooter's own
            // bullet). The throw path hides this copy in LinkTo() once the local twin is registered.
            ApplyVisualIfChanged();
            RefreshVisibility();
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

        public override void Render()
        {
            // T20: VisualIndex can land a snapshot after Spawned on late-joining proxies — keep it honest.
            ApplyVisualIfChanged();
        }

        /// <summary>T20 — (re)build the weapon cosmetic when the networked VisualIndex changes. The cosmetic is
        /// cached across pool lives (only rebuilt on a different index). Every client resolves the model from
        /// its own CombatSession catalog.</summary>
        private void ApplyVisualIfChanged()
        {
            if (Object == null || !Object.IsValid) return;
            int vi = VisualIndex;
            if (vi == _appliedVisual) return;
            _appliedVisual = vi;
            if (_visualHolder != null) { Destroy(_visualHolder); _visualHolder = null; }

            WeaponConfig cfg = null;
            if (vi > 0)
            {
                WeaponConfig[] catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
                if (catalog != null && vi - 1 < catalog.Length) cfg = catalog[vi - 1];
            }
            _visualHolder = WeaponVisuals.SpawnProjectileVisual(cfg, transform);
            RefreshVisibility();
        }

        /// <summary>Who renders what: a linked authority hides this network copy entirely (its LOCAL
        /// ThrowProjectile is the visible ball); everyone else shows the weapon cosmetic when present, else
        /// the base sphere.</summary>
        private void RefreshVisibility()
        {
            bool show = _localProjectile == null;
            if (_mr != null) _mr.enabled = show && _visualHolder == null;
            if (_visualHolder != null) _visualHolder.SetActive(show);
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
                if (Element == (int)RingElement.Ice) victim.RPC_Freeze(EffectSeconds > 0f ? EffectSeconds : 1f);
                else victim.RPC_TakeHit(dmg, transform.position, Shooter);
                hitAny = true;
                if (!_isAoe) break;   // single-target weapons stop at the first victim; AoE hits everyone in range
            }
            if (hitAny)
            {
                _hasHit = true;
                if (Element == (int)RingElement.Ice || Element == (int)RingElement.Fire) SpawnElementZone();
                if (Element != (int)RingElement.Ice && PlayerCombat.Local != null) PlayerCombat.Local.RewardHit();
            }
        }

        /// <summary>T10: Ice/Fire shots leave a persistent <see cref="BuffZone"/> hazard at the hit point — see
        /// Combat_Minigame_Design.md §10.</summary>
        private void SpawnElementZone()
        {
            if (_zonePrefab == null) return;
            int element = Element;
            float radius = _hitRadius * Mathf.Max(1f, AreaScale);
            float effectSeconds = EffectSeconds;
            NetworkId selfId = Object.Id;
            Runner.Spawn(_zonePrefab, transform.position, Quaternion.identity, PlayerRef.None,
                (runner, o) =>
                {
                    if (o.TryGetComponent(out BuffZone zone)) zone.Configure(element, radius, selfId, effectSeconds);
                });
        }

#if UNITY_EDITOR
        /// <summary>Debug visual (T17) — the live hit-detection sphere (yellow while flying, orange once the AoE
        /// scale is active, i.e. Grenade/BigBoom) so the actual blast radius used by
        /// <see cref="Physics.OverlapSphereNonAlloc"/> above is visible, not just AreaScale's raw number.
        /// Editor Scene view only.</summary>
        private void OnDrawGizmos()
        {
            // AreaScale is a [Networked] property — only readable after Spawned() has run (Fusion throws
            // otherwise). Gizmos can be called by the Editor before that, e.g. while sitting inactive in the
            // pool, so guard on Object validity like the rest of this codebase does.
            float areaScale = (Object != null && Object.IsValid) ? AreaScale : 1f;
            float radius = (_hitRadius > 0f ? _hitRadius : 0.3f) * Mathf.Max(1f, areaScale);
            Gizmos.color = _isAoe ? new Color(1f, 0.5f, 0f, 0.5f) : new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
#endif
