using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Procedural leg poser for the 3-point VR avatar (head + 2 wrists only — NO leg tracking, so legs are
    /// faked). Each foot has a planted ground target; as the body moves, the desired spot under the hip leads
    /// forward by the body's horizontal velocity and is snapped to the floor by a downward raycast. When a
    /// plant drifts past a threshold the foot takes a quick arced step (feet alternate, never both at once).
    /// A self-contained analytic two-bone IK (UpperLeg -> LowerLeg -> Foot) then bends the knee toward a
    /// forward hint to reach the plant. Standing still -> feet stay put (no skating); moving faster -> longer,
    /// quicker steps read as a run — no animation clips, no foot-sliding.
    ///
    /// Runs on EVERY client (owner + proxies) off the already-synced body transform, so legs cost zero extra
    /// network data. Deliberately does NOT move the hips, so it never disturbs the head/arm IK above
    /// (<see cref="AvatarArmPoser"/>). Drives only the leg bones, which nothing else touches (Animators are
    /// disabled / controller-less). Bones must be wired to Kyle's ROOT skeleton (the tree the mesh skins to),
    /// e.g. Hips/{Left|Right}Leg -> {}Calf -> {}Foot.
    /// </summary>
    [DefaultExecutionOrder(14000)]
    public class AvatarLegPoser : MonoBehaviour
    {
        [Header("Body reference (NetworkAvatar root: ground pos + facing). Empty = use parent.")]
        [SerializeField] private Transform _bodyRef;

        [Header("Leg bones (Kyle ROOT skeleton: UpperLeg -> LowerLeg -> Foot)")]
        [SerializeField] private Transform _lUpperLeg, _lLowerLeg, _lFoot;
        [SerializeField] private Transform _rUpperLeg, _rLowerLeg, _rFoot;

        [Header("Ground raycast")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("Ray starts this far above the hip and reaches this far below it.")]
        [SerializeField] private float _rayUp = 0.8f;
        [SerializeField] private float _rayDown = 2.5f;
        [Tooltip("Lift the planted foot off the ground by this (sole thickness).")]
        [SerializeField] private float _footYOffset = 0f;

        [Header("Stepping")]
        [Tooltip("Foot re-steps when its plant drifts this far (m) from the desired spot.")]
        [SerializeField] private float _stepThreshold = 0.22f;
        [Tooltip("Seconds for one step.")]
        [SerializeField] private float _stepDuration = 0.15f;
        [Tooltip("Peak height of the step arc (m).")]
        [SerializeField] private float _stepHeight = 0.12f;
        [Tooltip("Lead the foot forward by velocity * this (anticipates motion).")]
        [SerializeField] private float _velocityLead = 0.12f;

        [Header("Knee / foot")]
        [Tooltip("Direction (in body space) the knees prefer to point — forward & slightly up.")]
        [SerializeField] private Vector3 _kneeHint = new Vector3(0f, 0.2f, 1f);
        [Tooltip("Tilt the sole to match the ground normal.")]
        [SerializeField] private bool _alignToGround = true;

        private struct FootState
        {
            public Vector3 plant;     // current IK target (lifts during a step)
            public Vector3 grounded;  // where the foot actually rests when not stepping
            public bool stepping;
            public float t;           // 0..1 step progress
            public Vector3 from;
            public bool init;
        }

        private FootState _l, _r;
        private Quaternion _lFootRest = Quaternion.identity, _rFootRest = Quaternion.identity;
        private Quaternion _lLowerLegRest = Quaternion.identity, _rLowerLegRest = Quaternion.identity;
        private Vector3 _lastBodyPos;
        private bool _captured;

        private Transform Body =>
            _bodyRef != null ? _bodyRef : (transform.parent != null ? transform.parent : transform);

        private void Awake() => Capture();

        private void Capture()
        {
            Transform body = Body;
            if (_lFoot) _lFootRest = Quaternion.Inverse(body.rotation) * _lFoot.rotation;
            if (_rFoot) _rFootRest = Quaternion.Inverse(body.rotation) * _rFoot.rotation;
            // Use thigh direction as LookRotation secondary axis — world-up is near-parallel to the
            // shin (which points down) and causes roll singularity when the foot steps. The thigh
            // direction is always roughly horizontal, so it is never parallel to the shin.
            if (_lUpperLeg && _lLowerLeg && _lFoot) _lLowerLegRest = CaptureRollLeg(_lUpperLeg, _lLowerLeg, _lFoot);
            if (_rUpperLeg && _rLowerLeg && _rFoot) _rLowerLegRest = CaptureRollLeg(_rUpperLeg, _rLowerLeg, _rFoot);
            _lastBodyPos = body.position;
            _captured = true;
        }

        // Capture shin roll using thigh direction as the LookRotation "up" so the reference frame
        // matches SolveLeg (which also uses the thigh direction). Must be called at bind pose.
        private static Quaternion CaptureRollLeg(Transform upper, Transform lower, Transform foot)
        {
            Vector3 shinFwd = (foot.position - lower.position).normalized;
            if (shinFwd.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Vector3 thighDir = (lower.position - upper.position).normalized;
            Vector3 shinUp = Vector3.ProjectOnPlane(thighDir, shinFwd);
            if (shinUp.sqrMagnitude < 1e-4f) shinUp = Vector3.forward; // degenerate fallback
            else shinUp.Normalize();
            return Quaternion.Inverse(Quaternion.LookRotation(shinFwd, shinUp)) * lower.rotation;
        }

        private void LateUpdate()
        {
            if (!_captured) Capture();
            Transform body = Body;

            float dt = Time.deltaTime;
            Vector3 vel = dt > 1e-5f ? (body.position - _lastBodyPos) / dt : Vector3.zero;
            _lastBodyPos = body.position;
            vel.y = 0f;

            // Process one foot then the other; pass each the other's CURRENT stepping flag so they never
            // lift together (a foot only starts a step when its partner is grounded).
            StepAndSolve(ref _l, _lUpperLeg, _lLowerLeg, _lFoot, _lFootRest, _lLowerLegRest, body, vel, dt, _r.stepping);
            StepAndSolve(ref _r, _rUpperLeg, _rLowerLeg, _rFoot, _rFootRest, _rLowerLegRest, body, vel, dt, _l.stepping);
        }

        private void StepAndSolve(ref FootState f, Transform upper, Transform lower, Transform foot,
                                  Quaternion footRest, Quaternion lowerRest, Transform body, Vector3 vel, float dt, bool otherStepping)
        {
            if (upper == null || lower == null || foot == null) return;

            // Desired plant: under the hip (its XZ), led forward by velocity, snapped to the floor.
            Vector3 hip = upper.position;
            Vector3 desired = hip + vel * _velocityLead;
            Vector3 normal = Vector3.up;
            Vector3 rayOrigin = new Vector3(desired.x, hip.y + _rayUp, desired.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _rayUp + _rayDown, _groundMask,
                                QueryTriggerInteraction.Ignore))
            {
                desired = hit.point;
                normal = hit.normal;
            }
            else
            {
                desired.y = body.position.y; // no floor hit -> fall back to body ground height
            }
            desired.y += _footYOffset;

            if (!f.init) { f.grounded = desired; f.plant = desired; f.init = true; }

            if (f.stepping)
            {
                f.t += dt / Mathf.Max(_stepDuration, 1e-3f);
                if (f.t >= 1f)
                {
                    f.stepping = false;
                    f.grounded = desired;
                    f.plant = desired;
                }
                else
                {
                    Vector3 p = Vector3.Lerp(f.from, desired, f.t);
                    p.y += Mathf.Sin(f.t * Mathf.PI) * _stepHeight; // arc up then down
                    f.plant = p;
                }
            }
            else if (!otherStepping &&
                     (f.grounded - desired).sqrMagnitude > _stepThreshold * _stepThreshold)
            {
                f.stepping = true;
                f.t = 0f;
                f.from = f.grounded;
            }
            else
            {
                f.plant = f.grounded; // stay put -> no skating
            }

            SolveLeg(upper, lower, foot, f.plant, body, footRest, lowerRest, normal);
        }

        /// <summary>
        /// Analytic two-bone IK with explicit knee placement (mirrors AvatarArmPoser.SolveArm). Computes the
        /// knee from the bone-length triangle + a forward pole, aims the thigh at it, the shin at the target,
        /// then orients the foot to the body facing (optionally tilted to the ground normal).
        /// </summary>
        private void SolveLeg(Transform upper, Transform lower, Transform foot, Vector3 target,
                              Transform body, Quaternion footRest, Quaternion lowerRest, Vector3 groundNormal)
        {
            Vector3 a = upper.position;
            float lab = Vector3.Distance(a, lower.position);
            float lcb = Vector3.Distance(lower.position, foot.position);
            if (lab < 1e-4f || lcb < 1e-4f) return;

            Vector3 at = target - a;
            float dist = at.magnitude;
            if (dist < 1e-4f) return;
            float lat = Mathf.Clamp(dist, 1e-3f, lab + lcb - 1e-3f);
            Vector3 atDir = at / dist;

            float proj = (lab * lab - lcb * lcb + lat * lat) / (2f * lat);
            float height = Mathf.Sqrt(Mathf.Max(0f, lab * lab - proj * proj));
            Vector3 hintDir = body.TransformDirection(_kneeHint);
            Vector3 pole = Vector3.ProjectOnPlane(hintDir, atDir).normalized;
            if (pole.sqrMagnitude < 1e-4f) pole = Vector3.ProjectOnPlane(body.forward, atDir).normalized;
            if (pole.sqrMagnitude < 1e-4f) pole = Vector3.ProjectOnPlane(Vector3.right, atDir).normalized;
            Vector3 knee = a + atDir * proj + pole * height;

            upper.rotation = Quaternion.FromToRotation(lower.position - a, knee - a) * upper.rotation;
            // Stateless shin roll: use the thigh direction (upper→lower, already IK-solved above so
            // lower.position is now at the knee) as the LookRotation secondary axis. World-up causes
            // singularity because the shin points near-downward — thigh direction is always horizontal.
            Vector3 shinFwd = (target - lower.position).normalized;
            if (shinFwd.sqrMagnitude > 1e-6f)
            {
                Vector3 thighDir = (lower.position - upper.position).normalized;
                Vector3 shinUp = Vector3.ProjectOnPlane(thighDir, shinFwd);
                if (shinUp.sqrMagnitude < 1e-4f) shinUp = body.forward; // degenerate fallback
                else shinUp.Normalize();
                lower.rotation = Quaternion.LookRotation(shinFwd, shinUp) * lowerRest;
            }

            Quaternion footRot = body.rotation * footRest;
            if (_alignToGround) footRot = Quaternion.FromToRotation(Vector3.up, groundNormal) * footRot;
            foot.rotation = footRot;
        }
    }
}
