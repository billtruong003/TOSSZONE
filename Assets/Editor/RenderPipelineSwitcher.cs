using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderPipelineSwitcher : EditorWindow
{
    private RenderPipelineAsset _urpPCAsset;
    private RenderPipelineAsset _urpMobileAsset;

    [MenuItem("Tools/Render Pipeline Switcher", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<RenderPipelineSwitcher>("RP Switcher");
        window.minSize = new Vector2(320, 260);
    }

    private void OnEnable() => AutoFindURPAssets();

    private void AutoFindURPAssets()
    {
        _urpPCAsset = null;
        _urpMobileAsset = null;

        string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null) continue;

            if (path.ToLower().Contains("mobile"))
                _urpMobileAsset = asset;
            else if (path.ToLower().Contains("pc") || _urpPCAsset == null)
                _urpPCAsset = asset;
        }
    }

    private void OnGUI()
    {
        var current = GraphicsSettings.defaultRenderPipeline;
        bool isBuiltIn = current == null;

        EditorGUILayout.Space(8);
        DrawStatusBox(current, isBuiltIn);
        EditorGUILayout.Space(12);

        EditorGUILayout.LabelField("Switch Pipeline", EditorStyles.boldLabel);

        GUI.enabled = !isBuiltIn;
        if (GUILayout.Button("→  Built-in Render Pipeline", GUILayout.Height(32)))
            SwitchPipeline(null);
        GUI.enabled = true;

        if (_urpPCAsset != null)
        {
            GUI.enabled = !(current == _urpPCAsset);
            if (GUILayout.Button($"→  URP  —  PC  ({_urpPCAsset.name})", GUILayout.Height(32)))
                SwitchPipeline(_urpPCAsset);
            GUI.enabled = true;
        }

        if (_urpMobileAsset != null)
        {
            GUI.enabled = !(current == _urpMobileAsset);
            if (GUILayout.Button($"→  URP  —  Mobile  ({_urpMobileAsset.name})", GUILayout.Height(32)))
                SwitchPipeline(_urpMobileAsset);
            GUI.enabled = true;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("URP Assets", EditorStyles.boldLabel);
        _urpPCAsset = (RenderPipelineAsset)EditorGUILayout.ObjectField("PC Asset", _urpPCAsset, typeof(RenderPipelineAsset), false);
        _urpMobileAsset = (RenderPipelineAsset)EditorGUILayout.ObjectField("Mobile Asset", _urpMobileAsset, typeof(RenderPipelineAsset), false);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Auto-Detect URP Assets"))
            AutoFindURPAssets();
    }

    private void DrawStatusBox(RenderPipelineAsset current, bool isBuiltIn)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Active Pipeline", EditorStyles.miniBoldLabel);

        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = isBuiltIn ? new Color(0.95f, 0.65f, 0.2f) : new Color(0.3f, 0.85f, 0.45f) }
        };

        string label = isBuiltIn ? "Built-in Render Pipeline" : $"URP  —  {current.name}";
        EditorGUILayout.LabelField(label, labelStyle);
        EditorGUILayout.EndVertical();
    }

    private void SwitchPipeline(RenderPipelineAsset asset)
    {
        // Update GraphicsSettings (global default)
        var graphicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (graphicsAssets.Length > 0)
        {
            var so = new SerializedObject(graphicsAssets[0]);
            so.FindProperty("m_CustomRenderPipeline").objectReferenceValue = asset;
            so.ApplyModifiedProperties();
        }

        // Update all QualitySettings levels
        var qualityAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        if (qualityAssets.Length > 0)
        {
            var so = new SerializedObject(qualityAssets[0]);
            var levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var customRP = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                if (customRP != null)
                    customRP.objectReferenceValue = asset;
            }
            so.ApplyModifiedProperties();
        }

        string name = asset == null ? "Built-in Render Pipeline" : $"URP ({asset.name})";
        Debug.Log($"[RP Switcher] Switched to {name}");
        Repaint();
    }
}
