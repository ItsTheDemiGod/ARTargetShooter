using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARTargetShooter.EditorTools
{
    /// <summary>
    /// Editor-only helper: creates the "Target" layer, the three Material assets and the
    /// Target prefab, so no shader lookup is ever needed at runtime.
    /// </summary>
    public static class TargetPrefabBuilder
    {
        private const string LayerName = "Target";
        private const string MaterialFolder = "Assets/_Project/Materials";
        private const string PrefabPath = "Assets/_Project/Prefabs/Target.prefab";

        private static readonly string[] MaterialNames = { "Target_Red", "Target_White", "Target_Center" };

        [MenuItem("ARTargetShooter/Create Target Prefab")]
        public static void CreateTargetPrefab()
        {
            if (File.Exists(PrefabPath))
            {
                Debug.Log($"[TargetPrefabBuilder] {PrefabPath} already exists. Nothing done.");
                return;
            }

            EnsureLayer();

            Material red = GetOrCreateMaterial("Target_Red", new Color(0.85f, 0.1f, 0.1f));
            Material white = GetOrCreateMaterial("Target_White", Color.white);
            Material center = GetOrCreateMaterial("Target_Center", new Color(1f, 0.7f, 0f));
            if (red == null || white == null || center == null) return;   // error already logged

            // Concentric discs, facing local +Z. Later discs sit slightly further out (+Z) to avoid z-fighting.
            var root = new GameObject("Target");
            AddDisc(root.transform, "Ring_Outer", 0.34f, 0.000f, red);
            AddDisc(root.transform, "Ring_Middle", 0.22f, 0.006f, white);
            AddDisc(root.transform, "Ring_Center", 0.10f, 0.012f, center);

            // Single non-trigger sphere collider on the root for hit detection in Phase 5.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.18f;
            sphere.isTrigger = false;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            root.AddComponent<Target>();

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0) SetLayerRecursively(root.transform, layer);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[TargetPrefabBuilder] Created {PrefabPath}.");
        }

        [MenuItem("ARTargetShooter/Verify Phase 3 Setup")]
        public static void VerifySetup()
        {
            Report($"Layer '{LayerName}'", LayerMask.NameToLayer(LayerName) >= 0);
            foreach (string name in MaterialNames)
                Report($"Material {name}", File.Exists($"{MaterialFolder}/{name}.mat"));
            Report("Target prefab", File.Exists(PrefabPath));
        }

        private static void Report(string what, bool ok)
        {
            if (ok) Debug.Log($"[Phase 3 setup] OK: {what} exists.");
            else Debug.LogWarning($"[Phase 3 setup] MISSING: {what}.");
        }

        // Flat disc = cylinder (axis Y, 2 units tall) rotated 90 degrees about X so its axis points along Z.
        private static void AddDisc(Transform parent, string name, float diameter, float zOffset, Material mat)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localPosition = new Vector3(0f, 0f, zOffset);
            disc.transform.localScale = new Vector3(diameter, 0.005f, diameter);   // 0.005 * 2 = 1 cm thick
            disc.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;   // reuse, never overwrite

            // Editor-side Shader.Find is fine; the shader gets referenced by the Material asset in the build.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[TargetPrefabBuilder] Neither URP Unlit nor URP Lit shader found. Is URP installed? Stopping.");
                return null;
            }

            Directory.CreateDirectory(MaterialFolder);
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // Adds "Target" to the first free user layer slot (8-31) of TagManager.asset.
        private static void EnsureLayer()
        {
            if (LayerMask.NameToLayer(LayerName) >= 0) return;

            try
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                var tagManager = new SerializedObject(assets[0]);
                SerializedProperty layers = tagManager.FindProperty("layers");
                for (int i = 8; i < 32 && i < layers.arraySize; i++)
                {
                    SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = LayerName;
                        tagManager.ApplyModifiedProperties();
                        Debug.Log($"[TargetPrefabBuilder] Added layer '{LayerName}' at slot {i}.");
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TargetPrefabBuilder] Could not edit TagManager: {e.Message}");
            }

            Debug.LogError($"[TargetPrefabBuilder] Could not add layer '{LayerName}' automatically. Add it manually: " +
                           "Edit > Project Settings > Tags and Layers > Layers, type 'Target' in any empty User Layer, " +
                           "The prefab is still built; Target.cs also assigns the layer at runtime once it exists, " +
                           "but you should set the Target prefab's layer to 'Target' (including children) yourself.");
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }
    }
}
