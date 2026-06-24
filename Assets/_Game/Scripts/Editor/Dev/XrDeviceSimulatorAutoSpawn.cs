using UnityEditor;
using UnityEngine;

namespace TossZone.DevTools
{
    /// <summary>
    /// Editor-only: auto-spawns the XR Interaction Toolkit "XR Device Simulator" when entering Play
    /// mode, so you can drive the HMD + controllers with keyboard/mouse on a PC without a headset
    /// (no Quest Link needed). Persists across scene loads. Disable via Tools ▸ TOSSZONE ▸ XR Sim.
    /// Only runs in the editor — never in builds.
    /// </summary>
    [InitializeOnLoad]
    public static class XrDeviceSimulatorAutoSpawn
    {
        private const string PrefKey = "TOSSZONE.XrDeviceSimulatorAutoSpawn";
        private const string PrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/XR Device Simulator/XR Device Simulator.prefab";

        private static GameObject _instance;

        static XrDeviceSimulatorAutoSpawn()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem("Tools/TOSSZONE/XR Sim: Toggle Auto-Spawn")]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log("[XrSim] Auto-spawn " + (Enabled ? "ENABLED" : "DISABLED") + " (takes effect next Play).");
        }

        [MenuItem("Tools/TOSSZONE/XR Sim: Toggle Auto-Spawn", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Tools/TOSSZONE/XR Sim: Toggle Auto-Spawn", Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !Enabled) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[XrSim] Simulator prefab not found at " + PrefabPath);
                return;
            }

            _instance = Object.Instantiate(prefab);
            _instance.name = "XR Device Simulator (auto)";
            Object.DontDestroyOnLoad(_instance);
            Debug.Log("[XrSim] XR Device Simulator spawned — control HMD/hands with keyboard + mouse.");
        }
    }
}
