using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ARTargetShooter
{
    /// <summary>
    /// Tap anywhere to fire a projectile from the AR camera toward the touched point.
    /// Gravity on the projectile's Rigidbody makes the shot arc. Optional hold-to-charge (off by default).
    /// </summary>
    public class ShootingController : MonoBehaviour
    {
        [SerializeField] private Camera arCamera;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LayerMask targetMask;

        [Header("Shot")]
        [SerializeField] private float launchSpeed = 10f;
        [SerializeField] private float spawnForwardOffset = 0.15f;
        [Tooltip("Tilts the shot upward to compensate for gravity drop.")]
        [SerializeField] private float aimUpwardAngle = 3f;
        [SerializeField] private float fireCooldown = 0.15f;
        [SerializeField] private bool fireEnabled = true;
        [SerializeField] private int initialPoolSize = 10;

        [Header("Hold to charge (optional)")]
        [SerializeField] private bool enableHoldToCharge = false;
        [SerializeField] private float minSpeed = 7f;
        [SerializeField] private float maxSpeed = 16f;
        [SerializeField] private float maxChargeTime = 1f;

        /// <summary>Raised after every shot.</summary>
        public event Action OnFired;
        /// <summary>Raised when any projectile hits a live target. The projectile is recycled right after the handlers return.</summary>
        public event Action<Projectile, Target> OnTargetHit;
        public int ShotsFired { get; private set; }

        private readonly System.Collections.Generic.List<Projectile> subscribed = new System.Collections.Generic.List<Projectile>();

        private ProjectilePool pool;
        private float nextFireTime;
        private Finger chargingFinger;
        private float chargeStartTime;

        private void Awake()
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) Debug.LogWarning("[ShootingController] No AR camera assigned and no Camera.main found.");

            // Default mask: the "Target" layer, if the user left it at Nothing.
            if (targetMask.value == 0)
            {
                int layer = LayerMask.NameToLayer("Target");
                if (layer >= 0) targetMask = 1 << layer;
                else Debug.LogWarning("[ShootingController] Layer 'Target' not found; hits cannot be detected.");
            }

            if (projectilePrefab == null) Debug.LogWarning("[ShootingController] No projectile prefab assigned.");
            else
            {
                pool = new ProjectilePool(projectilePrefab);
                // Subscribe before prewarming so every pooled projectile is hooked up exactly once.
                pool.OnProjectileCreated += SubscribeToProjectile;
                pool.Prewarm(initialPoolSize);
            }
        }

        private void SubscribeToProjectile(Projectile p)
        {
            p.OnHitTarget += HandleProjectileHit;
            subscribed.Add(p);
        }

        // Forward projectile hits so other systems (score, effects) listen to the controller only.
        private void HandleProjectileHit(Projectile projectile, Target target) => OnTargetHit?.Invoke(projectile, target);

        private void OnDestroy()
        {
            if (pool != null) pool.OnProjectileCreated -= SubscribeToProjectile;
            foreach (Projectile p in subscribed)
                if (p != null) p.OnHitTarget -= HandleProjectileHit;
            subscribed.Clear();
        }

        private void OnEnable()
        {
            TouchInputService.Acquire();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerUp += OnFingerUp;
        }

        private void OnDisable()
        {
            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerUp -= OnFingerUp;
            TouchInputService.Release();
            chargingFinger = null;
        }

        public void SetFireEnabled(bool value) => fireEnabled = value;

        private void OnFingerDown(Finger finger)
        {
            if (!fireEnabled || IsOverUI(finger)) return;

            if (enableHoldToCharge)
            {
                // Charge starts on touch-down; the shot happens on release.
                if (chargingFinger == null)
                {
                    chargingFinger = finger;
                    chargeStartTime = Time.time;
                }
            }
            else
            {
                FireAtScreenPoint(finger.screenPosition, launchSpeed);
            }
        }

        private void OnFingerUp(Finger finger)
        {
            if (!enableHoldToCharge || finger != chargingFinger) return;
            chargingFinger = null;
            if (!fireEnabled) return;

            float t = Mathf.Clamp01((Time.time - chargeStartTime) / Mathf.Max(maxChargeTime, 0.01f));
            FireAtScreenPoint(finger.screenPosition, Mathf.Lerp(minSpeed, maxSpeed, t));
        }

        private static bool IsOverUI(Finger finger)
        {
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(finger.currentTouch.touchId);
        }

        private void FireAtScreenPoint(Vector2 screenPosition, float speed)
        {
            if (arCamera == null) return;
            Ray ray = arCamera.ScreenPointToRay(screenPosition);

            // Tilt the direction up around the camera's right axis (negative angle = pitch up in Unity).
            Vector3 direction = Quaternion.AngleAxis(-aimUpwardAngle, arCamera.transform.right) * ray.direction;
            Fire(ray.origin + ray.direction * spawnForwardOffset, direction, speed);
        }

        private void Fire(Vector3 position, Vector3 direction, float speed)
        {
            if (pool == null || Time.time < nextFireTime) return;
            nextFireTime = Time.time + fireCooldown;

            Projectile p = pool.Get();
            p.Launch(position, direction, speed, targetMask);
            ShotsFired++;
            OnFired?.Invoke();
        }

        [ContextMenu("Fire Test Shot")]
        private void FireTestShot()
        {
            if (!Application.isPlaying || arCamera == null)
            {
                Debug.LogWarning("[ShootingController] Fire Test Shot only works in play mode with a camera.");
                return;
            }
            Fire(arCamera.transform.position + arCamera.transform.forward * spawnForwardOffset,
                 arCamera.transform.forward, launchSpeed);
        }
    }
}
