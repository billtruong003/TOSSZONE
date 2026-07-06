using BillGameCore;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Pooled "poof + flash" at the hand on release (Throw_Mechanic_Spec.md §4.2) — masks the held-ball →
    /// flying-projectile swap so it reads as one continuous motion. A quick scale-up-then-fade unlit sphere;
    /// procedurally materialed so it works with no art assigned. Register under pool key <c>"releaseflash"</c>.
    /// </summary>
    public class ReleaseFlash : PooledObject
    {
        private const string PoolKey = "releaseflash";
        private const float Duration = 0.12f;

        [SerializeField] private float _maxScale = 0.22f;
        [SerializeField] private Color _color = new Color(1f, 0.95f, 0.7f, 1f);

        private MeshRenderer _mr;
        private static Material _mat;

        public static void Show(Vector3 worldPos, float power)
        {
            if (!Bill.IsReady) return;
            GameObject go = Bill.Pool.Spawn(PoolKey, worldPos, Quaternion.identity);
            if (go == null) return;
            if (go.TryGetComponent(out ReleaseFlash rf)) rf.Play(power);
        }

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            if (_mr != null) _mr.sharedMaterial = FlashMaterial(_color);
        }

        private void Play(float power)
        {
            BillTween.KillTarget(this);
            float target = _maxScale * Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(power));
            transform.localScale = Vector3.zero;
            BillTween.Float(0f, 1f, Duration, t =>
            {
                // Punch out fast, fade out — no lerp-back-down (a "poof", not a pulse).
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);   // fast start, eases into the peak
                transform.localScale = Vector3.one * (target * scaleT);
                if (_mr != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    Color c = _color; c.a = 1f - t;
                    mpb.SetColor("_BaseColor", c);
                    _mr.SetPropertyBlock(mpb);
                }
            })?.SetEase(EaseType.Linear)
              .SetTarget(this)
              .OnComplete(() => gameObject.ReturnToPool());
        }

        public override void OnReturnedToPool() => BillTween.KillTarget(this);

        private static Material FlashMaterial(Color c)
        {
            if (_mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                _mat = new Material(sh) { name = "ReleaseFlash(runtime)" };
                if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 1f);   // URP Unlit: transparent
                _mat.SetOverrideTag("RenderType", "Transparent");
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            return _mat;
        }
    }
}
