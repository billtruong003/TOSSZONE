#if PHOTON_FUSION
using UnityEngine;

namespace TossZone.Combat
{
    /// <summary>
    /// Local, GPU-instanced renderer for <see cref="ProjectileBurstSystem"/> bursts. Every frame it derives each
    /// live projectile's position from the analytic flight formula and "stamps" them all with
    /// <see cref="Graphics.DrawMeshInstanced"/> — one draw call per 1023 projectiles instead of one renderer each.
    /// Purely visual and local: reads the replicated burst data, touches no network state. MVP uses the
    /// non-indirect path (≤1023/batch, CPU-built matrices); upgrade to RenderMeshIndirect + compute cull later.
    /// </summary>
    public class ProjectileBurstRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;
        [SerializeField] private float _scale = 0.12f;

        private const int BatchMax = 1023;
        private readonly Matrix4x4[] _batch = new Matrix4x4[BatchMax];
        private Material _runtimeMat;

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

            for (int bi = 0; bi < bursts.Length; bi++)
            {
                ProjectileBurstSystem.Burst b = bursts.Get(bi);
                if (!b.Active) continue;
                float t = sys.BurstElapsed(b);
                int count = Mathf.Min(b.Count, ProjectileBurstSystem.MaxProjectilesPerBurst);

                for (int i = 0; i < count; i++)
                {
                    _batch[n++] = Matrix4x4.TRS(sys.ProjectilePosition(b, i, t), Quaternion.identity, s);
                    if (n == BatchMax)
                    {
                        Graphics.DrawMeshInstanced(_mesh, 0, _material, _batch, n);
                        n = 0;
                    }
                }
            }

            if (n > 0) Graphics.DrawMeshInstanced(_mesh, 0, _material, _batch, n);
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }
    }
}
#endif
