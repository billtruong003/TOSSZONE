using System.Text;
using Autohand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace TossZone.Player
{
    /// <summary>
    /// Drop-in locomotion driver that REPLACES AutoHand's legacy <c>XRHandPlayerControllerLink</c>. On the
    /// real Quest (OpenXR), the legacy <c>UnityEngine.XR.InputDevices</c> <c>primary2DAxis</c> (a Vector2
    /// feature) reads (0,0) while button/float features still work — so AutoHand's thumbstick locomotion dies
    /// even though grab works. This reads BOTH the new Input System thumbstick and the legacy axis, drives
    /// <see cref="AutoHandPlayer"/> with whichever has signal, and (optionally) shows a world-space HUD in the
    /// headset comparing the two so we can confirm on device. Self-disables AutoHand's legacy link so it can't
    /// zero our movement. Turn <see cref="_showDebug"/> off once locomotion is verified.
    /// </summary>
    public class TossLocomotionInput : MonoBehaviour
    {
        [SerializeField] private AutoHandPlayer _player;
        [Tooltip("World-space readout in the headset (raw NEW vs LEGACY axis). Turn off once verified.")]
        [SerializeField] private bool _showDebug = true;
        [Tooltip("DebugHud: a PRE-PLACED instance in the scene (used as-is — you position it) OR the DebugHud prefab (auto-instantiated + attached to the head). Empty = no HUD.")]
        [SerializeField] private TossZone.UI.DebugHud _hudRef;

        private TossZone.UI.DebugHud _hud;
        private Rigidbody _body;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            if (_player == null) _player = GetComponentInParent<AutoHandPlayer>();
            if (_player == null) _player = FindFirstObjectByType<AutoHandPlayer>();

            // Kill AutoHand's legacy thumbstick link so it doesn't fight us (it calls Move(0) every FixedUpdate).
            if (_player != null)
            {
                MonoBehaviour[] comps = _player.GetComponents<MonoBehaviour>();
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i] != null && comps[i].GetType().Name == "XRHandPlayerControllerLink")
                        comps[i].enabled = false;
                _body = _player.GetComponent<Rigidbody>();
            }

            if (_showDebug) CreateHud();
            Debug.Log("[Loco] TossLocomotionInput ready (player=" + (_player != null) + ").");
        }

        private void Update()
        {
            Vector2 moveNew = ReadNew(true);
            Vector2 turnNew = ReadNew(false);
            Vector2 moveLeg = ReadLegacy(true, out bool lValid, out string lName);
            Vector2 turnLeg = ReadLegacy(false, out bool rValid, out string rName);

            Vector2 move = Stronger(moveNew, moveLeg);
            Vector2 turn = Stronger(turnNew, turnLeg);

            if (_player != null)
            {
                _player.Move(move);
                _player.Turn(turn.x);
            }

            if (_hud != null)
            {
                _sb.Clear();
                _sb.Append("LOCO DEBUG\n");
                _sb.Append("L ").Append(lValid ? "ok " : "INV ").Append(lName).Append('\n');
                _sb.Append("R ").Append(rValid ? "ok " : "INV ").Append(rName).Append('\n');
                _sb.Append("MOVE new").Append(F(moveNew)).Append(" leg").Append(F(moveLeg)).Append('\n');
                _sb.Append("TURN new").Append(F(turnNew)).Append(" leg").Append(F(turnLeg)).Append('\n');
                _sb.Append("drive").Append(F(move)).Append(" plyr=").Append(_player != null);
                _sb.Append(" grnd=").Append(_player != null && _player.IsGrounded());
                _sb.Append(" vel=").Append(_body != null ? _body.linearVelocity.magnitude.ToString("0.0") : "?");
                if (_player != null)
                {
                    float gDist = -1f; int gLyr = -1;
                    if (Physics.Raycast(_player.transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit gh, 20f, ~0, QueryTriggerInteraction.Ignore))
                    { gDist = gh.distance; gLyr = gh.collider.gameObject.layer; }
                    bool myG = Physics.SphereCast(_player.transform.position + Vector3.up * 0.6f, 0.2f, Vector3.down, out RaycastHit sgh, 1.2f, _player.groundLayerMask, QueryTriggerInteraction.Ignore);
                    _sb.Append("\ny=").Append(_player.transform.position.y.ToString("0.00"))
                       .Append(" gDist=").Append(gDist.ToString("0.0")).Append(" gLyr=").Append(gLyr)
                       .Append(" myG=").Append(myG ? "Y" : "N");
                    _sb.Append("\nbody=").Append(_player.transform.position.x.ToString("0.0")).Append(",").Append(_player.transform.position.z.ToString("0.0"));
                    if (_player.trackingContainer != null)
                        _sb.Append(" cam=").Append(_player.trackingContainer.position.x.ToString("0.0")).Append(",").Append(_player.trackingContainer.position.z.ToString("0.0"));
                }
                _hud.SetText(_sb.ToString());
            }
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            _player.Move(Stronger(ReadNew(true), ReadLegacy(true, out _, out _)));
        }

        // ── input readers ────────────────────────────────────────────────────────

        /// <summary>New Input System: find the XR controller by hand usage, read its 2D thumbstick.</summary>
        private static Vector2 ReadNew(bool left)
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null || !d.added) continue;

                bool match = false;
                foreach (var u in d.usages)
                {
                    if (left && u == CommonUsages.LeftHand) { match = true; break; }
                    if (!left && u == CommonUsages.RightHand) { match = true; break; }
                }
                if (!match) continue;

                Vector2Control c = d.TryGetChildControl<Vector2Control>("thumbstick")
                                   ?? d.TryGetChildControl<Vector2Control>("primary2DAxis")
                                   ?? d.TryGetChildControl<Vector2Control>("joystick");
                if (c != null) return c.ReadValue();
            }
            return Vector2.zero;
        }

        /// <summary>Legacy UnityEngine.XR path (what AutoHand used) — for the on-device comparison.</summary>
        private static Vector2 ReadLegacy(bool left, out bool valid, out string devName)
        {
            var dev = XRInputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            valid = dev.isValid;
            devName = dev.isValid ? dev.name : "(invalid)";
            Vector2 v = Vector2.zero;
            if (dev.isValid) dev.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out v);
            return v;
        }

        private static Vector2 Stronger(Vector2 a, Vector2 b) => a.sqrMagnitude >= b.sqrMagnitude ? a : b;

        private static string F(Vector2 v) => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + ")";

        // ── debug HUD ────────────────────────────────────────────────────────────

        private void CreateHud()
        {
            if (_hud != null || _hudRef == null) return;
            if (_hudRef.gameObject.scene.IsValid())   // a scene instance → pre-placed, use as-is (you positioned it)
            {
                _hud = _hudRef;
                return;
            }
            // _hudRef is a prefab asset → instantiate + attach to the head
            Transform head = PlayerRig.Local != null ? PlayerRig.Local.Head : null;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            GameObject go = Instantiate(_hudRef.gameObject);
            go.name = "LocoDebugHUD";
            _hud = go.GetComponent<TossZone.UI.DebugHud>();
            if (_hud != null) _hud.AttachTo(head);
        }
    }
}
