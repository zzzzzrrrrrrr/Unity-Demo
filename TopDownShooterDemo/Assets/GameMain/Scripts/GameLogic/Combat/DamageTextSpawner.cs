using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Lightweight pooling spawner for world-space damage text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageTextSpawner : MonoBehaviour
    {
        [SerializeField] private DamageText damageTextPrefab;
        [SerializeField] private Transform container;
        [Header("Display-Only Pool")]
        [FormerlySerializedAs("initialCapacity")]
        [SerializeField] [Min(0)] private int initialPoolSize = 16;
        [FormerlySerializedAs("expandStep")]
        [SerializeField] [Min(1)] private int poolExpandStep = 6;
        [FormerlySerializedAs("maxCapacity")]
        [SerializeField] [Min(1)] private int maxPoolSize = 64;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.65f, 0f);
        [SerializeField] private Vector2 randomHorizontalOffset = new Vector2(-0.2f, 0.2f);
        [SerializeField] private Vector2 randomVerticalOffset = new Vector2(0f, 0.15f);

        private readonly Queue<DamageText> available = new Queue<DamageText>();
        private readonly HashSet<DamageText> allItems = new HashSet<DamageText>();
        private readonly HashSet<int> availableIds = new HashSet<int>();
        private bool warnedMissingPrefab;
        private bool warnedExhausted;
        private int totalCreated;
        private int totalSpawnRequests;
        private int totalSpawned;
        private int totalReused;
        private int totalReturned;
        private int totalPoolMisses;
        private int totalExpansionBatches;

        public static DamageTextSpawner Instance { get; private set; }
        public int InitialPoolSize => initialPoolSize;
        public int PoolExpandStep => poolExpandStep;
        public int MaxPoolSize => maxPoolSize;
        public int TotalCount => allItems.Count;
        public int AvailableCount => available.Count;
        public int ActiveCount => Mathf.Max(0, allItems.Count - available.Count);
        public int TotalCreated => totalCreated;
        public int TotalSpawnRequests => totalSpawnRequests;
        public int TotalSpawned => totalSpawned;
        public int TotalReused => totalReused;
        public int TotalReturned => totalReturned;
        public int TotalPoolMisses => totalPoolMisses;
        public int TotalExpansionBatches => totalExpansionBatches;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (container == null)
            {
                container = transform;
            }

            EnsureCapacity(initialPoolSize);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ConfigurePool(int newInitialCapacity, int newExpandStep, int newMaxCapacity)
        {
            initialPoolSize = Mathf.Max(0, newInitialCapacity);
            poolExpandStep = Mathf.Max(1, newExpandStep);
            maxPoolSize = Mathf.Max(1, newMaxCapacity);
        }

        public static bool TrySpawnAt(Vector3 worldPosition, float damage, bool isLethal = false)
        {
            if (Instance == null)
            {
                return false;
            }

            return Instance.Spawn(worldPosition, damage, isLethal) != null;
        }

        public DamageText Spawn(Vector3 worldPosition, float damage, bool isLethal)
        {
            if (damage <= 0f)
            {
                return null;
            }

            totalSpawnRequests++;
            var item = GetFromPool();
            if (item == null)
            {
                return null;
            }

            var position = worldPosition + spawnOffset;
            position.x += Random.Range(randomHorizontalOffset.x, randomHorizontalOffset.y);
            position.y += Random.Range(randomVerticalOffset.x, randomVerticalOffset.y);
            item.transform.SetPositionAndRotation(position, Quaternion.identity);
            item.gameObject.SetActive(true);
            item.Play(damage, isLethal, this);
            totalSpawned++;
            return item;
        }

        public void Release(DamageText item)
        {
            if (item == null || !allItems.Contains(item))
            {
                return;
            }

            if (container != null)
            {
                item.transform.SetParent(container, false);
            }

            item.gameObject.SetActive(false);
            var id = item.GetInstanceID();
            if (availableIds.Contains(id))
            {
                return;
            }

            available.Enqueue(item);
            availableIds.Add(id);
            totalReturned++;
        }

        public void ReleaseAllActive()
        {
            foreach (var item in allItems)
            {
                if (item == null || !item.gameObject.activeSelf)
                {
                    continue;
                }

                Release(item);
            }
        }

        private DamageText GetFromPool()
        {
            if (available.Count == 0)
            {
                EnsureCapacity(allItems.Count + Mathf.Max(1, poolExpandStep));
            }

            while (available.Count > 0)
            {
                var item = available.Dequeue();
                if (item == null)
                {
                    continue;
                }

                availableIds.Remove(item.GetInstanceID());
                totalReused++;
                return item;
            }

            if (!warnedExhausted)
            {
                Debug.LogWarning("DamageTextSpawner pool exhausted. Increase maxPoolSize for heavy combat scenes.", this);
                warnedExhausted = true;
            }

            totalPoolMisses++;
            return null;
        }

        private void EnsureCapacity(int targetCount)
        {
            if (maxPoolSize > 0)
            {
                targetCount = Mathf.Min(targetCount, maxPoolSize);
            }

            if (targetCount <= allItems.Count)
            {
                return;
            }

            totalExpansionBatches++;
            while (allItems.Count < targetCount)
            {
                var item = CreateItemInstance();
                if (item == null)
                {
                    return;
                }

                item.gameObject.SetActive(false);
                allItems.Add(item);
                available.Enqueue(item);
                availableIds.Add(item.GetInstanceID());
                totalCreated++;
            }
        }

        private DamageText CreateItemInstance()
        {
            if (container == null)
            {
                container = transform;
            }

            DamageText instance;
            if (damageTextPrefab != null)
            {
                instance = Instantiate(damageTextPrefab, container);
            }
            else
            {
                if (!warnedMissingPrefab)
                {
                    Debug.LogWarning("DamageTextSpawner has no prefab. Using generated fallback TextMesh instance.", this);
                    warnedMissingPrefab = true;
                }

                var go = new GameObject("DamageText");
                go.transform.SetParent(container, false);
                var textMesh = go.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontSize = 56;
                textMesh.characterSize = 0.1f;
                instance = go.AddComponent<DamageText>();
                instance.SetTextMesh(textMesh);
            }

            return instance;
        }
    }
}
