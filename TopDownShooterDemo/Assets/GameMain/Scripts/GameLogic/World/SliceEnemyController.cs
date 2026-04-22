using System;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Utils;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Minimal wave enemy used by the single-scene linear slice encounter room.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class SliceEnemyController : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 54f;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private float contactDamageInterval = 0.55f;
        [SerializeField] private CombatTeam team = CombatTeam.Boss;
        [SerializeField] private bool destroyOnDeath = true;
        [Header("Feedback")]
        [SerializeField] private HitFlashFeedback hitFlashFeedback;
        [SerializeField] private DeathFadeFeedback deathFadeFeedback;
        [SerializeField] private bool showDamageText = true;
        [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 0.62f, 0f);

        private Rigidbody2D cachedRigidbody;
        private Collider2D cachedCollider;
        private PlayerHealth targetPlayer;
        private float currentHealth;
        private float nextContactDamageTime;
        private bool deathHandled;
        private bool loggedMissingDamageTextSpawner;

        public CombatTeam Team => team;

        public bool IsDead => currentHealth <= 0f;

        public Vector2 Position => transform.position;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public event Action<SliceEnemyController> Defeated;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedCollider = GetComponent<Collider2D>();

            cachedRigidbody.bodyType = RigidbodyType2D.Dynamic;
            cachedRigidbody.simulated = true;
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;
            cachedRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            cachedCollider.isTrigger = false;
            cachedCollider.enabled = true;

            if (hitFlashFeedback == null)
            {
                hitFlashFeedback = GetComponent<HitFlashFeedback>();
            }

            if (deathFadeFeedback == null)
            {
                deathFadeFeedback = GetComponent<DeathFadeFeedback>();
            }

            currentHealth = Mathf.Max(1f, maxHealth);
        }

        private void OnEnable()
        {
            if (currentHealth <= 0f || deathHandled)
            {
                currentHealth = Mathf.Max(1f, maxHealth);
            }

            deathHandled = false;
            nextContactDamageTime = 0f;
            if (cachedCollider != null)
            {
                cachedCollider.enabled = true;
            }

            if (cachedRigidbody != null)
            {
                cachedRigidbody.simulated = true;
                cachedRigidbody.velocity = Vector2.zero;
            }

            deathFadeFeedback?.ResetVisuals();
        }

        private void OnDisable()
        {
            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
            }
        }

        private void Update()
        {
            if (deathHandled || IsDead)
            {
                if (cachedRigidbody != null)
                {
                    cachedRigidbody.velocity = Vector2.zero;
                }

                return;
            }

            if (!IsBattleRunning())
            {
                cachedRigidbody.velocity = Vector2.zero;
                return;
            }

            ResolveTargetPlayer();
            if (targetPlayer == null || targetPlayer.IsDead)
            {
                cachedRigidbody.velocity = Vector2.zero;
                return;
            }

            var direction = Physics2DUtility.SafeDirection(transform.position, targetPlayer.transform.position, Vector2.zero);
            cachedRigidbody.velocity = direction * Mathf.Max(0.1f, moveSpeed);
        }

        private void OnCollisionStay2D(Collision2D other)
        {
            if (deathHandled || IsDead || other == null || Time.time < nextContactDamageTime)
            {
                return;
            }

            var player = other.collider != null ? other.collider.GetComponentInParent<PlayerHealth>() : null;
            if (player == null || player.IsDead)
            {
                return;
            }

            nextContactDamageTime = Time.time + Mathf.Max(0.1f, contactDamageInterval);
            player.TakeDamage(Mathf.Max(0f, contactDamage), gameObject);
        }

        public void Initialize(PlayerHealth target, float health, float speed, float touchDamage)
        {
            targetPlayer = target;
            maxHealth = Mathf.Max(1f, health);
            currentHealth = maxHealth;
            moveSpeed = Mathf.Max(0.1f, speed);
            contactDamage = Mathf.Max(0f, touchDamage);
            deathHandled = false;
            nextContactDamageTime = 0f;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (deathHandled || IsDead || amount <= 0f)
            {
                return;
            }

            var previous = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            var appliedDamage = Mathf.Max(0f, previous - currentHealth);
            if (appliedDamage > 0f)
            {
                hitFlashFeedback?.PlayHit();
                var normalizedShake = maxHealth > 0f ? Mathf.Clamp01(appliedDamage / Mathf.Max(1f, maxHealth * 0.2f)) : 0f;
                CameraShakeFeedback.PlayDamage(normalizedShake * 0.6f);
                if (showDamageText && !DamageTextSpawner.TrySpawnAt(transform.position + damageTextOffset, appliedDamage, currentHealth <= 0f))
                {
                    if (!loggedMissingDamageTextSpawner)
                    {
                        loggedMissingDamageTextSpawner = true;
                        Debug.LogWarning("SliceEnemyController cannot spawn damage text because no active DamageTextSpawner exists.", this);
                    }
                }
            }

            if (currentHealth <= 0f && !deathHandled)
            {
                HandleDeath();
            }
        }

        public void ReceiveDamage(float amount, GameObject source)
        {
            TakeDamage(amount, source);
        }

        public void SetTeam(CombatTeam value)
        {
            team = value;
        }

        private void HandleDeath()
        {
            deathHandled = true;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
                cachedRigidbody.simulated = false;
            }

            if (cachedCollider != null)
            {
                cachedCollider.enabled = false;
            }

            deathFadeFeedback?.PlayDeath();
            Defeated?.Invoke(this);

            if (destroyOnDeath)
            {
                Destroy(gameObject, 0.02f);
            }
        }

        private void ResolveTargetPlayer()
        {
            if (targetPlayer != null && !targetPlayer.IsDead)
            {
                return;
            }

            var hooksPlayer = RuntimeSceneHooks.Active != null ? RuntimeSceneHooks.Active.PlayerHealth : null;
            if (hooksPlayer != null && !hooksPlayer.IsDead)
            {
                targetPlayer = hooksPlayer;
                return;
            }

            targetPlayer = FindObjectOfType<PlayerHealth>();
        }

        private static bool IsBattleRunning()
        {
            if (!GameEntryBridge.IsReady)
            {
                return true;
            }

            return GameEntryBridge.Procedure.CurrentProcedureType == ProcedureType.Battle;
        }
    }
}
