using BillGameCore;
using TossZone.Player;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Walk-through-portal matchmaking. Sits on the [ArenaPortal] trigger in 01_TOSSZONE_Main.
    /// When the local player enters, it starts a Fusion Shared session that loads 02_Arena
    /// (master loads the scene, clients follow). A 30s timeout guards the connect step.
    /// </summary>
    public class PortalMatchmaker : MonoBehaviour
    {
        [SerializeField] private string _sessionName = "TOSSZONE_DEMO";
        [SerializeField] private int _arenaSceneIndex = 2;
        [SerializeField] private string _arenaSceneName = "02_Arena";
        [SerializeField] private float _connectTimeoutSeconds = 30f;

        public bool IsBusy { get; private set; }

        private TimerHandle _timeoutTimer;
        private bool _timeoutActive;
#if PHOTON_FUSION
        private FusionNet _net;
#endif

        private void OnTriggerEnter(Collider other)
        {
            if (IsBusy) return;
            // The local player is the only thing carrying a LocalPlayerRig (on the XRPlayer root),
            // so this matches any of its colliders regardless of which layer AutoHand puts them on.
            if (other.GetComponentInParent<LocalPlayerRig>() == null) return;
            StartMatch();
        }

        public void StartMatch()
        {
            if (IsBusy) return;
            IsBusy = true;
            SetPhase(MatchPhase.Connecting, "Đang kết nối...");
#if PHOTON_FUSION
            _net = FusionNet.GetOrCreate();
            _net.Connected += OnConnected;
            _net.ConnectFailed += OnConnectFailed;
            _net.StartFailed += OnStartFailed;
            _net.StartShared(_sessionName, _arenaSceneIndex, 0, OnStartResult);
            _timeoutTimer = Bill.Timer.Delay(_connectTimeoutSeconds, OnTimeout);
            _timeoutActive = true;
#else
            // No Fusion (e.g. PC test build) → just load the arena locally.
            Bill.Scene.Load(_arenaSceneName, TransitionType.Fade);
#endif
        }

        private void OnDisable()
        {
            Cleanup();
        }

#if UNITY_EDITOR
        // PC test without VR: enter Play, select [ArenaPortal], right-click this component → run this.
        [ContextMenu("DEV ▸ Start Match (no VR)")]
        private void DevStartMatch() => StartMatch();
#endif

#if PHOTON_FUSION
        private void OnStartResult(bool ok)
        {
            if (!ok) Fail(MatchPhase.Failed, "Không khởi tạo được phiên");
        }

        private void OnConnected()
        {
            CancelTimeout();
            SetPhase(MatchPhase.Connected, "Đã kết nối — vào sân");
            // Main scene unloads as Fusion loads the arena; events are released in OnDisable.
        }

        private void OnConnectFailed(string reason)
        {
            Fail(MatchPhase.Failed, "Kết nối lỗi: " + reason);
        }

        private void OnStartFailed(string reason)
        {
            Fail(MatchPhase.Failed, "Lỗi khởi tạo: " + reason);
        }

        private void OnTimeout()
        {
            _timeoutActive = false;
            if (_net != null && _net.IsConnected) return;
            if (_net != null) _net.Shutdown();
            Fail(MatchPhase.TimedOut, "Hết 30s — đi lại vào cổng để thử lại");
        }

        private void Fail(MatchPhase phase, string message)
        {
            Cleanup();
            IsBusy = false;
            SetPhase(phase, message);
        }
#endif

        private void Cleanup()
        {
            CancelTimeout();
#if PHOTON_FUSION
            if (_net != null)
            {
                _net.Connected -= OnConnected;
                _net.ConnectFailed -= OnConnectFailed;
                _net.StartFailed -= OnStartFailed;
            }
#endif
        }

        private void CancelTimeout()
        {
            if (!_timeoutActive) return;
            _timeoutTimer.Cancel();
            _timeoutActive = false;
        }

        private void SetPhase(MatchPhase phase, string message)
        {
            Debug.Log("[Matchmaking] " + phase + " — " + message);
            Bill.Events.Fire(new MatchmakingStatusEvent { Phase = phase, Message = message });
        }
    }
}
