using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARTargetShooter
{
    /// <summary>
    /// On-screen debug text (top-left): ARSession state, smoothed FPS, device model.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class ARSessionDebugOverlay : MonoBehaviour
    {
        // Font size as a fraction of screen height (~3%).
        [SerializeField] private float fontHeightFraction = 0.03f;

        private float smoothedDelta = 1f / 60f;
        private GUIStyle style;
        private int styleFontSize;

        // Null-safe: the overlay still works when these components are missing ("n/a").
        private PlaneDetectionController planes;
        private PlacementReticle reticle;
        private TargetSpawner spawner;
        private ShootingController shooter;
        private ScoreManager score;
        private HitEffects hitEffects;
        private float nextLookup;

        private void Update()
        {
            // Exponential moving average of frame time; unscaled so it ignores timeScale.
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.1f);

            // Look up the AR scripts once per second until found; they may be absent from the scene.
            if ((planes == null || reticle == null || spawner == null || shooter == null || score == null || hitEffects == null) && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1f;
                if (hitEffects == null) hitEffects = FindFirstObjectByType<HitEffects>();
                if (score == null) score = FindFirstObjectByType<ScoreManager>();
                if (shooter == null) shooter = FindFirstObjectByType<ShootingController>();
                if (spawner == null) spawner = FindFirstObjectByType<TargetSpawner>();
                if (planes == null) planes = FindFirstObjectByType<PlaneDetectionController>();
                if (reticle == null) reticle = FindFirstObjectByType<PlacementReticle>();
            }
        }

        /// <summary>Debug text is hidden unless the game manager's debug mode turns this on.</summary>
        public bool DebugVisible { get; set; } = false;

        private void OnGUI()
        {
            if (!DebugVisible) return;

            // Rebuild style only when the screen size (hence font size) changes.
            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * fontHeightFraction));
            if (style == null || size != styleFontSize)
            {
                styleFontSize = size;
                style = new GUIStyle(GUI.skin.label) { fontSize = size, richText = false };
                style.normal.textColor = Color.yellow;
            }

            float fps = smoothedDelta > 0f ? 1f / smoothedDelta : 0f;
            string text = $"AR: {GetSessionState()}\nFPS: {fps:F0}\nDevice: {SystemInfo.deviceModel}"
                + $"\nPlanes: {(planes != null ? planes.PlaneCount.ToString() : "n/a")}"
                + $"\nReticle: {(reticle != null ? (reticle.HasValidPose ? "valid pose" : "no pose") : "n/a")}"
                + $"\nTargets: {(spawner != null ? spawner.ActiveCount.ToString() : "n/a")}"
                + $"\nBursts: {(hitEffects != null ? hitEffects.ActiveBursts.ToString() : "n/a")}"
                + $"\nShots: {(shooter != null ? shooter.ShotsFired.ToString() : "n/a")}"
                + $"\nScore: {(score != null ? score.Score.ToString() : "n/a")}"
                + $"\nHits: {(score != null ? score.Hits.ToString() : "n/a")}"
                + $"\nMisses: {(score != null ? score.Misses.ToString() : "n/a")}";

            // Height comes from the actual text, so every line fits however many lines there are.
            float pad = size * 0.5f;
            float width = Screen.width - pad * 2f;
            float height = style.CalcHeight(new GUIContent(text), width);
            GUI.Label(new Rect(pad, pad, width, height), text, style);
        }

        // ARSession.state is a static property; it can throw or be unavailable
        // (e.g. in the editor without an XR loader), so never let it break the overlay.
        private static string GetSessionState()
        {
            try { return ARSession.state.ToString(); }
            catch (System.Exception) { return "Unavailable"; }
        }
    }
}
