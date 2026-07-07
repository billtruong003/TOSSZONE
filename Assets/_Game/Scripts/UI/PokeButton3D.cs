using System.Collections.Generic;
using Autohand;
using BillGameCore;
using BillInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace TossZone.UI
{
    /// <summary>
    /// A physical 3D button for VR (T18): fires when an AutoHand <see cref="Hand"/> collider (fingertip/palm)
    /// enters this object's trigger collider — or, with <see cref="_requireGrip"/> on, only while a hand is
    /// inside AND squeezing grip (the "grab the hologram" gesture). Shared by the wrist weapon selector (T18)
    /// and the training-range buttons (T25). A cooldown debounces double-pokes; a haptic tick on the pressing
    /// hand + a scale pulse confirm the press. Set <see cref="Interactable"/> false to reject presses
    /// (e.g. hologram not affordable) — the button stays visible but won't fire.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PokeButton3D : MonoBehaviour
    {
        /// <summary>Which hands may press this button. The wrist selector (mounted on the LEFT wrist) accepts
        /// RightOnly so the host arm's own colliders can never press its buttons.</summary>
        public enum AcceptedHands { Any, LeftOnly, RightOnly }

        [Tooltip("false = fire on touch (poke button). true = fire only while a hand is inside AND squeezing grip (grab gesture).")]
        [SerializeField] private bool _requireGrip;
        [SerializeField] private AcceptedHands _acceptedHands = AcceptedHands.Any;
        [SerializeField] private float _cooldown = 0.4f;
        [SerializeField] private float _hapticAmplitude = 0.35f;
        [Tooltip("Optional designer hook (training-range scene buttons). Code subscribers use the Poked event.")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onPoked;

        /// <summary>Fired once per accepted press; argument = the pressing hand.</summary>
        public event System.Action<Hand> Poked;

        /// <summary>Gate for presses (default true). The wrist selector turns this off while the viewed
        /// weapon is locked/unaffordable.</summary>
        public bool Interactable { get; set; } = true;

        private readonly List<Hand> _handsInside = new List<Hand>(2);
        private float _readyTime;
        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        private void OnDisable() => _handsInside.Clear();

        private void OnTriggerEnter(Collider other)
        {
            Hand hand = other.GetComponentInParent<Hand>();
            if (hand == null || !Accepts(hand)) return;
            if (_handsInside.Contains(hand)) return;   // debounce: N finger colliders = 1 press
            _handsInside.Add(hand);
            if (!_requireGrip) TryFire(hand);
        }

        private bool Accepts(Hand hand)
        {
            if (_acceptedHands == AcceptedHands.LeftOnly) return hand.left;
            if (_acceptedHands == AcceptedHands.RightOnly) return !hand.left;
            return true;
        }

        private void OnTriggerExit(Collider other)
        {
            Hand hand = other.GetComponentInParent<Hand>();
            if (hand != null) _handsInside.Remove(hand);
        }

        private void Update()
        {
            if (!_requireGrip || _handsInside.Count == 0) return;
            for (int i = _handsInside.Count - 1; i >= 0; i--)
            {
                Hand hand = _handsInside[i];
                if (hand == null || !hand.gameObject.activeInHierarchy) { _handsInside.RemoveAt(i); continue; }
                if (GripPressed(hand)) { TryFire(hand); return; }
            }
        }

        private void TryFire(Hand hand)
        {
            if (!Interactable || Time.time < _readyTime) return;
            _readyTime = Time.time + _cooldown;
            if (hand != null) Haptic(hand);
            PulseScale();
            Poked?.Invoke(hand);
            _onPoked?.Invoke();
        }

        [BillButton("Simulate Poke (Play mode)")]
        public void Debug_SimulatePoke()
        {
            if (!Application.isPlaying) { Debug.Log("[PokeButton3D] Chỉ mô phỏng được lúc Play."); return; }
            if (!Interactable) { Debug.Log("[PokeButton3D] Interactable=false — bỏ qua."); return; }
            if (Time.time < _readyTime) { Debug.Log("[PokeButton3D] Đang cooldown."); return; }
            TryFire(null);
        }

        private static bool GripPressed(Hand hand)
        {
            var dev = XRInputDevices.GetDeviceAtXRNode(hand.left ? XRNode.LeftHand : XRNode.RightHand);
            if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.grip, out float v) && v > 0.6f) return true;
#if UNITY_EDITOR
            // Mirrors WeaponHolder's editor grip fallback so the grab gesture is testable without a headset.
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.gKey.isPressed) return true;
#endif
            return false;
        }

        private void Haptic(Hand hand)
        {
            if (_hapticAmplitude <= 0f) return;
            var dev = XRInputDevices.GetDeviceAtXRNode(hand.left ? XRNode.LeftHand : XRNode.RightHand);
            if (dev.isValid && dev.TryGetHapticCapabilities(out UnityEngine.XR.HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, _hapticAmplitude, 0.05f);
        }

        private void PulseScale()
        {
            BillTween.KillTarget(transform);
            transform.localScale = _baseScale;
            BillTween.Float(1f, 0.85f, 0.07f, v => transform.localScale = _baseScale * v)
                ?.SetEase(EaseType.OutQuad).SetLoops(1, LoopType.Yoyo).SetTarget(transform);
        }
    }
}
