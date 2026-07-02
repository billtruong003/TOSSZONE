using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TossZone.EditorFixes
{
    /// <summary>
    /// Workaround for Unity AssetImportWorker processes leaking blank container windows.
    ///
    /// On this machine (Unity 6000.3 × Windows 11), the parallel AssetImportWorker subprocesses
    /// (Unity.exe -batchMode -adb2 …) each leak 1+ visible, empty 136×39 "UnityContainerWndClass"
    /// windows instead of staying headless. They pile up over a long session (10–20+ blank windows
    /// in the taskbar). Proven via Win32 EnumWindows: every blank window belongs to a worker PID,
    /// never to the main editor. NOT caused by BillGameCore or Meta XR.
    ///
    /// Fix strategy (two layers):
    ///  1. Persist project settings so idle workers shut themselves down: StandbyImportWorkerCount = 0
    ///     and a short idle-shutdown delay. Fields are located by name through SerializedObject so the
    ///     fix survives Unity renaming/moving them (they are absent from EditorSettings.asset until set).
    ///  2. After every import batch, force the worker pool back down (AssetDatabase.ForceToDesiredWorkerCount,
    ///     called via reflection) — dead workers take their leaked windows with them.
    /// </summary>
    [InitializeOnLoad]
    public static class ImportWorkerGhostWindowFix
    {
        const string AppliedGuardKey = "TossZone.ImportWorkerFix.SessionApplied";
        const int DesiredWorkers = 2;      // enough for parallel import bursts
        const int StandbyWorkers = 0;      // no idle workers lingering (each one = leaked blank windows)
        const int IdleShutdownMs = 5000;   // workers exit 5 s after an import finishes

        static ImportWorkerGhostWindowFix()
        {
            if (SessionState.GetBool(AppliedGuardKey, false))
            {
                return;
            }
            SessionState.SetBool(AppliedGuardKey, true);
            EditorApplication.delayCall += () => Apply(verbose: true);
        }

        [MenuItem("Tools/TOSSZONE/Fix/Apply Import Worker Window Fix", priority = 42)]
        public static void ApplyFromMenu() => Apply(verbose: true);

        [MenuItem("Tools/TOSSZONE/Fix/Kill Idle Import Workers Now", priority = 43)]
        public static void KillIdleWorkersNow()
        {
            bool forced = ForceToDesiredWorkerCount();
            Debug.Log(forced
                ? "[TOSSZONE Fix] Đã ép Unity đóng các AssetImportWorker thừa — cửa sổ trắng của chúng sẽ biến mất."
                : "[TOSSZONE Fix] Không tìm thấy AssetDatabase.ForceToDesiredWorkerCount trên phiên bản Unity này.");
        }

        static void Apply(bool verbose)
        {
            int patched = PatchEditorSettings(verbose);
            ForceToDesiredWorkerCount();
            if (verbose)
            {
                Debug.Log($"[TOSSZONE Fix] Import-worker ghost-window fix: {patched} setting(s) persisted " +
                          $"(standby={StandbyWorkers}, desired={DesiredWorkers}, idleShutdown={IdleShutdownMs}ms). " +
                          "Worker rảnh sẽ tự thoát → hết cửa sổ trắng tích tụ. Menu: Tools ▸ TOSSZONE ▸ Fix.");
            }
        }

        /// <summary>Finds every int property on the EditorSettings singleton whose name mentions
        /// ImportWorker and pins it: Standby→0, Desired→2, Idle/Shutdown→5000 ms. Returns count patched.</summary>
        static int PatchEditorSettings(bool verbose)
        {
            EditorSettings[] all = Resources.FindObjectsOfTypeAll<EditorSettings>();
            if (all.Length == 0)
            {
                Debug.LogWarning("[TOSSZONE Fix] Không lấy được EditorSettings singleton — bỏ qua bước persist.");
                return 0;
            }

            var so = new SerializedObject(all[0]);
            int patched = 0;
            SerializedProperty prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = false;   // only top-level fields of EditorSettings
                if (prop.propertyType != SerializedPropertyType.Integer)
                {
                    continue;
                }
                string name = prop.name.ToLowerInvariant();
                if (!name.Contains("importworker"))
                {
                    continue;
                }

                int target = name.Contains("standby") ? StandbyWorkers
                           : name.Contains("desired") ? DesiredWorkers
                           : name.Contains("idle") || name.Contains("shutdown") ? IdleShutdownMs
                           : prop.intValue;
                if (prop.intValue != target)
                {
                    if (verbose)
                    {
                        Debug.Log($"[TOSSZONE Fix] EditorSettings.{prop.name}: {prop.intValue} → {target}");
                    }
                    prop.intValue = target;
                    patched++;
                }
            }

            if (patched > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();   // flushes ProjectSettings/EditorSettings.asset
            }
            else if (verbose)
            {
                Debug.Log("[TOSSZONE Fix] Import-worker settings đã đúng giá trị (hoặc Unity build này không " +
                          "expose field nào tên *ImportWorker* — khi đó chỉ còn lớp ForceToDesiredWorkerCount).");
            }
            return patched;
        }

        /// <summary>Reflection call so a Unity version without the API degrades gracefully instead of failing to compile.</summary>
        static bool ForceToDesiredWorkerCount()
        {
            MethodInfo method = typeof(AssetDatabase).GetMethod(
                "ForceToDesiredWorkerCount", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                return false;
            }
            method.Invoke(null, null);
            return true;
        }

        /// <summary>After every import batch, trim the worker pool once the editor is idle again.</summary>
        sealed class TrimWorkersAfterImport : AssetPostprocessor
        {
            static bool _queued;

            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (_queued)
                {
                    return;
                }
                _queued = true;
                EditorApplication.delayCall += () =>
                {
                    _queued = false;
                    ForceToDesiredWorkerCount();
                };
            }
        }
    }
}
