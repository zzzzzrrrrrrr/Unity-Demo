using GameMain.GameLogic.Boss;
using GameMain.GameLogic.World;
using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Display-only death FX hook. Health and flow state remain owned by the health/flow controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathFxFeedback : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private SliceEnemyController enemyTarget;
        [SerializeField] private BossHealth bossTarget;

        [Header("Display Only FX")]
        [SerializeField] private GameObject deathFxPrefab;
        [SerializeField] private Vector3 spawnOffset;
        [SerializeField] [Min(0.02f)] private float fxLifetime = 0.55f;

        private bool enemySubscribed;
        private bool bossSubscribed;

        private void Awake()
        {
            ResolveTargets();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void ResolveTargets()
        {
            if (enemyTarget == null)
            {
                enemyTarget = GetComponent<SliceEnemyController>();
            }

            if (bossTarget == null)
            {
                bossTarget = GetComponent<BossHealth>();
            }
        }

        private void Subscribe()
        {
            ResolveTargets();

            if (enemyTarget != null && !enemySubscribed)
            {
                enemyTarget.Defeated += OnEnemyDefeated;
                enemySubscribed = true;
            }

            if (bossTarget != null && !bossSubscribed)
            {
                bossTarget.Defeated += OnBossDefeated;
                bossSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (enemySubscribed && enemyTarget != null)
            {
                enemyTarget.Defeated -= OnEnemyDefeated;
            }

            if (bossSubscribed && bossTarget != null)
            {
                bossTarget.Defeated -= OnBossDefeated;
            }

            enemySubscribed = false;
            bossSubscribed = false;
        }

        private void OnEnemyDefeated(SliceEnemyController enemy)
        {
            var spawnPosition = enemy != null ? enemy.transform.position : transform.position;
            SpawnFx(spawnPosition);
        }

        private void OnBossDefeated()
        {
            SpawnFx(transform.position);
        }

        private void SpawnFx(Vector3 worldPosition)
        {
            if (deathFxPrefab == null)
            {
                return;
            }

            var fx = Instantiate(deathFxPrefab, worldPosition + spawnOffset, Quaternion.identity);
            Destroy(fx, Mathf.Max(0.02f, fxLifetime));
        }
    }
}
