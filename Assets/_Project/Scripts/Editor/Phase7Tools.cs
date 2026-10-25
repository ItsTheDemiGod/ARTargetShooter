using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARTargetShooter.EditorTools
{
    /// <summary>
    /// Editor-only Phase 7 helpers: builds the Paused and AR-error panels, applies the release
    /// Player Settings, and verifies the result. Reuses the UI helpers of GameUIBuilder.
    /// </summary>
    public static class Phase7Tools
    {
        private const string ProductName = "AR Target Shooter";
        private const string CompanyName = "Demian";
        private const string PackageId = "com.demian.artargetshooter";
        private const string Version = "1.0.0";

        // ---- Paused + error UI -------------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Add ARAvailabilityMonitor And Error UI")]
        public static void AddMonitorAndErrorUI()
        {
            var ui = Object.FindFirstObjectByType<GameUI>();
            if (ui == null)
            {
                Debug.LogError("[Phase7Tools] No GameUI in the open scene. Run ARTargetShooter > Create Game UI first.");
                return;
            }

            var so = new SerializedObject(ui);
            var safe = so.FindProperty("safeAreaRoot").objectReferenceValue as RectTransform;
            if (safe == null) { Debug.LogError("[Phase7Tools] GameUI has no SafeAreaRoot assigned."); return; }

            // Monitor object (one only).
            var monitor = Object.FindFirstObjectByType<ARAvailabilityMonitor>();
            if (monitor == null)
            {
                var go = new GameObject("ARAvailabilityMonitor");
                Undo.RegisterCreatedObjectUndo(go, "Create ARAvailabilityMonitor");
                monitor = go.AddComponent<ARAvailabilityMonitor>();
            }
            var monitorSo = new SerializedObject(monitor);
            GameUIBuilder.Set(monitorSo, "gameManager", Object.FindFirstObjectByType<GameManager>());
            monitorSo.ApplyModifiedProperties();
            GameUIBuilder.Set(so, "availabilityMonitor", monitor);

            // Paused panel (skipped if it exists).
            if (so.FindProperty("pausedPanel").objectReferenceValue == null)
            {
                RectTransform paused = GameUIBuilder.NewPanel("PausedPanel", safe, GameUIBuilder.PanelColor);
                GameUIBuilder.NewText("PausedText", paused, "Paused - point the camera at your surroundings", 60,
                                      new Vector2(0, 350), new Vector2(900, 300));
                Button resume = GameUIBuilder.NewButton("ResumeButton", paused, "RESUME", 64, new Vector2(0, -100), new Vector2(700, 180));
                Button menu = GameUIBuilder.NewButton("PausedMenuButton", paused, "MENU", 56, new Vector2(0, -330), new Vector2(700, 160));
                resume.interactable = false;
                GameUIBuilder.Set(so, "pausedPanel", paused.gameObject);
                GameUIBuilder.Set(so, "resumeButton", resume);
                GameUIBuilder.Set(so, "pausedMenuButton", menu);
                paused.gameObject.SetActive(false);
            }
            else Debug.Log("[Phase7Tools] Paused panel already exists; skipped.");

            // Error panel (skipped if it exists). Nearly opaque so the broken camera view is not distracting.
            if (so.FindProperty("errorPanel").objectReferenceValue == null)
            {
                RectTransform error = GameUIBuilder.NewPanel("ErrorPanel", safe, new Color(0.05f, 0.05f, 0.08f, 0.96f));
                TMP_Text message = GameUIBuilder.NewText("ErrorText", error, "AR is not available.", 56,
                                                         new Vector2(0, 350), new Vector2(900, 600));
                Button retry = GameUIBuilder.NewButton("RetryButton", error, "RETRY", 64, new Vector2(0, -150), new Vector2(700, 180));
                Button settings = GameUIBuilder.NewButton("OpenSettingsButton", error, "OPEN SETTINGS", 52, new Vector2(0, -380), new Vector2(700, 160));
                GameUIBuilder.Set(so, "errorPanel", error.gameObject);
                GameUIBuilder.Set(so, "errorText", message);
                GameUIBuilder.Set(so, "retryButton", retry);
                GameUIBuilder.Set(so, "openSettingsButton", settings);
                error.gameObject.SetActive(false);
            }
            else Debug.Log("[Phase7Tools] Error panel already exists; skipped.");

            so.ApplyModifiedProperties();

            // Keep the hidden debug toggle on top of the new panels so it always receives long-presses.
            var debugToggle = so.FindProperty("debugToggle").objectReferenceValue as Component;
            if (debugToggle != null) debugToggle.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Phase7Tools] ARAvailabilityMonitor, Paused panel and Error panel are set up. Save the scene (Ctrl+S).");
        }

        // ---- Release Player Settings ---------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Apply Release Player Settings")]
        public static void ApplyReleaseSettings()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Never lower the minimum API level; only raise it if it is below ARCore's minimum (24).
            if ((int)PlayerSettings.Android.minSdkVersion < (int)AndroidSdkVersions.AndroidApiLevel24)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // Medium (not High): High can strip code that is only reached through serialization.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase7Tools] Release settings applied: name '{PlayerSettings.productName}', company '{PlayerSettings.companyName}', " +
                      $"id '{PlayerSettings.GetApplicationIdentifier(android)}', version {PlayerSettings.bundleVersion} " +
                      $"(code {PlayerSettings.Android.bundleVersionCode}), backend {PlayerSettings.GetScriptingBackend(android)}, " +
                      $"arch {PlayerSettings.Android.targetArchitectures}, minSdk {PlayerSettings.Android.minSdkVersion}, " +
                      $"stripping {PlayerSettings.GetManagedStrippingLevel(android)}.");
        }

        // ---- Verify ---------------------------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Verify Phase 7 Setup")]
        public static void VerifySetup()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            Report($"Product name '{ProductName}'", PlayerSettings.productName == ProductName);
            Report($"Company name '{CompanyName}'", PlayerSettings.companyName == CompanyName);
            Report($"Package ID '{PackageId}'", PlayerSettings.GetApplicationIdentifier(android) == PackageId);
            Report($"Version '{Version}'", PlayerSettings.bundleVersion == Version);
            Report("Android version code is 1", PlayerSettings.Android.bundleVersionCode == 1);
            Report("IL2CPP scripting backend", PlayerSettings.GetScriptingBackend(android) == ScriptingImplementation.IL2CPP);
            Report("ARM64 only", PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64);
            Report("Min API level >= 24", (int)PlayerSettings.Android.minSdkVersion >= (int)AndroidSdkVersions.AndroidApiLevel24);

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Report("Graphics API OpenGLES3 only",
                   !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) && apis.Length == 1 && apis[0] == GraphicsDeviceType.OpenGLES3);
            Report("Managed stripping level Medium", PlayerSettings.GetManagedStrippingLevel(android) == ManagedStrippingLevel.Medium);

            // Scene list: exactly one enabled scene, and it is Main.unity.
            int enabled = 0;
            bool mainEnabled = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled) continue;
                enabled++;
                if (s.path.EndsWith("Main.unity")) mainEnabled = true;
            }
            Report("Scene list: only Main.unity enabled", enabled == 1 && mainEnabled);

            Report("GameManager in the open scene", Object.FindFirstObjectByType<GameManager>() != null);
            Report("GameUI in the open scene", Object.FindFirstObjectByType<GameUI>() != null);
            Report("ARAvailabilityMonitor in the open scene", Object.FindFirstObjectByType<ARAvailabilityMonitor>() != null);

            // Missing Script components (e.g. left behind by deleting a script before its scene object).
            int missing = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count > 0)
                {
                    missing += count;
                    Debug.LogWarning($"[Phase 7 setup] '{go.name}' has {count} Missing Script component(s).", go);
                }
            }
            Report("No Missing Script components in the open scene", missing == 0);
        }

        private static void Report(string what, bool ok)
        {
            if (ok) Debug.Log($"[Phase 7 setup] OK: {what}.");
            else Debug.LogWarning($"[Phase 7 setup] MISSING: {what}.");
        }
    }
}
