using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARTargetShooter
{
    /// <summary>
    /// Keeps a set number of floating targets around the player. Targets are placed in world space
    /// relative to the camera; a detected plane is only the "start" signal and a floor-height reference.
    /// </summary>
    public class TargetSpawner : MonoBehaviour
    {
        [SerializeField] private Target targetPrefab;
        [SerializeField] private Camera arCamera;
        [SerializeField] private PlaneDetectionController planeController;

        [Header("Amount and timing")]
        [SerializeField] private int maxActiveTargets = 5;
        [SerializeField] private float respawnDelay = 1f;
        [SerializeField] private float targetLifetime = 20f;
        [SerializeField] private bool requirePlaneToStart = true;
        [SerializeField] private bool autoStart = true;

        [Header("Placement (relative to the camera)")]
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 4f;
        [SerializeField] private float minHeightOffset = -0.4f;
        [SerializeField] private float maxHeightOffset = 1.2f;
        [SerializeField] private float frontArcDegrees = 75f;
        [SerializeField] private float minTargetSeparation = 0.7f;
        [SerializeField] private float minHeightAboveFloor = 0.3f;

        /// <summary>Raised with the new active target count whenever it changes.</summary>
        public event Action<int> OnActiveCountChanged;

        public int ActiveCount => active.Count;
        public bool IsSpawning { get; private set; }

        private const int PlacementAttempts = 15;
        private const float SpawnStagger = 0.25f;

        private readonly List<Target> active = new List<Target>();
        private Transform root;
        private ARPlaneManager planeManager;
        private Coroutine refill;
        private bool autoStartConsumed;   // auto-start happens once; later start/stop is up to the game loop

        private void Awake()
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) Debug.LogWarning("[TargetSpawner] No AR camera assigned and no Camera.main found.");
            if (planeController == null) planeController = FindFirstObjectByType<PlaneDetectionController>();
            if (planeController != null) planeManager = planeController.GetComponent<ARPlaneManager>();

            // Runtime root at world origin with no parent, so targets never live under XR Origin trackables.
            root = new GameObject("Targets").transform;
        }

        private void OnEnable()
        {
            if (planeController != null) planeController.OnPlaneCountChanged += OnPlaneCountChanged;
        }

        private void OnDisable()
        {
            if (planeController != null) planeController.OnPlaneCountChanged -= OnPlaneCountChanged;
        }

        private void Start()
        {
            TryAutoStart();
        }

        private void OnPlaneCountChanged(int count) => TryAutoStart();

        private void TryAutoStart()
        {
            if (!autoStart || autoStartConsumed) return;
            bool planeOk = !requirePlaneToStart || (planeController != null && planeController.PlaneCount >= 1);
            if (planeOk) StartSpawning();
        }

        [ContextMenu("Start Spawning")]
        public void StartSpawning()
        {
            autoStartConsumed = true;
            IsSpawning = true;
            ScheduleRefill(0f);
        }

        /// <summary>Stop spawning and let all current targets scale out.</summary>
        public void StopSpawning()
        {
            IsSpawning = false;
            if (refill != null) { StopCoroutine(refill); refill = null; }
            foreach (Target t in active.ToArray()) t.Despawn(false);
        }

        /// <summary>Remove all targets immediately (no animation). If still spawning, a refill follows.</summary>
        [ContextMenu("Clear All")]
        public void ClearAll()
        {
            foreach (Target t in active)
            {
                t.OnDespawned -= HandleDespawned;
                Destroy(t.gameObject);
            }
            active.Clear();
            OnActiveCountChanged?.Invoke(0);
            if (IsSpawning) ScheduleRefill(respawnDelay);
        }

        // One refill coroutine at a time: wait, then top up to maxActiveTargets with a small stagger.
        private void ScheduleRefill(float delay)
        {
            if (refill != null || !IsSpawning) return;
            refill = StartCoroutine(RefillRoutine(delay));
        }

        private IEnumerator RefillRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            while (IsSpawning && active.Count < maxActiveTargets)
            {
                if (!SpawnOne()) break;   // missing camera/prefab: do not loop forever
                yield return new WaitForSeconds(SpawnStagger);
            }
            refill = null;
        }

        private bool SpawnOne()
        {
            if (targetPrefab == null || arCamera == null) return false;

            Vector3 position = PickPosition();
            Target t = Instantiate(targetPrefab, position, Quaternion.identity, root);
            t.Initialize(position, targetLifetime);
            t.OnDespawned += HandleDespawned;
            active.Add(t);
            OnActiveCountChanged?.Invoke(active.Count);
            return true;
        }

        private void HandleDespawned(Target t)
        {
            t.OnDespawned -= HandleDespawned;
            active.Remove(t);
            OnActiveCountChanged?.Invoke(active.Count);
            ScheduleRefill(respawnDelay);   // no-op when spawning was stopped
        }

        // Random point on an arc in front of the camera; retried to keep targets apart.
        private Vector3 PickPosition()
        {
            Transform cam = arCamera.transform;

            // Camera forward flattened to horizontal; fall back to world forward when looking straight up/down.
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            bool hasFloor = TryGetFloorY(out float floorY);
            Vector3 candidate = cam.position;

            for (int i = 0; i < PlacementAttempts; i++)
            {
                float yaw = UnityEngine.Random.Range(-frontArcDegrees, frontArcDegrees);
                float dist = UnityEngine.Random.Range(minDistance, maxDistance);
                float height = UnityEngine.Random.Range(minHeightOffset, maxHeightOffset);

                candidate = cam.position + Quaternion.Euler(0f, yaw, 0f) * forward * dist + Vector3.up * height;
                // Keep targets above the floor when a horizontal plane is known.
                if (hasFloor) candidate.y = Mathf.Max(candidate.y, floorY + minHeightAboveFloor);

                if (IsFarEnoughFromOthers(candidate)) break;   // otherwise the last candidate is accepted
            }
            return candidate;
        }

        private bool IsFarEnoughFromOthers(Vector3 p)
        {
            foreach (Target t in active)
                if ((t.transform.position - p).sqrMagnitude < minTargetSeparation * minTargetSeparation)
                    return false;
            return true;
        }

        // Lowest upward-facing horizontal plane = best guess for the floor.
        private bool TryGetFloorY(out float floorY)
        {
            floorY = 0f;
            if (planeManager == null) return false;

            bool found = false;
            floorY = float.MaxValue;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
                floorY = Mathf.Min(floorY, plane.transform.position.y);
                found = true;
            }
            return found;
        }
    }
}
