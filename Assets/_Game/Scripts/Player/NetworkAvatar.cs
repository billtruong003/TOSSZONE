#if PHOTON_FUSION
using Fusion;
using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Thin networked avatar — one per player. Carries NO AutoHand / camera / physics: only the synced
    /// transforms (root + head + both wrists) and a colour. The owner (state authority) copies its local
    /// <see cref="PlayerRig"/> tracking points onto these nodes in <see cref="FixedUpdateNetwork"/>;
    /// NetworkTransform replicates them; proxies pose the low-poly visuals (head + two forearm "arms"
    /// stretched to the wrists) in <see cref="Render"/>. Remotes have NO hands — just low-poly arms that
    /// end at the wrist. Grab/throw is purely local on the toon hands, so nothing about it crosses the wire.
    /// Replaces the old NetworkPlayerRig (which spawned the whole rig on every client).
    /// </summary>
    public class NetworkAvatar : NetworkBehaviour
    {
        public const int ColorCount = 8;

        /// <summary>The local player's own avatar (the one we hold state authority over). Null until spawned;
        /// stays valid across scene loads so exactly ONE local avatar carries Main -> Arena. Mirrors
        /// <see cref="PlayerRig.Local"/>; the spawn manager guards on it because Fusion's player-object
        /// registry is NOT preserved across a Single-mode networked scene load.</summary>
        public static NetworkAvatar Local { get; private set; }

        [Header("Synced nodes (each carries its own NetworkTransform)")]
        [SerializeField] private Transform _headNode;
        [SerializeField] private Transform _wristLNode;
        [SerializeField] private Transform _wristRNode;

        [Header("Visuals — proxy only; renderers are disabled for the local owner (first-person)")]
        [SerializeField] private Transform _shoulderL;
        [SerializeField] private Transform _shoulderR;
        [Tooltip("Centre-pivot cube; placed at the shoulder->wrist midpoint and scaled on local Z to reach.")]
        [SerializeField] private Transform _armL;
        [SerializeField] private Transform _armR;
        [Tooltip("Every visual renderer (body, head, both arms): tinted by colour AND hidden for the owner.")]
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
            if (HasStateAuthority)
            {
                // Claim the local-avatar slot so the spawn manager won't spawn a second one after a scene load.
                Local = this;
                gameObject.name = "Avatar (Local)";
                // D1 — first-person: the owner sees only their local toon hands, so hide their own networked
                // visuals. Disable the RENDERERS (not the GameObjects) so the synced NetworkTransform nodes keep ticking.
                SetVisualsEnabled(false);
            }
            else
            {
                gameObject.name = "Avatar (Remote #" + Object.InputAuthority.PlayerId + ")";
            }
            ApplyColor();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Released only when this avatar is genuinely despawned (session end / explicit despawn) — NOT on a
            // scene load, so the surviving avatar keeps the slot and is reused. Self-heals if it ever is removed.
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            PlayerRig rig = PlayerRig.Local;
            if (rig == null || rig.Root == null) return;

            // Owner drives the synced nodes from the local rig; NetworkTransform replicates them out.
            // Body stands at the rig root and faces the head's horizontal look direction.
            Vector3 fwd = rig.Head != null ? rig.Head.forward : rig.Root.forward;
            fwd.y = 0f;
            Quaternion bodyRot = fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd, Vector3.up) : transform.rotation;
            transform.SetPositionAndRotation(rig.Root.position, bodyRot);

            CopyNode(_headNode, rig.Head);
            CopyNode(_wristLNode, rig.WristL);
            CopyNode(_wristRNode, rig.WristR);
        }

        public override void Render()
        {
            if (HasStateAuthority) return;   // owner avatar is hidden; nothing to pose
            StretchArm(_shoulderL, _armL, _wristLNode);
            StretchArm(_shoulderR, _armR, _wristRNode);
        }

        private static void CopyNode(Transform node, Transform src)
        {
            if (node != null && src != null) node.SetPositionAndRotation(src.position, src.rotation);
        }

        /// <summary>Place the centre-pivot <paramref name="arm"/> cube between shoulder and wrist and scale it to reach.</summary>
        private static void StretchArm(Transform shoulder, Transform arm, Transform wrist)
        {
            if (shoulder == null || arm == null || wrist == null) return;
            Vector3 a = shoulder.position;
            Vector3 b = wrist.position;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            arm.position = (a + b) * 0.5f;                 // centre pivot -> midpoint
            arm.rotation = Quaternion.LookRotation(dir);   // local +Z aims shoulder -> wrist
            Vector3 s = arm.localScale;
            arm.localScale = new Vector3(s.x, s.y, len);   // unit cube spans the full length on Z
        }

        private void SetVisualsEnabled(bool on)
        {
            if (_coloredRenderers == null) return;
            for (int i = 0; i < _coloredRenderers.Length; i++)
                if (_coloredRenderers[i] != null) _coloredRenderers[i].enabled = on;
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
    }
}
#endif
