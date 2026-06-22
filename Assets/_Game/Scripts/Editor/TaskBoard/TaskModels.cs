using System.Collections.Generic;

namespace TossZone.TaskBoardEditor
{
    public enum TaskStatus { Todo, InProgress, Done, Blocked }

    /// <summary>One checkbox task parsed from the markdown breakdown.</summary>
    public class TaskItem
    {
        public string id;            // e.g. "1.4.3" (section 1.4, 3rd task)
        public string title;
        public TaskStatus status;
        public int indent;           // nesting depth (0 = top level)
        public int lineIndex;        // 0-based line index in the source file
        public string rawLine;       // original full line (used for robust write-back)
        public string sectionKey;    // e.g. "1.4"
        public string sectionTitle;  // e.g. "1.4 — Cơ chế Ném (Core mechanic)"
        public string phaseKey;      // e.g. "P1"
        public string phaseTitle;    // e.g. "PHASE 1 — Demo Gameplay (Vertical Slice)"

        // Metadata (loaded from tasks.meta.json) — not derivable from markdown.
        public string verify;        // recipe Claude executes via Unity MCP
        public string evidence;      // relative path under Docs/ to screenshot
        public string verifiedAt;    // timestamp string
        public string result;        // "Pass" / "Fail" / null
        public string notes;
    }

    /// <summary>A top-level group (a PHASE heading, or a meta H2 section).</summary>
    public class TaskPhase
    {
        public string key;
        public string title;
        public bool isPhase;
        public List<TaskItem> tasks = new List<TaskItem>();

        public int Done
        {
            get { int n = 0; foreach (var t in tasks) if (t.status == TaskStatus.Done) n++; return n; }
        }
        public int Total { get { return tasks.Count; } }
    }
}
