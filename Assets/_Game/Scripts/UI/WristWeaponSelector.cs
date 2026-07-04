#if PHOTON_FUSION
using Autohand;
using BillGameCore;
using TossZone.Combat;
using UnityEngine;

namespace TossZone.UI
{
    /// <summary>
    /// Wrist-mounted weapon shop (T18 rework — owner's flow). Lives on a child of the LEFT wrist bone; call
    /// <see cref="Initialize"/> from NetworkAvatar.Spawned() (owner only — proxies keep it hidden).
    ///
    /// <b>Visibility:</b> the panel shows while the wrist sits inside the CENTER-VIEW CONE of the head camera
    /// (dot(cam.forward, cam→wrist) &gt; cos(<see cref="_viewHalfAngle"/>) within <see cref="_viewMaxDistance"/>)
    /// — look at your wrist to open it, look away to close. Replaces the old palm-up heuristic.
    ///
    /// <b>Browse:</b> two physical <see cref="PokeButton3D"/> arrows — poke with a fingertip to step
    /// prev/next through the catalog (labels on the old canvas slots stay as secondary info).
    ///
    /// <b>Equip:</b> the viewed weapon floats over <see cref="_hologramAnchor"/> as a slowly spinning
    /// HOLOGRAM (cosmetic copy, material swapped to <see cref="_hologramMat"/> — owner's custom shader goes
    /// here). REACH IN AND SQUEEZE GRIP (a grab) to buy/equip it: money is deducted for un-owned weapons,
    /// <see cref="PlayerCombat.EquipWeapon"/> fires, and the T19 WeaponHolder immediately force-grabs the REAL
    /// weapon into the still-squeezing hand. Locked/unaffordable weapons render with
    /// <see cref="_hologramDeniedMat"/> and the grab zone rejects presses.
    /// </summary>
    public class WristWeaponSelector : MonoBehaviour
    {
        [Header("Slots (prev / center / next) — legacy canvas, kept as labels")]
        [SerializeField] private WeaponSlotUI[] _slots = new WeaponSlotUI[3];

        [Header("View-cone visibility (thay palm-up)")]
        [Tooltip("Half-angle (deg) of the head-camera cone the wrist must sit in for the panel to show.")]
        [SerializeField] private float _viewHalfAngle = 22f;
        [Tooltip("Max head→wrist distance for the panel to show.")]
        [SerializeField] private float _viewMaxDistance = 1f;

        [Header("Poke buttons (prev / next)")]
        [SerializeField] private PokeButton3D _btnPrev;
        [SerializeField] private PokeButton3D _btnNext;

        [Header("Hologram (grab để equip)")]
        [Tooltip("Anchor the viewed weapon's hologram spins over. Needs a PokeButton3D (requireGrip) on it.")]
        [SerializeField] private Transform _hologramAnchor;
        [SerializeField] private PokeButton3D _hologramZone;
        [Tooltip("Hologram material — owner's custom blue holo shader goes here (URP Unlit placeholder for now).")]
        [SerializeField] private Material _hologramMat;
        [Tooltip("Shown when the viewed weapon is locked or unaffordable.")]
        [SerializeField] private Material _hologramDeniedMat;

        private const float PeriodicRefreshInterval = 0.5f;
        private const float HologramSpinSeconds = 6f;

        private PlayerCombat _combat;
        private WeaponConfig[] _catalog;
        private GameObject _holoModel;
        private int _viewIndex;          // catalog index currently in the center slot / hologram
        private float _nextPeriodicRefresh;
        private bool _visible;

        public void Initialize(PlayerCombat combat)
        {
            _combat = combat;
            _viewIndex = 0;
            if (_btnPrev != null) _btnPrev.Poked += OnPrevPoked;
            if (_btnNext != null) _btnNext.Poked += OnNextPoked;
            if (_hologramZone != null) _hologramZone.Poked += OnHologramGrabbed;
            TryResolveCatalog();
            SetVisible(false);
        }

        private void Awake() => SetVisible(false);   // proxies never Initialize — keep the panel off for them

        private void OnDestroy()
        {
            if (_btnPrev != null) _btnPrev.Poked -= OnPrevPoked;
            if (_btnNext != null) _btnNext.Poked -= OnNextPoked;
            if (_hologramZone != null) _hologramZone.Poked -= OnHologramGrabbed;
            DestroyHologram();
        }

        private void Update()
        {
            if (_combat == null) return;

            // Initialize() can run before ArenaManager fires MinigameEnteredEvent (avatar spawns before the
            // scene's combat authority attaches) — keep resolving until the catalog appears.
            if (_catalog == null && !TryResolveCatalog()) return;

            bool show = InViewCone();
            if (show != _visible) SetVisible(show);
            if (!_visible) return;

            // Affordability/unlocks drift while the panel is open (passive income, round timer) — refresh on
            // a slow cadence instead of every frame.
            if (Time.time >= _nextPeriodicRefresh)
            {
                _nextPeriodicRefresh = Time.time + PeriodicRefreshInterval;
                RefreshSlots();
                ApplyHologramState();
            }
        }

        // ── visibility ───────────────────────────────────────────────────────────

        private bool InViewCone()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Transform ct = cam.transform;
            Vector3 to = transform.position - ct.position;
            float dist = to.magnitude;
            if (dist > _viewMaxDistance || dist < 1e-4f) return false;
            return Vector3.Dot(ct.forward, to / dist) > Mathf.Cos(_viewHalfAngle * Mathf.Deg2Rad);
        }

        /// <summary>The cone test, exposed pure for edit-time verification.</summary>
        public static bool InViewCone(Vector3 camPos, Vector3 camForward, Vector3 wristPos, float halfAngleDeg, float maxDist)
        {
            Vector3 to = wristPos - camPos;
            float dist = to.magnitude;
            if (dist > maxDist || dist < 1e-4f) return false;
            return Vector3.Dot(camForward, to / dist) > Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        }

        private void SetVisible(bool v)
        {
            _visible = v;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].gameObject != null) _slots[i].gameObject.SetActive(v);
            if (_btnPrev != null) _btnPrev.gameObject.SetActive(v);
            if (_btnNext != null) _btnNext.gameObject.SetActive(v);
            if (_hologramAnchor != null) _hologramAnchor.gameObject.SetActive(v);

            if (!v) { DestroyHologram(); return; }
            _nextPeriodicRefresh = 0f;   // refresh immediately on open
            RefreshSlots();
            RebuildHologram();
        }

        // ── browse ───────────────────────────────────────────────────────────────

        private void OnPrevPoked(Hand hand) => Navigate(-1);
        private void OnNextPoked(Hand hand) => Navigate(+1);

        private void Navigate(int dir)
        {
            if (_catalog == null || _catalog.Length == 0) return;
            _viewIndex = Mod(_viewIndex + dir, _catalog.Length);
            RefreshSlots();
            RebuildHologram();
        }

        // ── equip (grab hologram) ────────────────────────────────────────────────

        private void OnHologramGrabbed(Hand hand)
        {
            if (_catalog == null || _viewIndex >= _catalog.Length || _combat == null) return;
            WeaponConfig cfg = _catalog[_viewIndex];
            if (cfg == null || !IsUnlocked(cfg)) return;

            if (!CombatSession.TrainingModeActive && cfg.cost > 0)
            {
                if (cfg.IsPayPerUse)
                {
                    if (_combat.AmmoFor(_viewIndex) <= 0
                        && !_combat.TryBuyAmmo(_viewIndex, cfg.cost, cfg.magazine)) return;
                }
                else if (!_combat.OwnsWeapon(_viewIndex))
                {
                    if (!_combat.TryBuyWeapon(_viewIndex, cfg.cost)) return;
                }
            }
            _combat.EquipWeapon(_viewIndex);   // T19 WeaponHolder grabs the real weapon into the squeezing hand
            RefreshSlots();
            ApplyHologramState();
        }

        // ── hologram ─────────────────────────────────────────────────────────────

        private void RebuildHologram()
        {
            DestroyHologram();
            if (_hologramAnchor == null || _catalog == null || _catalog.Length == 0) return;
            WeaponConfig cfg = _catalog[Mod(_viewIndex, _catalog.Length)];
            if (cfg == null || cfg.heldPrefab == null) return;

            _holoModel = TossZone.Throwing.HandWeapon.SpawnHeldVisual(cfg, _hologramAnchor);
            ApplyHologramState();

            BillTween.KillTarget(_hologramAnchor);
            BillTween.Float(0f, 360f, HologramSpinSeconds,
                    a => { if (_hologramAnchor != null) _hologramAnchor.localRotation = Quaternion.Euler(0f, a, 0f); })
                ?.SetEase(EaseType.Linear).SetLoops(-1, LoopType.Restart).SetTarget(_hologramAnchor);
        }

        private void DestroyHologram()
        {
            if (_hologramAnchor != null) BillTween.KillTarget(_hologramAnchor);
            if (_holoModel == null) return;
            Destroy(_holoModel);
            _holoModel = null;
        }

        /// <summary>Tint the hologram + gate the grab zone by unlock/affordability.</summary>
        private void ApplyHologramState()
        {
            if (_holoModel == null) return;
            WeaponConfig cfg = _catalog != null && _viewIndex < _catalog.Length ? _catalog[_viewIndex] : null;
            if (cfg == null) return;

            bool owned = cfg.IsPayPerUse ? _combat.AmmoFor(_viewIndex) > 0 : _combat.OwnsWeapon(_viewIndex);
            bool can = IsUnlocked(cfg) && (owned || cfg.cost <= 0 || _combat.Money >= cfg.cost);
            if (_hologramZone != null) _hologramZone.Interactable = can;

            Material mat = can ? _hologramMat : _hologramDeniedMat;
            if (mat == null) return;
            Renderer[] rs = _holoModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] mats = rs[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++) mats[m] = mat;
                rs[i].sharedMaterials = mats;
            }
        }

        // ── shared helpers ───────────────────────────────────────────────────────

        private bool TryResolveCatalog()
        {
            WeaponConfig[] cat = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            if (cat == null) return false;
            _catalog = cat;
            _viewIndex = Mod(_viewIndex, cat.Length == 0 ? 1 : cat.Length);
            return true;
        }

        private static bool IsUnlocked(WeaponConfig cfg)
        {
            if (CombatSession.TrainingModeActive) return true;   // T25: hub range = everything free
            float elapsed = CombatSession.Instance != null ? CombatSession.Instance.RoundElapsed : 0f;
            return cfg == null || elapsed >= cfg.unlockTime;
        }

        private void RefreshSlots()
        {
            if (_slots == null || _catalog == null || _catalog.Length == 0) return;
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

        private static int Mod(int x, int m) => m == 0 ? 0 : ((x % m) + m) % m;
    }

    /// <summary>One label slot in the weapon strip. Subcomponent of <see cref="WristWeaponSelector"/>.</summary>
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
