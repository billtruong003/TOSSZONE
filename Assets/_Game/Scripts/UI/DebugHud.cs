using TMPro;
using UnityEngine;

namespace TossZone.UI
{
    /// <summary>
    /// Reusable world-space debug HUD (a PREFAB, never code-spawned). A curved <b>TextMeshPro (3D)</b> readout
    /// (+ optional curved UI panel behind it) that bends toward the viewer via the <c>TOSSZONE/Curved *</c>
    /// shaders for VR comfort. Instantiate the prefab and call <see cref="AttachTo"/> with the head transform;
    /// distance + angle are tunable on the prefab so callers don't hard-code placement. Set text with
    /// <see cref="SetText"/>.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _text;

        [Header("Placement (relative to the head it attaches to)")]
        [Tooltip("Metres in front of the head.")]
        [SerializeField] private float _distance = 0.6f;
        [Tooltip("Left/right angle (deg).")]
        [SerializeField] private float _yaw = 0f;
        [Tooltip("Up/down angle (deg) — negative tilts the HUD down into comfortable view.")]
        [SerializeField] private float _pitch = -12f;
        [Tooltip("Extra local offset after the distance/angle placement.")]
        [SerializeField] private Vector3 _localOffset = Vector3.zero;

        public void SetText(string s)
        {
            if (_text != null) _text.text = s;
        }

        /// <summary>Parent under <paramref name="head"/> and place it in front, facing the user, at the tuned
        /// distance/angle.</summary>
        public void AttachTo(Transform head)
        {
            if (head == null) return;
            transform.SetParent(head, false);
            transform.localPosition = new Vector3(_localOffset.x, _localOffset.y, _distance + _localOffset.z);
            transform.localRotation = Quaternion.Euler(_pitch, 180f + _yaw, 0f);   // 180 = face back toward the user
        }
    }
}
