#if PHOTON_FUSION
using BillGameCore;
using TMPro;
using TossZone.Combat;
using UnityEngine;
using UnityEngine.XR;

namespace TossZone.UI
{
    public class AnnouncerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private float _holdSeconds = 1.4f;
        [SerializeField] private float _distance = 2.2f;

        private bool _subscribed;
        private int _lastCountdownSec = -1;

        private void OnEnable() => TrySubscribe();

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
            UpdateMatchEndCountdown();
        }

        // FLOW-04: last-5s countdown before ArenaManager auto-returns this client to the hub. Re-shows the
        // banner once per second; a rematch (Phase back to Warmup) drops Remaining below 0 and stops it.
        private void UpdateMatchEndCountdown()
        {
            ArenaManager am = ArenaManager.Instance;
            float remain = am != null ? am.ReturnToHubRemaining : -1f;
            if (remain < 0f) { _lastCountdownSec = -1; return; }
            int sec = Mathf.CeilToInt(remain);
            if (sec > 5 || sec == _lastCountdownSec) return;
            _lastCountdownSec = sec;
            Show("VỀ HUB SAU " + sec + "s", Color.white);
        }

        private void TrySubscribe()
        {
            if (_subscribed || !Bill.IsReady) return;
            _subscribed = true;
            Bill.Events.Subscribe<RoundEndEvent>(OnRoundEnd);
            Bill.Events.Subscribe<MatchEndEvent>(OnMatchEnd);
            Bill.Events.Subscribe<PlayerDiedEvent>(OnDied);
            Bill.Events.Subscribe<PlayerRespawnedEvent>(OnRespawned);
            Bill.Events.Subscribe<PlayerFrozenEvent>(OnFrozen);
            Bill.Events.Subscribe<BallCaughtEvent>(OnCaught);
            Bill.Events.Subscribe<DeflectEvent>(OnDeflect);
        }

        private void OnDisable()
        {
            if (!_subscribed || !Bill.IsReady) return;
            _subscribed = false;
            Bill.Events.Unsubscribe<RoundEndEvent>(OnRoundEnd);
            Bill.Events.Unsubscribe<MatchEndEvent>(OnMatchEnd);
            Bill.Events.Unsubscribe<PlayerDiedEvent>(OnDied);
            Bill.Events.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);
            Bill.Events.Unsubscribe<PlayerFrozenEvent>(OnFrozen);
            Bill.Events.Unsubscribe<BallCaughtEvent>(OnCaught);
            Bill.Events.Unsubscribe<DeflectEvent>(OnDeflect);
        }

        private static int LocalTeam()
        {
            PlayerCombat pc = PlayerCombat.Local;
            if (pc == null || pc.Object == null || !pc.Object.IsValid) return -1;
            return ArenaManager.GetTeam(pc.Object.InputAuthority);
        }

        private void OnRoundEnd(RoundEndEvent e)
        {
            string score = "  " + e.ScoreA + " - " + e.ScoreB;
            if (e.WinnerTeam < 0) { Show("HIỆP HÒA" + score, Color.white); return; }
            bool won = e.WinnerTeam == LocalTeam();
            Show(won ? "THẮNG HIỆP!" + score : "THUA HIỆP" + score,
                won ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.3f));
        }

        private void OnMatchEnd(MatchEndEvent e)
        {
            string score = "  " + e.ScoreA + " - " + e.ScoreB;
            if (e.WinnerTeam < 0) { Show("HÒA CHUNG CUỘC" + score, Color.white); return; }
            bool won = e.WinnerTeam == LocalTeam();
            Show(won ? "THẮNG TRẬN!" + score : "THUA TRẬN" + score,
                won ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.3f));
        }

        private void OnDied(PlayerDiedEvent e)
        {
            if (e.IsLocal) Show("BẠN BỊ HẠ", new Color(1f, 0.3f, 0.2f));
        }

        private void OnRespawned(PlayerRespawnedEvent e)
        {
            if (e.IsLocal) Show("HỒI SINH", new Color(0.6f, 0.9f, 1f));
        }

        private void OnFrozen(PlayerFrozenEvent e)
        {
            if (!e.IsLocalVictim) return;
            Show("BỊ ĐÓNG BĂNG " + e.Seconds.ToString("0.#") + "s", new Color(0.4f, 0.9f, 1f));
            Pulse(XRNode.LeftHand, 0.6f);
            Pulse(XRNode.RightHand, 0.6f);
        }

        private void OnCaught(BallCaughtEvent e)
        {
            Show(e.IsPower ? "BẮT POWER! +2 ĐẠN" : "BẮT ĐƯỢC! +1 ĐẠN", new Color(0.4f, 1f, 0.5f));
            Pulse(XRNode.LeftHand, 0.5f);
        }

        private void OnDeflect(DeflectEvent e)
        {
            RewardText.Show("DEFLECT!", e.Point, new Color(1f, 0.9f, 0.3f));
            Pulse(XRNode.RightHand, 0.4f);
        }

        private void Show(string text, Color color)
        {
            Camera cam = Camera.main;
            if (cam == null || _label == null) return;

            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            _label.transform.position = cam.transform.position + fwd * _distance + Vector3.up * 0.2f;
            _label.transform.rotation = Quaternion.LookRotation(fwd);
            _label.text = text;
            _label.color = color;
            _label.gameObject.SetActive(true);

            BillTween.KillTarget(_label.transform);
            _label.transform.localScale = Vector3.one * 0.6f;
            BillTween.Scale(_label.transform, 1f, 0.25f)?.SetEase(EaseType.OutBack).SetTarget(_label.transform);
            BillTween.Float(0f, 1f, _holdSeconds, _ => { })
                ?.SetTarget(_label.transform)
                .OnComplete(() =>
                {
                    BillTween.Float(1f, 0f, 0.3f, a =>
                        {
                            if (_label != null) { Color c = _label.color; c.a = a; _label.color = c; }
                        })
                        ?.SetTarget(_label.transform)
                        .OnComplete(() => { if (_label != null) _label.gameObject.SetActive(false); });
                });
        }

        private static void Pulse(XRNode node, float amplitude)
        {
            InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, amplitude, 0.12f);
        }
    }
}
#endif
