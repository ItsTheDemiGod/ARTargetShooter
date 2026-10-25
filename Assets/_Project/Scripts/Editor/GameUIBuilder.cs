using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARTargetShooter.EditorTools
{
    /// <summary>
    /// Editor-only helper: builds the game UI (Canvas, panels, buttons, EventSystem) in the OPEN scene,
    /// creates the GameManager object, and locks the orientation to portrait.
    /// </summary>
    public static class GameUIBuilder
    {
        private const string CanvasName = "GameCanvas";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.12f, 0.75f);
        private static readonly Color ButtonColor = new Color(0.1f, 0.55f, 0.85f, 1f);

        // ---- Create Game UI -----------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Create Game UI")]
        public static void CreateGameUI()
        {
            if (GameObject.Find(CanvasName) != null)
            {
                Debug.Log($"[GameUIBuilder] '{CanvasName}' already exists in the open scene. Nothing done.");
                return;
            }

            // TMP needs its essential resources (default font) before text can be created.
            if (!File.Exists(TmpSettingsPath))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.LogWarning("[GameUIBuilder] TextMeshPro Essentials were missing; importing them now. " +
                                 "When the import finishes, run ARTargetShooter > Create Game UI again. " +
                                 "(Manual alternative: Window > TextMeshPro > Import TMP Essential Resources.)");
                return;
            }

            EnsureEventSystem();

            // Canvas: overlay, scales from a 1080x2340 portrait reference.
            var canvasGO = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Game UI");
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safe = NewRect("SafeAreaRoot", canvasGO.transform);
            Stretch(safe);

            // ---- Menu panel
            RectTransform menu = NewPanel("MenuPanel", safe, PanelColor);
            TMP_Text title = NewText("TitleText", menu, "AR Target Shooter", 96, new Vector2(0, 700), new Vector2(1000, 160));
            TMP_Text status = NewText("StatusText", menu, "Scan a surface to begin", 52, new Vector2(0, 520), new Vector2(1000, 100));
            Button start = NewButton("StartButton", menu, "START", 72, new Vector2(0, 250), new Vector2(700, 180));
            NewText("DifficultyLabel", menu, "Difficulty", 44, new Vector2(0, 90), new Vector2(600, 70));
            Button easy = NewButton("EasyButton", menu, "EASY", 48, new Vector2(-330, -30), new Vector2(300, 160));
            Button normal = NewButton("NormalButton", menu, "NORMAL", 48, new Vector2(0, -30), new Vector2(300, 160));
            Button hard = NewButton("HardButton", menu, "HARD", 48, new Vector2(330, -30), new Vector2(300, 160));

            // ---- HUD panel (no background; texts do not block touches so taps still shoot)
            RectTransform hud = NewRect("HudPanel", safe);
            Stretch(hud);
            TMP_Text score = NewText("ScoreText", hud, "Score: 0", 64, new Vector2(30, -30), new Vector2(520, 120));
            AnchorTo(score.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30));
            score.alignment = TextAlignmentOptions.TopLeft;
            TMP_Text time = NewText("TimeText", hud, "Time: 60", 64, new Vector2(-30, -30), new Vector2(520, 120));
            AnchorTo(time.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30));
            time.alignment = TextAlignmentOptions.TopRight;

            // ---- Game over panel
            RectTransform over = NewPanel("GameOverPanel", safe, PanelColor);
            NewText("GameOverTitle", over, "GAME OVER", 96, new Vector2(0, 700), new Vector2(1000, 160));
            TMP_Text finalScore = NewText("FinalScoreText", over, "Final Score: 0", 72, new Vector2(0, 450), new Vector2(1000, 110));
            TMP_Text hits = NewText("HitsText", over, "Hits: 0", 56, new Vector2(0, 330), new Vector2(1000, 90));
            TMP_Text accuracy = NewText("AccuracyText", over, "Accuracy: 0%", 56, new Vector2(0, 230), new Vector2(1000, 90));
            TMP_Text best = NewText("BestScoreText", over, "Best Score: 0", 56, new Vector2(0, 130), new Vector2(1000, 90));
            TMP_Text newBest = NewText("NewBestText", over, "New best!", 64, new Vector2(0, 20), new Vector2(1000, 100));
            newBest.color = Color.yellow;
            Button playAgain = NewButton("PlayAgainButton", over, "PLAY AGAIN", 64, new Vector2(0, -250), new Vector2(700, 180));
            Button menuBtn = NewButton("MenuButton", over, "MENU", 56, new Vector2(0, -480), new Vector2(700, 160));

            // ---- Hidden debug toggle: transparent 120x120 area in the bottom-left; last sibling = on top.
            var debugGO = new GameObject("DebugToggle", typeof(RectTransform), typeof(Image), typeof(LongPressButton));
            debugGO.transform.SetParent(safe, false);
            var debugRect = (RectTransform)debugGO.transform;
            AnchorTo(debugRect, Vector2.zero, Vector2.zero, Vector2.zero);
            debugRect.pivot = Vector2.zero;
            debugRect.sizeDelta = new Vector2(120, 120);
            var debugImage = debugGO.GetComponent<Image>();
            debugImage.color = new Color(1, 1, 1, 0);
            debugImage.raycastTarget = true;

            // ---- GameManager object and GameUI component
            GameManager manager = FindOrCreateGameManager();

            var ui = canvasGO.AddComponent<GameUI>();
            var so = new SerializedObject(ui);
            Set(so, "gameManager", manager);
            Set(so, "scoreManager", Object.FindFirstObjectByType<ScoreManager>());
            Set(so, "planeController", Object.FindFirstObjectByType<PlaneDetectionController>());
            Set(so, "safeAreaRoot", safe);
            Set(so, "menuPanel", menu.gameObject);
            Set(so, "titleText", title);
            Set(so, "statusText", status);
            Set(so, "startButton", start);
            Set(so, "easyButton", easy);
            Set(so, "normalButton", normal);
            Set(so, "hardButton", hard);
            Set(so, "hudPanel", hud.gameObject);
            Set(so, "scoreText", score);
            Set(so, "timeText", time);
            Set(so, "gameOverPanel", over.gameObject);
            Set(so, "finalScoreText", finalScore);
            Set(so, "hitsText", hits);
            Set(so, "accuracyText", accuracy);
            Set(so, "bestScoreText", best);
            Set(so, "newBestText", newBest);
            Set(so, "playAgainButton", playAgain);
            Set(so, "menuButton", menuBtn);
            Set(so, "debugToggle", debugGO.GetComponent<LongPressButton>());
            so.ApplyModifiedProperties();

            // Only the menu is visible at design time; GameUI switches panels at runtime.
            hud.gameObject.SetActive(false);
            over.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGO;
            Debug.Log("[GameUIBuilder] Game UI created. Save the scene (Ctrl+S).");
        }

        // ---- Orientation -----------------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Lock Portrait Orientation")]
        public static void LockPortrait()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[GameUIBuilder] Orientation locked to Portrait.");
        }

        // ---- Verify ----------------------------------------------------------------------------------------------

        [MenuItem("ARTargetShooter/Verify Phase 6 Setup")]
        public static void VerifySetup()
        {
            Report("GameCanvas in the open scene", GameObject.Find(CanvasName) != null);

            var es = Object.FindFirstObjectByType<EventSystem>();
            Report("EventSystem with InputSystemUIInputModule",
                   es != null && es.GetComponent<InputSystemUIInputModule>() != null);
            Report("No StandaloneInputModule on the EventSystem",
                   es != null && es.GetComponent<StandaloneInputModule>() == null);

            Report("GameManager", Object.FindFirstObjectByType<GameManager>() != null);

            var ui = Object.FindFirstObjectByType<GameUI>();
            bool refsOk = ui != null;
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                SerializedProperty it = so.GetIterator();
                for (bool enter = true; it.NextVisible(enter); enter = false)
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" &&
                        it.objectReferenceValue == null)
                    {
                        Debug.LogWarning($"[Phase 6 setup] GameUI reference '{it.name}' is empty.");
                        refsOk = false;
                    }
                }
            }
            Report("GameUI references all assigned", refsOk);

            var spawner = Object.FindFirstObjectByType<TargetSpawner>();
            if (spawner != null)
            {
                bool auto = new SerializedObject(spawner).FindProperty("autoStart").boolValue;
                Debug.Log($"[Phase 6 setup] TargetSpawner Auto Start is {(auto ? "ON (GameManager turns it off at runtime; unticking it is cleaner)" : "OFF")}.");
            }
            else Report("TargetSpawner", false);

            Report("Orientation locked to Portrait",
                   PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait &&
                   !PlayerSettings.allowedAutorotateToLandscapeLeft && !PlayerSettings.allowedAutorotateToLandscapeRight &&
                   !PlayerSettings.allowedAutorotateToPortraitUpsideDown);
        }

        private static void Report(string what, bool ok)
        {
            if (ok) Debug.Log($"[Phase 6 setup] OK: {what}.");
            else Debug.LogWarning($"[Phase 6 setup] MISSING: {what}.");
        }

        // ---- Scene helpers -------------------------------------------------------------------------------------

        // The UI needs the Input System UI module; the legacy Standalone module breaks with the new Input System.
        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.GetComponent<EventSystem>();
            }

            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = es.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();   // hook up the default UI actions (point, click, ...)
            }
        }

        private static GameManager FindOrCreateGameManager()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                var go = new GameObject("GameManager");
                Undo.RegisterCreatedObjectUndo(go, "Create GameManager");
                manager = go.AddComponent<GameManager>();
            }

            var so = new SerializedObject(manager);
            Set(so, "shootingController", Object.FindFirstObjectByType<ShootingController>());
            Set(so, "targetSpawner", Object.FindFirstObjectByType<TargetSpawner>());
            Set(so, "scoreManager", Object.FindFirstObjectByType<ScoreManager>());
            Set(so, "planeController", Object.FindFirstObjectByType<PlaneDetectionController>());
            Set(so, "reticle", Object.FindFirstObjectByType<PlacementReticle>());
            Set(so, "debugOverlay", Object.FindFirstObjectByType<ARSessionDebugOverlay>());
            so.ApplyModifiedProperties();
            return manager;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[GameUIBuilder] Field '{field}' not found."); return; }
            p.objectReferenceValue = value;
            if (value == null) Debug.LogWarning($"[GameUIBuilder] '{field}' could not be assigned automatically; assign it in the Inspector.");
        }

        // ---- UI construction helpers ---------------------------------------------------------------------------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void AnchorTo(RectTransform r, Vector2 min, Vector2 max, Vector2 position)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = min;
            r.anchoredPosition = position;
        }

        private static RectTransform NewPanel(string name, Transform parent, Color color)
        {
            RectTransform r = NewRect(name, parent);
            Stretch(r);
            var image = r.gameObject.AddComponent<Image>();
            image.color = color;   // raycast target stays on so the panel blocks taps behind it
            return r;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Vector2 position, Vector2 dimensions)
        {
            RectTransform r = NewRect(name, parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = dimensions;
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;   // texts must never swallow touches
            return t;
        }

        private static Button NewButton(string name, Transform parent, string label, float fontSize, Vector2 position, Vector2 dimensions)
        {
            RectTransform r = NewRect(name, parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = dimensions;
            var image = r.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = NewText("Label", r, label, fontSize, Vector2.zero, dimensions);
            Stretch(text.rectTransform);
            return button;
        }
    }
}
