using System;
using System.Collections.Generic;
using GameMain.Builtin.Sound;
using GameMain.GameLogic.Combat;
using UnityEngine;

namespace GameMain.GameLogic.Projectiles
{
    /// <summary>
    /// Basic 2D projectile with trigger hit.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float defaultSpeed = 12f;
        [SerializeField] private float defaultDamage = 10f;
        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool destroyOnWorldCollision = true;
        [SerializeField] private bool allowFriendlyFire = false;
        [Header("Visual")]
        [SerializeField] private SpriteRenderer projectileRenderer;
        [SerializeField] private Color playerProjectileColor = new Color(0.42f, 0.98f, 1f, 1f);
        [SerializeField] private Color bossProjectileColor = new Color(1f, 0.45f, 0.35f, 1f);
        [SerializeField] private Color neutralProjectileColor = new Color(1f, 0.95f, 0.8f, 1f);
        [SerializeField] [Min(0.2f)] private float playerProjectileScaleMultiplier = 0.92f;
        [SerializeField] [Min(0.2f)] private float bossProjectileScaleMultiplier = 1.18f;
        [SerializeField] [Min(0.2f)] private float neutralProjectileScaleMultiplier = 1f;
        [SerializeField] [Min(0.1f)] private float worldHitLogInterval = 0.8f;
        [SerializeField] [Min(0.1f)] private float damageHitLogInterval = 0.45f;
        [SerializeField] [Min(0.1f)] private float friendlyIgnoreLogInterval = 0.8f;

        private Rigidbody2D cachedRigidbody;
        private Collider2D cachedCollider;
        private SpriteRenderer cachedRenderer;
        private Vector2 moveDirection = Vector2.right;
        private float moveSpeed;
        private float damageAmount;
        private float lifeTimer;
        private GameObject owner;
        private CombatTeam ownerTeam = CombatTeam.Neutral;
        private bool isReleased;
        private Vector3 baseLocalScale = Vector3.one;
        private readonly HashSet<int> hitTargetIds = new HashSet<int>();
        private static float nextWorldHitLogTime;
        private static float nextDamageHitLogTime;
        private static float nextFriendlyIgnoreLogTime;

        /// <summary>
        /// Optional hook for future pooling. If no listener exists, projectile self-destroys.
        /// </summary>
        public event Action<Projectile> ReleaseRequested;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedCollider = GetComponent<Collider2D>();
            cachedRenderer = projectileRenderer != null ? projectileRenderer : GetComponentInChildren<SpriteRenderer>();
            baseLocalScale = transform.localScale;

            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (!cachedCollider.isTrigger)
            {
                cachedCollider.isTrigger = true;
            }

            moveSpeed = defaultSpeed;
            damageAmount = defaultDamage;
            lifeTimer = lifeTime;
        }

        private void OnEnable()
        {
            isReleased = false;
            lifeTimer = Mathf.Max(0.1f, lifeTime);
            hitTargetIds.Clear();
        }

        private void OnDisable()
        {
            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
            }
        }

        public void Initialize(Vector2 direction, float speed, float damage, float lifetime, GameObject sourceOwner, CombatTeam sourceTeam)
        {
            moveDirection = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
            moveSpeed = Mathf.Max(0.1f, speed);
            damageAmount = Mathf.Max(0f, damage);
            lifeTime = Mathf.Max(0.1f, lifetime);
            lifeTimer = lifeTime;
            owner = sourceOwner;
            ownerTeam = sourceTeam;
            isReleased = false;
            hitTargetIds.Clear();
            ApplyTeamVisual(sourceTeam);
        }

        private void Update()
        {
            if (isReleased)
            {
                return;
            }

            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f)
            {
                Release();
            }
        }

        private void FixedUpdate()
        {
            if (isReleased)
            {
                return;
            }

            cachedRigidbody.velocity = moveDirection * moveSpeed;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isReleased || other == null)
            {
                return;
            }

            if (IsOwnerCollider(other))
            {
                return;
            }

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                HandleWorldCollision(other);
                return;
            }

            if (damageable.IsDead)
            {
                return;
            }

            if (!allowFriendlyFire && ownerTeam != CombatTeam.Neutral && damageable.Team == ownerTeam)
            {
                if (Time.unscaledTime >= nextFriendlyIgnoreLogTime)
                {
                    nextFriendlyIgnoreLogTime = Time.unscaledTime + Mathf.Max(0.1f, friendlyIgnoreLogInterval);
                    Debug.Log(
                        "Projectile ignored friendly target. projectile=" + name +
                        " ownerTeam=" + ownerTeam +
                        " targetTeam=" + damageable.Team +
                        " target=" + other.name,
                        this);
                }

                return;
            }

            var targetId = ResolveDamageableId(damageable, other);
            if (hitTargetIds.Contains(targetId))
            {
                return;
            }

            hitTargetIds.Add(targetId);
            damageable.TakeDamage(damageAmount, owner);
            if (Time.unscaledTime >= nextDamageHitLogTime)
            {
                nextDamageHitLogTime = Time.unscaledTime + Mathf.Max(0.1f, damageHitLogInterval);
                Debug.Log(
                    "Projectile damage applied. projectile=" + name +
                    " ownerTeam=" + ownerTeam +
                    " targetTeam=" + damageable.Team +
                    " target=" + other.name +
                    " damage=" + damageAmount.ToString("0.##"),
                    this);
            }

            var hitPoint = ResolveImpactPoint(other);
            ImpactFlashEffectSpawner.TrySpawnAt(hitPoint, ownerTeam);
            AudioService.PlaySfxById(SoundIds.SfxHit);

            if (destroyOnHit)
            {
                Release();
            }
        }

        private void HandleWorldCollision(Collider2D other)
        {
            if (!destroyOnWorldCollision || other.isTrigger)
            {
                return;
            }

            if (Time.unscaledTime >= nextWorldHitLogTime)
            {
                nextWorldHitLogTime = Time.unscaledTime + Mathf.Max(0.1f, worldHitLogInterval);
                Debug.Log(
                    "Projectile released on world collision. projectile=" + name +
                    " team=" + ownerTeam +
                    " hit=" + other.name,
                    this);
            }

            Release();
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            if (owner == null || other == null)
            {
                return false;
            }

            var ownerTransform = owner.transform;
            return other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform);
        }

        private int ResolveDamageableId(IDamageable damageable, Collider2D hitCollider)
        {
            var damageableComponent = damageable as Component;
            if (damageableComponent != null)
            {
                return damageableComponent.transform.root.GetInstanceID();
            }

            return hitCollider.transform.root.GetInstanceID();
        }

        private Vector3 ResolveImpactPoint(Collider2D collider)
        {
            if (collider == null)
            {
                return transform.position;
            }

            var point = collider.ClosestPoint(transform.position);
            if ((point - (Vector2)transform.position).sqrMagnitude <= 0.0001f)
            {
                return collider.bounds.center;
            }

            return point;
        }

        private void Release()
        {
            if (isReleased)
            {
                return;
            }

            isReleased = true;
            cachedRigidbody.velocity = Vector2.zero;

            if (ReleaseRequested != null)
            {
                ReleaseRequested.Invoke(this);
                return;
            }

            Destroy(gameObject);
        }

        private void ApplyTeamVisual(CombatTeam sourceTeam)
        {
            if (cachedRenderer == null)
            {
                return;
            }

            switch (sourceTeam)
            {
                case CombatTeam.Player:
                    cachedRenderer.color = playerProjectileColor;
                    transform.localScale = baseLocalScale * playerProjectileScaleMultiplier;
                    break;
                case CombatTeam.Boss:
                    cachedRenderer.color = bossProjectileColor;
                    transform.localScale = baseLocalScale * bossProjectileScaleMultiplier;
                    break;
                default:
                    cachedRenderer.color = neutralProjectileColor;
                    transform.localScale = baseLocalScale * neutralProjectileScaleMultiplier;
                    break;
            }
        }
    }
}
