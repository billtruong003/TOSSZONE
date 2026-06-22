using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TossZone.TaskBoardEditor
{
    /// <summary>
    /// Visual task board (Tools ▸ TOSSZONE ▸ Task Board). Reads the markdown breakdown,
    /// shows phases/tasks with status, lets you cycle status (written back to the .md),
    /// edit verify recipes, and capture screenshot evidence into Docs/verify/.
    /// </summary>
    public class TaskBoardWindow : EditorWindow
    {
        const string PrefPath = "TossZone.TaskBoard.MarkdownPath";

        string _mdPath;
        List<TaskPhase> _phases = new List<TaskPhase>();
        TaskMetaFile _meta = new TaskMetaFile();
        Vector2 _scroll;
        readonly Dictionary<string, bool> _fold = new Dictionary<string, bool>();
        string _filter = "";
        bool _hideDone;

        [MenuItem("Tools/TOSSZONE/Task Board %#t")]
        public static void Open()
        {
            var w = GetWindow<TaskBoardWindow>("TOSSZONE Tasks");
            w.minSize = new Vector2(540, 420);
            w.Reload();
            w.Show();
        }

        void OnEnable()
        {
            _mdPath = EditorPrefs.GetString(PrefPath, TaskBoardData.DefaultMarkdown);
            Reload();
        }

        void Reload()
        {
            if (string.IsNullOrEmpty(_mdPath)) _mdPath = TaskBoardData.DefaultMarkdown;
            _phases = MarkdownTaskParser.Parse(_mdPath);
            _meta = TaskBoardData.LoadMeta();
            TaskBoardData.ApplyMeta(_phases, _meta);
            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_phases.Count == 0)
                EditorGUILayout.HelpBox("Không đọc được task nào.\nFile: " + _mdPath, MessageType.Warning);
            foreach (var p in _phases) DrawPhase(p);
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(58))) Reload();
            if (GUILayout.Button("Export tasks.json", EditorStyles.toolbarButton, GUILayout.Width(118)))
            {
                TaskBoardData.Export(_phases, _mdPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                ShowNotification(new GUIContent("Exported tasks.json"));
            }
            if (GUILayout.Button("Open .md", EditorStyles.toolbarButton, GUILayout.Width(70)))
                if (File.Exists(_mdPath)) EditorUtility.RevealInFinder(_mdPath);
            GUILayout.FlexibleSpace();
            _hideDone = GUILayout.Toggle(_hideDone, "Hide done", EditorStyles.toolbarButton, GUILayout.Width(78));
            GUILayout.Label("Find", GUILayout.Width(30));
            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(170));
            EditorGUILayout.EndHorizontal();
        }

        void DrawSummary()
        {
            int total = 0, done = 0, prog = 0;
            foreach (var p in _phases)
                foreach (var t in p.tasks)
                {
                    total++;
                    if (t.status == TaskStatus.Done) done++;
                    else if (t.status == TaskStatus.InProgress) prog++;
                }
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Tổng: {total}   ✓ {done}   /{prog} đang làm", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
            Rect bar = EditorGUILayout.GetControlRect(false, 6);
            EditorGUI.ProgressBar(bar, total > 0 ? (float)done / total : 0f, total > 0 ? $"{done}/{total}" : "");
        }

        void DrawPhase(TaskPhase p)
        {
            var visible = Filter(p);
            if (!string.IsNullOrEmpty(_filter) && visible.Count == 0) return;

            EditorGUILayout.Space(4);
            bool open = EditorGUILayout.Foldout(GetFold(p.key, p.isPhase), $"{p.title}   ({p.Done}/{p.Total})", true, EditorStyles.foldoutHeader);
            _fold[p.key] = open;
            Rect bar = EditorGUILayout.GetControlRect(false, 4);
            EditorGUI.ProgressBar(bar, p.Total > 0 ? (float)p.Done / p.Total : 0f, "");
            if (!open) return;
            foreach (var t in visible) DrawTask(t);
        }

        void DrawTask(TaskItem t)
        {
            if (_hideDone && t.status == TaskStatus.Done) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14 + t.indent * 16);

            string label; Color color;
            StatusVisual(t.status, out label, out color);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(label, GUILayout.Width(40))) CycleStatus(t);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            { StatusContextMenu(t); Event.current.Use(); }
            GUI.backgroundColor = prev;

            var style = new GUIStyle(EditorStyles.label) { wordWrap = false };
            if (t.status == TaskStatus.Done) style.normal.textColor = new Color(0.55f, 0.7f, 0.55f);
            if (GUILayout.Button(new GUIContent(t.title), style)) _fold[t.id] = !GetFold(t.id, false);

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(t.evidence)) GUILayout.Label("[img]", EditorStyles.miniLabel, GUILayout.Width(34));
            if (!string.IsNullOrEmpty(t.result)) GUILayout.Label(t.result, EditorStyles.miniBoldLabel, GUILayout.Width(34));
            if (GUILayout.Button("…", GUILayout.Width(24))) _fold[t.id] = !GetFold(t.id, false);
            EditorGUILayout.EndHorizontal();

            if (GetFold(t.id, false)) DrawTaskDetail(t);
        }

        void DrawTaskDetail(TaskItem t)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"ID  {t.id}", $"line {t.lineIndex + 1}", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Verify recipe (Claude chạy qua Unity MCP):", EditorStyles.miniBoldLabel);
            string verify = EditorGUILayout.TextArea(t.verify ?? "", GUILayout.MinHeight(38));

            EditorGUILayout.BeginHorizontal();
            string[] results = { "—", "Pass", "Fail" };
            int ri = t.result == "Pass" ? 1 : t.result == "Fail" ? 2 : 0;
            ri = EditorGUILayout.Popup("Result", ri, results, GUILayout.Width(220));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Capture screenshot", GUILayout.Width(160))) CaptureShot(t);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(t.evidence))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Evidence", t.evidence);
                if (GUILayout.Button("Open", GUILayout.Width(56)))
                {
                    string abs = Path.Combine(TaskBoardData.DocsDir, t.evidence);
                    if (File.Exists(abs)) EditorUtility.RevealInFinder(abs);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("Notes:", EditorStyles.miniBoldLabel);
            string notes = EditorGUILayout.TextArea(t.notes ?? "", GUILayout.MinHeight(28));

            if (EditorGUI.EndChangeCheck())
            {
                t.verify = verify;
                t.notes = notes;
                t.result = ri == 1 ? "Pass" : ri == 2 ? "Fail" : null;
                PersistMeta(t);
            }

            if (!string.IsNullOrEmpty(t.verifiedAt))
                EditorGUILayout.LabelField("Verified at", t.verifiedAt, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        // ---- actions ----

        void CycleStatus(TaskItem t)
        {
            TaskStatus next =
                t.status == TaskStatus.Todo ? TaskStatus.InProgress :
                t.status == TaskStatus.InProgress ? TaskStatus.Done :
                t.status == TaskStatus.Done ? TaskStatus.Blocked :
                TaskStatus.Todo;
            SetStatus(t, next);
        }

        void StatusContextMenu(TaskItem t)
        {
            var m = new GenericMenu();
            foreach (TaskStatus s in Enum.GetValues(typeof(TaskStatus)))
            {
                var cap = s;
                m.AddItem(new GUIContent(s.ToString()), t.status == s, () => SetStatus(t, cap));
            }
            m.ShowAsContext();
        }

        void SetStatus(TaskItem t, TaskStatus s)
        {
            if (!MarkdownTaskParser.WriteStatus(_mdPath, t, s))
                Debug.LogWarning("[TaskBoard] Không ghi được status cho " + t.id);
            Repaint();
        }

        void CaptureShot(TaskItem t)
        {
            Directory.CreateDirectory(TaskBoardData.VerifyDir);
            string fileName = t.id.Replace('/', '_').Replace('.', '_') + ".png";
            string abs = Path.Combine(TaskBoardData.VerifyDir, fileName);
            ScreenCapture.CaptureScreenshot(abs);
            t.evidence = "verify/" + fileName;
            t.verifiedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            PersistMeta(t);
            Debug.Log("[TaskBoard] Screenshot -> " + abs + " (xuất hiện ở frame kế tiếp).");
        }

        void PersistMeta(TaskItem t)
        {
            var e = TaskBoardData.GetOrCreate(_meta, t.id);
            e.verify = t.verify; e.evidence = t.evidence;
            e.verifiedAt = t.verifiedAt; e.result = t.result; e.notes = t.notes;
            TaskBoardData.SaveMeta(_meta);
        }

        // ---- helpers ----

        List<TaskItem> Filter(TaskPhase p)
        {
            if (string.IsNullOrEmpty(_filter)) return p.tasks;
            var f = _filter.ToLowerInvariant();
            var list = new List<TaskItem>();
            foreach (var t in p.tasks)
                if ((t.title ?? "").ToLowerInvariant().Contains(f) || (t.id ?? "").Contains(f))
                    list.Add(t);
            return list;
        }

        bool GetFold(string key, bool def)
        {
            if (!_fold.ContainsKey(key)) _fold[key] = def;
            return _fold[key];
        }

        static void StatusVisual(TaskStatus s, out string label, out Color color)
        {
            switch (s)
            {
                case TaskStatus.Done: label = "[x]"; color = new Color(0.42f, 0.82f, 0.42f); break;
                case TaskStatus.InProgress: label = "[/]"; color = new Color(1f, 0.82f, 0.32f); break;
                case TaskStatus.Blocked: label = "[!]"; color = new Color(0.92f, 0.45f, 0.45f); break;
                default: label = "[ ]"; color = new Color(0.78f, 0.78f, 0.78f); break;
            }
        }
    }
}
