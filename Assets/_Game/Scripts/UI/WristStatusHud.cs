#if PHOTON_FUSION
using TMPro;
using TossZone.Combat;
using TossZone.Player;
using UnityEngine;

namespace TossZone.UI
{
    public class WristStatusHud : MonoBehaviour
    {
        [SerializeField] private Vector3 _localOffset = new Vector3(0f, 0.09f, -0.05f);
        [SerializeField] private float _fontSize = 0.5f;

        private TextMeshPro _label;

        private void LateUpdate()
        {
            PlayerRig rig = PlayerRig.Local;
            PlayerCombat combat = PlayerCombat.Local;
            if (rig == null || rig.WristL == null || combat == null || combat.Object == null || !combat.Object.IsValid)
            {
                if (_label != null) _label.gameObject.SetActive(false);
                return;
            }
            if (_label == null) CreateLabel();
            if (_label.transform.parent != rig.WristL)
            {
                _label.transform.SetParent(rig.WristL, false);
                _label.transform.localPosition = _localOffset;
            }
            _label.gameObject.SetActive(true);

            Camera cam = Camera.main;
            if (cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);

            _label.text = "$" + combat.Money + "\n" + AmmoLine(combat);
        }

        private string AmmoLine(PlayerCombat combat)
        {
            int idx = combat.EquippedIndex;
            if (idx < 0) return "∞";
            WeaponConfig[] catalog = CombatSession.Instance != null ? CombatSession.Instance.CurrentCatalog : null;
            WeaponConfig cfg = catalog != null && idx < catalog.Length ? catalog[idx] : null;
            if (cfg == null) return "∞";
            if (!cfg.IsPayPerUse) return "∞";
            return combat.AmmoFor(idx) + "/" + Mathf.Max(1, cfg.magazine);
        }

        private void CreateLabel()
        {
            var go = new GameObject("WristStatusHud");
            _label = go.AddComponent<TextMeshPro>();
            _label.fontSize = _fontSize;
            _label.color = new Color(0.85f, 1f, 0.85f);
            _label.alignment = TextAlignmentOptions.Center;
            _label.rectTransform.sizeDelta = new Vector2(0.5f, 0.2f);
        }
    }
}
#endif
