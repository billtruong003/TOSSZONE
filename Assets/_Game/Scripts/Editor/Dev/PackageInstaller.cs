using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TossZone.DevTools
{
    /// <summary>
    /// Editor helper to force-install a UPM package via Client.Add (resolves immediately and reports
    /// errors to the console, instead of waiting for the editor to regain focus). Dev-only utility.
    /// </summary>
    public static class PackageInstaller
    {
        private static AddRequest _request;

        [MenuItem("Tools/TOSSZONE/Install Meta XR Simulator")]
        public static void InstallMetaXrSimulator()
        {
            Debug.Log("[PackageInstaller] Adding com.meta.xr.simulator@74.0.0 ...");
            _request = Client.Add("com.meta.xr.simulator@74.0.0");
            EditorApplication.update += OnProgress;
        }

        private static void OnProgress()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= OnProgress;

            if (_request.Status == StatusCode.Success)
                Debug.Log("[PackageInstaller] Installed OK: " + _request.Result.packageId);
            else
                Debug.LogError("[PackageInstaller] FAILED: " +
                    (_request.Error != null ? _request.Error.message : "unknown error"));

            _request = null;
        }
    }
}
