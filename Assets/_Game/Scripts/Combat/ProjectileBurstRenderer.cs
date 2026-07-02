#if PHOTON_FUSION
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Local, GPU-instanced renderer for <see cref="ProjectileBurstSystem"/> bursts. Every frame it derives each
    /// live projectile's position from the analytic flight formula and "stamps" them all with
    /// <see cref="Graphics.DrawMeshInstanced"/> — one draw call per 1023 projectiles instead of one renderer each.
    /// Purely visual and local: reads the replicated burst data, touches no network state. MVP uses the
    /// non-indirect path (≤1023/batch, CPU-built matrices).
    ///
    /// T8: adds a simple distance cull vs <see cref="Camera.main"/> (XR head position) — cheap, stereo-safe
    /// (distance is eye-independent, unlike a single-eye frustum test which could cull something visible in the
    /// other eye and cause visible popping). Full Graphics.RenderMeshIndirect + compute-shader GPU culling is
    /// deferred: it's only needed once burst counts genuinely exceed the current ≤1023/batch MVP path (see
    /// Docs/Burst_Projectile_System_Design.md), and its single-pass-stereo eye-index correctness can only be
    /// trusted on real Quest hardware ("không tin sim" per the design doc) — build it in a session with a
    /// headset attached so it can be verified immediately instead of blind.
    /// </summary>
    public class ProjectileBurstRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;
        [SerializeField] private float _scale = 0.12f;

        [Tooltip("Projectiles farther than this from the camera are skipped (0 = no distance cull).")]
        [SerializeField] private float _maxRenderDistance = 80f;

        private const int BatchMax = 1023;
        private readonly Matrix4x4[] _batch = new Matrix4x4[BatchMax];
        private Material _runtimeMat;

        /// <summary>Live+visible projectiles actually stamped last frame (post dead-mask, post distance cull).</summary>
        public int LastRenderedCount { get; private set; }
        /// <summary>Live projectiles skipped last frame purely by the distance cull (diagnostics/verification).</summary>
        public int LastCulledCount { get; private set; }

        private void Awake()
        {
            if (_mesh == null)
            {
                // Built-in sphere mesh without spawning a GameObject.
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            if (_material == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _runtimeMat = new Material(sh) { name = "BurstProjectile(runtime)", enableInstancing = true };
                if (_runtimeMat.HasProperty("_BaseColor")) _runtimeMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f));
                _material = _runtimeMat;
            }
            else
            {
                _material.enableInstancing = true;
            }
        }

        private void LateUpdate()
        {
            ProjectileBurstSystem sys = ProjectileBurstSystem.Instance;
            if (sys == null || sys.Object == null || !sys.Object.IsValid || _mesh == null || _material == null) return;

            Vector3 s = Vector3.one * _scale;
            var bursts = sys.ActiveBursts;
            int n = 0;
            int rendered = 0;
            int culled = 0;

            Camera cam = Camera.main;
            bool distanceCull = _maxRenderDistance > 0f && cam != null;
            Vector3 camPos = distanceCull ? cam.transform.position : Vector3.zero;
            float maxSqr = _maxRenderDistance * _maxRenderDistance;

            for (int bi = 0; bi < bursts.Length; bi++)
            {
                ProjectileBurstSystem.Burst b = bursts.Get(bi);
                if (!b.Active) continue;
                float t = sys.BurstElapsed(b);
                int count = Mathf.Min(b.Count, ProjectileBurstSystem.MaxProjectilesPerBurst);

                for (int i = 0; i < count; i++)
                {
                    if (ProjectileBurstSystem.IsDead(b, i)) continue;   // hit/caught/deflected — stop drawing it
                    Vector3 pos = sys.ProjectilePosition(b, i, t);
                    if (distanceCull && (pos - camPos).sqrMagnitude > maxSqr) { culled++; continue; }
                    rendered++;
                    _batch[n++] = Matrix4x4.TRS(pos, Quaternion.identity, s);
                    if (n == BatchMax)
                    {
                        Graphics.DrawMeshInstanced(_mesh, 0, _material, _batch, n);
                        n = 0;
                    }
                }
            }

            if (n > 0) Graphics.DrawMeshInstanced(_mesh, 0, _material, _batch, n);
            LastRenderedCount = rendered;
            LastCulledCount = culled;
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }
    }
}
#endif
