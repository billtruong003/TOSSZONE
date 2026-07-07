using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

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
            ApplyStandingPose(_instance);
            Debug.Log("[XrSim] XR Device Simulator spawned — control HMD/hands with keyboard + mouse.");
        }

        // The simulator's default rest pose parks the HMD/controllers at (0,0,0) / near-floor height (see
        // XRSimulatorUtility.left/rightDeviceDefaultInitialPosition) until the tester holds RMB/Shift/Space +
        // E to manually raise them — without this, the rig (and AutoHand's hand-follow target) sits at floor
        // level from the first frame, which reads as "the hand fell to the floor". m_HMDState/m_Left.../
        // m_Right...ControllerState are private fields with no public setter, hence the reflection.
        private static void ApplyStandingPose(GameObject instance)
        {
            XRDeviceSimulator sim = instance.GetComponentInChildren<XRDeviceSimulator>();
            if (sim == null) return;

            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo hmdField = typeof(XRDeviceSimulator).GetField("m_HMDState", flags);
            FieldInfo leftField = typeof(XRDeviceSimulator).GetField("m_LeftControllerState", flags);
            FieldInfo rightField = typeof(XRDeviceSimulator).GetField("m_RightControllerState", flags);
            if (hmdField == null || leftField == null || rightField == null) return;

            var hmd = (XRSimulatedHMDState)hmdField.GetValue(sim);
            hmd.centerEyePosition = new Vector3(0f, 1.6f, 0f);
            hmd.devicePosition = hmd.centerEyePosition;
            hmdField.SetValue(sim, hmd);

            var left = (XRSimulatedControllerState)leftField.GetValue(sim);
            left.devicePosition = new Vector3(-0.25f, 1.1f, 0.3f);
            leftField.SetValue(sim, left);

            var right = (XRSimulatedControllerState)rightField.GetValue(sim);
            right.devicePosition = new Vector3(0.25f, 1.1f, 0.3f);
            rightField.SetValue(sim, right);
        }
    }
}
