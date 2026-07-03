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
    /// Fix strategy (verified on 6000.3.8f1 — EditorSettings has NO *ImportWorker* fields and the
    /// registry holds no worker EditorPrefs, so persisting a setting is impossible on this build;
    /// the session-scoped AssetDatabase API is the only lever):
    ///  1. On load, set AssetDatabase.DesiredWorkerCount = 0 (reflection) — no out-of-process import
    ///     workers means no leaked windows at all. Imports fall back in-process (slower on huge
    ///     reimports; use Tools ▸ TOSSZONE ▸ Fix ▸ Enable Parallel Import before one, it auto-reverts
    ///     next editor restart).
    ///  2. Trim the pool (ForceToDesiredWorkerCount) after every import batch AND every 30 s, so any
    ///     worker Unity spawns anyway dies immediately — taking its leaked windows with it.
    ///  3. Legacy layer: if a future Unity build re-exposes *ImportWorker* fields on EditorSettings,
    ///     pin them via SerializedObject discovery (currently a no-op).
    /// </summary>
    [InitializeOnLoad]
    public static class ImportWorkerGhostWindowFix
    {
        const string AppliedGuardKey = "TossZone.ImportWorkerFix.SessionApplied";
        const int DesiredWorkers = 0;      // 0 = no background import workers -> zero ghost windows
        const int StandbyWorkers = 0;      // no idle workers lingering (each one = leaked blank windows)
        const int IdleShutdownMs = 5000;   // workers exit 5 s after an import finishes
        const double TrimIntervalSeconds = 30d;

        static double _nextTrimTime;

        static ImportWorkerGhostWindowFix()
        {
            // Re-subscribe on EVERY domain reload (managed subscriptions don't survive one) …
            EditorApplication.update += TrimPeriodically;

            // … but only log/apply the one-shot setup once per editor session.
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
            SetDesiredWorkerCount(DesiredWorkers, verbose: false);
            bool forced = ForceToDesiredWorkerCount();
            Debug.Log(forced
                ? "[TOSSZONE Fix] Đã ép Unity đóng các AssetImportWorker thừa — cửa sổ trắng của chúng sẽ biến mất."
                : "[TOSSZONE Fix] Không tìm thấy AssetDatabase.ForceToDesiredWorkerCount trên phiên bản Unity này.");
        }

        [MenuItem("Tools/TOSSZONE/Fix/Enable Parallel Import (big reimport — ghost windows return)", priority = 44)]
        public static void EnableParallelImport()
        {
            SetDesiredWorkerCount(4, verbose: true);
            Debug.Log("[TOSSZONE Fix] Bật lại 4 import worker cho đợt reimport lớn. Trim định kỳ đang TẮT tạm; " +
                      "restart Unity (hoặc menu Kill Idle Import Workers Now) để về chế độ không-cửa-sổ.");
            EditorApplication.update -= TrimPeriodically;   // don't fight the user's explicit choice
        }

        static void Apply(bool verbose)
        {
            int patched = PatchEditorSettings(verbose: false);
            bool desiredSet = SetDesiredWorkerCount(DesiredWorkers, verbose);
            ForceToDesiredWorkerCount();
            if (verbose)
            {
                Debug.Log($"[TOSSZONE Fix] Import-worker ghost-window fix: DesiredWorkerCount→{DesiredWorkers} " +
                          $"({(desiredSet ? "OK" : "API KHÔNG CÓ — fix không hoạt động, báo Claude")}), " +
                          $"trim mỗi {TrimIntervalSeconds}s + sau mỗi import, legacy settings patched={patched}. " +
                          "Menu: Tools ▸ TOSSZONE ▸ Fix.");
            }
        }

        /// <summary>AssetDatabase.DesiredWorkerCount is session-scoped on 6000.3 (nothing to persist),
        /// so this runs on every editor load. Reflection keeps it compile-safe across Unity versions.</summary>
        static bool SetDesiredWorkerCount(int count, bool verbose)
        {
            PropertyInfo prop = typeof(AssetDatabase).GetProperty(
                "DesiredWorkerCount", BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite)
            {
                return false;
            }
            int before = (int)prop.GetValue(null);
            prop.SetValue(null, count);
            int after = (int)prop.GetValue(null);
            if (verbose && after != count)
            {
                Debug.LogWarning($"[TOSSZONE Fix] DesiredWorkerCount bị Unity clamp: yêu cầu {count}, thực tế {after} (trước đó {before}).");
            }
            return true;
        }

        static void TrimPeriodically()
        {
            if (EditorApplication.timeSinceStartup < _nextTrimTime)
            {
                return;
            }
            _nextTrimTime = EditorApplication.timeSinceStartup + TrimIntervalSeconds;
            SetDesiredWorkerCount(DesiredWorkers, verbose: false);
            ForceToDesiredWorkerCount();
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
