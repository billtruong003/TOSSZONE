using Autohand;
using TossZone.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace TossZone.Throwing
{
    /// <summary>
    /// Gives the throwing hand a REAL AutoHand <see cref="Grabbable"/> as the in-hand "held ball" — so the
    /// fingers wrap it correctly (via AutoHand <b>auto-pose</b> — no GrabbablePose needed for a simple ball)
    /// instead of the flat parented sphere.
    /// The ball is a VISUAL that stays in the hand: pressing grip force-grabs it; the throw gesture
    /// (<see cref="ThrowController"/>) still fires a separate projectile WITHOUT removing it; releasing grip hides
    /// it. Set <c>ThrowController._showVisualHeldBall = false</c> so the two don't both show. Arena-scoped — place
    /// it on a scene object in <c>02_Arena</c>; it finds the local <see cref="PlayerRig"/> at runtime.
    /// </summary>
    public class ThrowBallHolder : MonoBehaviour
    {
        [Tooltip("ThrowBall prefab: an AutoHand Grabbable. Fingers wrap via auto-pose (no GrabbablePose needed).")]
        [SerializeField] private GameObject _throwBallPrefab;
        [SerializeField] private bool _rightHand = true;
        [Tooltip("Editor/dev: hold to simulate grip with no XR controller.")]
        [SerializeField] private Key _editorGripKey = Key.G;

        private PlayerRig _rig;
        private Hand _hand;
        private Grabbable _ball;
        private int _grabbableMask;
        private bool _held;
        private bool _ready;

        private void Update()
        {
            if (!_ready) { TryInit(); return; }
            if (_hand == null || PlayerRig.Local == null) { _ready = false; return; }

            bool grip = ReadGrip();
            if (grip && !_held) GrabBall();
            else if (!grip && _held) ReleaseBall();
        }

        private void TryInit()
        {
            if (_throwBallPrefab == null) return;
            _rig = PlayerRig.Local;
            if (_rig == null) return;

            Transform wrist = _rightHand ? _rig.WristR : _rig.WristL;
            if (wrist == null) return;
            _hand = wrist.GetComponent<Hand>();
            if (_hand == null) _hand = wrist.GetComponentInChildren<Hand>();
            if (_hand == null) _hand = wrist.GetComponentInParent<Hand>();
            if (_hand == null) return;

            GameObject go = Instantiate(_throwBallPrefab);
            go.name = "ThrowBall(held)";
            _ball = go.GetComponent<Grabbable>();
            if (_ball == null) { Destroy(go); return; }
            go.SetActive(false);

            _grabbableMask = LayerMask.GetMask("Grabbable");
            _ready = true;
            Debug.Log("[ThrowBallHolder] ready (hand=" + (_rightHand ? "R" : "L") + ").");
        }

        private void GrabBall()
        {
            if (_ball == null || _ball.IsHeld() || _hand.IsGrabbing()) return;

            // Place the ball a little in front of the hand so the raycast has a target (GrabbableToHand floats it in).
            Vector3 handPos = _hand.transform.position;
            _ball.transform.position = handPos + _hand.transform.forward * 0.08f;
            _ball.gameObject.SetActive(true);
            Physics.SyncTransforms();   // make the just-moved collider visible to the raycast this frame

            // Shmackle pattern (AutoGrabber.cs): raycast hand → grabbable, then Grab(hit) so AutoHand gets a real
            // grab point → auto-pose engages + gentle grab. ForceGrab (no hit) skips that. Body MUST be non-kinematic
            // or Hand.Grab early-outs (Grabbable body objectFree check).
            Vector3 dir = (_ball.transform.position - handPos).normalized;
            if (Physics.Raycast(handPos, dir, out RaycastHit hit, 0.5f, _grabbableMask, QueryTriggerInteraction.Ignore)
                && (hit.collider.gameObject == _ball.gameObject || hit.collider.transform.IsChildOf(_ball.transform)))
                _hand.Grab(hit, _ball);
            else
                _hand.ForceGrab(_ball);   // fallback if the raycast missed

            _held = true;
        }

        private void ReleaseBall()
        {
            if (_ball == null) return;
            _ball.ForceHandsRelease();
            _ball.gameObject.SetActive(false);
            _held = false;
        }

        private bool ReadGrip()
        {
            var dev = XRInputDevices.GetDeviceAtXRNode(_rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.grip, out float v) && v > 0.6f) return true;
            Keyboard kb = Keyboard.current;
            return kb != null && kb[_editorGripKey].isPressed;
        }
    }
}
