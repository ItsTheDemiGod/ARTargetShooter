using System;
using UnityEngine;

namespace ARTargetShooter
{
    public enum GameState { Menu, Playing, GameOver }

    /// <summary>
    /// The game loop: Menu -> Playing -> GameOver -> (Play Again) -> Playing.
    /// Never touches Time.timeScale; the game "stops" by stopping the spawner and disabling firing/scoring.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private const string BestScoreKey = "ARTargetShooter.BestScore";

        [SerializeField] private ShootingController shootingController;
        [SerializeField] private TargetSpawner targetSpawner;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PlaneDetectionController planeController;
        [SerializeField] private PlacementReticle reticle;
        [SerializeField] private ARSessionDebugOverlay debugOverlay;

        [SerializeField] private float roundSeconds = 60f;
        [Tooltip("0 = Easy, 1 = Normal, 2 = Hard")]
        [SerializeField] private int selectedDifficulty = 1;

        // Presets: max active targets, target lifetime (s), respawn delay (s).
        private static readonly (int maxActive, float lifetime, float respawn)[] Presets =
        {
            (4, 25f, 1.2f),   // Easy
            (5, 20f, 1.0f),   // Normal
            (7, 12f, 0.6f),   // Hard
        };

        public GameState State { get; private set; } = GameState.Menu;
        public float TimeRemaining { get; private set; }
        public int SelectedDifficulty => selectedDifficulty;
        public bool DebugMode { get; private set; }

        public event Action<GameState> OnStateChanged;
        public event Action<float> OnTimeChanged;
        public event Action<int, int, int, int, bool> OnGameOver;   // score, hits, accuracy %, best, newBest
        public event Action<bool> OnDebugModeChanged;
        public event Action<int> OnDifficultyChanged;

        private int lastShownSecond = -1;

        private void Awake()
        {
            if (shootingController == null) shootingController = FindFirstObjectByType<ShootingController>();
            if (targetSpawner == null) targetSpawner = FindFirstObjectByType<TargetSpawner>();
            if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
            if (planeController == null) planeController = FindFirstObjectByType<PlaneDetectionController>();
            if (reticle == null) reticle = FindFirstObjectByType<PlacementReticle>();
            if (debugOverlay == null) debugOverlay = FindFirstObjectByType<ARSessionDebugOverlay>();

            selectedDifficulty = Mathf.Clamp(selectedDifficulty, 0, Presets.Length - 1);

            // Only plain flag changes here (other components may not have run Awake yet).
            // All of these run before TargetSpawner.Start, so auto-start can never trigger.
            if (targetSpawner != null) targetSpawner.SetAutoStart(false);
            if (shootingController != null) shootingController.SetFireEnabled(false);
            if (scoreManager != null) scoreManager.SetScoringEnabled(false);
        }

        private void Start()
        {
            // Now every component has initialised, so visuals and spawner can be touched safely.
            EnterMenu();
        }

        private void Update()
        {
            if (State != GameState.Playing) return;

            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);

            // Only notify listeners when the displayed whole second changes.
            int second = Mathf.CeilToInt(TimeRemaining);
            if (second != lastShownSecond)
            {
                lastShownSecond = second;
                OnTimeChanged?.Invoke(TimeRemaining);
            }

            if (TimeRemaining <= 0f) EndGame();
        }

        public void SetDifficulty(int index)
        {
            selectedDifficulty = Mathf.Clamp(index, 0, Presets.Length - 1);
            OnDifficultyChanged?.Invoke(selectedDifficulty);
        }

        public void StartGame()
        {
            if (State == GameState.Playing) return;
            if (planeController == null || planeController.PlaneCount < 1)
            {
                Debug.LogWarning("[GameManager] Cannot start: no plane detected yet.");
                return;
            }

            // Clean slate: no stale targets, score or shot count from the previous round.
            if (targetSpawner != null)
            {
                targetSpawner.StopSpawning();
                targetSpawner.ClearAll();
                var p = Presets[selectedDifficulty];
                targetSpawner.ApplyDifficulty(p.maxActive, p.lifetime, p.respawn);
            }
            if (scoreManager != null) { scoreManager.ResetScore(); scoreManager.SetScoringEnabled(true); }
            if (shootingController != null) { shootingController.ResetShots(); shootingController.SetFireEnabled(true); }

            TimeRemaining = roundSeconds;
            lastShownSecond = Mathf.CeilToInt(TimeRemaining);
            SetState(GameState.Playing);
            OnTimeChanged?.Invoke(TimeRemaining);

            if (targetSpawner != null) targetSpawner.StartSpawning();
        }

        public void EndGame()
        {
            if (State != GameState.Playing) return;

            if (scoreManager != null) scoreManager.SetScoringEnabled(false);
            if (shootingController != null) shootingController.SetFireEnabled(false);
            if (targetSpawner != null) { targetSpawner.StopSpawning(); targetSpawner.ClearAll(); }

            int score = scoreManager != null ? scoreManager.Score : 0;
            int hits = scoreManager != null ? scoreManager.Hits : 0;
            int accuracy = scoreManager != null ? scoreManager.GetAccuracyPercent() : 0;

            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool newBest = score > best;
            if (newBest)
            {
                best = score;
                PlayerPrefs.SetInt(BestScoreKey, best);
                PlayerPrefs.Save();
            }

            TimeRemaining = 0f;
            SetState(GameState.GameOver);
            OnGameOver?.Invoke(score, hits, accuracy, best, newBest);
        }

        public void ReturnToMenu()
        {
            // Also safe mid-round: everything is stopped and cleared first.
            if (scoreManager != null) scoreManager.SetScoringEnabled(false);
            if (shootingController != null) shootingController.SetFireEnabled(false);
            EnterMenu();
        }

        public void PlayAgain() => StartGame();

        public void SetDebugMode(bool value)
        {
            DebugMode = value;
            ApplyVisuals();
            OnDebugModeChanged?.Invoke(value);
        }

        private void EnterMenu()
        {
            if (targetSpawner != null) { targetSpawner.StopSpawning(); targetSpawner.ClearAll(); }
            TimeRemaining = 0f;
            SetState(GameState.Menu);
        }

        private void SetState(GameState next)
        {
            State = next;
            ApplyVisuals();
            OnStateChanged?.Invoke(next);
        }

        // Debug visuals (planes, reticle ring, debug text) show in debug mode, and planes/reticle also show
        // while not playing (so the player can see the scan). Detection itself always keeps running.
        private void ApplyVisuals()
        {
            bool playing = State == GameState.Playing;
            bool showWorldDebug = DebugMode || !playing;

            if (planeController != null) planeController.SetPlanesVisible(showWorldDebug);
            if (reticle != null) reticle.SetActive(showWorldDebug);
            if (debugOverlay != null) debugOverlay.DebugVisible = DebugMode;
        }
    }
}
