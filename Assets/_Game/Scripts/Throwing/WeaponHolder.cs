using System.Collections.Generic;
using Autohand;
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace TossZone.Throwing
{
    /// <summary>
    /// Puts a REAL AutoHand <see cref="Grabbable"/> of the CURRENTLY EQUIPPED weapon into the throwing hand
    /// (T19). Generalizes the old ThrowBallHolder, which force-grabbed the generic ball on grip regardless of
    /// <see cref="PlayerCombat.EquippedIndex"/> — the "sword swing still leaves a ball" bug. Grip → force-grab
    /// the equipped weapon's <see cref="WeaponConfig.heldPrefab"/> instance (fingers wrap via AutoHand
    /// auto-pose; per-weapon GrabbablePose can be authored later); release grip → it vanishes. Swapping weapons
    /// while gripping releases the old instance and grabs the new one next frame. One instance per weapon is
    /// kept alive and SetActive-toggled (no per-grab Instantiate). Equip -1 (default) and ThrowBallistic
    /// weapons without a heldPrefab fall back to <see cref="_throwBallPrefab"/> — Rock behaves as before T19.
    ///
    /// Owner-side visual handover: while a ready holder exists for a hand (<see cref="IsActiveFor"/>),
    /// <see cref="ThrowController"/>'s parented sphere and <see cref="HandWeapon"/>'s cosmetic wrist model stay
    /// OFF for the LOCAL player. Remote proxies keep the cosmetic path — other machines have no local
    /// <see cref="Hand"/> to grab with, they only need to see a model.
    /// Scene-scoped: place one per scene; it finds the local <see cref="PlayerRig"/> at runtime.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        [Tooltip("Fallback held Grabbable: default equip (-1) + ThrowBallistic weapons with no heldPrefab.")]
        [SerializeField] private GameObject _throwBallPrefab;
        [SerializeField] private bool _rightHand = true;
        [Tooltip("Editor/dev: hold to simulate grip with no XR controller.")]
        [SerializeField] private Key _editorGripKey = Key.G;

        private static WeaponHolder _activeRight;
        private static WeaponHolder _activeLeft;

        private PlayerRig _rig;
        private Hand _hand;
        private readonly Dictionary<WeaponConfig, Grabbable> _instances = new Dictionary<WeaponConfig, Grabbable>();
        private Grabbable _ballInstance;
        private Grabbable _heldGrabbable;
        private WeaponConfig _heldCfg;
        private int _grabbableMask;
        private int _grabbableLayer;
        private float _nextRegrabTime;
        private bool _held;
        private bool _ready;

        private const float RegrabInterval = 0.25f;

        /// <summary>True when a ready WeaponHolder serves the local player's hand — the owner's cosmetic held
        /// visuals (ThrowController sphere / HandWeapon wrist model) must stay off while this is true.</summary>
        public static bool IsActiveFor(bool rightHand) => rightHand ? _activeRight != null : _activeLeft != null;

        private void OnDisable() => Deactivate();

        private void OnDestroy()
        {
            if (_ballInstance != null) Destroy(_ballInstance.gameObject);
            foreach (KeyValuePair<WeaponConfig, Grabbable> kv in _instances)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _instances.Clear();
        }

        private void Update()
        {
            if (!_ready) { TryInit(); return; }
            if (_hand == null || PlayerRig.Local == null) { Deactivate(); return; }

            WeaponConfig cfg = ResolveEquippedConfig();
            if (_held && cfg != _heldCfg) Release();   // weapon swapped mid-grip → re-grab the new one below

            bool grip = ReadGrip();
            if (grip && !_held) Grab(cfg);
            else if (!grip && _held) Release();
            else if (grip && _held) EnsureAttached();
        }

        /// <summary>AutoHand's grab is a multi-frame coroutine and can silently bail (e.g. the instance was
        /// activated the same frame, or a fast weapon swap released the hand mid-grab) — <see cref="Grab"/>
        /// only records INTENT. While gripping, re-force the grab until the hand actually holds the item.</summary>
        private void EnsureAttached()
        {
            if (_heldGrabbable == null) return;
            if (_heldGrabbable.IsHeld() || _hand.IsGrabbing()) return;
            if (Time.time < _nextRegrabTime) return;
            _nextRegrabTime = Time.time + RegrabInterval;
            _heldGrabbable.transform.position = _hand.transform.position + _hand.transform.forward * 0.08f;
            _hand.ForceGrab(_heldGrabbable);
        }

        private void TryInit()
        {
            if (_throwBallPrefab == null) return;
            _rig = PlayerRig.Local;
            if (_rig == null) return;

            Transform wrist = _rightHand ? _rig.WristR : _rig.WristL;
            if (wrist == null) return;
            _hand = wrist.GetComponent<Hand>();
            if (_hand == null) _hand = wrist.GetComponentInChildren<Hand>();
            if (_hand == null) _hand = wrist.GetComponentInParent<Hand>();
            if (_hand == null) return;

            _grabbableLayer = LayerMask.NameToLayer("Grabbable");
            _grabbableMask = _grabbableLayer >= 0 ? 1 << _grabbableLayer : ~0;
            _ready = true;
            SetRegistry(true);
            Debug.Log("[WeaponHolder] ready (hand=" + (_rightHand ? "R" : "L") + ").");
        }

        private void Deactivate()
        {
            if (_held) Release();
            if (!_ready) return;
            _ready = false;
            _hand = null;
            _rig = null;
            SetRegistry(false);
        }

        private void Grab(WeaponConfig cfg)
        {
            Grabbable g = GetInstance(cfg);
            if (g == null || g.IsHeld() || _hand.IsGrabbing()) return;

            // Place the item a little in front of the hand so the raycast has a target (GrabbableToHand floats
            // it in). Shmackle pattern (AutoGrabber.cs): raycast hand → grabbable, then Grab(hit) so AutoHand
            // gets a real grab point → auto-pose engages + gentle grab. ForceGrab (no hit) skips that.
            Vector3 handPos = _hand.transform.position;
            g.transform.position = handPos + _hand.transform.forward * 0.08f;
            g.gameObject.SetActive(true);
            Physics.SyncTransforms();   // make the just-moved collider visible to the raycast this frame

            Vector3 dir = (g.transform.position - handPos).normalized;
            if (Physics.Raycast(handPos, dir, out RaycastHit hit, 0.5f, _grabbableMask, QueryTriggerInteraction.Ignore)
                && (hit.collider.gameObject == g.gameObject || hit.collider.transform.IsChildOf(g.transform)))
                _hand.Grab(hit, g);
            else
                _hand.ForceGrab(g);   // fallback if the raycast missed (offset collider etc.)

            _held = true;
            _heldGrabbable = g;
            _heldCfg = cfg;
        }

        private void Release()
        {
            if (_heldGrabbable != null)
            {
                _heldGrabbable.ForceHandsRelease();
                _heldGrabbable.gameObject.SetActive(false);
            }
            _held = false;
            _heldGrabbable = null;
            _heldCfg = null;
        }

        private Grabbable GetInstance(WeaponConfig cfg)
        {
            GameObject prefab = ResolveHeldPrefab(cfg);
            if (prefab == null) return null;

            if (prefab == _throwBallPrefab)
            {
                if (_ballInstance == null) _ballInstance = CreateInstance(_throwBallPrefab, "HeldWeapon(ball)", 1f);
                return _ballInstance;
            }

            if (_instances.TryGetValue(cfg, out Grabbable g))
            {
                if (g != null) return g;
                // Reference-null = creation failed earlier (bad prefab — already logged once); Unity-fake-null
                // = the instance was destroyed externally → drop the stale entry and recreate below.
                if (ReferenceEquals(g, null)) return null;
                _instances.Remove(cfg);
            }
            g = CreateInstance(prefab, "HeldWeapon(" + cfg.id + ")", cfg.holdScale);
            _instances[cfg] = g;
            return g;
        }

        private GameObject ResolveHeldPrefab(WeaponConfig cfg)
        {
            if (cfg == null) return _throwBallPrefab;
            if (cfg.heldPrefab != null) return cfg.heldPrefab;
            // No art wired: ballistic weapons still need SOMETHING in the hand to sell the throw; a
            // trigger/melee weapon with no model just grabs nothing (fires fine without a held prop).
            return cfg.fireMode == FireMode.ThrowBallistic ? _throwBallPrefab : null;
        }

        private Grabbable CreateInstance(GameObject prefab, string label, float scale)
        {
            GameObject go = Instantiate(prefab);
            go.name = label;
            Grabbable g = go.GetComponent<Grabbable>();
            if (g == null) g = go.GetComponentInChildren<Grabbable>();
            if (g == null)
            {
                Debug.LogError("[WeaponHolder] heldPrefab '" + prefab.name + "' has no AutoHand Grabbable — cannot hold it.", this);
                Destroy(go);
                return null;
            }

            // Hand.Grab early-outs on kinematic bodies (Grabbable body objectFree check) — force dynamic.
            Rigidbody rb = g.GetComponent<Rigidbody>();
            if (rb == null) rb = go.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic) rb.isKinematic = false;

            // The grab raycast + AutoHand both expect grabbables on the Grabbable layer (HeldBall's layer) —
            // the MS_WP_* art prefabs aren't guaranteed to be authored there.
            if (_grabbableLayer >= 0) SetLayerRecursively(go.transform, _grabbableLayer);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= Mathf.Max(0.01f, scale);

            go.SetActive(false);
            return g;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
        }

        private static WeaponConfig ResolveEquippedConfig()
        {
#if PHOTON_FUSION
            PlayerCombat combat = PlayerCombat.Local;
            if (combat == null) return null;
            int idx = combat.EquippedIndex;
            if (idx < 0) return null;
            WeaponConfig[] catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            return (catalog != null && idx < catalog.Length) ? catalog[idx] : null;
#else
            return null;
#endif
        }

        private void SetRegistry(bool on)
        {
            if (_rightHand) _activeRight = on ? this : (_activeRight == this ? null : _activeRight);
            else _activeLeft = on ? this : (_activeLeft == this ? null : _activeLeft);
            NudgeLocalHandVisuals();
        }

        /// <summary>The owner's HandWeapon/ThrowController cache their last-seen equip index — force a
        /// re-evaluate so cosmetic visuals hand over to (or back from) this holder immediately instead of
        /// waiting for the next weapon change.</summary>
        private static void NudgeLocalHandVisuals()
        {
#if PHOTON_FUSION
            PlayerCombat combat = PlayerCombat.Local;
            if (combat == null) return;
            HandWeapon[] weapons = combat.GetComponentsInChildren<HandWeapon>(true);
            for (int i = 0; i < weapons.Length; i++) weapons[i].ReevaluateEquip();
            ThrowController[] throwers = combat.GetComponentsInChildren<ThrowController>(true);
            for (int i = 0; i < throwers.Length; i++) throwers[i].ReevaluateHeldVisual();
#endif
        }

        private bool ReadGrip()
        {
            var dev = XRInputDevices.GetDeviceAtXRNode(_rightHand ? XRNode.RightHand : XRNode.LeftHand);
            if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.grip, out float v) && v > 0.6f) return true;
            Keyboard kb = Keyboard.current;
            return kb != null && kb[_editorGripKey].isPressed;
        }
    }
}
