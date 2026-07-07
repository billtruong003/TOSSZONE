#if PHOTON_FUSION
using TMPro;
using TossZone.Combat;
using UnityEngine;

namespace TossZone.UI
{
    public class ScoreboardUI : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _front;
        [SerializeField] private TextMeshPro _back;

        private ArenaManager _arena;

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
