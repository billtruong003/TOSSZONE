using BillGameCore;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Pooled impact juice (Throw_Mechanic_Spec.md §4.4) — particle burst + an expanding, fading shockwave ring.
    /// Used both for a ball landing on the ground (<see cref="BallLandedEvent"/>) and a real player hit
    /// (<see cref="TossZone.Combat.PlayerHitEvent"/>). Register under pool key <c>"impactburst"</c>.
    /// Both visual refs are optional — with neither wired this still runs its lifetime via the shockwave tween
    /// and returns to the pool cleanly (no dangling state).
    /// </summary>
    public class ImpactBurst : PooledObject
    {
        private const string PoolKey = "impactburst";

        [SerializeField] private ParticleSystem _particles;
        [Tooltip("Flat disc/quad that scales out and fades — the shockwave ring.")]
        [SerializeField] private Transform _shockwave;
        [SerializeField] private Renderer _shockwaveRenderer;
        [SerializeField] private float _shockwaveMaxScale = 1.5f;
        [SerializeField] private float _shockwaveDuration = 0.35f;
        [SerializeField] private Color _shockwaveColor = new Color(1f, 1f, 1f, 0.6f);

        private static Material _shockwaveMat;

        public static void Show(Vector3 worldPos, float power)
        {
            if (!Bill.IsReady) return;
            GameObject go = Bill.Pool.Spawn(PoolKey, worldPos, Quaternion.identity);
            if (go == null) return;
            if (go.TryGetComponent(out ImpactBurst ib)) ib.Play(power);
        }

        private void Awake()
        {
            if (_shockwaveRenderer != null) _shockwaveRenderer.sharedMaterial = ShockwaveMaterial(_shockwaveColor);
        }

        private void Play(float power)
        {
            float p = Mathf.Clamp01(power);

            if (_particles != null)
            {
                var main = _particles.main;
                main.startSpeedMultiplier = Mathf.Lerp(0.6f, 1.6f, p);
                _particles.Clear();
                _particles.Play();
            }

            float target = _shockwaveMaxScale * Mathf.Lerp(0.7f, 1.3f, p);
            if (_shockwave != null) _shockwave.localScale = Vector3.zero;

            BillTween.KillTarget(this);
            BillTween.Float(0f, 1f, _shockwaveDuration, t =>
            {
                if (_shockwave != null) _shockwave.localScale = Vector3.one * (target * t);
                if (_shockwaveRenderer != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    Color c = _shockwaveColor; c.a = _shockwaveColor.a * (1f - t);
                    mpb.SetColor("_BaseColor", c);
                    _shockwaveRenderer.SetPropertyBlock(mpb);
                }
            })?.SetEase(EaseType.OutQuad)
              .SetTarget(this)
              .OnComplete(() => gameObject.ReturnToPool());
        }

        public override void OnReturnedToPool() => BillTween.KillTarget(this);

        private static Material ShockwaveMaterial(Color c)
        {
            if (_shockwaveMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                _shockwaveMat = new Material(sh) { name = "Shockwave(runtime)" };
                if (_shockwaveMat.HasProperty("_Surface")) _shockwaveMat.SetFloat("_Surface", 1f);
                _shockwaveMat.SetOverrideTag("RenderType", "Transparent");
                _shockwaveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _shockwaveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _shockwaveMat.SetInt("_ZWrite", 0);
                _shockwaveMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (_shockwaveMat.HasProperty("_BaseColor")) _shockwaveMat.SetColor("_BaseColor", c);
            return _shockwaveMat;
        }
    }
}
