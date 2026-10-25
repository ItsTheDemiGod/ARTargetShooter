using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARTargetShooter
{
    /// <summary>
    /// Every frame, raycasts from the screen centre against detected planes and moves a
    /// reticle onto the hit surface. Hidden when no plane is under the screen centre.
    /// </summary>
    public class PlacementReticle : MonoBehaviour
    {
        [SerializeField] private ARRaycastManager raycastManager;
        [Tooltip("Optional. If empty, a ring reticle is built at runtime from primitives.")]
        [SerializeField] private GameObject reticleVisual;

        private bool isActive = true;

        public bool HasValidPose { get; private set; }
        public Pose CurrentPose { get; private set; }

        // Reused every frame to avoid allocating a new list per raycast.
        private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        private void Awake()
        {
            if (reticleVisual == null)
                reticleVisual = BuildDefaultReticle();
            reticleVisual.SetActive(false);
        }

        /// <summary>Hide the ring and pause the per-frame raycast (HasValidPose becomes false) when false.</summary>
        public void SetActive(bool active)
        {
            isActive = active;
            if (active) return;
            HasValidPose = false;
            if (reticleVisual != null) reticleVisual.SetActive(false);
        }

        private void Update()
        {
            if (!isActive || raycastManager == null) return;

            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            // PlaneWithinPolygon: only hit points inside the detected plane's boundary.
            if (raycastManager.Raycast(centre, hits, TrackableType.PlaneWithinPolygon))
            {
                // Hits are sorted closest-first, so [0] is the nearest surface.
                CurrentPose = hits[0].pose;
                HasValidPose = true;
                transform.SetPositionAndRotation(CurrentPose.position, CurrentPose.rotation);
            }
            else
            {
                HasValidPose = false;
            }

            if (reticleVisual.activeSelf != HasValidPose)
                reticleVisual.SetActive(HasValidPose);
        }

        // Ring look from flattened cylinders: outer cyan disc, dark inner disc, cyan centre dot.
        private GameObject BuildDefaultReticle()
        {
            var root = new GameObject("ReticleVisual");
            root.transform.SetParent(transform, false);
            AddDisc(root.transform, 0.16f, 0.000f, new Color(0f, 0.9f, 1f));
            AddDisc(root.transform, 0.12f, 0.001f, new Color(0f, 0.1f, 0.15f));
            AddDisc(root.transform, 0.02f, 0.002f, new Color(0f, 0.9f, 1f));
            return root;
        }

        private static void AddDisc(Transform parent, float diameter, float height, Color color)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Collider removed so the reticle never blocks physics raycasts or projectiles.
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, height, 0f);
            // Cylinder is 2 units tall; Y scale 0.0025 gives a ~5 mm thin disc.
            disc.transform.localScale = new Vector3(diameter, 0.0025f, diameter);
            disc.GetComponent<Renderer>().material = MakeColorMaterial(color);
        }

        /// <summary>
        /// Flat-colour material usable under URP. Shader.Find only works in a build if the shader
        /// is included there, so fall back to the default primitive material (always in the build).
        /// </summary>
        internal static Material MakeColorMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                // Fallback: clone the default primitive material (URP Lit, referenced by the pipeline asset).
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Destroy(tmp);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            return mat;
        }
    }
}
