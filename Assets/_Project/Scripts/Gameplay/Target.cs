using System;
using UnityEngine;

namespace ARTargetShooter
{
    /// <summary>
    /// A floating bullseye target. Moves purely in world space around its spawn position
    /// (bob + small Lissajous drift), faces the player, scales in on spawn and scales out
    /// when its lifetime ends or it is despawned. Never parented to any AR trackable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Target : MonoBehaviour
    {
        [Header("Bobbing (randomised per target within these ranges)")]
        [SerializeField] private Vector2 bobAmplitudeRange = new Vector2(0.05f, 0.15f);
        [SerializeField] private Vector2 bobSpeedRange = new Vector2(0.8f, 1.6f);

        [Header("Horizontal drift")]
        [SerializeField] private Vector2 driftRadiusRange = new Vector2(0.15f, 0.3f);
        [SerializeField] private Vector2 driftSpeedRange = new Vector2(0.2f, 0.45f);

        [Header("Behaviour")]
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private float defaultLifetime = 20f;
        [SerializeField] private float scaleTime = 0.3f;

        /// <summary>Raised once, after the scale-out finished and just before the object is destroyed.</summary>
        public event Action<Target> OnDespawned;

        private enum State { ScalingIn, Alive, ScalingOut }

        private State state = State.ScalingIn;
        private bool initialized;
        private Vector3 anchorPosition;   // world-space centre of the floating motion
        private Vector3 baseScale;
        private float lifetime;
        private float age;
        private float scaleOutTimer;
        private float bobAmplitude, bobSpeed, bobPhase;
        private float driftRadius, driftSpeed, driftPhase;
        private Transform cam;

        /// <summary>False once the target is shrinking away (expired or hit).</summary>
        public bool IsAlive => state != State.ScalingOut;

        /// <summary>
        /// True when a projectile may hit this target: alive, and past ~60% of the scale-in animation
        /// (so an almost invisible, just-spawned target cannot be hit).
        /// </summary>
        public bool IsHittable => IsAlive && (state != State.ScalingIn || age / scaleTime > 0.6f);

        private void Awake()
        {
            baseScale = transform.localScale;

            // Put the target (and children) on the "Target" layer so projectiles can use a layer mask.
            int layer = LayerMask.NameToLayer("Target");
            if (layer < 0)
                Debug.LogWarning("[Target] Layer 'Target' does not exist. Add it in Edit > Project Settings > Tags and Layers (or run ARTargetShooter > Create Target Prefab).");
            else
                SetLayerRecursively(transform, layer);
        }

        private void Start()
        {
            // If nobody called Initialize (e.g. prefab dragged into the scene), float around where we are.
            if (!initialized) Initialize(transform.position, defaultLifetime);
            if (Camera.main != null) cam = Camera.main.transform;
        }

        /// <summary>Set the world position to float around and how long the target lives.</summary>
        public void Initialize(Vector3 worldPosition, float lifetime)
        {
            initialized = true;
            anchorPosition = worldPosition;
            this.lifetime = lifetime;
            transform.position = worldPosition;
            if (Camera.main != null) cam = Camera.main.transform;

            // Random parameters and phases so targets do not move in sync.
            bobAmplitude = UnityEngine.Random.Range(bobAmplitudeRange.x, bobAmplitudeRange.y);
            bobSpeed = UnityEngine.Random.Range(bobSpeedRange.x, bobSpeedRange.y);
            bobPhase = UnityEngine.Random.value * Mathf.PI * 2f;
            driftRadius = UnityEngine.Random.Range(driftRadiusRange.x, driftRadiusRange.y);
            driftSpeed = UnityEngine.Random.Range(driftSpeedRange.x, driftSpeedRange.y);
            driftPhase = UnityEngine.Random.value * Mathf.PI * 2f;

            state = State.ScalingIn;
            age = 0f;
            transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// Shrink away, then raise OnDespawned and destroy the GameObject.
        /// Phase 5 will call Despawn(true) when a projectile hits; wasHit is kept for that.
        /// </summary>
        public void Despawn(bool wasHit)
        {
            if (state == State.ScalingOut) return;
            state = State.ScalingOut;
            // Start the shrink from the current scale so an early despawn does not pop.
            scaleOutTimer = scaleTime * Mathf.Clamp01(transform.localScale.x / Mathf.Max(baseScale.x, 0.0001f));
        }

        private void Update()
        {
            if (!initialized) return;
            age += Time.deltaTime;

            switch (state)
            {
                case State.ScalingIn:
                    float t = Mathf.Clamp01(age / scaleTime);
                    transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, t);
                    if (t >= 1f) state = State.Alive;
                    break;

                case State.ScalingOut:
                    scaleOutTimer -= Time.deltaTime;
                    float k = Mathf.Clamp01(scaleOutTimer / scaleTime);
                    transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, k);
                    if (scaleOutTimer <= 0f)
                    {
                        OnDespawned?.Invoke(this);
                        Destroy(gameObject);
                        return;
                    }
                    break;
            }

            if (state != State.ScalingOut && age >= lifetime) Despawn(false);

            Float();
            FaceCamera();
        }

        // Vertical sine bob plus a figure-eight (Lissajous 1:2) drift in the horizontal plane.
        private void Float()
        {
            float bob = Mathf.Sin(age * bobSpeed + bobPhase) * bobAmplitude;
            float a = age * driftSpeed + driftPhase;
            float dx = Mathf.Sin(a) * driftRadius;
            float dz = Mathf.Sin(2f * a) * driftRadius * 0.5f;
            // Moving the transform of a kinematic Rigidbody is fine here: targets do not push anything.
            transform.position = anchorPosition + new Vector3(dx, bob, dz);
        }

        // Rotate around Y only, so the bullseye (local +Z) points at the player and the target stays upright.
        private void FaceCamera()
        {
            if (!faceCamera || cam == null) return;
            Vector3 toCam = cam.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
        }

        private static void SetLayerRecursively(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursively(child, layer);
        }
    }
}
