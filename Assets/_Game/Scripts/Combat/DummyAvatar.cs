#if PHOTON_FUSION
using Fusion;
using TossZone.UI;
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Stationary training dummy for solo hit testing. Place as a SCENE NetworkObject in the Arena —
    /// Fusion (Shared Mode) assigns StateAuthority to the master client automatically.
    ///
    /// Has a <see cref="PlayerCombat"/> + trigger Hitbox collider on layer Hittable so
    /// <see cref="TossZone.Throwing.NetworkProjectile"/> registers damage. Auto-respawns after
    /// <see cref="_respawnDelay"/> seconds once health reaches 0. Visual tints grey while dead.
    /// </summary>
    public class DummyAvatar : NetworkBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Renderer[] _bodyRenderers;
        [SerializeField] private Color _aliveColor = new Color(0.78f, 0.29f, 0.10f);
        [SerializeField] private Color _deadColor  = new Color(0.22f, 0.22f, 0.22f);

        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 3f;

        [Networked] private TickTimer RespawnTimer { get; set; }

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private PlayerCombat _combat;
        private bool _wasDeadLastRender;

        public override void Spawned()
        {
            _combat  = GetComponent<PlayerCombat>();
            _block   = new MaterialPropertyBlock();

            if (_combat != null) _combat.IsPlayer = false;   // excluded from win-condition alive count

            HealthUI healthUI = GetComponentInChildren<HealthUI>();
            if (healthUI != null && _combat != null) healthUI.Bind(_combat);

            // Sync cached dead-state so Render() diffs correctly from the first frame.
            _wasDeadLastRender = _combat != null && _combat.Health <= 0;
            SetColor(_wasDeadLastRender ? _deadColor : _aliveColor);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _combat == null) return;

            bool dead = _combat.Health <= 0;

            // Start respawn countdown the moment health hits 0.
            if (dead && RespawnTimer.ExpiredOrNotRunning(Runner))
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, _respawnDelay);

            // Timer elapsed → restore health and clear the timer.
            if (RespawnTimer.Expired(Runner))
            {
                RespawnTimer = default;
                _combat.ResetForRound();
            }
        }

        public override void Render()
        {
            if (_combat == null) return;
            bool dead = _combat.Health <= 0;
            if (dead == _wasDeadLastRender) return;
            _wasDeadLastRender = dead;
            SetColor(dead ? _deadColor : _aliveColor);
        }

        private void SetColor(Color c)
        {
            if (_bodyRenderers == null) return;
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                Renderer r = _bodyRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(_colorId, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
#endif
