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

        private void Update()
        {
            // Exponential moving average of frame time; unscaled so it ignores timeScale.
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.1f);
        }

        private void OnGUI()
        {
            // Rebuild style only when the screen size (hence font size) changes.
            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * fontHeightFraction));
            if (style == null || size != styleFontSize)
            {
                styleFontSize = size;
                style = new GUIStyle(GUI.skin.label) { fontSize = size, richText = false };
                style.normal.textColor = Color.yellow;
            }

            float fps = smoothedDelta > 0f ? 1f / smoothedDelta : 0f;
            string text = $"AR: {GetSessionState()}\nFPS: {fps:F0}\nDevice: {SystemInfo.deviceModel}";

            float pad = size * 0.5f;
            var rect = new Rect(pad, pad, Screen.width - pad * 2f, size * 5f);
            GUI.Label(rect, text, style);
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
