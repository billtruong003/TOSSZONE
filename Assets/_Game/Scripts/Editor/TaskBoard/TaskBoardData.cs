using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TossZone.TaskBoardEditor
{
    // ---- tasks.meta.json (persistent per-task verify metadata) ----
    [Serializable]
    public class TaskMetaEntry
    {
        public string id;
        public string verify;     // recipe Claude runs via Unity MCP
        public string evidence;   // relative path under Docs/ to screenshot
        public string verifiedAt;
        public string result;     // "Pass" / "Fail"
        public string notes;
    }

    [Serializable]
    public class TaskMetaFile
    {
        public List<TaskMetaEntry> entries = new List<TaskMetaEntry>();
    }

    // ---- tasks.json (generated snapshot for tooling / Claude) ----
    [Serializable]
    public class TaskExport
    {
        public string id, section, sectionTitle, title, status;
        public int indent, line;
        public string verify, evidence, verifiedAt, result, notes;
    }

    [Serializable]
    public class PhaseExport
    {
        public string key, title;
        public bool isPhase;
        public int done, total;
        public List<TaskExport> tasks = new List<TaskExport>();
    }

    [Serializable]
    public class TasksExport
    {
        public string generatedAt, sourceFile;
        public int totalTasks, totalDone;
        public List<PhaseExport> phases = new List<PhaseExport>();
    }

    public static class TaskBoardData
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string DocsDir => Path.Combine(ProjectRoot, "Docs");
        public static string DefaultMarkdown => Path.Combine(DocsDir, "TOSSZONE_TaskBreakdown.md");
        public static string MetaPath => Path.Combine(DocsDir, "tasks.meta.json");
        public static string ExportPath => Path.Combine(DocsDir, "tasks.json");
        public static string VerifyDir => Path.Combine(DocsDir, "verify");

        public static TaskMetaFile LoadMeta()
        {
            try
            {
                if (File.Exists(MetaPath))
                    return JsonUtility.FromJson<TaskMetaFile>(File.ReadAllText(MetaPath)) ?? new TaskMetaFile();
            }
            catch (Exception e) { Debug.LogWarning("[TaskBoard] meta load failed: " + e.Message); }
            return new TaskMetaFile();
        }

        public static void SaveMeta(TaskMetaFile meta)
        {
            Directory.CreateDirectory(DocsDir);
            File.WriteAllText(MetaPath, JsonUtility.ToJson(meta, true));
        }

        public static TaskMetaEntry GetOrCreate(TaskMetaFile meta, string id)
        {
            foreach (var e in meta.entries) if (e.id == id) return e;
            var ne = new TaskMetaEntry { id = id };
            meta.entries.Add(ne);
            return ne;
        }

        public static void ApplyMeta(List<TaskPhase> phases, TaskMetaFile meta)
        {
            var map = new Dictionary<string, TaskMetaEntry>();
            foreach (var e in meta.entries) map[e.id] = e;
            foreach (var p in phases)
                foreach (var t in p.tasks)
                    if (map.TryGetValue(t.id, out var e))
                    {
                        t.verify = e.verify; t.evidence = e.evidence;
                        t.verifiedAt = e.verifiedAt; t.result = e.result; t.notes = e.notes;
                    }
        }

        public static void Export(List<TaskPhase> phases, string sourceFile, string nowIso)
        {
            var ex = new TasksExport { generatedAt = nowIso, sourceFile = sourceFile };
            int total = 0, done = 0;
            foreach (var p in phases)
            {
                var pe = new PhaseExport { key = p.key, title = p.title, isPhase = p.isPhase, done = p.Done, total = p.Total };
                foreach (var t in p.tasks)
                {
                    total++;
                    if (t.status == TaskStatus.Done) done++;
                    pe.tasks.Add(new TaskExport
                    {
                        id = t.id, section = t.sectionKey, sectionTitle = t.sectionTitle, title = t.title,
                        status = t.status.ToString(), indent = t.indent, line = t.lineIndex + 1,
                        verify = t.verify, evidence = t.evidence, verifiedAt = t.verifiedAt,
                        result = t.result, notes = t.notes
                    });
                }
                ex.phases.Add(pe);
            }
            ex.totalTasks = total; ex.totalDone = done;
            Directory.CreateDirectory(DocsDir);
            File.WriteAllText(ExportPath, JsonUtility.ToJson(ex, true));
        }
    }
}
