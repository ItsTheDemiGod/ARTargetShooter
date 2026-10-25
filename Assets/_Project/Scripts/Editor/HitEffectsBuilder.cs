using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARTargetShooter.EditorTools
{
    /// <summary>
    /// Editor-only helper: builds the hit particle material, the HitBurst particle prefab and two
    /// generated WAV sounds, so nothing needs a runtime shader lookup or external assets.
    /// </summary>
    public static class HitEffectsBuilder
    {
        private const int SampleRate = 44100;
        private const string MaterialPath = "Assets/_Project/Materials/HitParticle_Material.mat";
        private const string PrefabPath = "Assets/_Project/Prefabs/HitBurst.prefab";
        private const string HitWavPath = "Assets/_Project/Audio/HitSound.wav";
        private const string CentreWavPath = "Assets/_Project/Audio/CentreHitSound.wav";

        [MenuItem("ARTargetShooter/Create Hit Effects Assets")]
        public static void CreateAssets()
        {
            Material mat = GetOrCreateMaterial();
            if (mat == null) return;   // error already logged

            CreatePrefab(mat);

            // Short pop: 800 Hz sweeping down to 300 Hz with a quick decay.
            CreateWav(HitWavPath, 0.15f, sweep: true);

            // Brighter two-tone: 900 Hz then 1400 Hz.
            CreateWav(CentreWavPath, 0.25f, sweep: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HitEffectsBuilder] Done. Run ARTargetShooter > Verify Phase 5 Setup to check.");
        }

        [MenuItem("ARTargetShooter/Verify Phase 5 Setup")]
        public static void VerifySetup()
        {
            Report("HitParticle_Material", File.Exists(MaterialPath));
            Report("HitBurst.prefab", File.Exists(PrefabPath));
            Report("HitSound.wav", File.Exists(HitWavPath));
            Report("CentreHitSound.wav", File.Exists(CentreWavPath));
        }

        private static void Report(string what, bool ok)
        {
            if (ok) Debug.Log($"[Phase 5 setup] OK: {what} exists.");
            else Debug.LogWarning($"[Phase 5 setup] MISSING: {what}.");
        }

        private static Material GetOrCreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;   // reuse, never overwrite

            // Editor-side Shader.Find is fine; the build keeps the shader because the Material asset references it.
            string used = "Universal Render Pipeline/Particles/Unlit";
            Shader shader = Shader.Find(used);
            if (shader == null) { used = "Universal Render Pipeline/Unlit"; shader = Shader.Find(used); }
            if (shader == null) { used = "Sprites/Default"; shader = Shader.Find(used); }
            if (shader == null)
            {
                Debug.LogError("[HitEffectsBuilder] No usable particle shader found. Stopping.");
                return null;
            }
            Debug.Log($"[HitEffectsBuilder] HitParticle_Material uses shader '{used}'.");

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var mat = new Material(shader);
            // Opaque (default surface): renders reliably on OpenGLES3. Particles shrink instead of fading.
            Color bright = new Color(1f, 0.6f, 0.1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", bright);
            else mat.color = bright;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static void CreatePrefab(Material mat)
        {
            if (File.Exists(PrefabPath))
            {
                Debug.Log($"[HitEffectsBuilder] {PrefabPath} already exists. Nothing done.");
                return;
            }

            var go = new GameObject("HitBurst");
            var ps = go.AddComponent<ParticleSystem>();
            // Component defaults to playing; make sure it is stopped before configuring the main module.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.startSize = 0.04f;
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 30;

            // One burst of 25 particles, no continuous emission.
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Orange to yellow; alpha fades out if the shader is transparent.
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.45f, 0f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            // Shrink to nothing over the lifetime (works with opaque materials).
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Selection.activeObject = prefab;
            Debug.Log($"[HitEffectsBuilder] Created {PrefabPath}.");
        }

        // Generates a mono 16-bit PCM WAV. sweep=true: pop (800->300 Hz); false: two-tone (900 then 1400 Hz).
        private static void CreateWav(string path, float seconds, bool sweep)
        {
            if (File.Exists(path))
            {
                Debug.Log($"[HitEffectsBuilder] {path} already exists. Not overwritten.");
                return;
            }

            int count = Mathf.RoundToInt(seconds * SampleRate);
            var samples = new float[count];
            double phase = 0;
            float peak = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / seconds;
                float freq, env;
                if (sweep)
                {
                    freq = Mathf.Lerp(800f, 300f, u);
                    env = Mathf.Exp(-t * 25f);
                }
                else
                {
                    // Second tone starts at 0.1 s; each tone has its own decay.
                    bool second = t >= 0.1f;
                    freq = second ? 1400f : 900f;
                    env = Mathf.Exp(-(second ? t - 0.1f : t) * 18f);
                }
                env *= Mathf.Min(1f, t / 0.002f);   // 2 ms attack to avoid a click at the start

                phase += 2.0 * Math.PI * freq / SampleRate;   // accumulate phase so the sweep has no glitches
                float s = (float)Math.Sin(phase) * env;
                samples[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            // Normalise to ~0.8 peak.
            float gain = peak > 0f ? 0.8f / peak : 1f;
            byte[] wav = EncodeWav(samples, gain);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, wav);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            Debug.Log($"[HitEffectsBuilder] Created {path}.");
        }

        private static byte[] EncodeWav(float[] samples, float gain)
        {
            int dataSize = samples.Length * 2;
            using (var ms = new MemoryStream(44 + dataSize))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataSize);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);                   // PCM chunk size
                w.Write((short)1);             // PCM
                w.Write((short)1);             // mono
                w.Write(SampleRate);
                w.Write(SampleRate * 2);       // byte rate
                w.Write((short)2);             // block align
                w.Write((short)16);            // bits per sample
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataSize);
                foreach (float s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * gain * 32767f), -32767, 32767));
                return ms.ToArray();
            }
        }
    }
}
