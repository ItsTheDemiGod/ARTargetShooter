using System;
using UnityEngine;

namespace ARTargetShooter
{
    /// <summary>
    /// Scores projectile hits and is the only place that despawns a target on a hit.
    /// Only hits score; targets that merely expire (Despawn(false)) never reach this class.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] private ShootingController shootingController;
        [SerializeField] private int normalPoints = 10;
        [SerializeField] private int centrePoints = 25;
        [Tooltip("Hits within this distance (m) of the target centre, measured in the target's plane, score centre points.")]
        [SerializeField] private float centreRadius = 0.05f;

        /// <summary>Raised after a scored hit: target, points awarded, target world position BEFORE despawn.</summary>
        public event Action<Target, int, Vector3> OnTargetScored;
        public event Action<int> OnScoreChanged;

        public int Score { get; private set; }
        public int Hits { get; private set; }
        public int CentrePoints => centrePoints;

        // Misses = shots that did not hit; clamped so it never goes negative.
        public int Misses => shootingController != null ? Mathf.Max(0, shootingController.ShotsFired - Hits) : 0;

        private void Awake()
        {
            if (shootingController == null) shootingController = FindFirstObjectByType<ShootingController>();
            if (shootingController == null) Debug.LogWarning("[ScoreManager] No ShootingController found; nothing will be scored.");
        }

        private void OnEnable()
        {
            if (shootingController != null) shootingController.OnTargetHit += HandleTargetHit;
        }

        private void OnDisable()
        {
            if (shootingController != null) shootingController.OnTargetHit -= HandleTargetHit;
        }

        private void HandleTargetHit(Projectile projectile, Target target)
        {
            // Two projectiles can hit the same target in one physics step: only the first one scores,
            // because Despawn(true) below makes IsAlive false.
            if (target == null || !target.IsAlive) return;

            Vector3 targetPosition = target.transform.position;   // captured before despawn for the effects
            int points = IsCentreHit(projectile, target) ? centrePoints : normalPoints;

            Score += points;
            Hits++;
            target.Despawn(true);

            OnTargetScored?.Invoke(target, points, targetPosition);
            OnScoreChanged?.Invoke(Score);
        }

        // Ring from the hit point's distance to the target centre in the target's own plane (depth ignored).
        private bool IsCentreHit(Projectile projectile, Target target)
        {
            // LastHitPoint is zero if never set; fall back to the projectile position (recycled only after handlers run).
            Vector3 point = projectile != null && projectile.LastHitPoint != Vector3.zero
                ? projectile.LastHitPoint
                : (projectile != null ? projectile.transform.position : target.transform.position);

            Vector3 offset = Vector3.ProjectOnPlane(point - target.transform.position, target.transform.forward);
            return offset.magnitude <= centreRadius;
        }

        public void ResetScore()
        {
            Score = 0;
            Hits = 0;
            OnScoreChanged?.Invoke(Score);
        }
    }
}
