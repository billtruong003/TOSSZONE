using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TossZone.DevTools
{
    /// <summary>
    /// Editor helpers to force-install UPM packages via Client.Add (resolves immediately and reports
    /// errors to the console instead of waiting for the editor to regain focus). Dev-only utility.
    /// </summary>
    public static class PackageInstaller
    {
        private static AddRequest _request;

        [MenuItem("Tools/TOSSZONE/Install ParrelSync")]
        public static void InstallParrelSync() => Add("https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync");

        [MenuItem("Tools/TOSSZONE/Install Stylized Toon World Kit")]
        public static void InstallToonKit() => Add("https://github.com/billtruong003/stylized-toon-world-kit.git?path=/Assets/StylizedToonWorldKit");

        [MenuItem("Tools/TOSSZONE/Install XR Interaction Toolkit")]
        public static void InstallXri() => Add("com.unity.xr.interaction.toolkit@3.3.1");

        private static void Add(string id)
        {
            Debug.Log("[PackageInstaller] Adding " + id + " ...");
            _request = Client.Add(id);
            EditorApplication.update += OnProgress;
        }

        private static void OnProgress()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= OnProgress;
            if (_request.Status == StatusCode.Success)
                Debug.Log("[PackageInstaller] Installed OK: " + _request.Result.packageId);
            else
                Debug.LogError("[PackageInstaller] FAILED: " + (_request.Error != null ? _request.Error.message : "unknown error"));
            _request = null;
        }
    }
}
