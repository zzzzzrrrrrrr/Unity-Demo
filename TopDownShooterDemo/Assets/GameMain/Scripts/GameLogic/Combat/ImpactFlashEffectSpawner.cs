using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Display-only pooled spawner for impact flashes after projectile hit confirmation.
    /// Hit truth comes from Projectile.OnTriggerEnter2D; this must not drive damage, death, flow, or weapon state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactFlashEffectSpawner : MonoBehaviour
    {
        [Header("Impact Asset")]
        [SerializeField]
        [Tooltip("Display-only impact FX prefab spawned at the projectile hit point after a confirmed hit.")]
        private ImpactFlashEffect impactPrefab;

        [SerializeField]
        [Tooltip("When enabled, uses a generated sprite effect if Impact Prefab is empty. Disabled by default so missing art is a clear no-op.")]
        private bool useFallbackWhenPrefabMissing = false;

        [SerializeField]
        [Tooltip("Parent for pooled impact FX instances. Defaults to this transform when empty.")]
        private Transform container;

        [Header("Pool")]
        [FormerlySerializedAs("initialCapacity")]
        [SerializeField] [Min(0)]
        [Tooltip("Number of impact FX instances pre-created on Awake.")]
        private int initialPoolSize = 12;

        [FormerlySerializedAs("expandStep")]
        [SerializeField] [Min(1)]
        [Tooltip("Number of additional impact FX instances created when the pool expands.")]
        private int poolExpandStep = 6;

        [FormerlySerializedAs("maxCapacity")]
        [SerializeField] [Min(1)]
        [Tooltip("Maximum number of pooled impact FX instances.")]
        private int maxPoolSize = 48;

        [SerializeField]
        [Tooltip("Sorting order applied to SpriteRenderer-based impact FX.")]
        private int sortingOrder = 7;

        [Header("Team Colors")]
        [SerializeField]
        [Tooltip("Tint used for impacts caused by player-owned projectiles.")]
        private Color playerShotImpactColor = new Color(1f, 0.78f, 0.35f, 0.9f);

        [SerializeField]
        [Tooltip("Tint used for impacts caused by boss-owned projectiles.")]
        private Color bossShotImpactColor = new Color(1f, 0.45f, 0.38f, 0.9f);

        [SerializeField]
        [Tooltip("Tint used for impacts with no specific owner team.")]
        private Color neutralImpactColor = new Color(0.92f, 0.92f, 1f, 0.85f);

        private readonly Queue<ImpactFlashEffect> available = new Queue<ImpactFlashEffect>();
        private readonly HashSet<ImpactFlashEffect> allItems = new HashSet<ImpactFlashEffect>();
        private readonly HashSet<int> availableIds = new HashSet<int>();
        private bool warnedPoolExhausted;
        private bool warnedMissingPrefab;
        private int totalCreated;
        private int totalSpawnRequests;
        private int totalSpawned;
        private int totalReused;
        private int totalReturned;
        private int totalPoolMisses;
        private int totalExpansionBatches;
        private static Sprite runtimeFallbackSprite;

        public static ImpactFlashEffectSpawner Instance { get; private set; }
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

        private bool CanCreateItem => impactPrefab != null || useFallbackWhenPrefabMissing;

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

        public void SetSortingOrder(int newSortingOrder)
        {
            sortingOrder = newSortingOrder;
            foreach (var item in allItems)
            {
                if (item == null)
                {
                    continue;
                }

                var renderer = item.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = sortingOrder;
                }
            }
        }

        public static bool TrySpawnAt(Vector3 worldPosition, CombatTeam sourceTeam)
        {
            if (Instance == null)
            {
                return false;
            }

            return Instance.Spawn(worldPosition, sourceTeam) != null;
        }

        public ImpactFlashEffect Spawn(Vector3 worldPosition, CombatTeam sourceTeam)
        {
            if (!CanCreateItem)
            {
                WarnMissingPrefabNoOp();
                return null;
            }

            totalSpawnRequests++;
            var item = GetFromPool();
            if (item == null)
            {
                return null;
            }

            item.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            item.gameObject.SetActive(true);
            item.Play(ResolveColor(sourceTeam), this);
            totalSpawned++;
            return item;
        }

        public void Release(ImpactFlashEffect item)
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

        private ImpactFlashEffect GetFromPool()
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

            if (!CanCreateItem)
            {
                return null;
            }

            if (!warnedPoolExhausted)
            {
                warnedPoolExhausted = true;
                Debug.LogWarning("ImpactFlashEffectSpawner pool exhausted. Increase maxPoolSize for heavy hit scenes.", this);
            }

            totalPoolMisses++;
            return null;
        }

        private void EnsureCapacity(int targetCount)
        {
            if (!CanCreateItem)
            {
                return;
            }

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

        private ImpactFlashEffect CreateItemInstance()
        {
            if (container == null)
            {
                container = transform;
            }

            ImpactFlashEffect instance;
            if (impactPrefab != null)
            {
                instance = Instantiate(impactPrefab, container);
            }
            else
            {
                var go = new GameObject("ImpactFlashEffect");
                go.transform.SetParent(container, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = GetRuntimeFallbackSprite();
                renderer.sortingOrder = sortingOrder;
                instance = go.AddComponent<ImpactFlashEffect>();
                instance.SetRenderer(renderer);
            }

            var existingRenderer = instance.GetComponent<SpriteRenderer>();
            if (existingRenderer != null)
            {
                existingRenderer.sortingOrder = sortingOrder;
            }

            return instance;
        }

        private void WarnMissingPrefabNoOp()
        {
            if (warnedMissingPrefab)
            {
                return;
            }

            warnedMissingPrefab = true;
            Debug.LogWarning(
                "ImpactFlashEffectSpawner has no Impact Prefab assigned. Impact FX will no-op unless Use Fallback When Prefab Missing is enabled.",
                this);
        }

        private Color ResolveColor(CombatTeam sourceTeam)
        {
            switch (sourceTeam)
            {
                case CombatTeam.Player:
                    return playerShotImpactColor;
                case CombatTeam.Boss:
                    return bossShotImpactColor;
                default:
                    return neutralImpactColor;
            }
        }

        private static Sprite GetRuntimeFallbackSprite()
        {
            if (runtimeFallbackSprite != null)
            {
                return runtimeFallbackSprite;
            }

            var size = 16;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            var center = (size - 1) * 0.5f;
            var maxDistance = center;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance01 = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDistance);
                    var alpha = Mathf.Clamp01(1f - distance01);
                    var color = new Color(1f, 1f, 1f, alpha);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, false);
            runtimeFallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            runtimeFallbackSprite.name = "RuntimeImpactFlashSprite";
            return runtimeFallbackSprite;
        }
    }
}
