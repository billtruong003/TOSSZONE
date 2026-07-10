#if PHOTON_FUSION
using BillGameCore;
using TossZone.Player;
using TossZone.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TossZone.Network
{
    /// <summary>
    /// Hold the right controller's B (secondary) button to summon a "VỀ HUB" panel fixed in world space in
    /// front of the head at the moment of the press — not attached to the rig, so it doesn't chase the
    /// player around. Release to dismiss without leaving; poke the button to actually leave. Purely local
    /// (no networked state) — leaving reuses ConnectionFlowController's existing disconnect-recovery flow
    /// (FusionNet.Shutdown() fires FusionShutdownEvent, which fades to hub + reconnects via QuickPlay),
    /// the same path an unexpected disconnect already takes.
    /// </summary>
    public class ArenaQuickMenu : MonoBehaviour
    {
        [SerializeField] private float _spawnDistance = 1.0f;
        [SerializeField] private float _spawnHeightOffset = -0.15f;
        [SerializeField] private GameObject _panel;
        [SerializeField] private PokeButton3D _button;
        [Tooltip("Optional — visible only while Phase == MatchEnd: requests a rematch (RPC to master) instead of leaving.")]
        [SerializeField] private PokeButton3D _rematchButton;

        private InputAction _menuButton;
        private bool _shown;

        private void Awake()
        {
            _menuButton = new InputAction("ArenaQuickMenu", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            _menuButton.Enable();
            if (_button != null) _button.Poked += _ => ReturnToHub();
            if (_rematchButton != null) _rematchButton.Poked += _ => RequestRematch();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            _menuButton?.Disable();
            _menuButton?.Dispose();
        }

        private void Update()
        {
            bool held = _menuButton != null && _menuButton.IsPressed();
            if (held && !_shown) Show();
            else if (!held && _shown) Hide();
        }

        private void Show()
        {
            _shown = true;
            if (_panel == null) return;
            Transform head = PlayerRig.Local != null ? PlayerRig.Local.Head : null;
            if (head == null) return;

            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = head.position + fwd * _spawnDistance;
            pos.y = head.position.y + _spawnHeightOffset;
            _panel.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));

            // Rematch only makes sense while the match is over — hide the button the rest of the time.
            if (_rematchButton != null)
            {
                TossZone.Combat.ArenaManager am = TossZone.Combat.ArenaManager.Instance;
                _rematchButton.gameObject.SetActive(
                    am != null && am.Phase == TossZone.Combat.ArenaManager.MatchPhase.MatchEnd);
            }

            _panel.SetActive(true);
        }

        private void Hide()
        {
            _shown = false;
            if (_panel != null) _panel.SetActive(false);
        }

        private void ReturnToHub()
        {
            Hide();
            FusionNet.Instance?.Shutdown();
        }

        private void RequestRematch()
        {
            Hide();
            TossZone.Combat.ArenaManager am = TossZone.Combat.ArenaManager.Instance;
            if (am != null && am.Object != null && am.Object.IsValid) am.RPC_RequestRematch();
        }
    }
}
#endif
