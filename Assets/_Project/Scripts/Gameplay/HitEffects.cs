using System.Collections.Generic;
using UnityEngine;

namespace ARTargetShooter
{
    /// <summary>
    /// Plays a particle burst and a pop sound at the position of a scored target.
    /// Bursts come from a pre-built pool so a hit never allocates.
    /// </summary>
    public class HitEffects : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ParticleSystem hitEffectPrefab;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip centreHitClip;   // optional
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private int poolSize = 6;

        private readonly Queue<ParticleSystem> free = new Queue<ParticleSystem>();
        private readonly List<ParticleSystem> busy = new List<ParticleSystem>();
        private readonly List<float> busyUntil = new List<float>();   // parallel to busy: time each burst can be reused
        private AudioSource audioSource;
        private Transform root;

        private void Awake()
        {
            if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
            if (scoreManager == null) Debug.LogWarning("[HitEffects] No ScoreManager found; no effects will play.");

            // 2D audio source on this object (hit sounds are UI-like, not positional).
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            if (hitEffectPrefab == null)
            {
                Debug.LogWarning("[HitEffects] No hit effect prefab assigned; particles are skipped.");
            }
            else
            {
                // Runtime root at world origin with no parent; whole pool is built now, not on demand.
                root = new GameObject("HitEffects").transform;
                for (int i = 0; i < poolSize; i++) free.Enqueue(CreateBurst());
            }
            if (hitClip == null) Debug.LogWarning("[HitEffects] No hit clip assigned; the hit sound is skipped.");
        }

        private void OnEnable()
        {
            if (scoreManager != null) scoreManager.OnTargetScored += HandleTargetScored;
        }

        private void OnDisable()
        {
            if (scoreManager != null) scoreManager.OnTargetScored -= HandleTargetScored;
        }

        private ParticleSystem CreateBurst()
        {
            ParticleSystem ps = Instantiate(hitEffectPrefab, root);
            ps.gameObject.SetActive(false);
            return ps;
        }

        private void HandleTargetScored(Target target, int points, Vector3 position)
        {
            PlayParticles(position);
            PlaySound(points);
        }

        private void PlayParticles(Vector3 position)
        {
            if (hitEffectPrefab == null) return;

            // Expand only if every burst is in use.
            ParticleSystem ps = free.Count > 0 ? free.Dequeue() : CreateBurst();
            ps.transform.position = position;
            ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();

            var main = ps.main;
            busy.Add(ps);
            busyUntil.Add(Time.time + main.duration + main.startLifetime.constantMax);
        }

        private void PlaySound(int points)
        {
            AudioClip clip = hitClip;
            if (centreHitClip != null && scoreManager != null && points == scoreManager.CentrePoints) clip = centreHitClip;
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        // Return finished bursts to the pool (walk backwards so removal is safe).
        private void Update()
        {
            for (int i = busy.Count - 1; i >= 0; i--)
            {
                if (Time.time < busyUntil[i]) continue;
                ParticleSystem ps = busy[i];
                busy.RemoveAt(i);
                busyUntil.RemoveAt(i);
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                free.Enqueue(ps);
            }
        }
    }
}
