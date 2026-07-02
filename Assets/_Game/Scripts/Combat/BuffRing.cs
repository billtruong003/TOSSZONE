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

        [Tooltip("Wander speed knob (T9) — higher = the ring roams its zone box faster. Only used when a " +
                 "RingSpawner zone is resolved; otherwise falls back to the old fixed up/down drift.")]
        [SerializeField] private float _wanderFrequency = 0.15f;

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        [Networked] public RingElement Element { get; set; }

        /// <summary>T11 — rolled per-spawn by RingSpawner (1=common .. 5=rare), independent of Element. Tier 4-5
        /// drift faster (design: "Tier 4-5 hiếm + trôi nhanh"). Defaults to 1 for rings placed outside the
        /// RingSpawner flow.</summary>
        [Networked] public int Tier { get; set; }

        /// <summary>This ring's configured multiplier (RC_Multi.multiplier) — used by
        /// <see cref="ProjectileBurstSystem"/> when a data-driven rain burst stacks through this ring (T7).
        /// Falls back to 2 if the config hasn't resolved yet (shouldn't normally happen for an active ring).</summary>
        public int StackMultiplier => _config != null ? _config.multiplier : 2;

        private BuffRingConfig _config;
        private Vector3 _originPos;
        private Tween _driftTween;

        // T9 wander: resolved locally from RingSpawner.Instance (identical scene data on every client, no
        // networking needed). Position is a deterministic function of Object.Id + Runner.SimulationTime — every
        // client (authority AND proxies) computes the SAME path each frame, unlike the old per-client BillTween
        // sin drift whose phase depended on when that client's own Spawned() happened to fire.
        private bool _hasWanderZone;
        private Vector3 _wanderCenter;
        private Vector3 _wanderHalfExtents;
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
            // The prefab carries a convex ColliderRing mesh collider (not a SphereCollider); take whatever
            // Collider is present and make sure it's a trigger. GetComponent<SphereCollider>() here threw a
            // MissingComponentException and aborted the rest of Spawned() (no color/label/drift).
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            _config = ResolveConfig();
            ApplyColor();
            ApplyLabel();

            _originPos = transform.position;
            PlayBounceIn();

            // T9: prefer wandering the shared "vùng giữa" box (every client resolves the same RingSpawner scene
            // data); fall back to the old fixed up/down drift for a ring placed outside RingSpawner's flow.
            if (RingSpawner.Instance != null)
            {
                _wanderCenter = RingSpawner.Instance.ZoneCenter;
                _wanderHalfExtents = RingSpawner.Instance.ZoneHalfExtents;
                _hasWanderZone = true;
            }
            else
            {
                StartDrift();
            }
        }

        private void Update()
        {
            if (!_hasWanderZone || Runner == null) return;
            transform.position = WanderPosition((float)Runner.SimulationTime);
        }

        /// <summary>Deterministic wander path inside the zone box — same seed (Object.Id) + same clock
        /// (Runner.SimulationTime) on every client, so authority and proxies render the ring in the same place
        /// without replicating a single extra byte.</summary>
        private Vector3 WanderPosition(float simTime)
        {
            // Tier 4-5 drift noticeably faster than Tier 1-3 (design: "Tier 4-5 hiếm + trôi nhanh").
            int tier = Mathf.Clamp(Tier, 1, 5);
            float tierSpeedMul = 1f + (tier - 1) * 0.2f;   // Tier1=1.0x .. Tier5=1.8x
            float seed = (Object.Id.Raw % 10000) * 0.1013f;
            float f = simTime * _wanderFrequency * tierSpeedMul;
            float nx = Mathf.PerlinNoise(seed, f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(f, seed) * 2f - 1f;
            float nz = Mathf.PerlinNoise(seed + 5.5f, f + 5.5f) * 2f - 1f;
            return _wanderCenter + new Vector3(nx * _wanderHalfExtents.x, ny * _wanderHalfExtents.y, nz * _wanderHalfExtents.z);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Kill any tween still targeting this ring (drift/label/scale) — otherwise it fires after the
            // GameObject is destroyed and throws MissingReferenceException. Covers despawn paths other than
            // the consume anim (RingSpawner.ResetRings, respawn cycling).
            BillTween.KillTarget(this);
            _driftTween = null;
        }

        // ── Visual setup ──────────────────────────────────────────────────────────────

        private BuffRingConfig ResolveConfig()
        {
            int idx = (int)Element;
            return (idx >= 0 && idx < _catalog.Length) ? _catalog[idx] : null;
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
            BillTween.Scale(transform, 1.0f, 0.5f)
                ?.SetEase(EaseType.OutBack)
                .SetTarget(this);
        }

        private void StartDrift()
        {
            float amp = _config != null ? _config.driftAmplitude : 0.2f;
            float period = _config != null && _config.driftPeriod > 0f ? _config.driftPeriod : 3f;
            _driftTween = BillTween.Float(0f, 1f, period, t =>
            {
                float y = Mathf.Sin(t * Mathf.PI * 2f) * amp;
                transform.position = _originPos + Vector3.up * y;
            })?.SetLoops(-1, LoopType.Restart)
              .SetEase(EaseType.Linear)
              .SetTarget(this);
        }

        // ── Hit detection ─────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!HasStateAuthority || _config == null || _consumed) return;
            if (!other.TryGetComponent(out NetworkProjectile proj)) return;
            if (proj.Object == null || !proj.Object.IsValid) return;

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
            // Multi ring → convert the single ball into a data-driven BURST (the "rain") aimed along its travel,
            // then consume the original. The burst is DATA, not N NetworkObjects (see ProjectileBurstSystem).
            // SpawnBurst writes to ProjectileBurstSystem, which THIS client (the ring's authority) already owns —
            // no cross-authority issue there. Despawning the projectile itself needs the RPC (see above).
            if (_config.element == RingElement.Multi && ProjectileBurstSystem.Instance != null)
            {
                int count = Mathf.Max(2, _config.multiplier);
                ProjectileBurstSystem.Instance.SpawnBurst(
                    proj.transform.position, proj.transform.forward, count, _burstGravity, (int)_config.element, proj.Shooter);
                proj.RPC_RequestSelfDespawn();
                return;
            }

            float velocityScale = _config.velocityScale > 1f ? _config.velocityScale : 0f;
            float areaScale = _config.areaScale > 1f ? _config.areaScale : 0f;
            int element = _config.element != RingElement.None ? (int)_config.element : 0;
            proj.RPC_ApplyRingBuff(velocityScale, areaScale, element);
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
            _hasWanderZone = false;   // freeze position for the shrink — don't wander away mid-despawn

            // "EFFECTIVE!" flash on label then shrink ring to zero and despawn.
            if (_label != null) _label.text = "EFFECTIVE!";

            _driftTween?.Kill();
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
