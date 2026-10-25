using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARTargetShooter.EditorTools
{
    /// <summary>
    /// Editor-only helper: creates the "Projectile" layer, its material and prefab, and sets the
    /// physics layer collision rules so projectiles ignore AR planes (Default layer) and each other.
    /// </summary>
    public static class ProjectileSetupBuilder
    {
        private const string LayerName = "Projectile";
        private const string MaterialPath = "Assets/_Project/Materials/Projectile_Material.mat";
        private const string PrefabPath = "Assets/_Project/Prefabs/Projectile.prefab";

        // Built-in layers projectiles should never collide with: Default, TransparentFX, Ignore Raycast, Water, UI.
        private static readonly int[] IgnoredBuiltInLayers = { 0, 1, 2, 4, 5 };

        [MenuItem("ARTargetShooter/Create Projectile Prefab")]
        public static void CreateProjectilePrefab()
        {
            int layer = EnsureLayer();
            if (layer >= 0) ApplyCollisionRules(layer);

            if (File.Exists(PrefabPath))
            {
                Debug.Log($"[ProjectileSetupBuilder] {PrefabPath} already exists. Layer and collision rules were checked; nothing else done.");
                return;
            }

            Material mat = GetOrCreateMaterial();
            if (mat == null) return;   // error already logged

            // Root holds physics (unscaled, so collider radius is in metres); a child sphere is the visual.
            var root = new GameObject("Projectile");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.08f;   // 8 cm diameter
            visual.GetComponent<Renderer>().sharedMaterial = mat;

            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.04f;
            sphere.isTrigger = false;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            root.AddComponent<Projectile>();

            if (layer >= 0) SetLayerRecursively(root.transform, layer);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[ProjectileSetupBuilder] Created {PrefabPath}.");
        }

        [MenuItem("ARTargetShooter/Verify Phase 4 Setup")]
        public static void VerifySetup()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            Report($"Layer '{LayerName}'", layer >= 0);
            Report("Projectile_Material", File.Exists(MaterialPath));
            Report("Projectile.prefab", File.Exists(PrefabPath));
            Report("Collision matrix: Projectile vs Default disabled",
                   layer >= 0 && Physics.GetIgnoreLayerCollision(layer, 0));
            Report("Collision matrix: Projectile vs Projectile disabled",
                   layer >= 0 && Physics.GetIgnoreLayerCollision(layer, layer));
            int target = LayerMask.NameToLayer("Target");
            Report("Collision matrix: Projectile vs Target still enabled",
                   layer >= 0 && target >= 0 && !Physics.GetIgnoreLayerCollision(layer, target));
        }

        private static void Report(string what, bool ok)
        {
            if (ok) Debug.Log($"[Phase 4 setup] OK: {what}.");
            else Debug.LogWarning($"[Phase 4 setup] MISSING: {what}.");
        }

        // Disables Projectile collisions with built-in layers and itself, leaving Target enabled.
        // Uses the Physics API (the serialized matrix bit layout is easy to get wrong), then marks
        // DynamicsManager.asset dirty and saves so the change persists in ProjectSettings.
        private static void ApplyCollisionRules(int projectileLayer)
        {
            try
            {
                foreach (int other in IgnoredBuiltInLayers)
                    Physics.IgnoreLayerCollision(projectileLayer, other, true);
                Physics.IgnoreLayerCollision(projectileLayer, projectileLayer, true);

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
                if (assets != null && assets.Length > 0) EditorUtility.SetDirty(assets[0]);
                AssetDatabase.SaveAssets();

                if (Physics.GetIgnoreLayerCollision(projectileLayer, 0))
                {
                    Debug.Log("[ProjectileSetupBuilder] Layer collision rules applied.");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProjectileSetupBuilder] Collision matrix edit failed: {e.Message}");
            }

            Debug.LogError("[ProjectileSetupBuilder] Could not set the collision matrix. Do it manually: Edit > Project Settings > Physics > " +
                           "Layer Collision Matrix, untick the Projectile row/column for Default, Projectile (and TransparentFX, Ignore Raycast, Water, UI). Keep Target ticked.");
        }

        private static Material GetOrCreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;   // reuse, never overwrite

            // Editor-side Shader.Find is fine; the build includes the shader via the Material asset.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ProjectileSetupBuilder] Neither URP Unlit nor URP Lit shader found. Is URP installed? Stopping.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var mat = new Material(shader);
            Color cyan = new Color(0f, 0.9f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            else mat.color = cyan;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        // Returns the layer index, adding "Projectile" to the first free user slot (8-31) if needed; -1 on failure.
        private static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer(LayerName);
            if (existing >= 0) return existing;

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
                        Debug.Log($"[ProjectileSetupBuilder] Added layer '{LayerName}' at slot {i}.");
                        return i;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ProjectileSetupBuilder] Could not edit TagManager: {e.Message}");
            }

            Debug.LogError($"[ProjectileSetupBuilder] Could not add layer '{LayerName}'. Add it manually: Edit > Project Settings > Tags and Layers > " +
                           "Layers, type 'Projectile' in an empty User Layer, set the Projectile prefab (and children) to that layer, then run this menu again.");
            return -1;
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }
    }
}
