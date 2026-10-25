using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARTargetShooter
{
    /// <summary>
    /// Menu, HUD and Game Over panels. Shows only the panel for the current game state and keeps
    /// the texts up to date; texts are only rewritten when the displayed value changes.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PlaneDetectionController planeController;
        [SerializeField] private RectTransform safeAreaRoot;

        [Header("Menu")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button easyButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button hardButton;
        [SerializeField] private Color selectedColor = new Color(0.1f, 0.7f, 0.9f, 1f);
        [SerializeField] private Color unselectedColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        [Header("HUD")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;

        [Header("Game over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text hitsText;
        [SerializeField] private TMP_Text accuracyText;
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private TMP_Text newBestText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;

        [Header("Debug toggle (hidden long-press area)")]
        [SerializeField] private LongPressButton debugToggle;

        private int shownScore = -1;
        private int shownSecond = -1;
        private bool lastPlaneReady;
        private bool planeReadyKnown;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
            if (planeController == null) planeController = FindFirstObjectByType<PlaneDetectionController>();
        }

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleStateChanged;
                gameManager.OnTimeChanged += HandleTimeChanged;
                gameManager.OnGameOver += HandleGameOver;
                gameManager.OnDifficultyChanged += HandleDifficultyChanged;
                gameManager.OnDebugModeChanged += HandleDebugModeChanged;
            }
            if (scoreManager != null) scoreManager.OnScoreChanged += HandleScoreChanged;
            if (planeController != null) planeController.OnPlaneCountChanged += HandlePlaneCountChanged;
            if (debugToggle != null) debugToggle.OnLongPress += HandleDebugLongPress;

            AddListener(startButton, OnStartClicked);
            AddListener(easyButton, () => SelectDifficulty(0));
            AddListener(normalButton, () => SelectDifficulty(1));
            AddListener(hardButton, () => SelectDifficulty(2));
            AddListener(playAgainButton, OnPlayAgainClicked);
            AddListener(menuButton, OnMenuClicked);
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.OnStateChanged -= HandleStateChanged;
                gameManager.OnTimeChanged -= HandleTimeChanged;
                gameManager.OnGameOver -= HandleGameOver;
                gameManager.OnDifficultyChanged -= HandleDifficultyChanged;
                gameManager.OnDebugModeChanged -= HandleDebugModeChanged;
            }
            if (scoreManager != null) scoreManager.OnScoreChanged -= HandleScoreChanged;
            if (planeController != null) planeController.OnPlaneCountChanged -= HandlePlaneCountChanged;
            if (debugToggle != null) debugToggle.OnLongPress -= HandleDebugLongPress;

            // The listeners are lambdas, so clear them all rather than removing one by one.
            RemoveAllListeners(startButton);
            RemoveAllListeners(easyButton);
            RemoveAllListeners(normalButton);
            RemoveAllListeners(hardButton);
            RemoveAllListeners(playAgainButton);
            RemoveAllListeners(menuButton);
        }

        private void Start()
        {
            if (titleText != null) titleText.text = "AR Target Shooter";
            ApplySafeArea();
            ShowPanelFor(gameManager != null ? gameManager.State : GameState.Menu);
            HandleDifficultyChanged(gameManager != null ? gameManager.SelectedDifficulty : 1);
            RefreshPlaneStatus();
            HandleScoreChanged(scoreManager != null ? scoreManager.Score : 0);
        }

        private void Update()
        {
            // Re-apply the safe area if the screen size or cutout changes (e.g. rotation).
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
                ApplySafeArea();
        }

        // ---- Safe area -----------------------------------------------------------------------------------------

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (safeAreaRoot == null || Screen.width == 0 || Screen.height == 0) return;

            // Convert the safe rect from pixels to 0..1 anchors of the full-screen canvas.
            Vector2 min = lastSafeArea.position;
            Vector2 max = lastSafeArea.position + lastSafeArea.size;
            safeAreaRoot.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
        }

        // ---- Game events -----------------------------------------------------------------------------------------

        private void HandleStateChanged(GameState state)
        {
            ShowPanelFor(state);
            if (state == GameState.Playing)
            {
                // Force a rewrite of the HUD values for the new round.
                shownScore = -1;
                shownSecond = -1;
                HandleScoreChanged(0);
                if (gameManager != null) HandleTimeChanged(gameManager.TimeRemaining);
            }
        }

        private void ShowPanelFor(GameState state)
        {
            if (menuPanel != null) menuPanel.SetActive(state == GameState.Menu);
            if (hudPanel != null) hudPanel.SetActive(state == GameState.Playing);
            if (gameOverPanel != null) gameOverPanel.SetActive(state == GameState.GameOver);
        }

        private void HandleScoreChanged(int score)
        {
            if (score == shownScore || scoreText == null) return;
            shownScore = score;
            scoreText.text = $"Score: {score}";
        }

        private void HandleTimeChanged(float remaining)
        {
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == shownSecond || timeText == null) return;
            shownSecond = seconds;
            timeText.text = $"Time: {seconds}";
            // Red for the last 10 seconds.
            timeText.color = seconds <= 10 ? Color.red : Color.white;
        }

        private void HandleGameOver(int score, int hits, int accuracy, int best, bool newBest)
        {
            if (finalScoreText != null) finalScoreText.text = $"Final Score: {score}";
            if (hitsText != null) hitsText.text = $"Hits: {hits}";
            if (accuracyText != null) accuracyText.text = $"Accuracy: {accuracy}%";
            if (bestScoreText != null) bestScoreText.text = $"Best Score: {best}";
            if (newBestText != null)
            {
                newBestText.text = "New best!";
                newBestText.gameObject.SetActive(newBest);
            }
        }

        private void HandleDifficultyChanged(int index)
        {
            SetSelected(easyButton, index == 0);
            SetSelected(normalButton, index == 1);
            SetSelected(hardButton, index == 2);
        }

        private void HandleDebugModeChanged(bool on) { /* no visible UI for debug mode itself */ }

        private void HandleDebugLongPress()
        {
            if (gameManager != null) gameManager.SetDebugMode(!gameManager.DebugMode);
        }

        // ---- Menu: plane status / Start button -------------------------------------------------------------------

        private void HandlePlaneCountChanged(int count) => RefreshPlaneStatus();

        private void RefreshPlaneStatus()
        {
            bool ready = planeController != null && planeController.PlaneCount >= 1;
            if (planeReadyKnown && ready == lastPlaneReady) return;
            planeReadyKnown = true;
            lastPlaneReady = ready;

            if (startButton != null) startButton.interactable = ready;
            if (statusText != null) statusText.text = ready ? "Ready!" : "Scan a surface to begin";
        }

        // ---- Buttons -------------------------------------------------------------------------------------------

        private void OnStartClicked() { if (gameManager != null) gameManager.StartGame(); }
        private void OnPlayAgainClicked() { if (gameManager != null) gameManager.PlayAgain(); }
        private void OnMenuClicked() { if (gameManager != null) gameManager.ReturnToMenu(); }
        private void SelectDifficulty(int index) { if (gameManager != null) gameManager.SetDifficulty(index); }

        private void SetSelected(Button button, bool selected)
        {
            if (button != null && button.image != null)
                button.image.color = selected ? selectedColor : unselectedColor;
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void RemoveAllListeners(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }
}
