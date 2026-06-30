#if PHOTON_FUSION
using BillGameCore;
using TossZone.Combat;
using UnityEngine;
using UnityEngine.XR;

namespace TossZone.UI
{
    /// <summary>
    /// Wrist-mounted weapon carousel. Shows 3 weapon icons in a horizontal strip (prev / current / next).
    /// Left/right thumbstick flick navigates; trigger or grip on the wrist hand confirms buy/equip.
    ///
    /// Place on a child of the left-wrist bone. Call <see cref="Initialize"/> from NetworkAvatar.Spawned().
    ///
    /// Buy flow: if not owned → TryBuyWeapon (deduct money, set bit) → EquipWeapon.
    /// Equip flow: if owned → EquipWeapon directly.
    /// Unlock time: greys out slots not yet unlocked.
    /// </summary>
    public class WristWeaponSelector : MonoBehaviour
    {
        [Header("Slots (prev / center / next)")]
        [SerializeField] private WeaponSlotUI[] _slots = new WeaponSlotUI[3];

        [Header("Input thresholds")]
        [SerializeField] private float _flickThreshold = 0.7f;
        [SerializeField] private float _flickCooldown  = 0.4f;

        private PlayerCombat _combat;
        private WeaponConfig[] _catalog;
        private int _viewIndex;          // index currently in the center slot
        private float _flickEnd;
        private bool _visible;

        public void Initialize(PlayerCombat combat)
        {
            _combat  = combat;
            _catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            _viewIndex = 0;
            RefreshSlots();
            SetVisible(false);
        }

        private void Update()
        {
            if (_combat == null) return;

            // Show selector when wrist is turned (palm up heuristic: local Y down is towards floor).
            bool palmUp = transform.up.y < -0.3f;
            if (palmUp != _visible) SetVisible(palmUp);
            if (!_visible) return;

            HandleNavigation();
        }

        private void HandleNavigation()
        {
            if (_catalog == null || _catalog.Length == 0 || Time.time < _flickEnd) return;

            InputDevice dev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;

            if (stick.x > _flickThreshold)  { Navigate(+1); return; }
            if (stick.x < -_flickThreshold) { Navigate(-1); return; }

            // Confirm (grip)
            if (dev.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip)
                ConfirmSelected();
        }

        private void Navigate(int dir)
        {
            _flickEnd = Time.time + _flickCooldown;
            _viewIndex = Mod(_viewIndex + dir, _catalog.Length);
            RefreshSlots();
        }

        private void ConfirmSelected()
        {
            if (_catalog == null || _viewIndex >= _catalog.Length || _combat == null) return;
            WeaponConfig cfg = _catalog[_viewIndex];
            if (cfg == null) return;

            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;
            if (elapsed < cfg.unlockTime) return;

            if (!_combat.OwnsWeapon(_viewIndex))
            {
                if (!_combat.TryBuyWeapon(_viewIndex, cfg.cost)) return;
            }
            _combat.EquipWeapon(_viewIndex);
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (_slots == null || _catalog == null) return;
            int[] indices = { Mod(_viewIndex - 1, _catalog.Length), _viewIndex, Mod(_viewIndex + 1, _catalog.Length) };
            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;

            for (int i = 0; i < _slots.Length && i < indices.Length; i++)
            {
                if (_slots[i] == null) continue;
                WeaponConfig cfg = _catalog[indices[i]];
                bool equipped = _combat != null && _combat.EquippedIndex == indices[i];
                bool owned    = _combat != null && _combat.OwnsWeapon(indices[i]);
                bool unlocked = cfg == null || elapsed >= cfg.unlockTime;
                _slots[i].Bind(cfg, owned, equipped, unlocked);
            }
        }

        private void SetVisible(bool v)
        {
            _visible = v;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) _slots[i].gameObject.SetActive(v);
        }

        private static int Mod(int x, int m) => m == 0 ? 0 : ((x % m) + m) % m;
    }

    /// <summary>One icon slot in the weapon carousel. Subcomponent of <see cref="WristWeaponSelector"/>.</summary>
    [System.Serializable]
    public class WeaponSlotUI
    {
        public GameObject gameObject;
        public UnityEngine.UI.Image icon;
        public TMPro.TextMeshProUGUI nameLabel;
        public TMPro.TextMeshProUGUI priceLabel;
        public UnityEngine.UI.Image equippedIndicator;
        public CanvasGroup lockedOverlay;

        public void Bind(WeaponConfig cfg, bool owned, bool equipped, bool unlocked)
        {
            if (cfg == null) { if (gameObject) gameObject.SetActive(false); return; }
            if (icon != null) icon.sprite = cfg.icon;
            if (nameLabel  != null) nameLabel.text  = cfg.displayName;
            if (priceLabel != null) priceLabel.text = owned ? "✓" : $"${cfg.cost}";
            if (equippedIndicator != null) equippedIndicator.enabled = equipped;
            if (lockedOverlay != null) lockedOverlay.alpha = unlocked ? 0f : 0.6f;
        }
    }
}
#endif
