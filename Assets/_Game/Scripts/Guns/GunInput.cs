#if PHOTON_FUSION
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TossZone.Guns
{
    /// <summary>
    /// Local-only: equips the P0 placeholder gun on the gun hand's wrist and drives it from the new Input
    /// System (never legacy UnityEngine.XR — Network_Architecture_Lessons.md Bài 2). One gun, no swap/slots yet
    /// (P0 scope — GDD's 3-slot system is a Phase 2 concern). Hand is hardcoded to Right, matching every other
    /// P0-era script's convention (HandednessSetting doesn't exist yet — Tech Review §2).
    /// </summary>
    public class GunInput : MonoBehaviour
    {
        [SerializeField] private GunConfig _config;

        private HitscanGun _gun;
        private bool _triggerHeldLast;
        private bool _reloadHeldLast;

        private void Start()
        {
            if (_config == null || _config.modelPrefab == null)
            {
                Debug.LogError("[GunInput] No GunConfig/modelPrefab assigned — nothing to equip.");
                enabled = false;
                return;
            }
            TryEquip();
        }

        private void TryEquip()
        {
            Transform wristR = PlayerRig.Local != null ? PlayerRig.Local.WristR : null;
            if (wristR == null) return; // retried next frame via Update below until the rig exists

            GameObject instance = Instantiate(_config.modelPrefab, wristR);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            _gun = instance.GetComponent<HitscanGun>();
            if (_gun == null)
            {
                Debug.LogError("[GunInput] modelPrefab is missing a HitscanGun component: " + _config.modelPrefab.name);
                return;
            }

            // Publish local truth for the network mirror (task 1.2.1): AvatarWeaponSync copies this onto its
            // [Networked] EquippedSlot so remotes render the proxy gun. P0 never unequips, so set-once is fine.
            AvatarWeaponSync.LocalEquippedWeaponId = _config.weaponId;
        }

        private void Update()
        {
            if (_gun == null) { TryEquip(); return; }

            _gun.MatchPhaseAllowsFire = ArenaManager.Instance == null
                || ArenaManager.Instance.Phase == ArenaManager.MatchPhase.Playing;

            bool triggerHeld = ReadTriggerHeld();
            if (triggerHeld && !_triggerHeldLast) _gun.TriggerDown();
            else if (!triggerHeld && _triggerHeldLast) _gun.TriggerUp();
            _triggerHeldLast = triggerHeld;

            bool reloadHeld = ReadReloadHeld();
            if (reloadHeld && !_reloadHeldLast) _gun.StartReload();
            _reloadHeldLast = reloadHeld;
        }

        /// <summary>Right-hand controller trigger, new Input System — same device-matching pattern as
        /// TossLocomotionInput.ReadNew.</summary>
        private static bool ReadTriggerHeld()
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null || !d.added || !IsRightHand(d)) continue;

                ButtonControl trigger = d.TryGetChildControl<ButtonControl>("triggerButton")
                                        ?? d.TryGetChildControl<ButtonControl>("trigger");
                if (trigger != null) return trigger.isPressed;
            }
            return false;
        }

        /// <summary>Right-hand grip button — reload. Deliberately NOT the right thumbstick click (that's the
        /// existing dash binding in TossLocomotionInput; GDD's remap of that button to reload is a separate,
        /// wider input-remap task, out of scope here).</summary>
        private static bool ReadReloadHeld()
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice d = devices[i];
                if (d == null || !d.added || !IsRightHand(d)) continue;

                ButtonControl grip = d.TryGetChildControl<ButtonControl>("gripButton");
                if (grip != null) return grip.isPressed;
            }
            return false;
        }

        private static bool IsRightHand(InputDevice d)
        {
            foreach (var u in d.usages)
                if (u == CommonUsages.RightHand) return true;
            return false;
        }
    }
}
#endif
