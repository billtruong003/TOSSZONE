#if PHOTON_FUSION
using TossZone.Combat;
using UnityEngine;

namespace TossZone.UI
{
    /// <summary>
    /// World-space health display: 5 pip spheres in a gentle curved arc, billboard-facing the main camera.
    /// Call <see cref="Bind"/> from the owning avatar's Spawned() to link it to a <see cref="PlayerCombat"/>.
    /// Works on both player avatars and <see cref="DummyAvatar"/>. Polls the networked Health value each
    /// LateUpdate — cheap field read, visible on all clients via Fusion replication.
    /// </summary>
    public class HealthUI : MonoBehaviour
    {
        [SerializeField] private Renderer[] _pipRenderers;                              // 5, left → right
        [SerializeField] private Color _activeColor   = new Color(0.22f, 0.85f, 0.55f);
        [SerializeField] private Color _inactiveColor = new Color(0.18f, 0.18f, 0.18f, 0.45f);

        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private PlayerCombat _combat;
        private int _lastHealth = -1;

        private void Awake() => _block = new MaterialPropertyBlock();

        /// <summary>Wire this UI to a <see cref="PlayerCombat"/> instance. Call from Spawned().</summary>
        public void Bind(PlayerCombat combat)
        {
            _combat = combat;
            Refresh(PlayerCombat.MaxHealth);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (_combat == null) return;
            int h = _combat.Health;
            if (h != _lastHealth) Refresh(h);
        }

        private void Refresh(int health)
        {
            _lastHealth = health;
            if (_pipRenderers == null) return;
            for (int i = 0; i < _pipRenderers.Length; i++)
            {
                Renderer r = _pipRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(_colorId, i < health ? _activeColor : _inactiveColor);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
#endif
