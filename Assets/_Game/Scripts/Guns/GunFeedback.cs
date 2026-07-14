#if PHOTON_FUSION
using BillGameCore;
using TossZone.Throwing;
using UnityEngine;
using UnityEngine.XR;

namespace TossZone.Guns
{
    /// <summary>
    /// The ONE consumer of <see cref="GunFiredEvent"/>/<see cref="GunDryFireEvent"/> — muzzle flash, tracer,
    /// impact, fire sound and (local-only) haptic/recoil. Fires from a local shot AND from the cosmetic
    /// RPC re-fire on other clients (task 1.2.2) — same event, same rendering path, matching
    /// Gun_System_Architecture.md §6 ("local và remote đi chung một đường render"). Local-only pieces
    /// (haptic/recoil) are naturally skipped for remote shots because those are driven by input, not the event.
    /// </summary>
    public class GunFeedback : MonoBehaviour
    {
        [SerializeField] private float _fireHapticAmplitude = 0.35f;
        [SerializeField] private float _fireHapticDuration = 0.05f;
        [SerializeField] private float _dryFireHapticAmplitude = 0.12f;
        [SerializeField] private float _dryFireHapticDuration = 0.03f;

        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        // BillBootstrap may not have registered the EventBus yet when this component enables (e.g. it lives on
        // a DontDestroyOnLoad object present from the very first scene) — poll until ready, matching
        // PlayerSpawnManager/CombatSession's established pattern in this codebase.
        private void Update()
        {
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed || !Bill.IsReady) return;
            _subscribed = true;
            Bill.Events.Subscribe<GunFiredEvent>(OnFired);
            Bill.Events.Subscribe<GunDryFireEvent>(OnDryFire);
        }

        private void OnDisable()
        {
            if (!_subscribed || !Bill.IsReady) return;
            _subscribed = false;
            Bill.Events.Unsubscribe<GunFiredEvent>(OnFired);
            Bill.Events.Unsubscribe<GunDryFireEvent>(OnDryFire);
        }

        private void OnFired(GunFiredEvent e)
        {
            ShotInfo shot = e.Shot;
            GunConfig cfg = GunCatalog.Default != null ? GunCatalog.Default.Get(shot.WeaponId) : null;
            if (cfg == null) return;

            SpawnMuzzleFlash(cfg, shot.MuzzlePos, shot.Direction);
            SpawnTracer(cfg, shot.MuzzlePos, shot.HitPoint);
            if (shot.HitPart != HitPart.World) ImpactBurst.Show(shot.HitPoint, 1f);

            Bill.Audio.PlayPitched(cfg.fireSoundKey, 1f + Random.Range(-0.03f, 0.03f));

            if (shot.Shooter == LocalShooterRef()) Pulse(XRNode.RightHand, _fireHapticAmplitude, _fireHapticDuration);
        }

        private void OnDryFire(GunDryFireEvent e)
        {
            GunConfig cfg = GunCatalog.Default != null ? GunCatalog.Default.Get(e.WeaponId) : null;
            if (cfg != null) Bill.Audio.Play(cfg.dryFireSoundKey);
            Pulse(XRNode.RightHand, _dryFireHapticAmplitude, _dryFireHapticDuration);
        }

        private static Fusion.PlayerRef LocalShooterRef()
        {
            var local = TossZone.Player.NetworkAvatar.Local;
            return local != null && local.Object != null && local.Object.IsValid
                ? local.Object.InputAuthority : Fusion.PlayerRef.None;
        }

        private static void SpawnMuzzleFlash(GunConfig cfg, Vector3 pos, Vector3 dir)
        {
            if (string.IsNullOrEmpty(cfg.muzzleFlashPoolKey)) return;
            GameObject go = Bill.Pool.Spawn(cfg.muzzleFlashPoolKey, pos, Quaternion.LookRotation(dir));
            if (go != null) go.ReturnToPool(0.1f);
        }

        private static void SpawnTracer(GunConfig cfg, Vector3 start, Vector3 end)
        {
            if (string.IsNullOrEmpty(cfg.tracerPoolKey)) return;
            GameObject go = Bill.Pool.Spawn(cfg.tracerPoolKey);
            TracerFx tracer = go != null ? go.GetComponent<TracerFx>() : null;
            tracer?.Init(start, end);
        }

        // Legacy XR API used ONLY for haptics — verified safe on OpenXR (button/haptic features work; only
        // Vector2 axis reads are broken, see Network_Architecture_Lessons.md Bài 2). Matches CombatJuice.
        private static void Pulse(XRNode node, float amplitude, float duration)
        {
            InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
#endif
