using UnityEngine;

namespace TossZone.Minigame
{
    /// <summary>
    /// A hub portal: step into it (trigger) to enter a minigame — Gorilla-Tag-style. Put on a trigger collider in
    /// the hub (<c>01_TOSSZONE_Main</c>) and assign the <see cref="MinigameDef"/>. After the player dwells inside
    /// for <see cref="_dwell"/> seconds, it calls <see cref="MinigameManager.Enter(MinigameDef)"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MinigamePortal : MonoBehaviour
    {
        [SerializeField] private MinigameDef _minigame;
        [Tooltip("Only colliders on these layers arm the portal (e.g. the player body). Default = any.")]
        [SerializeField] private LayerMask _playerMask = ~0;
        [Tooltip("Seconds the player must stay inside before it fires (avoids accidental entry).")]
        [SerializeField] private float _dwell = 0.6f;

        private float _timer;
        private bool _inside;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other)) { _inside = true; _timer = 0f; }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other)) { _inside = false; _timer = 0f; }
        }

        private void Update()
        {
            if (!_inside || _minigame == null) return;
            _timer += Time.deltaTime;
            if (_timer < _dwell) return;

            _inside = false;
            if (MinigameManager.Instance != null) MinigameManager.Instance.Enter(_minigame);
            else Debug.LogWarning("[MinigamePortal] No MinigameManager in the session.");
        }

        private bool IsPlayer(Collider other) => (_playerMask.value & (1 << other.gameObject.layer)) != 0;
    }
}
