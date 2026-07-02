using BillGameCore;
using TossZone.Throwing;
using UnityEngine;
using UnityEngine.XR;

namespace TossZone.Combat
{
    /// <summary>
    /// Global, decoupled listener for combat impact juice (Throw_Mechanic_Spec.md §4.4) — ANY hit on ANY player,
    /// from ANY weapon (thrown rock, gun, bazooka, burst rain, sword), spawns a bounce number + impact burst at
    /// the hit point, and strongly rumbles the LOCAL player's hands when they're the one hit
    /// (<see cref="PlayerHitEvent.IsLocalVictim"/>). Deliberately NOT coupled to <see cref="ThrowController"/> —
    /// that only cares about the local player's own throws; this covers every hit in the arena regardless of
    /// source. Place one instance in the arena scene.
    /// </summary>
    public class CombatJuice : MonoBehaviour
    {
        [SerializeField] private Color _damageNumberColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private float _victimHapticAmplitude = 0.9f;
        [SerializeField] private float _victimHapticDuration = 0.15f;
        [Tooltip("Damage value that maps to full (1.0) impact-burst power — just a juice scale, not a balance value.")]
        [SerializeField] private int _maxDamageForFullPower = 3;

        private System.Action<PlayerHitEvent> _onHitCb;
        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        private void OnDisable()
        {
            if (!_subscribed || !Bill.IsReady || _onHitCb == null) return;
            Bill.Events.Unsubscribe<PlayerHitEvent>(_onHitCb);
            _subscribed = false;
        }

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (!Bill.IsReady || _subscribed) return;
            _onHitCb ??= OnPlayerHit;
            Bill.Events.Subscribe<PlayerHitEvent>(_onHitCb);
            _subscribed = true;
        }

        private void OnPlayerHit(PlayerHitEvent e)
        {
            float power = Mathf.Clamp01(e.Damage / (float)Mathf.Max(1, _maxDamageForFullPower));
            BounceNumber.Show(e.Damage, e.Point + Vector3.up * 0.2f, _damageNumberColor);
            ImpactBurst.Show(e.Point, power);
            if (e.IsLocalVictim) HapticBoth();
        }

        private void HapticBoth()
        {
            Pulse(XRNode.LeftHand);
            Pulse(XRNode.RightHand);
        }

        private void Pulse(XRNode node)
        {
            InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, _victimHapticAmplitude, _victimHapticDuration);
        }
    }
}
