using System;
using UnityEditor;
using UnityEngine;

namespace TossZone.TaskBoardEditor
{
    /// <summary>
    /// Menu entry points that can be invoked headlessly (e.g. by Claude via Unity MCP
    /// executing a menu item) to keep tasks.json in sync with the markdown.
    /// </summary>
    public static class TaskBoardMenu
    {
        [MenuItem("Tools/TOSSZONE/Export tasks.json", priority = 20)]
        public static void ExportJson()
        {
            var phases = MarkdownTaskParser.Parse(TaskBoardData.DefaultMarkdown);
            var meta = TaskBoardData.LoadMeta();
            TaskBoardData.ApplyMeta(phases, meta);
            TaskBoardData.Export(phases, TaskBoardData.DefaultMarkdown, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            Debug.Log("[TaskBoard] Exported -> " + TaskBoardData.ExportPath);
        }

        [MenuItem("Tools/TOSSZONE/Capture Game View (verify shot)", priority = 21)]
        public static void CaptureGameView()
        {
            System.IO.Directory.CreateDirectory(TaskBoardData.VerifyDir);
            string abs = System.IO.Path.Combine(TaskBoardData.VerifyDir,
                "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(abs);
            Debug.Log("[TaskBoard] Capturing Game View -> " + abs + " (frame kế tiếp).");
        }
    }
}
