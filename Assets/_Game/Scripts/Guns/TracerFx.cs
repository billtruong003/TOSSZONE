using BillGameCore;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>
    /// Pooled hitscan tracer — a thin stretched primitive spanning muzzle -> hit point for ~60-80ms. Reset
    /// rules follow BillGameCore_Usage.md §3 (S15 lesson): never hold a `Tween` reference across frames, always
    /// `SetTarget`/`KillTarget(this)` so a pool-recycled tween can't get killed by a stale ref, and clear all
    /// state in <see cref="OnReturnedToPool"/> so a reused instance never shows the previous shot's pose.
    /// </summary>
    public class TracerFx : PooledObject
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _life = 0.07f;

        public void Init(Vector3 start, Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            transform.position = (start + end) * 0.5f;
            transform.rotation = distance > 0.001f ? Quaternion.LookRotation(end - start) : Quaternion.identity;

            if (_visual != null)
            {
                // _visual is a Cylinder primitive whose mesh-space long axis is Y, rotated 90 deg on X in the
                // prefab so that (scaled) axis visually points along this transform's forward (Z) — scale
                // must therefore stretch localScale.y, not .z, to lengthen the tracer.
                Vector3 scale = _visual.localScale;
                scale.y = distance;
                _visual.localScale = scale;
            }

            BillTween.DelayedCall(_life, ReturnToPool)?.SetTarget(this);
        }

        public override void OnReturnedToPool()
        {
            BillTween.KillTarget(this);
        }
    }
}
