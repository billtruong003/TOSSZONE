using UnityEngine;
#if PHOTON_FUSION
using BillGameCore;
using TMPro;
using TossZone.Network;
#endif

namespace TossZone.UI
{
    /// <summary>
    /// Physical room console in the hub (GDD §VII, pragmatic first cut): a standing panel (prefab) with a
    /// HOST button (creates a private room and shows its 5-letter code), a letter keyboard to type a
    /// friend's code + VÀO PHÒNG, and QUICK PLAY to return to public matchmaking. All buttons are
    /// <see cref="PokeButton3D"/>; all connects go through <see cref="ConnectionFlowController"/>.
    /// </summary>
    public class RoomCodeConsole : MonoBehaviour
    {
#if PHOTON_FUSION
        private const float StatusPollInterval = 0.5f;

        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _entryText;
        [SerializeField] private PokeButton3D _hostButton;
        [SerializeField] private PokeButton3D _quickPlayButton;
        [SerializeField] private PokeButton3D _deleteButton;
        [SerializeField] private PokeButton3D _joinButton;
        [SerializeField] private PokeButton3D[] _letterButtons;

        private string _entry = "";
        private string _lastStatus = "";
        private float _nextPoll;

        private void Awake()
        {
            if (_hostButton != null) _hostButton.Poked += _ => OnHost();
            if (_quickPlayButton != null) _quickPlayButton.Poked += _ => OnQuickPlay();
            if (_deleteButton != null) _deleteButton.Poked += _ => OnDelete();
            if (_joinButton != null) _joinButton.Poked += _ => OnJoin();

            string chars = ConnectionFlowController.CodeChars;
            if (_letterButtons != null)
            {
                for (int i = 0; i < _letterButtons.Length && i < chars.Length; i++)
                {
                    char c = chars[i];
                    if (_letterButtons[i] != null) _letterButtons[i].Poked += _ => OnLetter(c);
                }
            }
            RefreshEntry();
        }

        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + StatusPollInterval;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;

            string status;
            FusionNet net = FusionNet.Instance;
            ConnectionFlowController flow = ConnectionFlowController.Instance;
            if (net == null || !net.IsRunning)
                status = flow != null && flow.IsBusy ? "Đang kết nối..." : "Chưa kết nối";
            else if (flow != null && flow.RoomCode != null)
                status = "MÃ PHÒNG: " + flow.RoomCode + "   " + net.PlayerCount + "/" + net.MaxPlayers;
            else
                status = "Phòng công khai   " + net.PlayerCount + "/" + net.MaxPlayers;

            if (status == _lastStatus) return;
            _lastStatus = status;
            _statusText.text = status;
        }

        // ── Button actions ────────────────────────────────────────────────────────────

        private void OnHost()
        {
            ConnectionFlowController flow = ConnectionFlowController.GetOrCreate();
            if (flow.IsBusy) return;
            flow.HostPrivateRoom();
        }

        private void OnQuickPlay()
        {
            ConnectionFlowController flow = ConnectionFlowController.GetOrCreate();
            if (flow.IsBusy) return;
            if (FusionNet.Exists && FusionNet.Instance.IsRunning && flow.RoomCode == null) return;
            flow.QuickPlay();
        }

        private void OnLetter(char c)
        {
            if (_entry.Length >= ConnectionFlowController.CodeLength) return;
            _entry += c;
            RefreshEntry();
        }

        private void OnDelete()
        {
            if (_entry.Length == 0) return;
            _entry = _entry.Substring(0, _entry.Length - 1);
            RefreshEntry();
        }

        private void OnJoin()
        {
            if (_entry.Length != ConnectionFlowController.CodeLength) return;
            ConnectionFlowController flow = ConnectionFlowController.GetOrCreate();
            if (flow.IsBusy) return;
            string code = _entry;
            flow.JoinPrivateRoom(code, ok =>
            {
                if (!ok) return;
                _entry = "";
                RefreshEntry();
            });
        }

        private void RefreshEntry()
        {
            if (_entryText == null) return;
            int len = ConnectionFlowController.CodeLength;
            char[] slots = new char[len * 2 - 1];
            for (int i = 0; i < len; i++)
            {
                slots[i * 2] = i < _entry.Length ? _entry[i] : '_';
                if (i < len - 1) slots[i * 2 + 1] = ' ';
            }
            _entryText.text = new string(slots);
        }
#endif
    }
}
