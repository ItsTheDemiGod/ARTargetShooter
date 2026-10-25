using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARTargetShooter
{
    /// <summary>
    /// Configures plane detection (horizontal + vertical), counts tracked planes and
    /// toggles plane visibility. Lives on the XR Origin next to ARPlaneManager.
    /// </summary>
    [RequireComponent(typeof(ARPlaneManager))]
    public class PlaneDetectionController : MonoBehaviour
    {
        /// <summary>Raised whenever the number of tracked planes changes.</summary>
        public event Action<int> OnPlaneCountChanged;

        public int PlaneCount => trackedPlanes.Count;
        public bool PlanesVisible { get; private set; } = true;

        private ARPlaneManager planeManager;

        // Ids of planes currently tracked. Counting ids (not just +1/-1) keeps the count
        // correct even if an "added" or "removed" event is ever delivered twice.
        private readonly HashSet<TrackableId> trackedPlanes = new HashSet<TrackableId>();

        private void Awake()
        {
            planeManager = GetComponent<ARPlaneManager>();
        }

        private void OnEnable()
        {
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        // AR Foundation 6 event: one call carries lists of added, updated and removed planes.
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            int before = trackedPlanes.Count;

            foreach (ARPlane plane in args.added)
            {
                trackedPlanes.Add(plane.trackableId);
                // New planes must follow the current visibility setting.
                ApplyVisibility(plane, PlanesVisible);
            }

            // "updated" planes are already counted. A plane merged into another
            // ("subsumed") is reported as removed, so the count stays accurate.
            foreach (var removed in args.removed)
                trackedPlanes.Remove(removed.Key);

            if (trackedPlanes.Count != before)
                OnPlaneCountChanged?.Invoke(trackedPlanes.Count);
        }

        /// <summary>Show or hide all current and future detected planes (detection keeps running).</summary>
        public void SetPlanesVisible(bool visible)
        {
            PlanesVisible = visible;
            foreach (ARPlane plane in planeManager.trackables)
                ApplyVisibility(plane, visible);
        }

        // Plane visuals are renderers (mesh + outline line) on the plane object or its children.
        private static void ApplyVisibility(ARPlane plane, bool visible)
        {
            foreach (Renderer r in plane.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
        }
    }
}
