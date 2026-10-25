using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ARTargetShooter
{
    /// <summary>
    /// Watches ARSession.state and the Android camera permission. When AR cannot work
    /// (unsupported device, ARCore missing, camera permission denied) it raises OnErrorChanged with a
    /// clear message (GameUI shows it) and tells GameManager to block play. Defensive: every
    /// dependency is optional and no call here may throw.
    /// </summary>
    public class ARAvailabilityMonitor : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [Tooltip("Seconds to wait before treating a missing camera permission as denied (the system prompt may be showing).")]
        [SerializeField] private float permissionGraceSeconds = 2f;

        /// <summary>Raised with the new message, or null when everything is fine.</summary>
        public event Action<string> OnErrorChanged;
        public string CurrentError { get; private set; }

        private bool installTried;
        private bool installing;
        private bool hasFocus = true;
        private float focusedSince;
        private float nextCheck;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            focusedSince = Time.unscaledTime;
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            if (focus) focusedSince = Time.unscaledTime;   // restart the grace period after the prompt closes
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            Evaluate();
        }

        /// <summary>Retry button: re-check availability and ask for the camera permission again.</summary>
        public void Retry()
        {
            installTried = false;
            try { StartCoroutine(ARSession.CheckAvailability()); }
            catch (Exception e) { Debug.LogWarning($"[ARAvailabilityMonitor] CheckAvailability failed: {e.Message}"); }
#if UNITY_ANDROID && !UNITY_EDITOR
            // If the user chose "don't ask again" this does nothing; the message then points to app settings.
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);
#endif
            Evaluate();
        }

        /// <summary>Opens this app's page in Android settings so the user can enable the Camera permission.</summary>
        public void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);   // FLAG_ACTIVITY_NEW_TASK
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ARAvailabilityMonitor] Could not open settings: {e.Message}");
            }
#endif
        }

        private void Evaluate()
        {
            string error = null;

            switch (GetState())
            {
                case ARSessionState.Unsupported:
                    error = "This device does not support AR (Google ARCore). The game cannot run here.";
                    break;

                case ARSessionState.NeedsInstall:
                    // Try the install flow once; if it is declined or fails, show the message.
                    if (!installTried)
                    {
                        installTried = true;
                        StartCoroutine(InstallRoutine());
                    }
                    else if (!installing)
                    {
                        error = "Google Play Services for AR (ARCore) is required. Install or update it from the Play Store, then tap Retry.";
                    }
                    break;
            }

            if (error == null && CameraPermissionDenied())
                error = "Camera permission is denied. Enable the Camera permission for this app in the Android app settings, then tap Retry.";

            SetError(error);
        }

        private IEnumerator InstallRoutine()
        {
            installing = true;
            // ARSession.Install() is itself a coroutine-style IEnumerator; it can throw if AR is unsupported.
            IEnumerator install = null;
            try { install = ARSession.Install(); }
            catch (Exception e) { Debug.LogWarning($"[ARAvailabilityMonitor] Install failed: {e.Message}"); }
            if (install != null) yield return StartCoroutine(install);
            installing = false;
        }

        // On Android only: no camera permission after the grace period (and while the app has focus,
        // because the system permission prompt takes focus away). Never "denied" in the editor.
        private bool CameraPermissionDenied()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!hasFocus || Time.unscaledTime - focusedSince < permissionGraceSeconds) return false;
            try { return !Permission.HasUserAuthorizedPermission(Permission.Camera); }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        private void SetError(string error)
        {
            if (error == CurrentError) return;
            CurrentError = error;
            if (gameManager != null) gameManager.SetAvailabilityBlocked(error != null);
            OnErrorChanged?.Invoke(error);
        }

        private static ARSessionState GetState()
        {
            try { return ARSession.state; }
            catch (Exception) { return ARSessionState.None; }
        }
    }
}
