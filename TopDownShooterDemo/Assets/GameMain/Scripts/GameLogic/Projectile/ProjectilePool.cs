using System.Collections.Generic;
using UnityEngine;

namespace GameMain.GameLogic.Projectiles
{
    /// <summary>
    /// Lightweight projectile-only pool with expandable capacity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform container;
        [SerializeField] private int initialCapacity = 24;
        [SerializeField] private int expandStep = 8;
        [SerializeField] private int maxCapacity = 256;

        private readonly Queue<Projectile> available = new Queue<Projectile>();
        private readonly HashSet<int> availableIds = new HashSet<int>();
        private readonly HashSet<Projectile> allProjectiles = new HashSet<Projectile>();
        private bool loggedMissingPrefab;
        private bool loggedPoolExhausted;
        private int totalCreated;
        private int totalGetRequests;
        private int totalReused;
        private int totalReturned;
        private int totalPoolMisses;
        private int totalExpansionBatches;

        public Projectile ProjectilePrefab => projectilePrefab;
        public int InitialCapacity => initialCapacity;
        public int ExpandStep => expandStep;
        public int MaxCapacity => maxCapacity;
        public int TotalCount => allProjectiles.Count;
        public int AvailableCount => available.Count;
        public int ActiveCount => Mathf.Max(0, allProjectiles.Count - available.Count);
        public int TotalCreated => totalCreated;
        public int TotalGetRequests => totalGetRequests;
        public int TotalReused => totalReused;
        public int TotalReturned => totalReturned;
        public int TotalPoolMisses => totalPoolMisses;
        public int TotalExpansionBatches => totalExpansionBatches;

        private void Awake()
        {
            if (container == null)
            {
                container = transform;
            }

            EnsureCapacity(Mathf.Max(0, initialCapacity));
        }

        public void SetProjectilePrefab(Projectile prefab)
        {
            projectilePrefab = prefab;
        }

        public void ConfigureCapacity(int newInitialCapacity, int newExpandStep, int newMaxCapacity)
        {
            initialCapacity = Mathf.Max(0, newInitialCapacity);
            expandStep = Mathf.Max(1, newExpandStep);
            maxCapacity = Mathf.Max(0, newMaxCapacity);
        }

        public Projectile Get(Vector3 position, Quaternion rotation)
        {
            totalGetRequests++;

            if (projectilePrefab == null)
            {
                if (!loggedMissingPrefab)
                {
                    Debug.LogWarning("ProjectilePool has no projectilePrefab assigned.", this);
                    loggedMissingPrefab = true;
                }

                return null;
            }

            if (available.Count == 0)
            {
                var expectedIncrease = Mathf.Max(1, expandStep);
                if (!EnsureCapacity(allProjectiles.Count + expectedIncrease))
                {
                    return null;
                }
            }

            while (available.Count > 0)
            {
                var projectile = available.Dequeue();
                if (projectile == null)
                {
                    continue;
                }

                availableIds.Remove(projectile.GetInstanceID());
                projectile.transform.SetPositionAndRotation(position, rotation);
                projectile.gameObject.SetActive(true);
                totalReused++;
                return projectile;
            }

            if (!loggedPoolExhausted)
            {
                Debug.LogWarning("ProjectilePool exhausted. Increase maxCapacity to avoid fallback instantiate.", this);
                loggedPoolExhausted = true;
            }

            totalPoolMisses++;
            return null;
        }

        public void Return(Projectile projectile)
        {
            if (projectile == null || !allProjectiles.Contains(projectile))
            {
                return;
            }

            var id = projectile.GetInstanceID();
            if (availableIds.Contains(id))
            {
                return;
            }

            if (container != null)
            {
                projectile.transform.SetParent(container, false);
            }

            projectile.gameObject.SetActive(false);
            available.Enqueue(projectile);
            availableIds.Add(id);
            totalReturned++;
        }

        public void ReleaseAllActiveProjectiles()
        {
            foreach (var projectile in allProjectiles)
            {
                if (projectile == null || !projectile.gameObject.activeSelf)
                {
                    continue;
                }

                Return(projectile);
            }
        }

        private bool EnsureCapacity(int targetCount)
        {
            if (projectilePrefab == null)
            {
                if (!loggedMissingPrefab)
                {
                    Debug.LogWarning("ProjectilePool cannot expand because projectilePrefab is missing.", this);
                    loggedMissingPrefab = true;
                }

                return false;
            }

            if (maxCapacity > 0)
            {
                targetCount = Mathf.Min(targetCount, maxCapacity);
            }

            if (targetCount <= allProjectiles.Count)
            {
                return true;
            }

            totalExpansionBatches++;
            while (allProjectiles.Count < targetCount)
            {
                var projectile = Instantiate(projectilePrefab, container != null ? container : transform);
                projectile.gameObject.SetActive(false);
                projectile.ReleaseRequested += OnReleaseRequested;
                allProjectiles.Add(projectile);
                available.Enqueue(projectile);
                availableIds.Add(projectile.GetInstanceID());
                totalCreated++;
            }

            return true;
        }

        private void OnReleaseRequested(Projectile projectile)
        {
            Return(projectile);
        }
    }
}
