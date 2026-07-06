using BillGameCore;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// Pooled world-space "bounce number" (Throw_Mechanic_Spec.md §4.4) — punch-scale in, hold, rise + fade.
    /// Distinct from <see cref="TossZone.UI.RewardText"/> (money/reward feedback): this is combat-impact juice
    /// (damage dealt), punchier and shorter. Register under pool key <c>"bouncenumber"</c>. Requires a
    /// TMPro.TextMeshPro child wired to <see cref="_label"/>.
    /// </summary>
    public class BounceNumber : PooledObject
    {
        private const string PoolKey = "bouncenumber";

        [SerializeField] private TMPro.TextMeshPro _label;
        [SerializeField] private float _punchDuration = 0.12f;
        [SerializeField] private float _punchScale = 1.4f;
        [SerializeField] private float _holdAndRiseDuration = 0.55f;
        [SerializeField] private float _riseHeight = 0.4f;

        /// <summary>Show a bounce number at <paramref name="worldPos"/> (e.g. damage dealt on a hit).</summary>
        public static void Show(int amount, Vector3 worldPos, Color? color = null)
        {
            if (!Bill.IsReady) return;
            GameObject go = Bill.Pool.Spawn(PoolKey, worldPos, Quaternion.identity);
            if (go == null) return;
            if (!go.TryGetComponent(out BounceNumber bn)) return;
            if (bn._label != null)
            {
                bn._label.text = amount.ToString();
                if (color.HasValue) bn._label.color = color.Value;
            }
            bn.Play();
        }

        private void Play()
        {
            BillTween.KillTarget(this);
            if (_label != null) { Color c = _label.color; c.a = 1f; _label.color = c; }

            Vector3 start = transform.position;
            Vector3 end = start + Vector3.up * _riseHeight;
            transform.localScale = Vector3.zero;

            // Punch scale 0 -> punchScale -> 1, THEN hold+rise+fade.
            BillTween.Float(0f, 1f, _punchDuration, t =>
            {
                transform.localScale = Vector3.one * (_punchScale * t);
            })?.SetEase(EaseType.OutBack)
              .SetTarget(this)
              .OnComplete(() =>
              {
                  transform.localScale = Vector3.one;
                  BillTween.Float(0f, 1f, _holdAndRiseDuration, t =>
                  {
                      transform.position = Vector3.Lerp(start, end, t);
                      if (_label != null)
                      {
                          Color c = _label.color;
                          c.a = 1f - Mathf.Pow(t, 2f);
                          _label.color = c;
                      }
                  })?.SetEase(EaseType.Linear)
                    .SetTarget(this)
                    .OnComplete(() => gameObject.ReturnToPool());
              });
        }

        public override void OnReturnedToPool() => BillTween.KillTarget(this);
    }
}
