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
    /// Despawn is driven from <see cref="ThrowController"/>'s per-ball twin map when
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

        [SerializeField] private LayerMask _groundMask = 1;
        [SerializeField] private float _mineTriggerRadius = 0.6f;
        [SerializeField] private float _mineLifetime = 60f;

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
        [Networked] public NetworkBool Uncatchable { get; set; }
        [Networked] public NetworkBool Exploded { get; set; }

        /// <summary>T20 — which weapon's shot this projectile LOOKS like: 0 = default sphere, i+1 = weapon
        /// catalog index i. Set by the shooter in onBeforeSpawned (so proxies see it in their first snapshot);
        /// every client dresses the projectile from its own catalog copy — sync the cause, not the mesh.</summary>
        [Networked] public int VisualIndex { get; set; }

        private bool _hasHit;
        private bool _isAoe;
        private float _age;
        private float _customGravity;
        private int _damageOverride;
        private float _crossWidth;
        private float _crossLength;
        private float _crossSeconds;
        private Vector3 _prevPos;
        private bool _prevPosValid;
        private Vector3 _origin;
        private bool _explosive;
        private bool _fxPlayed;
        private int _appliedElementTint;
        private MaterialPropertyBlock _tintBlock;

        private const float ExplosiveAoeThreshold = 1.0f;
        // Bug #3 (Session 17.13): was 0.7f — close-range throws (2 players adjacent) could NEVER hit because
        // the ball never travelled far enough to arm. The shooter is already excluded from every victim scan
        // (InputAuthority == Shooter), so the arm gate only needs to cover the spawn-frame overlap, not 0.7m.
        private const float MinArmDistance = 0.15f;
        // Bug #4 (Session 17.13): after a confirmed hit the authority keeps the corpse at the impact point for
        // this many ticks before despawning, so proxy NetworkTransform interpolation (which lags the authority
        // tick) can catch up to the contact position — otherwise remotes see the ball vanish mid-air.
        private const int HitLingerTicks = 10;
        private bool _isMine;
        private float _mineFuse;
        private bool _mineLanded;
        private float _mineArmRemaining;
        private int _despawnCountdown;
        private Rigidbody _rb;
        // T20 visual cache — survives pool lives on purpose (rebuilt only when VisualIndex changes).
        private GameObject _visualHolder;
        private int _appliedVisual;
        private static readonly Collider[] _overlap = new Collider[8];
        private static readonly RaycastHit[] _sweepHits = new RaycastHit[8];

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

        /// <summary>Splash radius. radiusMeters ≥ ExplosiveAoeThreshold (grenade/bazooka/nuke/cross) → this shot
        /// detonates with a fireball on ground/proximity. Below it (rock/gun) → plain direct hit, no fireball.</summary>
        public void SetAoe(float radiusMeters)
        {
            _isAoe = true;
            if (radiusMeters >= ExplosiveAoeThreshold) _explosive = true;
            if (_hitRadius > 0.0001f) AreaScale = Mathf.Max(AreaScale, radiusMeters / _hitRadius);
        }

        public void SetCrossZones(float width, float length, float seconds)
        {
            _explosive = true;
            _crossWidth = width;
            _crossLength = length;
            _crossSeconds = seconds;
        }

        public void SetMine(float fuseDelay)
        {
            _isMine = true;
            _mineFuse = fuseDelay;
        }

        public bool PersistsAfterLanding => _isMine;

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
            if (element != 0)
            {
                Element = element;
                if (_localThrowProj != null)
                    _localThrowProj.SetTrailTint(TossZone.Combat.BuffRingConfig.ElementColor((TossZone.Combat.RingElement)element));
            }
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
            _crossWidth = 0f;
            _crossLength = 0f;
            _crossSeconds = 0f;
            _explosive = false;
            _prevPos = transform.position;
            _prevPosValid = false;
            _origin = transform.position;
            _fxPlayed = false;
            _appliedElementTint = 0;
            _isMine = false;
            _mineFuse = 0f;
            _mineLanded = false;
            _mineArmRemaining = 0f;
            _despawnCountdown = 0;
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
            if (_appliedElementTint != Element)
            {
                _appliedElementTint = Element;
                if (_mr != null && Element != 0)
                {
                    _tintBlock ??= new MaterialPropertyBlock();
                    _tintBlock.SetColor("_BaseColor", BuffRingConfig.ElementColor((RingElement)Element));
                    _mr.SetPropertyBlock(_tintBlock);
                }
            }
            if (Exploded && !_fxPlayed)
            {
                _fxPlayed = true;
                ExplosionFx.Play(transform.position, _hitRadius * Mathf.Max(1f, AreaScale));
                if (_mr != null) _mr.enabled = false;
                if (_visualHolder != null) _visualHolder.SetActive(false);
            }
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

            if (Exploded)
            {
                if (--_despawnCountdown <= 0) Runner.Despawn(Object);
                return;
            }

            _age += Runner.DeltaTime;
            float maxAge = _mineLanded ? _mineLifetime : _lifetime;
            if (_age >= maxAge) { Runner.Despawn(Object); return; }

            if (_hasHit)
            {
                // Bug #4: linger at the impact point for a few ticks (position frozen — do NOT keep copying
                // the local twin) so proxy interpolation catches up before the object disappears.
                if (--_despawnCountdown <= 0) Runner.Despawn(Object);
                return;
            }

            if (_localProjectile != null)
            {
                transform.SetPositionAndRotation(_localProjectile.position, _localProjectile.rotation);
            }
            else if (_rb != null && !_mineLanded && _customGravity != 0f)
            {
                _rb.linearVelocity += Vector3.down * _customGravity * Runner.DeltaTime;
            }

            if (_hasHit) return;

            // T-bugfix (Session 17.12/17.13): used to `return` here on the very first tick without checking
            // anything — _prevPos is already seeded to the spawn origin in Spawned(), so skipping meant the
            // origin→current segment of tick 1 was NEVER swept. A fast projectile (buffed Speed rings, or a
            // thrown ball whose local-simulation position already advanced before its first network tick) can
            // cover its entire distance to a close target within that unblinded first tick and tunnel through
            // undetected. Confirmed via live MCP testing (Session 17.13) — do not reintroduce the skip.
            _prevPosValid = true;

            if (_mineLanded)
            {
                TickMine();
                _prevPos = transform.position;
                return;
            }

            bool onGround = TryGroundContact(out Vector3 groundPoint);

            if (_isMine)
            {
                if (onGround) LandMine(groundPoint);
                _prevPos = transform.position;
                return;
            }

            if (_explosive)
            {
                if (onGround) Explode(groundPoint);
                else if (IsArmed() && AnyVictimInRange(out Vector3 victimPoint)) Explode(victimPoint);
            }
            else
            {
                bool hit = IsArmed() && HitFirstVictim();
                if (!hit && onGround) BeginHitLinger(groundPoint);
            }
            _prevPos = transform.position;
        }

        private bool IsArmed() => (transform.position - _origin).sqrMagnitude >= MinArmDistance * MinArmDistance;

        // Contact uses the BASE _hitRadius (~ball size); the buffed/config AoE radius only widens the SPLASH
        // applied after a real contact. Using _hitRadius*AreaScale for contact made the rock "touch" people
        // 0.7m from its surface and pop mid-air near bystanders (PT-06).
        //
        // Bug #3 (Session 17.13): a single point-overlap per tick let fast balls tunnel THROUGH a capsule
        // between two ticks. Now also sphere-sweeps the _prevPos→current segment (same idea as
        // TryGroundContact's raycast) and reports the contact point ON the segment.
        private bool HitFirstVictim()
        {
            Vector3 contactPoint = transform.position;
            bool contact = FirstVictimOnPath(_hitRadius, ref contactPoint) != null;
            if (!contact) return false;

            int dmg = _damageOverride > 0 ? _damageOverride : _baseDamage;
            float splash = _isAoe ? _hitRadius * AreaScale : _hitRadius;
            int n = Physics.OverlapSphereNonAlloc(contactPoint, splash, _overlap,
                _hittableMask, QueryTriggerInteraction.Collide);
            bool applied = false;
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null || victim.Health <= 0) continue;
                if (victim.Object.InputAuthority == Shooter) continue;
                if (Element == (int)RingElement.Ice) victim.RPC_Freeze(EffectSeconds > 0f ? EffectSeconds : 1f);
                else victim.RPC_TakeHit(dmg, contactPoint, Shooter);
                applied = true;
                if (!_isAoe) break;
            }
            if (applied)
            {
                BeginHitLinger(contactPoint);
                if (Element == (int)RingElement.Ice || Element == (int)RingElement.Fire) SpawnElementZone();
            }
            return applied;
        }

        /// <summary>Bug #3 core: point-overlap at the current position (catches slow/stationary contact —
        /// SphereCast skips colliders it starts inside of), then a sphere-sweep along _prevPos→current for
        /// everything that would have been tunneled past. Returns the first valid victim, with
        /// <paramref name="contactPoint"/> updated to where on the path the contact happened.</summary>
        private PlayerCombat FirstVictimOnPath(float radius, ref Vector3 contactPoint)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlap,
                _hittableMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null || victim.Health <= 0) continue;
                if (victim.Object.InputAuthority == Shooter) continue;
                contactPoint = transform.position;
                return victim;
            }

            Vector3 delta = transform.position - _prevPos;
            float dist = delta.magnitude;
            if (dist < 1e-5f) return null;
            Vector3 dir = delta / dist;
            int h = Physics.SphereCastNonAlloc(_prevPos, radius, dir, _sweepHits, dist,
                _hittableMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            PlayerCombat bestVictim = null;
            for (int i = 0; i < h; i++)
            {
                PlayerCombat victim = _sweepHits[i].collider != null
                    ? _sweepHits[i].collider.GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null || victim.Health <= 0) continue;
                if (victim.Object.InputAuthority == Shooter) continue;
                if (_sweepHits[i].distance < best)
                {
                    best = _sweepHits[i].distance;
                    bestVictim = victim;
                    contactPoint = _prevPos + dir * _sweepHits[i].distance;
                }
            }
            return bestVictim;
        }

        /// <summary>Bug #4: instead of despawning the instant a hit/landing is confirmed (which made remotes —
        /// whose interpolation lags the authority tick — see the ball vanish mid-air), snap to the contact
        /// point, freeze, and let FixedUpdateNetwork despawn after <see cref="HitLingerTicks"/>.</summary>
        private void BeginHitLinger(Vector3 point)
        {
            _hasHit = true;
            transform.position = point;
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _despawnCountdown = HitLingerTicks;
        }

        private bool TryGroundContact(out Vector3 point)
        {
            point = default;
            Vector3 delta = transform.position - _prevPos;
            float dist = delta.magnitude;
            if (dist < 1e-5f) return false;
            if (!Physics.Raycast(_prevPos, delta / dist, out RaycastHit hit, dist + 0.05f, _groundMask,
                    QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            return true;
        }

        // Bug #3 (Session 17.13): explosive proximity check now sweeps the inter-tick segment too, and
        // reports WHERE the victim was met so Explode() detonates on the path instead of past the target.
        private bool AnyVictimInRange(out Vector3 point)
        {
            point = transform.position;
            return FirstVictimOnPath(_hitRadius * AreaScale, ref point) != null;
        }

        private void Explode(Vector3 point)
        {
            _hasHit = true;
            Exploded = true;
            transform.position = point;
            if (_rb != null && !_rb.isKinematic) _rb.linearVelocity = Vector3.zero;
            DamagePlayersAround(point);
            if (Element == (int)RingElement.Ice || Element == (int)RingElement.Fire) SpawnElementZone();
            if (_crossWidth > 0f) SpawnCrossZones();
            _despawnCountdown = 5;
        }

        private void DamagePlayersAround(Vector3 point)
        {
            int dmg = _damageOverride > 0 ? _damageOverride : _baseDamage;
            int n = Physics.OverlapSphereNonAlloc(point, _hitRadius * AreaScale, _overlap, _hittableMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null || victim.Health <= 0) continue;
                if (victim.Object.InputAuthority == Shooter) continue;
                if (Element == (int)RingElement.Ice) victim.RPC_Freeze(EffectSeconds > 0f ? EffectSeconds : 1f);
                else victim.RPC_TakeHit(dmg, point, Shooter);
                if (!_isAoe) break;
            }
        }

        private void LandMine(Vector3 point)
        {
            _mineLanded = true;
            _mineArmRemaining = _mineFuse;
            transform.position = point + Vector3.up * 0.05f;
            _localProjectile = null;
            _localThrowProj = null;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            RefreshVisibility();
        }

        private void TickMine()
        {
            if (_mineArmRemaining > 0f)
            {
                _mineArmRemaining -= Runner.DeltaTime;
                return;
            }
            int n = Physics.OverlapSphereNonAlloc(transform.position, _mineTriggerRadius, _overlap,
                _hittableMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                PlayerCombat victim = _overlap[i] != null ? _overlap[i].GetComponentInParent<PlayerCombat>() : null;
                if (victim == null || victim.Object == null || victim.Health <= 0) continue;
                if (victim.Object.InputAuthority == Shooter) continue;
                Explode(transform.position);
                return;
            }
        }

        private void SpawnCrossZones()
        {
            if (_zonePrefab == null) return;
            Vector3 half = new Vector3(_crossWidth * 0.5f, 2f, _crossLength * 0.5f);
            float seconds = _crossSeconds;
            Vector3 pos = new Vector3(transform.position.x, 0f, transform.position.z);
            NetworkId selfId = Object.Id;
            for (int i = 0; i < 2; i++)
            {
                Quaternion rot = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Runner.Spawn(_zonePrefab, pos, rot, PlayerRef.None,
                    (runner, o) =>
                    {
                        if (o.TryGetComponent(out BuffZone zone))
                            zone.ConfigureBox((int)RingElement.Fire, half, selfId, seconds);
                    });
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
