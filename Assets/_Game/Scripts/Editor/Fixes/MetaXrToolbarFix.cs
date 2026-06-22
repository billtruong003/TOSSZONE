using UnityEditor;
using UnityEngine;

namespace TossZone.EditorFixes
{
    /// <summary>
    /// Workaround for a Meta XR SDK core (v74) × Unity 6000.3 incompatibility.
    ///
    /// Meta.XR.Editor.StatusMenu.StatusIcon re-adds its "Meta XR Tools" button to the main
    /// toolbar on EVERY EditorApplication.update. Unity 6.3 rejects this as an "unsupported
    /// user element" added to a fake toolbar, so the add never sticks and is retried forever —
    /// spamming the console and leaving detached/ghost floating elements that can't be closed.
    ///
    /// The icon is gated by the user pref "Meta.XR.SDK.StatusIcon.Enabled" (default true),
    /// read live each update. Setting it false makes StatusIcon.Update() call Disable() and stop.
    /// Key = KeyPrefix("Meta.XR.SDK") + "." + Uid("StatusIcon.Enabled").
    /// </summary>
    [InitializeOnLoad]
    public static class MetaXrToolbarFix
    {
        const string StatusIconKey = "Meta.XR.SDK.StatusIcon.Enabled";
        const string AppliedGuardKey = "TossZone.MetaXrToolbarFix.AutoAppliedOnce";

        static MetaXrToolbarFix()
        {
            // Auto-apply once, so the ghost-window spam stops as soon as Unity recompiles —
            // without the user having to find a menu in the broken UI. Reversible below.
            if (!EditorPrefs.GetBool(AppliedGuardKey, false))
            {
                EditorPrefs.SetBool(AppliedGuardKey, true);
                if (EditorPrefs.GetBool(StatusIconKey, true))
                {
                    EditorPrefs.SetBool(StatusIconKey, false);
                    Debug.Log("[TOSSZONE Fix] Đã tự tắt Meta XR toolbar status icon để dừng lỗi cửa sổ ma " +
                              "(Meta XR v74 × Unity 6.3). Bật lại: Tools ▸ TOSSZONE ▸ Fix ▸ Re-enable Meta XR Toolbar Icon.");
                }
            }
        }

        [MenuItem("Tools/TOSSZONE/Fix/Disable Meta XR Toolbar Icon", priority = 40)]
        public static void Disable()
        {
            EditorPrefs.SetBool(StatusIconKey, false);
            Debug.Log("[TOSSZONE Fix] Tắt Meta XR toolbar status icon ('" + StatusIconKey + "' = false). " +
                      "Spam dừng ở update kế tiếp. Nếu còn cửa sổ kẹt: Window ▸ Layouts ▸ Default Layout.");
        }

        [MenuItem("Tools/TOSSZONE/Fix/Re-enable Meta XR Toolbar Icon", priority = 41)]
        public static void Enable()
        {
            EditorPrefs.SetBool(StatusIconKey, true);
            Debug.Log("[TOSSZONE Fix] Bật lại Meta XR toolbar status icon (lỗi cửa sổ ma có thể quay lại trên Unity 6.3).");
        }
    }
}
