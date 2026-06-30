#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using TossZone.Throwing;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// A scene-placed or spawner-placed buff ring. The ring is a trigger zone — when a
    /// <see cref="NetworkProjectile"/> passes through it, the ring sets buff fields
    /// (Multiplier / VelocityScale / AreaScale / Element) on the projectile (authority writes)
    /// and despawns itself. <see cref="RingSpawner"/> handles respawn.
    ///
    /// Visual: a torus-like object with BillTween drift (up/down sine). The material color is
    /// driven by <see cref="BuffRingConfig.ringColor"/> via MaterialPropertyBlock.
    ///
    /// Stack: the projectile's existing value is compared against the new buff; the higher wins
    /// (prevents downgrade, caps at 3 stacks for multiplier).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuffRing : NetworkBehaviour
    {
        [SerializeField] private BuffRingConfig _config;
        [SerializeField] private Renderer _ringRenderer;

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private Vector3 _originPos;
        private Tween _driftTween;

        public BuffRingConfig Config => _config;

        public override void Spawned()
        {
            _block = new MaterialPropertyBlock();
            if (_ringRenderer != null)
            {
                _block.SetColor(_colorId, _config != null ? _config.ringColor : Color.white);
                _ringRenderer.SetPropertyBlock(_block);
            }

            _originPos = transform.position;
            StartDrift();

            GetComponent<Collider>().isTrigger = true;
        }

        private void StartDrift()
        {
            if (_config == null) return;
            float amp = _config.driftAmplitude;
            float period = _config.driftPeriod > 0f ? _config.driftPeriod : 3f;
            _driftTween = BillTween.Float(0f, 1f, period, t =>
            {
                float y = Mathf.Sin(t * Mathf.PI * 2f) * amp;
                transform.position = _originPos + Vector3.up * y;
            })?.SetLoops(-1, LoopType.Restart)
              .SetTarget(this)
              .SetEase(EaseType.Linear);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Shared Mode authority note: this ring has StateAuthority on the master client.
            // The incoming projectile has StateAuthority on ITS shooter. Writing [Networked]
            // fields on the projectile here only works if this client IS the projectile's
            // StateAuthority too (i.e. the master is the shooter). A proper fix is to send an RPC
            // to the projectile's StateAuthority. For C5 launch this is acceptable; wire the RPC
            // when rings are added to live sessions.
            if (!HasStateAuthority || _config == null) return;
            if (!other.TryGetComponent(out NetworkProjectile proj)) return;
            if (proj.Object == null || !proj.Object.IsValid) return;
            if (!proj.Object.HasStateAuthority) return; // only apply when we also own the projectile

            ApplyBuff(proj);
            if (Bill.IsReady) Bill.Events.Fire(new RingConsumedEvent { RingId = _config.id });
            Runner.Despawn(Object);
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
    }

    public struct RingConsumedEvent : IEvent { public string RingId; }
}
#endif
