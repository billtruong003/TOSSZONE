using UnityEngine;
#if PHOTON_FUSION
using System;
using System.Collections;
using BillGameCore;
#endif

namespace TossZone.Network
{
    /// <summary>
    /// Turns the splash screen into a real loading gate (connect-before-reveal): registers a
    /// <see cref="BillGameCore.BillStartup"/> async step that Quick-Play-connects via
    /// <see cref="ConnectionFlowController"/> and only lets the hub load once the player is in a room.
    /// Retries forever on failure with live status text. Sits next to BillStartup in 00_Bootstrap.
    /// </summary>
    public class StartupConnectStep : MonoBehaviour
    {
#if PHOTON_FUSION
        private const float AttemptTimeoutSeconds = 30f;
        private const float RetryDelaySeconds = 3f;

        [SerializeField] private BillStartup _startup;

        private void Awake()
        {
            if (_startup == null) _startup = GetComponent<BillStartup>();
            if (_startup == null) _startup = FindFirstObjectByType<BillStartup>();
            if (_startup == null)
            {
                Debug.LogError("[StartupConnect] No BillStartup in bootstrap scene — connect gate skipped.");
                return;
            }
            _startup.AddStepAsync("Đang kết nối máy chủ...", ConnectRoutine);
        }

        private IEnumerator ConnectRoutine(Action<string> log)
        {
            ConnectionFlowController flow = ConnectionFlowController.GetOrCreate();
            int attempt = 0;
            while (true)
            {
                attempt++;
                bool done = false, ok = false;
                flow.QuickPlay(r => { done = true; ok = r; }, autoRetry: false);

                float elapsed = 0f;
                while (!done && elapsed < AttemptTimeoutSeconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (ok)
                {
                    SetStatus("Đã vào phòng!");
                    yield break;
                }

                log?.Invoke("connect attempt " + attempt + " failed");
                SetStatus("Kết nối thất bại — thử lại (" + attempt + ")...");
                yield return new WaitForSeconds(RetryDelaySeconds);
            }
        }

        private void SetStatus(string msg)
        {
            if (_startup != null && _startup.statusText != null) _startup.statusText.text = msg;
        }
#endif
    }
}
