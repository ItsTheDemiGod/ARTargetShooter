using System.Collections.Generic;
using UnityEngine;

namespace ARTargetShooter
{
    /// <summary>
    /// Minimal projectile pool: reuses inactive projectiles instead of instantiating one per shot.
    /// Plain C# class created by ShootingController; objects live under a root named "Projectiles".
    /// </summary>
    public class ProjectilePool
    {
        private readonly Projectile prefab;
        private readonly Transform root;
        private readonly Queue<Projectile> free = new Queue<Projectile>();

        public ProjectilePool(Projectile prefab, int initialSize)
        {
            this.prefab = prefab;
            // Own root at world origin, no parent (never under XR Origin).
            root = new GameObject("Projectiles").transform;
            for (int i = 0; i < initialSize; i++) Return(Create());
        }

        /// <summary>Returns an active projectile; creates a new one if the pool is empty.</summary>
        public Projectile Get()
        {
            Projectile p = null;
            // Skip entries destroyed externally (e.g. scene change).
            while (free.Count > 0 && p == null) p = free.Dequeue();
            if (p == null) p = Create();
            p.gameObject.SetActive(true);
            return p;
        }

        public void Return(Projectile p)
        {
            if (p == null) return;
            p.gameObject.SetActive(false);
            free.Enqueue(p);
        }

        private Projectile Create()
        {
            Projectile p = Object.Instantiate(prefab, root);
            p.SetPool(this);
            return p;
        }
    }
}
