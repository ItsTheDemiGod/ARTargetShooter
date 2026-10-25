using System;
using UnityEngine;

namespace ARTargetShooter
{
    /// <summary>
    /// A ballistic projectile: a Rigidbody with gravity so it arcs. Hits are detected by a sphere-cast
    /// between FixedUpdate positions (so fast shots cannot tunnel through small targets), with
    /// collision/trigger callbacks as a second path. Phase 4 only reports hits; Phase 5 reacts to OnHitTarget.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float maxDistance = 30f;

        /// <summary>Raised once per shot when it hits a live, hittable target (Phase 5 hooks in here).</summary>
        public event Action<Projectile, Target> OnHitTarget;

        private Rigidbody body;
        private float radius;
        private LayerMask targetMask;
        private Vector3 launchPoint;
        private Vector3 previousPosition;
        private float launchTime;
        private bool hasHit;       // a projectile can register only one hit
        private ProjectilePool pool;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            var sphere = GetComponent<SphereCollider>();
            // World-space radius for the sphere-cast.
            radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            int layer = LayerMask.NameToLayer("Projectile");
            if (layer < 0)
            {
                Debug.LogWarning("[Projectile] Layer 'Projectile' does not exist. Run ARTargetShooter > Create Projectile Prefab or add it in Project Settings > Tags and Layers.");
            }
            else
            {
                SetLayerRecursively(transform, layer);
                // Safety net in case the collision matrix was not saved in the project:
                // projectiles never collide with default-layer objects (AR planes) or each other.
                Physics.IgnoreLayerCollision(layer, 0, true);
                Physics.IgnoreLayerCollision(layer, layer, true);
            }
        }

        internal void SetPool(ProjectilePool owner) => pool = owner;

        /// <summary>Place the projectile and fire it. Call after the object is active.</summary>
        public void Launch(Vector3 position, Vector3 direction, float speed, LayerMask mask)
        {
            targetMask = mask;
            hasHit = false;
            launchPoint = position;
            previousPosition = position;
            launchTime = Time.time;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized);
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = direction.normalized * speed;
        }

        private void FixedUpdate()
        {
            if (hasHit) return;

            if (Time.time - launchTime > lifetime ||
                (body.position - launchPoint).sqrMagnitude > maxDistance * maxDistance)
            {
                Release();
                return;
            }

            // Sweep a sphere over the distance travelled since the last physics step.
            Vector3 current = body.position;
            Vector3 delta = current - previousPosition;
            float dist = delta.magnitude;
            if (dist > 0.0001f &&
                Physics.SphereCast(previousPosition, radius, delta / dist, out RaycastHit hit, dist,
                                   targetMask, QueryTriggerInteraction.Ignore))
            {
                TryRegisterHit(hit.collider);
            }
            previousPosition = current;
        }

        private void OnCollisionEnter(Collision collision) => TryRegisterHit(collision.collider);
        private void OnTriggerEnter(Collider other) => TryRegisterHit(other);

        // Single entry point for all hit paths. Anything that is not a live, hittable Target is ignored.
        private void TryRegisterHit(Collider other)
        {
            if (hasHit || other == null) return;
            Target target = other.GetComponentInParent<Target>();
            if (target == null || !target.IsHittable) return;

            hasHit = true;
            float distance = Vector3.Distance(launchPoint, target.transform.position);
            Debug.Log($"Projectile hit target: {target.name} at {distance:F2} m");
            OnHitTarget?.Invoke(this, target);
            Release();
        }

        // Back to the pool (or destroyed if it has none).
        private void Release()
        {
            if (pool != null) pool.Return(this);
            else Destroy(gameObject);
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }
    }
}
