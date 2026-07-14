#if PHOTON_FUSION
using Fusion;
using TossZone.Player;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>
    /// Default gun behaviour for P0 — one instant raycast per accepted shot. Semi vs. Auto is
    /// <see cref="GunConfig.fireMode"/> (handled by the base <see cref="Gun"/> class); this subclass only
    /// exists because "resolve one shot" is the one thing every hitscan weapon shares (Gun_System_Architecture.md
    /// §3 — subclass only when the fire-loop SHAPE differs, e.g. SpinUpGun/BoltActionGun post-P0).
    /// </summary>
    public class HitscanGun : Gun
    {
        [SerializeField] private Transform _muzzle;

        /// <summary>Approximate head-zone height above a target's feet (its root transform's Y). Both
        /// NetworkAvatar (explicit head node at ~1.6m) and the scene DummyAvatar (no head node, capsule top at
        /// 1.8m) fall out correctly with a single constant — see investigation in this task's commit message
        /// for why a shared "head transform" interface wasn't used instead.</summary>
        private const float HeadZoneMinHeight = 1.5f;

        // LayerMask.NameToLayer must NOT run in a field initializer (Unity throws on that path even for a
        // static field on a MonoBehaviour) — compute lazily on first use instead.
        private static int _hitMask;
        private static bool _hitMaskReady;

        public Transform Muzzle { get => _muzzle; set => _muzzle = value; }

        private static int HitMask()
        {
            if (_hitMaskReady) return _hitMask;
            _hitMask = ~((1 << LayerMask.NameToLayer("Ignore Raycast"))
                        | (1 << LayerMask.NameToLayer("UI"))
                        | (1 << LayerMask.NameToLayer("RemoteVisual")));
            _hitMaskReady = true;
            return _hitMask;
        }

        protected override ShotInfo ResolveShot()
        {
            Vector3 origin = _muzzle != null ? _muzzle.position : transform.position;
            Vector3 aim = _muzzle != null ? _muzzle.forward : transform.forward;
            Vector3 direction = ApplySpread(aim, Config.spreadDegrees);

            var shot = new ShotInfo
            {
                MuzzlePos = origin,
                Direction = direction,
                HitPart = HitPart.World,
                HitPoint = origin + direction * Config.range,
                HitNormal = -direction,
                Victim = PlayerRef.None,
            };

            if (Physics.Raycast(origin, direction, out RaycastHit hit, Config.range, HitMask(),
                    QueryTriggerInteraction.Collide))
            {
                shot.HitPoint = hit.point;
                shot.HitNormal = hit.normal;
                ClassifyTarget(hit, ref shot);
            }

            NetworkAvatar local = NetworkAvatar.Local;
            if (local != null && local.Object != null && local.Object.IsValid)
                shot.Shooter = local.Object.InputAuthority;

            return shot;
        }

        private static void ClassifyTarget(RaycastHit hit, ref ShotInfo shot)
        {
            NetworkAvatar avatar = hit.collider.GetComponentInParent<NetworkAvatar>();
            Transform targetRoot = avatar != null ? avatar.transform : hit.collider.transform.root;

            bool isSelf = avatar != null && avatar == NetworkAvatar.Local;
            if (isSelf) { shot.HitPart = HitPart.World; return; }

            bool isPlayerHitbox = hit.collider.gameObject.layer == LayerMask.NameToLayer("Hittable");
            if (!isPlayerHitbox) { shot.HitPart = HitPart.World; return; }

            float heightAboveFeet = hit.point.y - targetRoot.position.y;
            shot.HitPart = heightAboveFeet >= HeadZoneMinHeight ? HitPart.Head : HitPart.Body;
            if (avatar != null && avatar.Object != null && avatar.Object.IsValid)
                shot.Victim = avatar.Object.InputAuthority;
        }

        private static Vector3 ApplySpread(Vector3 aim, float halfAngleDegrees)
        {
            if (halfAngleDegrees <= 0f) return aim;
            Vector2 jitter = Random.insideUnitCircle * halfAngleDegrees;
            Quaternion spread = Quaternion.Euler(jitter.y, jitter.x, 0f);
            return spread * aim;
        }
    }
}
#endif
