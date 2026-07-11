using System.IO;
using UnityEditor;
using UnityEngine;

namespace TossZone.EditorTools
{
    /// <summary>
    /// Batch palette assigner. Unity's UI can't set a default material across many models at once, so this does it:
    ///  • Right-click model/prefab assets in the Project → <b>TOSSZONE ▸ Assign Palette (M_Pallet)</b>.
    ///  • Menu <b>TOSSZONE/Palette/Assign M_Pallet to ALL kit models</b> → every MS_ + LightSword prefab.
    /// Finds M_Pallet by NAME (survives the .mat being moved). Prefabs → every Mesh/SkinnedMesh renderer slot;
    /// raw FBX/OBJ → remaps its embedded materials + reimports.
    /// </summary>
    public static class PaletteAssigner
    {
        private static Material FindPalette()
        {
            foreach (string g in AssetDatabase.FindAssets("M_Pallet t:Material"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".mat")) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            Debug.LogError("[Palette] M_Pallet.mat not found in project.");
            return null;
        }

        // ── Right-click on selected assets (Project window) ──────────────────────
        [MenuItem("Assets/TOSSZONE/Assign Palette (M_Pallet)", false, 1100)]
        private static void AssignSelection()
        {
            Material pal = FindPalette();
            if (pal == null) return;
            int n = 0;
            foreach (Object o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(path)) n += AssignPath(path, pal);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Palette] Assigned M_Pallet to {n} asset(s) from selection.");
        }

        [MenuItem("Assets/TOSSZONE/Assign Palette (M_Pallet)", true)]
        private static bool AssignSelectionValidate() => Selection.objects.Length > 0;

        // ── Batch every kit model at once ────────────────────────────────────────
        [MenuItem("TOSSZONE/Palette/Assign M_Pallet to ALL kit models")]
        private static void AssignAllKit()
        {
            Material pal = FindPalette();
            if (pal == null) return;
            int n = 0;
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("MS_") || name == "LightSword") n += AssignPath(path, pal);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Palette] Batch: {n} kit prefab(s) now use M_Pallet.");
        }

        // ── core ─────────────────────────────────────────────────────────────────
        /// <summary>Returns 1 if the asset was changed, else 0.</summary>
        private static int AssignPath(string path, Material pal)
        {
            if (path.EndsWith(".prefab")) return AssignPrefab(path, pal) ? 1 : 0;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".obj" || ext == ".blend") return RemapModel(path, pal) ? 1 : 0;
            return 0;
        }

        private static bool AssignPrefab(string path, Material pal)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                Material[] mats = r.sharedMaterials;
                if (mats.Length == 0) mats = new Material[] { null };
                bool need = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != pal) { mats[i] = pal; need = true; }
                if (need) { r.sharedMaterials = mats; dirty = true; }
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return dirty;
        }

        private static bool RemapModel(string path, Material pal)
        {
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return false;
            bool any = false;
            // Materials still embedded (not yet remapped) → point them at the palette.
            foreach (Object rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (rep is Material) { mi.AddRemap(new AssetImporter.SourceAssetIdentifier(rep), pal); any = true; }
            // Materials already remapped to something else → re-point to the palette (e.g. re-run after a kit update).
            foreach (System.Collections.Generic.KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in mi.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material) && kv.Value != pal) { mi.AddRemap(kv.Key, pal); any = true; }
            if (any) mi.SaveAndReimport();
            return any;
        }
    }
}
