using BillGameCore;
using UnityEngine;

namespace TossZone.UI
{
    /// <summary>
    /// Pooled world-space floating text (damage numbers, money gain). Spawns at a world position, rises with
    /// <see cref="BillTween"/> and fades out, then returns to the pool.
    /// Register the prefab under key <c>"rewardtext"</c> in <see cref="BillBootstrapConfig.defaultPools"/>.
    /// Requires a TMPro.TextMeshPro child wired to <see cref="_label"/>.
    /// </summary>
    public class RewardText : PooledObject
    {
        private const string PoolKey = "rewardtext";

        [SerializeField] private TMPro.TextMeshPro _label;
        [SerializeField] private float _duration = 1.0f;
        [SerializeField] private float _riseHeight = 0.6f;

        private Tween _tween;

        /// <summary>Show floating text at <paramref name="worldPos"/> from the pool.</summary>
        public static void Show(string text, Vector3 worldPos, Color? color = null)
        {
            if (!Bill.IsReady) return;
            GameObject go = Bill.Pool.Spawn(PoolKey, worldPos, Quaternion.identity);
            if (go == null) return;
            if (!go.TryGetComponent(out RewardText rt)) return;
            rt._label.text = text;
            if (color.HasValue) rt._label.color = color.Value;
        }

        public override void OnSpawnedFromPool()
        {
            _tween?.Kill();

            if (_label != null) { Color c = _label.color; c.a = 1f; _label.color = c; }

            Vector3 start = transform.position;
            Vector3 end   = start + Vector3.up * _riseHeight;

            _tween = BillTween.Float(0f, 1f, _duration, t =>
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
        }

        public override void OnReturnedToPool() => _tween?.Kill();
    }
}
