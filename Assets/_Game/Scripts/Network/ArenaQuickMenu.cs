#if PHOTON_FUSION
using BillGameCore;
using TMPro;
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

        private InputAction _menuButton;
        private GameObject _panel;
        private bool _shown;

        private void Awake()
        {
            _menuButton = new InputAction("ArenaQuickMenu", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            _menuButton.Enable();
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
            Transform head = PlayerRig.Local != null ? PlayerRig.Local.Head : null;
            if (head == null) return;

            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = head.position + fwd * _spawnDistance;
            pos.y = head.position.y + _spawnHeightOffset;
            Build(pos, Quaternion.LookRotation(fwd));
        }

        private void Hide()
        {
            _shown = false;
            if (_panel != null) Destroy(_panel);
            _panel = null;
        }

        private void Build(Vector3 pos, Quaternion rot)
        {
            _panel = new GameObject("[ArenaQuickMenu]");
            _panel.transform.SetPositionAndRotation(pos, rot);

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(_panel.transform, false);
            board.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            board.transform.localScale = new Vector3(0.4f, 0.22f, 0.02f);
            board.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { color = new Color(0.10f, 0.12f, 0.18f) };

            GameObject btnGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnGo.name = "Btn_VeHub";
            btnGo.transform.SetParent(_panel.transform, false);
            btnGo.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            btnGo.transform.localScale = new Vector3(0.3f, 0.1f, 0.05f);
            var col = btnGo.GetComponent<BoxCollider>();
            col.isTrigger = true;
            btnGo.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { color = new Color(0.48f, 0.20f, 0.18f) };

            var poke = btnGo.AddComponent<PokeButton3D>();
            poke.Poked += _ => ReturnToHub();

            var label = new GameObject("Label_VỀ HUB").AddComponent<TextMeshPro>();
            label.transform.SetParent(btnGo.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            label.transform.localScale = new Vector3(1f / btnGo.transform.localScale.x, 1f / btnGo.transform.localScale.y, 1f / btnGo.transform.localScale.z);
            label.text = "VỀ HUB";
            label.fontSize = 0.3f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void ReturnToHub()
        {
            Hide();
            FusionNet.Instance?.Shutdown();
        }
    }
}
#endif
