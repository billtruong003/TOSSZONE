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
    /// Locomotion driver that REPLACES AutoHand's legacy <c>XRHandPlayerControllerLink</c>. On Quest (OpenXR)
    /// the legacy <c>UnityEngine.XR.InputDevices</c> <c>primary2DAxis</c> (a Vector2 feature) reads (0,0) while
    /// button/float features still work — so AutoHand's thumbstick locomotion dies even though grab works. This
    /// reads the new Input System thumbstick (with a legacy fallback for non-OpenXR runtimes), drives
    /// <see cref="AutoHandPlayer"/> with whichever has signal, and self-disables AutoHand's legacy link so it
    /// can't zero our movement.
    /// </summary>
    public class TossLocomotionInput : MonoBehaviour
    {
        [SerializeField] private AutoHandPlayer _player;

        [Header("Dash (right stick click → horizontal burst)")]
        [SerializeField] private float _dashStrength = 3.5f;
        [SerializeField] private float _dashDuration = 0.18f;
        [SerializeField] private float _dashCooldown  = 0.8f;

        private float _dashEnd;
        private float _dashCooldownEnd;
        private Vector2 _dashDir;

        private bool _rightStickClickLast;
        private bool _jumpButtonLast;

        private void Start()
        {
            if (_player == null) _player = GetComponentInParent<AutoHandPlayer>();
            if (_player == null) _player = FindFirstObjectByType<AutoHandPlayer>();
            if (_player == null) return;

            // Kill AutoHand's legacy thumbstick link so it doesn't fight us (it calls Move(0) every FixedUpdate).
            MonoBehaviour[] comps = _player.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
                if (comps[i] != null && comps[i].GetType().Name == "XRHandPlayerControllerLink")
                    comps[i].enabled = false;
        }

        private void Update()
        {
            if (_player == null) return;

            HandleDashInput();
            HandleJumpInput();

            Vector2 move = Time.time < _dashEnd ? _dashDir * _dashStrength : ReadMove();
            _player.Move(move);
            _player.Turn(ReadTurn().x);
        }

        private void FixedUpdate()
        {
            if (_player == null) return;
            Vector2 move = Time.time < _dashEnd ? _dashDir * _dashStrength : ReadMove();
            _player.Move(move);
        }

        private void HandleDashInput()
        {
            // Right thumbstick click (stick button) → dash in current move direction.
            var dev = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool click = dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.secondary2DAxisClick, out bool b) && b;
            if (click && !_rightStickClickLast && Time.time >= _dashCooldownEnd)
            {
                Vector2 dir = ReadMove();
                if (dir.sqrMagnitude < 0.1f) dir = Vector2.up; // forward if stick is neutral
                _dashDir = dir.normalized;
                _dashEnd = Time.time + _dashDuration;
                _dashCooldownEnd = Time.time + _dashCooldown;
            }
            _rightStickClickLast = click;
        }

        private void HandleJumpInput()
        {
            // A button (right hand primaryButton) → jump.
            var dev = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool pressed = dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool b) && b;
            if (pressed && !_jumpButtonLast) _player.Jump();
            _jumpButtonLast = pressed;
        }

        private static Vector2 ReadMove() => Stronger(ReadNew(true), ReadLegacy(true));
        private static Vector2 ReadTurn() => Stronger(ReadNew(false), ReadLegacy(false));

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

        /// <summary>Legacy UnityEngine.XR path — fallback for non-OpenXR runtimes (reads (0,0) on OpenXR).</summary>
        private static Vector2 ReadLegacy(bool left)
        {
            var dev = XRInputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            return dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 v) ? v : Vector2.zero;
        }

        private static Vector2 Stronger(Vector2 a, Vector2 b) => a.sqrMagnitude >= b.sqrMagnitude ? a : b;
    }
}
