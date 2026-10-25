using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ARTargetShooter
{
    /// <summary>
    /// TEMPORARY Phase 2 test tool: tap to drop a small cube at the reticle, proving that
    /// plane raycasting and touch input work. To be removed or replaced in a later phase.
    /// Disable the TapTestMarker GameObject in Unity before Phase 4 so taps fire projectiles
    /// instead of placing cubes (and untick it during Phase 3 testing).
    /// </summary>
    public class TapToPlaceTestMarker : MonoBehaviour
    {
        private const int MaxMarkers = 20;

        [SerializeField] private PlacementReticle reticle;
        [Tooltip("Optional. If empty, a 5 cm cube is created at runtime.")]
        [SerializeField] private GameObject markerPrefab;

        // Oldest marker first, so the cap removes index 0.
        private readonly List<GameObject> markers = new List<GameObject>();

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
        }

        private void OnDisable()
        {
            Touch.onFingerDown -= OnFingerDown;
            EnhancedTouchSupport.Disable();
        }

        private void OnFingerDown(Finger finger)
        {
            if (reticle == null || !reticle.HasValidPose) return;

            // Ignore touches that start on UI (only checked if an EventSystem exists).
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(finger.currentTouch.touchId))
                return;

            Pose pose = reticle.CurrentPose;
            GameObject marker;
            if (markerPrefab != null)
            {
                marker = Instantiate(markerPrefab, pose.position, pose.rotation);
            }
            else
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.transform.localScale = Vector3.one * 0.05f;
                // Lift by half the height along the surface normal so the cube sits on it, not in it.
                marker.transform.SetPositionAndRotation(
                    pose.position + pose.rotation * Vector3.up * 0.025f, pose.rotation);
                marker.GetComponent<Renderer>().material = PlacementReticle.MakeColorMaterial(Color.red);
            }

            markers.Add(marker);
            if (markers.Count > MaxMarkers)
            {
                Destroy(markers[0]);
                markers.RemoveAt(0);
            }
        }
    }
}
