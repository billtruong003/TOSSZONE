#if PHOTON_FUSION
using Fusion;
using UnityEngine;

namespace TossZone.Player
{
    /// <summary>
    /// Drives a networked avatar from the local VR rig. The state authority (local owner) copies
    /// head/hand poses each network tick so Fusion's NetworkTransform captures and replicates them;
    /// remote instances are interpolated by NetworkTransform and run no logic here.
    /// Root transform = the player's feet (XZ under the head); Head/HandL/HandR are nested transforms.
    /// </summary>
    public class NetworkPlayerAvatar : NetworkBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _handLeft;
        [SerializeField] private Transform _handRight;

        public override void Spawned()
        {
            // The local owner sees through the AutoHand rig, so hide this avatar's own visuals
            // (otherwise a cube head would block their view). Remote proxies keep them visible.
            if (!HasStateAuthority) return;
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            LocalPlayerRig rig = LocalPlayerRig.Instance;
            if (rig == null) return;

            Transform head = rig.Head;
            if (head != null)
            {
                Vector3 p = head.position;
                transform.position = new Vector3(p.x, 0f, p.z); // root = feet → root NetworkTransform
                if (_head != null) _head.SetPositionAndRotation(p, head.rotation);
            }
            CopyPose(rig.HandLeft, _handLeft);
            CopyPose(rig.HandRight, _handRight);
        }

        private static void CopyPose(Transform src, Transform dst)
        {
            if (src == null || dst == null) return;
            dst.SetPositionAndRotation(src.position, src.rotation);
        }
    }
}
#endif
