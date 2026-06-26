using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Custom procedural poser for the humanoid avatar (SpaceRobotKyle) driven by the three synced tracking
    /// nodes (head + both wrists). Self-written IK (kept over Animation Rigging on purpose, so it stays fully
    /// controllable). The head bone follows the head node; each arm uses analytic two-bone IK with EXPLICIT
    /// elbow placement toward its wrist node. Bind-relative rotation offsets are captured at start (head + both
    /// hands) so the model's head axis and PALM orientation stay correct — the avatar palm faces the same way as
    /// the controller hand, avoiding a flipped wrist. Runs in LateUpdate (high order) so it owns the final pose;
    /// disable the model's Animator so it doesn't fight. (Legs will extend this same custom approach.)
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public class KyleAvatarPoser : MonoBehaviour
    {
        [Header("Synced tracking nodes (NetworkAvatar Head / WristL / WristR)")]
        [SerializeField] private Transform _headNode;
        [SerializeField] private Transform _wristL;
        [SerializeField] private Transform _wristR;

        [Header("Model bones (from Kyle's root skeleton)")]
        [SerializeField] private Transform _headBone;
        [SerializeField] private Transform _lUpperArm, _lLowerArm, _lHand;
        [SerializeField] private Transform _rUpperArm, _rLowerArm, _rHand;

        [Header("Tuning (fine-tune on top of the captured bind offsets)")]
        [Tooltip("World-space direction the elbows prefer to point (down & slightly back).")]
        [SerializeField] private Vector3 _elbowHint = new Vector3(0f, -1f, -0.4f);
        [SerializeField] private Vector3 _headRotOffset;
        [Tooltip("Roll the palm if it still reads flipped after the captured bind offset.")]
        [SerializeField] private Vector3 _handRotOffsetL;
        [SerializeField] private Vector3 _handRotOffsetR;

        // Bind-relative offsets captured at rest: bone rotation expressed in its driving node's space, so applying
        // node.rotation * offset preserves the bone's axis (head) and palm (hands) as the node rotates.
        private Quaternion _headRest = Quaternion.identity, _lHandRest = Quaternion.identity, _rHandRest = Quaternion.identity;
        private bool _captured;

        private void Awake() => Capture();

        private void Capture()
        {
            if (_headBone && _headNode) _headRest = Quaternion.Inverse(_headNode.rotation) * _headBone.rotation;
            if (_lHand && _wristL) _lHandRest = Quaternion.Inverse(_wristL.rotation) * _lHand.rotation;
            if (_rHand && _wristR) _rHandRest = Quaternion.Inverse(_wristR.rotation) * _rHand.rotation;
            _captured = true;
        }

        private void LateUpdate()
        {
            if (!_captured) Capture();

            if (_headBone && _headNode)
                _headBone.rotation = _headNode.rotation * _headRest * Quaternion.Euler(_headRotOffset);

            SolveArm(_lUpperArm, _lLowerArm, _lHand, _wristL, _lHandRest * Quaternion.Euler(_handRotOffsetL));
            SolveArm(_rUpperArm, _rLowerArm, _rHand, _wristR, _rHandRest * Quaternion.Euler(_handRotOffsetR));
        }

        /// <summary>
        /// Analytic two-bone IK with EXPLICIT elbow placement: compute where the elbow sits from the bone-length
        /// triangle + the hint pole, aim the upper bone at it, aim the forearm at the target. Deterministic — the
        /// arm never contorts, regardless of the model's bone-axis convention.
        /// </summary>
        private void SolveArm(Transform upper, Transform lower, Transform hand, Transform target, Quaternion handRot)
        {
            if (upper == null || lower == null || hand == null || target == null) return;

            Vector3 a = upper.position;
            float lab = Vector3.Distance(a, lower.position);
            float lcb = Vector3.Distance(lower.position, hand.position);
            if (lab < 1e-4f || lcb < 1e-4f) return;

            Vector3 at = target.position - a;
            float dist = at.magnitude;
            if (dist < 1e-4f) return;
            float lat = Mathf.Clamp(dist, 1e-3f, lab + lcb - 1e-3f);
            Vector3 atDir = at / dist;

            float proj = (lab * lab - lcb * lcb + lat * lat) / (2f * lat);
            float height = Mathf.Sqrt(Mathf.Max(0f, lab * lab - proj * proj));
            Vector3 hint = _elbowHint.sqrMagnitude > 1e-6f ? _elbowHint.normalized : Vector3.down;
            Vector3 pole = Vector3.ProjectOnPlane(hint, atDir).normalized;
            if (pole.sqrMagnitude < 1e-4f) pole = Vector3.ProjectOnPlane(Vector3.forward, atDir).normalized;
            if (pole.sqrMagnitude < 1e-4f) pole = Vector3.ProjectOnPlane(Vector3.right, atDir).normalized;
            Vector3 elbow = a + atDir * proj + pole * height;

            upper.rotation = Quaternion.FromToRotation(lower.position - a, elbow - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target.position - lower.position) * lower.rotation;

            // Palm stays aligned with the controller hand via the captured bind offset (no wrist flip).
            hand.rotation = target.rotation * handRot;
        }
    }
}
