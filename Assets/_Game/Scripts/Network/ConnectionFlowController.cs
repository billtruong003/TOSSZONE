#if PHOTON_FUSION
using System;
using BillGameCore;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Owns the whole connect lifecycle as a state machine (Idle → Connecting → Connected → Failed) and is the
    /// ONLY place game code starts/switches Fusion sessions. Quick Play random-joins any open room (max 8);
    /// private rooms use a 5-letter code and are hidden from matchmaking. Fires
    /// <see cref="MatchmakingStatusEvent"/> on every transition, auto-retries the initial connect, and on a
    /// mid-game connection loss fades back to the hub and reconnects.
    /// </summary>
    public class ConnectionFlowController : MonoBehaviour
    {
        public const int RoomCapacity = 8;
        public const string HubSceneName = "01_TOSSZONE_Main";
        public const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 5;

        private const int MaxAutoRetries = 3;
        private const float RetryBaseSeconds = 2f;
        private const float RecoverReconnectDelay = 1.5f;

        public static ConnectionFlowController Instance { get; private set; }

        public static ConnectionFlowController GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[ConnectionFlow]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ConnectionFlowController>();
            var hudPrefab = Resources.Load<GameObject>(TossZone.UI.ConnectionStatusHud.ResourcePath);
            if (hudPrefab != null) Instantiate(hudPrefab, go.transform);
            return Instance;
        }

        public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
        public string RoomCode { get; private set; }
        public bool IsBusy => Phase == MatchPhase.Connecting;

        private bool _subscribed;
        private bool _switching;
        private bool _recovering;
        private bool _quitting;
        private int _retriesLeft;
        private Action<bool> _pendingResult;
        private FusionConnectArgs _pendingArgs;

        public void QuickPlay(Action<bool> onDone = null, bool autoRetry = true)
            => Begin(FusionConnectArgs.Shared(null, -1, RoomCapacity), null, autoRetry ? MaxAutoRetries : 0, onDone);

        public string HostPrivateRoom(Action<bool> onDone = null)
        {
            string code = GenerateCode();
            FusionConnectArgs args = FusionConnectArgs.Shared(code, -1, RoomCapacity);
            args.HideFromMatchmaking = true;
            Begin(args, code, 0, onDone);
            return code;
        }

        public bool JoinPrivateRoom(string code, Action<bool> onDone = null)
        {
            string normalized = NormalizeCode(code);
            if (normalized == null)
            {
                SetPhase(MatchPhase.Failed, "Mã phòng không hợp lệ");
                onDone?.Invoke(false);
                return false;
            }
            FusionConnectArgs args = FusionConnectArgs.Shared(normalized, -1, RoomCapacity);
            args.HideFromMatchmaking = true;
            args.JoinOnly = true;
            Begin(args, normalized, 0, onDone);
            return true;
        }

        public void EnsureConnected()
        {
            FusionNet net = FusionNet.GetOrCreate();
            if (net.IsRunning || net.IsConnecting || IsBusy) return;
            QuickPlay();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.quitting += OnAppQuitting;
        }

        private void OnDestroy()
        {
            Application.quitting -= OnAppQuitting;
            if (_subscribed && Bill.IsReady)
            {
                Bill.Events.Unsubscribe<FusionShutdownEvent>(OnFusionShutdown);
                Bill.Events.Unsubscribe<FusionDisconnectedEvent>(OnFusionDisconnected);
            }
            if (Instance == this) Instance = null;
        }

        private void OnAppQuitting() => _quitting = true;

        private void Begin(FusionConnectArgs args, string code, int retries, Action<bool> onDone)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[ConnectionFlow] Connect ignored — already connecting.");
                onDone?.Invoke(false);
                return;
            }
            EnsureSubscribed();
            RoomCode = code;
            _retriesLeft = retries;
            _pendingResult = onDone;
            _pendingArgs = args;
            SetPhase(MatchPhase.Connecting, code == null ? "Đang tìm phòng..." : "Đang vào phòng " + code + "...");

            FusionNet net = FusionNet.GetOrCreate();
            if (net.IsRunning)
            {
                _switching = true;
                net.Shutdown();
                return;
            }
            Attempt();
        }

        private void Attempt() => FusionNet.GetOrCreate().Connect(_pendingArgs, OnConnectResult);

        private void OnConnectResult(bool ok)
        {
            if (ok)
            {
                SetPhase(MatchPhase.Connected, RoomCode == null ? "Đã vào phòng" : "Đã vào phòng " + RoomCode);
                InvokePendingResult(true);
                return;
            }

            if (_retriesLeft > 0)
            {
                int attemptIndex = MaxAutoRetries - _retriesLeft;
                _retriesLeft--;
                float delay = RetryBaseSeconds * Mathf.Pow(2f, attemptIndex);
                SetPhase(MatchPhase.Connecting, "Kết nối thất bại — thử lại sau " + delay.ToString("0") + "s");
                Bill.Timer.Delay(delay, Attempt);
                return;
            }

            SetPhase(MatchPhase.Failed, RoomCode == null ? "Không thể kết nối máy chủ" : "Không vào được phòng " + RoomCode);
            bool wasPrivate = RoomCode != null;
            RoomCode = null;
            InvokePendingResult(false);
            // A failed private host/join has already torn down the previous session — don't leave the player
            // stranded offline in the hub; fall back to public matchmaking.
            if (wasPrivate) Bill.Timer.Delay(2f, EnsureConnected);
        }

        private void InvokePendingResult(bool ok)
        {
            Action<bool> cb = _pendingResult;
            _pendingResult = null;
            cb?.Invoke(ok);
        }

        private void EnsureSubscribed()
        {
            if (_subscribed || !Bill.IsReady) return;
            _subscribed = true;
            Bill.Events.Subscribe<FusionShutdownEvent>(OnFusionShutdown);
            Bill.Events.Subscribe<FusionDisconnectedEvent>(OnFusionDisconnected);
        }

        private void OnFusionShutdown(FusionShutdownEvent e)
        {
            if (_quitting) return;
            if (_switching)
            {
                _switching = false;
                Attempt();
                return;
            }
            HandleConnectionLost(e.Reason);
        }

        private void OnFusionDisconnected(FusionDisconnectedEvent e) => HandleConnectionLost(e.Reason);

        private void HandleConnectionLost(string reason)
        {
            if (_quitting || _recovering || Phase != MatchPhase.Connected) return;
            _recovering = true;
            RoomCode = null;
            SetPhase(MatchPhase.Failed, "Mất kết nối (" + reason + ") — đang quay về sảnh");
            Bill.Scene.Load(HubSceneName, TransitionType.Fade, 0.5f);
            Bill.Timer.Delay(RecoverReconnectDelay, RecoverReconnect);
        }

        private void RecoverReconnect()
        {
            _recovering = false;
            if (_quitting) return;
            QuickPlay();
        }

        private void SetPhase(MatchPhase phase, string message)
        {
            Phase = phase;
            Debug.Log("[ConnectionFlow] " + phase + ": " + message);
            if (Bill.IsReady) Bill.Events.Fire(new MatchmakingStatusEvent { Phase = phase, Message = message });
        }

        private static string GenerateCode()
        {
            char[] buf = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                buf[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(buf);
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            code = code.Trim().ToUpperInvariant();
            if (code.Length != CodeLength) return null;
            for (int i = 0; i < code.Length; i++)
                if (CodeChars.IndexOf(code[i]) < 0) return null;
            return code;
        }
    }
}
#endif
