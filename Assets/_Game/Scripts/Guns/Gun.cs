#if PHOTON_FUSION
using BillGameCore;
using Fusion;
using UnityEngine;

namespace TossZone.Guns
{
    /// <summary>Local fire-loop state. Ammo/reload/spread are the shooter's OWN business — plain fields, never
    /// [Networked] (Gun_System_Architecture.md §2: "Authority = ai cần write frame này").</summary>
    public enum GunState { Ready, Reloading, Blocked }

    /// <summary>
    /// Base fire-gate/ammo/reload state machine shared by every gun behaviour. Concrete subclasses (only
    /// <see cref="HitscanGun"/> for P0) implement <see cref="ResolveShot"/> — the one method that differs
    /// between hitscan/spin-up/bolt weapons. Semi vs. Auto is config (<see cref="GunConfig.fireMode"/>), not a
    /// subclass, per the "15-minute new weapon" rule in Docs/Gun_System_Architecture.md §3.
    /// </summary>
    public abstract class Gun : MonoBehaviour
    {
        [SerializeField] protected GunConfig _config;

        public GunConfig Config => _config;
        public GunState State { get; private set; } = GunState.Ready;
        public int Ammo { get; private set; }
        public bool MatchPhaseAllowsFire { get; set; } = true;

        /// <summary>The most recently fired shot — for a debug overlay/telemetry hook to inspect without
        /// re-deriving it from the event bus.</summary>
        public ShotInfo LastShot { get; private set; }

        private float _nextFireAllowedAt;
        private float _reloadEndsAt;
        private uint _localShotCounter;

        protected virtual void Awake()
        {
            if (_config != null) Ammo = _config.magazineSize;
        }

        protected virtual void OnEnable()
        {
            State = GunState.Ready;
        }

        public void TriggerDown()
        {
            _triggerHeld = true;
            TryFire();
        }

        public void TriggerUp()
        {
            _triggerHeld = false;
        }

        private bool _triggerHeld;

        /// <summary>Auto weapons call this every tick while the trigger is held; semi weapons only fire once
        /// per TriggerDown (see <see cref="TriggerDown"/>).</summary>
        protected virtual void Update()
        {
            if (State == GunState.Reloading && Time.unscaledTime >= _reloadEndsAt) FinishReload();
            if (_triggerHeld && _config != null && _config.fireMode == GunFireMode.Auto) TryFire();
        }

        /// <summary>Attempt one shot right now. Returns true if a shot was actually fired (ammo consumed, event
        /// raised) — false for dry-fire/blocked/reloading/fire-rate-gated attempts.</summary>
        public bool TryFire()
        {
            if (_config == null) return false;
            if (!MatchPhaseAllowsFire) return false;
            if (State != GunState.Ready) return false;
            if (Time.unscaledTime < _nextFireAllowedAt) return false;

            if (Ammo <= 0)
            {
                if (Bill.IsReady) Bill.Events.Fire(new GunDryFireEvent { WeaponId = _config.weaponId });
                StartReload();
                return false;
            }

            _nextFireAllowedAt = Time.unscaledTime + _config.FireIntervalSeconds;
            Ammo--;

            ShotInfo shot = ResolveShot();
            shot.ShotId = ++_localShotCounter;
            shot.WeaponId = _config.weaponId;
            LastShot = shot;
            if (Bill.IsReady) Bill.Events.Fire(new GunFiredEvent { Shot = shot });
            OnShotFired(shot);
            return true;
        }

        /// <summary>Manual reload input. No-op if already reloading, full, or blocked.</summary>
        public void StartReload()
        {
            if (_config == null || State != GunState.Ready) return;
            if (Ammo >= _config.magazineSize) return;

            State = GunState.Reloading;
            _reloadEndsAt = Time.unscaledTime + _config.reloadSeconds;
            if (Bill.IsReady)
                Bill.Events.Fire(new GunReloadStartEvent { WeaponId = _config.weaponId, Duration = _config.reloadSeconds });
        }

        /// <summary>Force back to Ready with trigger state cleared — called on equip/swap/death so a held
        /// trigger from before doesn't carry over (edge case #2, Gun_System_Architecture.md §9).</summary>
        public void ForceReset()
        {
            _triggerHeld = false;
            State = GunState.Ready;
        }

        private void FinishReload()
        {
            State = GunState.Ready;
            Ammo = _config.magazineSize;
            if (Bill.IsReady) Bill.Events.Fire(new GunReloadEndEvent { WeaponId = _config.weaponId });
        }

        /// <summary>Subclass hook: compute the ray/target result for one shot. ShotId/WeaponId are filled by
        /// the base class right after this returns.</summary>
        protected abstract ShotInfo ResolveShot();

        /// <summary>Subclass hook for anything beyond the generic event (currently unused by HitscanGun, kept
        /// for SpinUpGun/BoltActionGun when they're built post-P0).</summary>
        protected virtual void OnShotFired(ShotInfo shot) { }
    }
}
#endif
