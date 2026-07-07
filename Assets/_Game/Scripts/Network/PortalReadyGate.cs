#if PHOTON_FUSION
using BillGameCore;
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

        private TMP_Text _statusText;
        private PokeButton3D _button;
        private string _lastStatus = "";
        private float _nextPoll;

        public bool AllReady
        {
            get
            {
                if (Object == null || !Object.IsValid) return false;
                bool any = false;
                foreach (PlayerRef p in Runner.ActivePlayers)
                {
                    any = true;
                    if (!Ready.TryGet(p, out NetworkBool r) || !r) return false;
                }
                return any;
            }
        }

        public override void Spawned()
        {
            Instance = this;
            Build();
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
            string status = "SẴN SÀNG: " + ready + "/" + total + (localReady ? "  (BẠN: OK)" : "");
            if (status == _lastStatus) return;
            _lastStatus = status;
            _statusText.text = status;
            if (_button != null)
                _button.GetComponent<MeshRenderer>().material.color = localReady
                    ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.55f, 0.42f, 0.12f);
        }

        // ── Runtime build (local visuals only — no networked state here) ───────────────

        private void Build()
        {
            var panelMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.10f, 0.12f, 0.18f) };

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(transform, false);
            board.transform.localPosition = new Vector3(0f, 0.3f, 0.05f);
            board.transform.localScale = new Vector3(0.9f, 0.55f, 0.04f);
            board.GetComponent<MeshRenderer>().sharedMaterial = panelMat;

            _statusText = CreateLabel("Chưa sẵn sàng", new Vector3(0f, 0.5f, -0.001f), 0.42f);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Btn_SanSang";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            go.transform.localScale = new Vector3(0.5f, 0.16f, 0.05f);
            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;
            go.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { color = new Color(0.55f, 0.42f, 0.12f) };

            _button = go.AddComponent<PokeButton3D>();
            _button.Poked += _ => ToggleLocalReady();

            TMP_Text label = CreateLabel("SẴN SÀNG", Vector3.zero, 0.28f);
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            label.transform.localScale = new Vector3(1f / go.transform.localScale.x, 1f / go.transform.localScale.y, 1f / go.transform.localScale.z);
        }

        private TMP_Text CreateLabel(string content, Vector3 localPos, float fontSize)
        {
            var go = new GameObject("Label_" + content);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var text = go.AddComponent<TextMeshPro>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
#endif
