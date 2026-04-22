using GameMain.Builtin.Sound;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Projectiles;
using UnityEngine;

namespace GameMain.GameLogic.Weapons
{
    /// <summary>
    /// Shared weapon launcher for player and boss.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float fireInterval = 0.25f;
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private float projectileDamage = 10f;
        [SerializeField] private float projectileLifeTime = 3f;
        [SerializeField] private CombatTeam ownerTeam = CombatTeam.Neutral;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private bool playFireSfx = true;
        [SerializeField] private int fireSoundIdOverride;

        private float nextFireTimestamp;
        private bool loggedMissingProjectilePrefab;
        private bool loggedPoolFallback;
        private bool loggedRecoveredPrefabFromPool;
        private bool loggedMissingPoolPrefab;
        private bool loggedRecoveredPoolFromScene;
        private bool loggedAutoSpawnPoint;
        private bool loggedDisabledFireAttempt;

        public Projectile ProjectilePrefab => projectilePrefab;

        public Transform ProjectileSpawnPoint => projectileSpawnPoint;

        public ProjectilePool ProjectilePool => projectilePool;

        public CombatTeam OwnerTeam => ownerTeam;

        public float FireInterval => fireInterval;

        public float ProjectileSpeed => projectileSpeed;

        public float ProjectileDamage => projectileDamage;

        public float ProjectileLifeTime => projectileLifeTime;

        public bool IsCooldownActive => Time.time < nextFireTimestamp;

        public bool TryFire(Vector2 direction, GameObject owner = null)
        {
            return FireInternal(direction, owner, false, 1f);
        }

        public bool FireImmediate(Vector2 direction, GameObject owner = null)
        {
            return FireInternal(direction, owner, true, 1f);
        }

        public bool FireImmediateWithSpeedMultiplier(Vector2 direction, float speedMultiplier, GameObject owner = null)
        {
            return FireInternal(direction, owner, true, speedMultiplier);
        }

        public void EnsureRuntimeReferences()
        {
            RecoverProjectilePoolFromScene();
            RecoverProjectilePrefabFromPool();
            EnsureProjectileSpawnPoint();
        }

        public string BuildRuntimeDebugSummary()
        {
            var cooldownRemaining = Mathf.Max(0f, nextFireTimestamp - Time.time);
            return
                "enabled=" + enabled +
                " team=" + ownerTeam +
                " spawnPoint=" + (projectileSpawnPoint != null ? projectileSpawnPoint.name : "null") +
                " pool=" + (projectilePool != null ? projectilePool.name : "null") +
                " prefab=" + (projectilePrefab != null ? projectilePrefab.name : "null") +
                " fireInterval=" + fireInterval.ToString("0.###") +
                " cooldownRemaining=" + cooldownRemaining.ToString("0.###");
        }

        private bool FireInternal(Vector2 direction, GameObject owner, bool ignoreCooldown, float speedMultiplier)
        {
            EnsureRuntimeReferences();

            if (!enabled)
            {
                if (!loggedDisabledFireAttempt)
                {
                    loggedDisabledFireAttempt = true;
                    Debug.LogWarning("WeaponController fire ignored because component is disabled on " + name + ".", this);
                }

                return false;
            }

            if (projectilePrefab == null)
            {
                if (!loggedMissingProjectilePrefab)
                {
                    Debug.LogWarning(
                        "WeaponController on " + name +
                        " is missing projectilePrefab." +
                        " projectilePool=" + (projectilePool != null ? projectilePool.name : "null") +
                        " poolPrefab=" + (projectilePool != null && projectilePool.ProjectilePrefab != null
                            ? projectilePool.ProjectilePrefab.name
                            : "null"),
                        this);
                    loggedMissingProjectilePrefab = true;
                }

                return false;
            }

            if (!ignoreCooldown && Time.time < nextFireTimestamp)
            {
                return false;
            }

            var normalizedDirection = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            nextFireTimestamp = Time.time + Mathf.Max(0.01f, fireInterval);

            var spawnPosition = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
            var spawnRotation = Quaternion.FromToRotation(Vector3.right, normalizedDirection);
            var projectileInstance = projectilePool != null ? projectilePool.Get(spawnPosition, spawnRotation) : null;
            if (projectileInstance == null)
            {
                if (projectilePool != null && !loggedPoolFallback)
                {
                    Debug.LogWarning("ProjectilePool fallback instantiate triggered on " + name + ".", this);
                    loggedPoolFallback = true;
                }

                if (projectilePool != null && projectilePool.ProjectilePrefab == null && !loggedMissingPoolPrefab)
                {
                    loggedMissingPoolPrefab = true;
                    Debug.LogWarning("ProjectilePool on " + name + " has no ProjectilePrefab assigned.", this);
                }

                projectileInstance = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
            }

            projectileInstance.Initialize(
                normalizedDirection,
                projectileSpeed * Mathf.Max(0.05f, speedMultiplier),
                projectileDamage,
                projectileLifeTime,
                owner != null ? owner : gameObject,
                ownerTeam);

            if (playFireSfx)
            {
                var soundId = ResolveFireSoundId();
                AudioService.PlaySfxById(soundId);
            }

            return true;
        }

        private void RecoverProjectilePoolFromScene()
        {
            if (projectilePool != null)
            {
                return;
            }

            projectilePool = Object.FindObjectOfType<ProjectilePool>();
            if (projectilePool != null && !loggedRecoveredPoolFromScene)
            {
                loggedRecoveredPoolFromScene = true;
                Debug.Log("WeaponController on " + name + " recovered ProjectilePool from scene lookup.", this);
            }
        }

        private void RecoverProjectilePrefabFromPool()
        {
            if (projectilePrefab != null || projectilePool == null || projectilePool.ProjectilePrefab == null)
            {
                return;
            }

            projectilePrefab = projectilePool.ProjectilePrefab;
            if (!loggedRecoveredPrefabFromPool)
            {
                loggedRecoveredPrefabFromPool = true;
                Debug.Log("WeaponController on " + name + " recovered projectilePrefab from ProjectilePool.", this);
            }
        }

        private void EnsureProjectileSpawnPoint()
        {
            if (projectileSpawnPoint != null)
            {
                return;
            }

            var spawnName = ownerTeam == CombatTeam.Boss ? "BossFirePoint" : "PlayerFirePoint";
            var existing = transform.Find(spawnName);
            if (existing == null)
            {
                existing = new GameObject(spawnName).transform;
                existing.SetParent(transform, false);
            }

            existing.localPosition = ownerTeam == CombatTeam.Boss
                ? new Vector3(-0.95f, 0f, 0f)
                : new Vector3(0.82f, 0f, 0f);
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            projectileSpawnPoint = existing;

            if (!loggedAutoSpawnPoint)
            {
                loggedAutoSpawnPoint = true;
                Debug.Log("WeaponController on " + name + " auto-created projectile spawn point: " + projectileSpawnPoint.name, this);
            }
        }

        public void Configure(float newFireInterval, float newProjectileSpeed, float newProjectileDamage, float newProjectileLifeTime)
        {
            fireInterval = Mathf.Max(0.01f, newFireInterval);
            projectileSpeed = Mathf.Max(0.1f, newProjectileSpeed);
            projectileDamage = Mathf.Max(0f, newProjectileDamage);
            projectileLifeTime = Mathf.Max(0.1f, newProjectileLifeTime);
        }

        public void SetProjectilePrefab(Projectile prefab)
        {
            projectilePrefab = prefab;
        }

        public void SetProjectileSpawnPoint(Transform spawnPoint)
        {
            projectileSpawnPoint = spawnPoint;
        }

        public void SetOwnerTeam(CombatTeam team)
        {
            ownerTeam = team;
        }

        public void SetProjectilePool(ProjectilePool pool)
        {
            projectilePool = pool;
        }

        public void ResetFireCooldown()
        {
            nextFireTimestamp = 0f;
        }

        private int ResolveFireSoundId()
        {
            if (fireSoundIdOverride > 0)
            {
                return fireSoundIdOverride;
            }

            switch (ownerTeam)
            {
                case CombatTeam.Player:
                    return SoundIds.SfxPlayerShoot;
                case CombatTeam.Boss:
                    return SoundIds.SfxBossShoot;
                default:
                    return 0;
            }
        }
    }
}
