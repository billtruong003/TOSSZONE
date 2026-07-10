#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Single shared-prefab buff ring. <see cref="RingSpawner"/> spawns one instance and immediately sets
    /// <see cref="Element"/>; <see cref="Spawned"/> resolves the matching <see cref="BuffRingConfig"/> from
    /// <see cref="Catalog"/> and applies color + label + bounce-in animation.
    ///
    /// Detection: a convex trigger collider (the ColliderRing mesh) spanning the ring opening. A ball flying
    /// through the ring enters the trigger and applies the buff.
    ///
    /// Shared Mode note: ring has StateAuthority on master. Buff writes to projectile only when master is also
    /// the projectile's StateAuthority. RPC fix deferred to C5 live launch.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuffRing : NetworkBehaviour
    {
        [Header("Refs (set on prefab)")]
        [SerializeField] private Renderer _ringRenderer;
        [SerializeField] private TMPro.TextMeshPro _label;

        /// <summary>The 5 ring configs indexed by RingElement value — assign on the prefab (shared across all instances).</summary>
        [SerializeField] private BuffRingConfig[] _catalog = new BuffRingConfig[5];

        [Tooltip("Gravity applied to the Multi-ring burst rain (arc). Higher = falls faster.")]
        [SerializeField] private float _burstGravity = 2f;

        [Tooltip("Đường kính miệng ring của mesh prefab (m) tại localScale=1 — scale tier = đường kính GDD / số này.")]
        [SerializeField] private float _prefabDiameter = 1.8f;

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        [Networked] public RingElement Element { get; set; }

        /// <summary>T11 — rolled per-spawn by RingSpawner (1=common .. 5=rare), independent of Element. Tier 4-5
        /// drift faster (design: "Tier 4-5 hiếm + trôi nhanh"). Defaults to 1 for rings placed outside the
        /// RingSpawner flow.</summary>
        [Networked] public int Tier { get; set; }

        /// <summary>Session 17.12 bug #2 — spawn-time anchor, replicated once. This prefab intentionally has no
        /// NetworkTransform (drift is deterministic + local), but that also means the SPAWN POSITION never
        /// replicates: proxies attach at the prefab default (0,0,0), so _driftAnchor resolved to ground level /
        /// zone center on every remote client. Authority writes its rolled spawn position here in Spawned();
        /// proxies snap to it before caching anchors. Do NOT replace this with a NetworkTransform — the local
        /// per-frame drift in Update() would fight NT interpolation on proxies.</summary>
        [Networked] private Vector3 SpawnAnchor { get; set; }

        public int StackMultiplier => _config != null
            ? Mathf.Max(2, Mathf.RoundToInt(_config.ValueForTier(Tier))) : 2;

        public bool IsConsumed => _consumed;

        private BuffRingConfig _config;
        private Vector3 _originPos;
        private float _tierScale = 1f;
        private bool _hasDriftZone;
        private Vector3 _driftCenter;
        private Vector3 _driftHalfExtents;
        private Vector3 _driftAnchor;
        // Set the INSTANT consumption starts (not when the 0.25s shrink tween's despawn finally completes) — the
        // ring stays alive/visible/collidable during that shrink, so without this guard it could be consumed
        // AGAIN by another ball or by a burst re-sampling it every tick (T7 hit this: without the guard a single
        // rain burst re-triggered the same still-shrinking ring for several ticks, multiplying Count each time
        // and blowing straight through the 4096 cap instead of stacking exactly once).
        private bool _consumed;

        public override void Spawned()
        {
            _consumed = false;   // defensive reset (matches NetworkProjectile's per-life pattern) in case
                                  // this prefab is ever pooled later — a fresh instance already starts false.
            _block = new MaterialPropertyBlock();
            Collider col = GetComponent<Collider>();
            if (col is MeshCollider)
            {
                Destroy(col);
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(_prefabDiameter, _prefabDiameter, 1f);
                box.isTrigger = true;
            }
            else if (col != null) col.isTrigger = true;

            _config = ResolveConfig();
            _tierScale = BuffRingConfig.DiameterForTier(Tier) / Mathf.Max(0.01f, _prefabDiameter);
            ApplyColor();
            ApplyLabel();

            // Anchor sync (bug #2): authority publishes its spawn position; proxies snap to it BEFORE caching
            // anchors below — at attach time a proxy's transform still sits at the prefab default (0,0,0).
            if (HasStateAuthority) SpawnAnchor = transform.position;
            else if (SpawnAnchor != Vector3.zero) transform.position = SpawnAnchor;

            _originPos = transform.position;
            _driftAnchor = transform.position;
            PlayBounceIn();

            if (RingSpawner.Instance != null)
            {
                _driftCenter = RingSpawner.Instance.ZoneCenter;
                _driftHalfExtents = RingSpawner.Instance.ZoneHalfExtents;
                _hasDriftZone = true;
            }
            else
            {
                StartDrift();
            }
        }

        private void Update()
        {
            if (!_hasDriftZone || Runner == null) return;
            transform.position = DriftPosition((float)Runner.SimulationTime);
        }

        private Vector3 DriftPosition(float simTime)
        {
            float width = _driftHalfExtents.x * 2f;
            if (width <= 0.01f) return _driftAnchor;
            float speed = BuffRingConfig.DriftSpeedForTier(Tier);
            float phase = (Object.Id.Raw % 10000) * 0.1013f * width;
            float x = Mathf.PingPong(simTime * speed + phase, width) - _driftHalfExtents.x;
            return new Vector3(_driftCenter.x + x, _driftAnchor.y, _driftAnchor.z);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Kill any tween still targeting this ring (drift/label/scale) — otherwise it fires after the
            // GameObject is destroyed and throws MissingReferenceException. Covers despawn paths other than
            // the consume anim (RingSpawner.ResetRings, respawn cycling).
            BillTween.KillTarget(this);
        }

        // ── Visual setup ──────────────────────────────────────────────────────────────

        private BuffRingConfig ResolveConfig()
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
                if (_catalog[i] != null && _catalog[i].element == Element) return _catalog[i];
            return null;
        }

        private void ApplyColor()
        {
            if (_ringRenderer == null || _config == null) return;
            // The ring mesh may use a palette shader that ignores MPB tinting.
            // Create a runtime URP Unlit material instance so color always shows correctly.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null && _ringRenderer.sharedMaterial != null) sh = _ringRenderer.sharedMaterial.shader;
            Material mat = sh != null ? new Material(sh) : new Material(_ringRenderer.sharedMaterial);
            Color c = _config.ringColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
            _ringRenderer.material = mat; // per-instance, GC'd on despawn
        }

        private void ApplyLabel()
        {
            if (_label == null || _config == null) return;
            int tier = Mathf.Clamp(Tier, 1, 5);
            _label.text = tier > 1 ? _config.displayName + " T" + tier : _config.displayName;
            Color c = _config.ringColor; c.a = 0f; _label.color = c;
            // Fade label in after bounce.
            BillTween.Float(0f, 1f, 0.3f, a =>
            {
                Color lc = _label.color; lc.a = a; _label.color = lc;
            })?.SetDelay(0.35f).SetEase(EaseType.OutCubic).SetTarget(this);
        }

        private void PlayBounceIn()
        {
            transform.localScale = Vector3.zero;
            BillTween.Scale(transform, _tierScale, 0.5f)
                ?.SetEase(EaseType.OutBack)
                .SetTarget(this);
        }

        private void StartDrift()
        {
            const float HalfSweep = 1f;
            float speed = BuffRingConfig.DriftSpeedForTier(Tier);
            float period = (HalfSweep * 4f) / Mathf.Max(0.01f, speed);
            BillTween.Float(0f, 1f, period, t =>
            {
                float x = Mathf.PingPong(t * HalfSweep * 4f, HalfSweep * 2f) - HalfSweep;
                transform.position = _originPos + Vector3.right * x;
            })?.SetLoops(-1, LoopType.Restart)
              .SetEase(EaseType.Linear)
              .SetTarget(this);
        }

        // ── Hit detection ─────────────────────────────────────────────────────────────

        /// <summary>World radius of the ring opening at the current tier scale.</summary>
        public float OpeningRadius => _prefabDiameter * 0.5f * Mathf.Max(0.01f, transform.lossyScale.x);

        private void OnTriggerEnter(Collider other)
        {
            if (!HasStateAuthority || _config == null || _consumed) return;
            if (!other.TryGetComponent(out NetworkProjectile proj)) return;
            if (proj.Object == null || !proj.Object.IsValid) return;
            if (proj.RingsApplied >= NetworkProjectile.MaxRingStack) return;

            Vector3 local = transform.InverseTransformPoint(other.transform.position);
            local.z = 0f;
            if (local.magnitude > _prefabDiameter * 0.5f) return;

            ApplyBuff(proj);
            PlayConsumeAnim();
        }

        /// <summary>T12 — Shared Mode (Fusion_Shared_Mode_Gotchas.md §1): this ring's authority (the round's
        /// master, per RingSpawner spawning with PlayerRef.None) is NOT necessarily the projectile's own
        /// authority (the shooter). Only an object's own State Authority may write its [Networked] state or
        /// despawn it — writing proj.VelocityScale/AreaScale/Element or calling proj.Runner.Despawn directly here
        /// silently no-ops for any shooter other than the ring's own authority (this is exactly what "solo
        /// testing only" was masking). Route both through RPCs targeted at the projectile's authority instead.</summary>
        private void ApplyBuff(NetworkProjectile proj)
        {
            int tier = Mathf.Clamp(Tier, 1, 5);
            float value = _config.ValueForTier(tier);

            if (_config.element == RingElement.Multi && ProjectileBurstSystem.Instance != null)
            {
                int count = Mathf.Max(2, Mathf.RoundToInt(value));
                ProjectileBurstSystem.Instance.SpawnBurst(
                    proj.transform.position, proj.transform.forward, count, _burstGravity, (int)_config.element,
                    proj.Shooter, proj.RingsApplied + 1);
                proj.RPC_RequestSelfDespawn();
                return;
            }

            float velocityScale = _config.element == RingElement.Speed ? value : 0f;
            float areaScale = _config.element == RingElement.Area ? value : 0f;
            float effectSeconds = (_config.element == RingElement.Ice || _config.element == RingElement.Fire) ? value : 0f;
            int element = _config.element != RingElement.None ? (int)_config.element : 0;
            proj.RPC_ApplyRingBuff(velocityScale, areaScale, element, effectSeconds);
        }

        /// <summary>Called by <see cref="ProjectileBurstSystem"/> (authority) when a data-driven rain burst
        /// passes through this ring (T7 — stacking, e.g. 12×12×12). Bursts have no collider so they can't hit
        /// <see cref="OnTriggerEnter"/> normally; only Multi rings cause stacking (only Multi has meaning for a
        /// burst that's already a rain — other elements would need per-projectile buff state the mass burst
        /// doesn't carry, out of scope here). Returns true if this ring was actually consumed.</summary>
        public bool TryConsumeByBurst()
        {
            if (!HasStateAuthority || _config == null || _consumed || Element != RingElement.Multi) return false;
            PlayConsumeAnim();
            return true;
        }

        private void PlayConsumeAnim()
        {
            // Mark consumed IMMEDIATELY — the ring stays alive/visible/collidable for the ~0.25s shrink below,
            // so without this it could be re-consumed again before the despawn actually removes it (see the
            // _consumed field comment).
            _consumed = true;
            _hasDriftZone = false;   // freeze position for the shrink — don't drift away mid-despawn

            // "EFFECTIVE!" flash on label then shrink ring to zero and despawn.
            if (_label != null) _label.text = "EFFECTIVE!";

            BillTween.KillTarget(this);
            BillTween.Scale(transform, 0f, 0.25f)
                ?.SetEase(EaseType.InBack)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    if (Bill.IsReady) Bill.Events.Fire(new RingConsumedEvent { RingId = _config.id });
                    if (Runner != null && Object != null) Runner.Despawn(Object);
                });
        }
    }

    public struct RingConsumedEvent : IEvent { public string RingId; }
}
#endif
