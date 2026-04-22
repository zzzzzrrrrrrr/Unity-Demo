using System;
using GameMain.GameLogic.Combat;
using UnityEngine;

namespace GameMain.GameLogic.Boss
{
    /// <summary>
    /// 2D boss health receiver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 300f;
        [SerializeField] private CombatTeam team = CombatTeam.Boss;
        [Header("Feedback")]
        [SerializeField] private HitFlashFeedback hitFlashFeedback;
        [SerializeField] private DeathFadeFeedback deathFadeFeedback;
        [SerializeField] private bool showDamageText = true;
        [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 0.9f, 0f);

        private float currentHealth;
        private bool isDefeatHandled;
        private bool loggedMissingDamageTextSpawner;

        public CombatTeam Team => team;

        public bool IsDead => currentHealth <= 0f;

        public Vector2 Position => transform.position;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public event Action Defeated;
        public event Action<float, float> HealthChanged;
        public event Action<float, float, GameObject> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
            if (hitFlashFeedback == null)
            {
                hitFlashFeedback = GetComponent<HitFlashFeedback>();
            }

            if (deathFadeFeedback == null)
            {
                deathFadeFeedback = GetComponent<DeathFadeFeedback>();
            }
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            var previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            var appliedDamage = Mathf.Max(0f, previousHealth - currentHealth);

            if (appliedDamage > 0f)
            {
                TriggerHitFeedback(appliedDamage);
            }

            HealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke(currentHealth, maxHealth, source);

            if (currentHealth <= 0f && !isDefeatHandled)
            {
                HandleDefeated();
            }
        }

        public void ReceiveDamage(float amount, GameObject source)
        {
            TakeDamage(amount, source);
        }

        public void SetMaxHealth(float value, bool resetCurrent)
        {
            maxHealth = Mathf.Max(1f, value);
            if (resetCurrent)
            {
                currentHealth = maxHealth;
                HealthChanged?.Invoke(currentHealth, maxHealth);
                if (currentHealth > 0f)
                {
                    isDefeatHandled = false;
                }
            }
            else
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDefeatHandled = false;
            deathFadeFeedback?.ResetVisuals();
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetTeam(CombatTeam value)
        {
            team = value;
        }

        private void TriggerHitFeedback(float appliedDamage)
        {
            hitFlashFeedback?.PlayHit();
            var normalizedShake = maxHealth > 0f ? Mathf.Clamp01(appliedDamage / Mathf.Max(1f, maxHealth * 0.16f)) : 0f;
            CameraShakeFeedback.PlayDamage(normalizedShake);

            if (!showDamageText)
            {
                return;
            }

            if (!DamageTextSpawner.TrySpawnAt(transform.position + damageTextOffset, appliedDamage, currentHealth <= 0f))
            {
                if (!loggedMissingDamageTextSpawner)
                {
                    Debug.LogWarning("BossHealth cannot spawn damage text because no active DamageTextSpawner is present.", this);
                    loggedMissingDamageTextSpawner = true;
                }
            }
        }

        private void HandleDefeated()
        {
            isDefeatHandled = true;
            deathFadeFeedback?.PlayDeath();
            CameraShakeFeedback.PlayDeath();
            Defeated?.Invoke();
            OnDied?.Invoke();
        }
    }
}
