#if PHOTON_FUSION
using TMPro;
using TossZone.Combat;
using UnityEngine;

namespace TossZone.UI
{
    public class ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private float _fontSize = 6f;
        [SerializeField] private Color _color = Color.white;

        private TextMeshPro _front;
        private TextMeshPro _back;
        private ArenaManager _arena;

        private void Awake()
        {
            _front = CreateFace("Front", Quaternion.identity);
            _back = CreateFace("Back", Quaternion.Euler(0f, 180f, 0f));
        }

        private TextMeshPro CreateFace(string faceName, Quaternion localRot)
        {
            var go = new GameObject("Scoreboard" + faceName);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = localRot;
            go.transform.localPosition = localRot * new Vector3(0f, 0f, -0.01f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = _fontSize;
            tmp.color = _color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 4f);
            // TMP's SDF shader is double-sided by default (_CullMode=0): each viewer saw BOTH faces' glyphs
            // superimposed (the far face mirrored through the near one) — the REG-18 1cm offset couldn't fix
            // that. Cull backfaces so each side only ever sees its own face (PT-02).
            if (tmp.fontMaterial.HasProperty("_CullMode"))
                tmp.fontMaterial.SetFloat("_CullMode", 2f);
            return tmp;
        }

        private void Update()
        {
            if (_arena == null) _arena = FindFirstObjectByType<ArenaManager>();
            bool valid = _arena != null && _arena.Object != null && _arena.Object.IsValid;
            string text = valid ? Compose() : "";
            _front.text = text;
            _back.text = text;
        }

        private string Compose()
        {
            float remaining = _arena.PhaseTimer.IsRunning
                ? _arena.PhaseTimer.RemainingTime(_arena.Runner) ?? 0f
                : 0f;
            string status;
            switch (_arena.Phase)
            {
                case ArenaManager.MatchPhase.Warmup:
                    status = "SẴN SÀNG… " + Mathf.CeilToInt(remaining);
                    break;
                case ArenaManager.MatchPhase.Playing:
                    int t = Mathf.Max(0, Mathf.CeilToInt(remaining));
                    status = (t / 60) + ":" + (t % 60).ToString("00");
                    break;
                case ArenaManager.MatchPhase.RoundEnd:
                    status = "NGHỈ — ĐỔI BÊN " + Mathf.CeilToInt(remaining);
                    break;
                default:
                    status = "KẾT THÚC";
                    break;
            }
            return "<color=#4FA8FF>XANH " + _arena.ScoreA + "</color>  -  <color=#FF5F4F>" + _arena.ScoreB
                + " ĐỎ</color>\nHIỆP " + Mathf.Max(1, _arena.Round) + "\n" + status;
        }
    }
}
#endif
