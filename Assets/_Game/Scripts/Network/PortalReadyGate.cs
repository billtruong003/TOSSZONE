#if PHOTON_FUSION
using BillGameCore;
using BillInspector;
using Fusion;
using TMPro;
using TossZone.UI;
using UnityEngine;

namespace TossZone.Network
{
    /// <summary>
    /// Mutual-consent gate for <see cref="PortalMatchmaker"/>: walking into the portal only teleports the
    /// room to the arena once EVERY active player has pressed "SẴN SÀNG" here. Without this, a single
    /// player brushing the portal trigger (or hosting) yanked everyone else across the map mid-conversation.
    /// Runtime-spawned (master-only, see PortalReadyBootstrap) — hub scene NetworkObjects stay dormant since
    /// the hub isn't Fusion-loaded, same reason RingSpawnerHub is spawned rather than scene-placed.
    /// </summary>
    public class PortalReadyGate : NetworkBehaviour
    {
        public static PortalReadyGate Instance { get; private set; }

        private const float StatusPollInterval = 0.3f;

        [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, NetworkBool> Ready => default;

        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private PokeButton3D _button;
        private string _lastStatus = "";
        private float _nextPoll;

        public bool AllReady
        {
            get
            {
                if (Object == null || !Object.IsValid) return false;
                int count = 0;
                foreach (PlayerRef p in Runner.ActivePlayers)
                {
                    count++;
                    if (!Ready.TryGet(p, out NetworkBool r) || !r) return false;
                }
                return count >= 2;
            }
        }

        public override void Spawned()
        {
            Instance = this;
            if (_button != null) _button.Poked += _ => ToggleLocalReady();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            // Drop entries for players who left — otherwise a stale "ready" from someone who quit could
            // never be cleared and would misreport AllReady for whoever's left (or block it forever if the
            // leaver's slot is reused with a fresh default(false)).
            foreach (var kv in Ready)
            {
                bool active = false;
                foreach (PlayerRef ap in Runner.ActivePlayers) if (ap == kv.Key) { active = true; break; }
                if (!active) { Ready.Remove(kv.Key); break; }   // one per tick is plenty; FUN runs every tick anyway
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetReady(PlayerRef who, NetworkBool ready) => Ready.Set(who, ready);

        private void ToggleLocalReady()
        {
            NetworkRunner runner = Runner;
            if (runner == null) return;
            PlayerRef me = runner.LocalPlayer;
            bool current = Ready.TryGet(me, out NetworkBool r) && r;
            RPC_SetReady(me, !current);
        }

        [BillButton("Toggle My Ready (Play mode)")]
        private void Debug_ToggleMyReady()
        {
            if (Object == null || !Object.IsValid) { Debug.Log("[PortalReadyGate] Chưa spawn/network — chỉ dùng được lúc Play."); return; }
            ToggleLocalReady();
        }

        [BillButton("Log Ready State")]
        private void Debug_LogState()
        {
            if (Object == null || !Object.IsValid) { Debug.Log("[PortalReadyGate] Chưa spawn/network."); return; }
            Debug.Log("[PortalReadyGate] AllReady=" + AllReady);
        }

        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + StatusPollInterval;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusText == null || Object == null || !Object.IsValid) return;
            int total = 0, ready = 0;
            foreach (PlayerRef p in Runner.ActivePlayers)
            {
                total++;
                if (Ready.TryGet(p, out NetworkBool r) && r) ready++;
            }
            bool localReady = Ready.TryGet(Runner.LocalPlayer, out NetworkBool lr) && lr;
            string status = total < 2
                ? "SẴN SÀNG: " + ready + "/" + total + "  (ĐANG CHỜ THÊM NGƯỜI)"
                : "SẴN SÀNG: " + ready + "/" + total + (localReady ? "  (BẠN: OK)" : "");
            if (status == _lastStatus) return;
            _lastStatus = status;
            _statusText.text = status;
            if (_button != null)
                _button.GetComponent<MeshRenderer>().material.color = localReady
                    ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.55f, 0.42f, 0.12f);
        }
    }
}
#endif
