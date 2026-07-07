using BillGameCore;
using TMPro;
using TossZone.Network;
using UnityEngine;

namespace TossZone.UI
{
    /// <summary>
    /// Minimal world-space readout for <see cref="MatchmakingStatusEvent"/> — floats in front of the camera
    /// while connecting/failed, fades away shortly after Connected. Instantiated from
    /// Assets/_Game/Resources/UI/ConnectionStatusHud.prefab by <see cref="ConnectionFlowController"/> onto
    /// its own DDOL object, so connection feedback exists in every scene including the hub.
    /// </summary>
    public class ConnectionStatusHud : MonoBehaviour
    {
        public const string ResourcePath = "UI/ConnectionStatusHud";

        private const float Distance = 2f;
        private const float HeightOffset = -0.25f;
        private const float ConnectedLinger = 2.5f;

        [SerializeField] private TextMeshPro _text;
        private bool _subscribed;
        private float _hideAt = -1f;

        private void Update()
        {
            if (!_subscribed && Bill.IsReady)
            {
                _subscribed = true;
                Bill.Events.Subscribe<MatchmakingStatusEvent>(OnStatus);
            }
        }

        private void OnDestroy()
        {
            if (_subscribed && Bill.IsReady) Bill.Events.Unsubscribe<MatchmakingStatusEvent>(OnStatus);
        }

        private void OnStatus(MatchmakingStatusEvent e)
        {
            if (_text == null) return;
            _text.text = e.Message;
            _text.color = e.Phase == MatchPhase.Connected ? new Color(0.5f, 1f, 0.6f)
                : e.Phase == MatchPhase.Failed || e.Phase == MatchPhase.TimedOut ? new Color(1f, 0.45f, 0.4f)
                : Color.white;
            _text.gameObject.SetActive(true);
            _hideAt = e.Phase == MatchPhase.Connected ? Time.unscaledTime + ConnectedLinger : float.MaxValue;
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.gameObject.activeSelf) return;

            if (Time.unscaledTime > _hideAt)
            {
                _text.gameObject.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;
            Transform t = _text.transform;
            Vector3 target = cam.transform.position + cam.transform.forward * Distance + Vector3.up * HeightOffset;
            t.position = Vector3.Lerp(t.position, target, 8f * Time.unscaledDeltaTime);
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
        }
    }
}
