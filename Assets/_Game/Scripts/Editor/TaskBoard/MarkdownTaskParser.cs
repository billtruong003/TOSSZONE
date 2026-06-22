using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TossZone.TaskBoardEditor
{
    /// <summary>
    /// Parses the TOSSZONE task-breakdown markdown into phases + tasks, and writes
    /// checkbox status back. The markdown stays the single source of truth.
    /// Status convention: [ ] todo · [/] in-progress · [x] done · [!] blocked.
    /// </summary>
    public static class MarkdownTaskParser
    {
        static readonly Regex H2 = new Regex(@"^##\s+(?<t>.+?)\s*$");
        static readonly Regex H3 = new Regex(@"^###\s+(?<t>.+?)\s*$");
        static readonly Regex TaskLine = new Regex(@"^(?<indent>[ \t]*)-\s+\[(?<mark>.)\]\s+(?<text>.*?)\s*$");
        static readonly Regex LeadingNum = new Regex(@"^(?<num>\d+(?:\.\d+)*)");
        static readonly Regex PhaseNum = new Regex(@"PHASE\s+(?<n>\d+)", RegexOptions.IgnoreCase);

        public static TaskStatus MarkToStatus(char m)
        {
            switch (m)
            {
                case 'x': case 'X': return TaskStatus.Done;
                case '/': case '~': return TaskStatus.InProgress;
                case '!': return TaskStatus.Blocked;
                default: return TaskStatus.Todo;
            }
        }

        public static char StatusToMark(TaskStatus s)
        {
            switch (s)
            {
                case TaskStatus.Done: return 'x';
                case TaskStatus.InProgress: return '/';
                case TaskStatus.Blocked: return '!';
                default: return ' ';
            }
        }

        public static List<TaskPhase> Parse(string filePath)
        {
            var phases = new List<TaskPhase>();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return phases;

            var lines = File.ReadAllLines(filePath);
            TaskPhase curPhase = null;
            string sectionKey = null, sectionTitle = null;
            int taskIdx = 0;
            var idCounts = new Dictionary<string, int>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                var mh2 = H2.Match(line);
                if (mh2.Success)
                {
                    string title = mh2.Groups["t"].Value.Trim();
                    var pm = PhaseNum.Match(title);
                    bool isPhase = pm.Success;
                    string key;
                    if (isPhase) key = "P" + pm.Groups["n"].Value;
                    else { var ln = LeadingNum.Match(title); key = ln.Success ? ln.Groups["num"].Value : Slug(title); }

                    curPhase = new TaskPhase { key = key, title = title, isPhase = isPhase };
                    phases.Add(curPhase);
                    sectionKey = null; sectionTitle = null; taskIdx = 0;
                    continue;
                }

                var mh3 = H3.Match(line);
                if (mh3.Success)
                {
                    sectionTitle = mh3.Groups["t"].Value.Trim();
                    var ln = LeadingNum.Match(sectionTitle);
                    sectionKey = ln.Success ? ln.Groups["num"].Value : Slug(sectionTitle);
                    taskIdx = 0;
                    continue;
                }

                var mt = TaskLine.Match(line);
                if (mt.Success && curPhase != null)
                {
                    taskIdx++;
                    string indentStr = mt.Groups["indent"].Value.Replace("\t", "  ");
                    int indent = indentStr.Length / 2;
                    char mark = mt.Groups["mark"].Value.Length > 0 ? mt.Groups["mark"].Value[0] : ' ';
                    string text = mt.Groups["text"].Value.Trim();

                    string baseId = sectionKey ?? (curPhase.key + ".0");
                    string id = baseId + "." + taskIdx;
                    if (idCounts.ContainsKey(id)) { idCounts[id]++; id = id + "-" + idCounts[id]; }
                    else idCounts[id] = 0;

                    curPhase.tasks.Add(new TaskItem
                    {
                        id = id,
                        title = text,
                        status = MarkToStatus(mark),
                        indent = indent,
                        lineIndex = i,
                        rawLine = line,
                        sectionKey = sectionKey,
                        sectionTitle = sectionTitle,
                        phaseKey = curPhase.key,
                        phaseTitle = curPhase.title
                    });
                }
            }
            return phases;
        }

        /// <summary>Flip a single task's checkbox status in the file. Matches by rawLine, falls back to lineIndex.</summary>
        public static bool WriteStatus(string filePath, TaskItem item, TaskStatus newStatus)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            var lines = new List<string>(File.ReadAllLines(filePath));

            int idx = -1;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] == item.rawLine) { idx = i; break; }
            if (idx < 0 && item.lineIndex >= 0 && item.lineIndex < lines.Count) idx = item.lineIndex;
            if (idx < 0) return false;
            if (!TaskLine.IsMatch(lines[idx])) return false;

            string line = lines[idx];
            int br = line.IndexOf('[');           // checkbox is always the first '[' after the "- "
            if (br < 0 || br + 1 >= line.Length) return false;

            var sb = new StringBuilder(line);
            sb[br + 1] = StatusToMark(newStatus);
            lines[idx] = sb.ToString();
            File.WriteAllLines(filePath, lines);

            item.status = newStatus;
            item.rawLine = lines[idx];
            item.lineIndex = idx;
            return true;
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_')
                { if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-'); }
                if (sb.Length >= 24) break;
            }
            return sb.ToString().Trim('-');
        }
    }
}
