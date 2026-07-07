using UnityEngine;
#if PHOTON_FUSION
using BillGameCore;
using TMPro;
using TossZone.Network;
#endif

namespace TossZone.UI
{
    /// <summary>
    /// Physical room console in the hub (GDD §VII, pragmatic first cut): a standing panel built entirely at
    /// runtime — HOST button (creates a private room and shows its 5-letter code), a letter keyboard to type
    /// a friend's code + VÀO PHÒNG, and QUICK PLAY to return to public matchmaking. All buttons are
    /// <see cref="PokeButton3D"/>; all connects go through <see cref="ConnectionFlowController"/>.
    /// Reposition the scene object freely — everything is local-space children.
    /// </summary>
    public class RoomCodeConsole : MonoBehaviour
    {
#if PHOTON_FUSION
        private const float StatusPollInterval = 0.5f;

        private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.18f);
        private static readonly Color KeyColor = new Color(0.20f, 0.24f, 0.34f);
        private static readonly Color HostColor = new Color(0.15f, 0.45f, 0.28f);
        private static readonly Color QuickColor = new Color(0.16f, 0.32f, 0.52f);
        private static readonly Color JoinColor = new Color(0.55f, 0.42f, 0.12f);
        private static readonly Color DeleteColor = new Color(0.48f, 0.20f, 0.18f);

        private TMP_Text _statusText;
        private TMP_Text _entryText;
        private Material _panelMat;
        private Material _keyMat;
        private readonly System.Collections.Generic.List<Material> _mats = new System.Collections.Generic.List<Material>(8);
        private string _entry = "";
        private string _lastStatus = "";
        private float _nextPoll;

        private void Start() => Build();

        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + StatusPollInterval;
            RefreshStatus();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _mats.Count; i++)
                if (_mats[i] != null) Destroy(_mats[i]);
            _mats.Clear();
        }

        private Material MakeMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
            _mats.Add(mat);
            return mat;
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

        // ── Runtime build ─────────────────────────────────────────────────────────────

        private void Build()
        {
            _panelMat = MakeMaterial(PanelColor);
            _keyMat = MakeMaterial(KeyColor);

            CreateBoard(new Vector3(0f, 0.42f, 0.05f), new Vector3(1.25f, 1.25f, 0.04f));

            CreateLabel("BẢNG PHÒNG", new Vector3(0f, 0.94f, -0.001f), 0.9f, Color.white);
            _statusText = CreateLabel("Chưa kết nối", new Vector3(0f, 0.82f, -0.001f), 0.5f, new Color(0.75f, 0.85f, 1f));

            CreateButton("HOST PHÒNG RIÊNG", new Vector3(-0.3f, 0.66f, 0f), new Vector3(0.5f, 0.11f, 0.05f), HostColor, 0.32f, OnHost);
            CreateButton("QUICK PLAY", new Vector3(0.3f, 0.66f, 0f), new Vector3(0.5f, 0.11f, 0.05f), QuickColor, 0.32f, OnQuickPlay);

            _entryText = CreateLabel("_ _ _ _ _", new Vector3(0f, 0.5f, -0.001f), 0.8f, new Color(1f, 0.9f, 0.5f));

            string chars = ConnectionFlowController.CodeChars;
            const int cols = 8;
            const float step = 0.145f;
            float x0 = -step * (cols - 1) * 0.5f;
            for (int i = 0; i < chars.Length; i++)
            {
                int row = i / cols, col = i % cols;
                Vector3 pos = new Vector3(x0 + col * step, 0.34f - row * step, 0f);
                char c = chars[i];
                CreateButton(c.ToString(), pos, new Vector3(0.12f, 0.12f, 0.05f), KeyColor, 0.55f, () => OnLetter(c));
            }

            CreateButton("XÓA", new Vector3(-0.3f, -0.28f, 0f), new Vector3(0.34f, 0.11f, 0.05f), DeleteColor, 0.32f, OnDelete);
            CreateButton("VÀO PHÒNG", new Vector3(0.25f, -0.28f, 0f), new Vector3(0.5f, 0.11f, 0.05f), JoinColor, 0.32f, OnJoin);
        }

        private void CreateBoard(Vector3 localPos, Vector3 scale)
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(transform, false);
            board.transform.localPosition = localPos;
            board.transform.localScale = scale;
            board.GetComponent<MeshRenderer>().sharedMaterial = _panelMat;
        }

        private void CreateButton(string label, Vector3 localPos, Vector3 size, Color color, float fontSize, System.Action onPoked)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Btn_" + label;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = color == KeyColor ? _keyMat : MakeMaterial(color);

            var button = go.AddComponent<PokeButton3D>();
            button.Poked += _ => onPoked();

            TMP_Text text = CreateLabel(label, Vector3.zero, fontSize, Color.white);
            text.transform.SetParent(go.transform, false);
            text.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            text.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
        }

        private TMP_Text CreateLabel(string content, Vector3 localPos, float fontSize, Color color)
        {
            var go = new GameObject("Label_" + content);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var text = go.AddComponent<TextMeshPro>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
#endif
    }
}
