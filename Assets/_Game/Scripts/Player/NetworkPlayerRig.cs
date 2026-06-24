#if PHOTON_FUSION
using Fusion;
using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Unified networked player. This single prefab IS the player: it contains the AutoHand XR rig
    /// (camera + hands, which the input authority controls) plus the networked visuals (a head mesh,
    /// the rig's hand meshes, and a body capsule). Fusion spawns one per player; the rig's own camera
    /// and hand transforms carry NetworkTransform, so no separate avatar is needed.
    ///
    /// - Input authority (local): the rig is active and drives the transforms; the head mesh is hidden
    ///   (it sits at the camera and would block the view). NetworkTransform replicates the poses out.
    /// - Proxy (remote): the AutoHand rig (camera, locomotion, hand physics) is disabled so it does not
    ///   fight the network; the meshes stay visible and are driven by NetworkTransform.
    /// Each player carries a networked colour so players are visually distinct.
    /// </summary>
    public class NetworkPlayerRig : NetworkBehaviour
    {
        public const int ColorCount = 8;

        [Tooltip("Head mesh (child of the camera) — hidden for the local owner so it doesn't block the view.")]
        [SerializeField] private Transform _headVisual;

        [Tooltip("Renderers tinted by the player's colour: body capsule, head, hand meshes.")]
        [SerializeField] private Renderer[] _coloredRenderers;

        [Networked] public int ColorIndex { get; set; }

        private static readonly Color[] _palette =
        {
            new Color(0.92f, 0.24f, 0.24f), // 0 red
            new Color(0.96f, 0.58f, 0.18f), // 1 orange
            new Color(0.96f, 0.83f, 0.24f), // 2 yellow
            new Color(0.30f, 0.80f, 0.36f), // 3 green
            new Color(0.22f, 0.85f, 0.85f), // 4 cyan
            new Color(0.24f, 0.50f, 0.93f), // 5 blue
            new Color(0.64f, 0.32f, 0.88f), // 6 purple
            new Color(0.96f, 0.46f, 0.74f), // 7 pink
        };

        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _block;

        public override void Spawned()
        {
            ApplyColor();

            bool isLocal = HasInputAuthority || HasStateAuthority;
            if (isLocal)
            {
                // You see through the rig's own camera, so hide the head mesh that sits on it.
                SetRendererEnabled(_headVisual, false);
            }
            else
            {
                DisableRigForRemote();
            }
        }

        /// <summary>Turns off the local-only AutoHand rig on proxies so NetworkTransform owns the visuals.</summary>
        private void DisableRigForRemote()
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++) cameras[i].enabled = false;

            AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++) listeners[i].enabled = false;

            // Disable AutoHand / locomotion / pose-driver behaviours by type name (keeps this decoupled
            // from the AutoHand assembly). NetworkTransform + NetworkBehaviour must stay enabled.
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null || mb is NetworkBehaviour || mb is NetworkTransform) continue;
                string n = mb.GetType().Name;
                if (n.Contains("AutoHand") || n == "Hand" || n.Contains("TrackedPoseDriver")
                    || n.Contains("Locomotion") || n.Contains("Grabbable") || n.Contains("Mover"))
                {
                    mb.enabled = false;
                }
            }

            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) bodies[i].isKinematic = true;
        }

        private void ApplyColor()
        {
            if (_coloredRenderers == null || _coloredRenderers.Length == 0) return;
            Color c = _palette[Mathf.Clamp(ColorIndex, 0, _palette.Length - 1)];
            _block ??= new MaterialPropertyBlock();
            for (int i = 0; i < _coloredRenderers.Length; i++)
            {
                Renderer r = _coloredRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(_baseColorId, c);
                _block.SetColor(_colorId, c);
                r.SetPropertyBlock(_block);
            }
        }

        private static void SetRendererEnabled(Transform t, bool on)
        {
            if (t == null) return;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = on;
        }
    }
}
#endif
