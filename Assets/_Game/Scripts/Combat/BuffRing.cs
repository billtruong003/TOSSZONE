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
    /// Detection: SphereCollider trigger at the ring center (radius = inner hole radius). A ball passing through
    /// the hole enters the sphere and triggers the buff — no MeshCollider needed.
    ///
    /// Shared Mode note: ring has StateAuthority on master. Buff writes to projectile only when master is also
    /// the projectile's StateAuthority. RPC fix deferred to C5 live launch.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class BuffRing : NetworkBehaviour
    {
        [Header("Refs (set on prefab)")]
        [SerializeField] private Renderer _ringRenderer;
        [SerializeField] private TMPro.TextMeshPro _label;

        /// <summary>The 5 ring configs indexed by RingElement value — assign on the prefab (shared across all instances).</summary>
        [SerializeField] private BuffRingConfig[] _catalog = new BuffRingConfig[5];

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        [Networked] public RingElement Element { get; set; }

        private BuffRingConfig _config;
        private Vector3 _originPos;
        private Tween _driftTween;

        public override void Spawned()
        {
            _block = new MaterialPropertyBlock();
            GetComponent<SphereCollider>().isTrigger = true;

            _config = ResolveConfig();
            ApplyColor();
            ApplyLabel();

            _originPos = transform.position;
            PlayBounceIn();
            StartDrift();
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
            _label.text = _config.displayName;
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
            float amp    = _config != null ? _config.driftAmplitude : 0.2f;
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
            if (!HasStateAuthority || _config == null) return;
            if (!other.TryGetComponent(out NetworkProjectile proj)) return;
            if (proj.Object == null || !proj.Object.IsValid) return;
            if (!proj.Object.HasStateAuthority) return;

            ApplyBuff(proj);
            PlayConsumeAnim();
        }

        private void ApplyBuff(NetworkProjectile proj)
        {
            if (_config.multiplier > 1)
                proj.Multiplier = Mathf.Min(proj.Multiplier + _config.multiplier - 1, 3);
            if (_config.velocityScale > 1f)
                proj.VelocityScale = Mathf.Max(proj.VelocityScale, _config.velocityScale);
            if (_config.areaScale > 1f)
                proj.AreaScale = Mathf.Max(proj.AreaScale, _config.areaScale);
            if (_config.element != RingElement.None)
                proj.Element = (int)_config.element;
        }

        private void PlayConsumeAnim()
        {
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
