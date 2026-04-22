using System;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// Simple 2D health component for player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private CombatTeam team = CombatTeam.Player;
        [SerializeField] private bool disableControlOnDeath = true;
        [SerializeField] private bool disableWeaponOnDeath = true;
        [Header("Feedback")]
        [SerializeField] private HitFlashFeedback hitFlashFeedback;
        [SerializeField] private DeathFadeFeedback deathFadeFeedback;
        [SerializeField] private bool showDamageText = true;
        [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 0.7f, 0f);

        private float currentHealth;
        private bool isDeathHandled;
        private PlayerController playerController;
        private WeaponController weaponController;
        private bool loggedMissingDamageTextSpawner;
        private float nextDodgeBlockLogTime;

        public CombatTeam Team => team;

        public bool IsDead => currentHealth <= 0f;

        public Vector2 Position => transform.position;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public event Action Died;
        public event Action<float, float> HealthChanged;
        public event Action<float, float, GameObject> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
            playerController = GetComponent<PlayerController>();
            weaponController = GetComponent<WeaponController>();
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

            if (playerController != null)
            {
                var incomingMultiplier = playerController.GetIncomingDamageMultiplier();
                if (incomingMultiplier <= 0f)
                {
                    if (Time.unscaledTime >= nextDodgeBlockLogTime)
                    {
                        nextDodgeBlockLogTime = Time.unscaledTime + 0.7f;
                        Debug.Log("PlayerHealth ignored damage because dodge invulnerability is active.", this);
                    }

                    return;
                }

                amount *= incomingMultiplier;
                if (amount <= 0f)
                {
                    return;
                }
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

            if (currentHealth <= 0f && !isDeathHandled)
            {
                HandleDeath();
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
                    isDeathHandled = false;
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
            isDeathHandled = false;
            deathFadeFeedback?.ResetVisuals();
            if (playerController != null)
            {
                playerController.enabled = true;
            }

            if (weaponController != null)
            {
                weaponController.enabled = true;
            }

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
                    Debug.LogWarning("PlayerHealth cannot spawn damage text because no active DamageTextSpawner is present.", this);
                    loggedMissingDamageTextSpawner = true;
                }
            }
        }

        private void HandleDeath()
        {
            isDeathHandled = true;

            if (disableControlOnDeath && playerController != null)
            {
                playerController.enabled = false;
            }

            if (disableWeaponOnDeath && weaponController != null)
            {
                weaponController.enabled = false;
            }

            deathFadeFeedback?.PlayDeath();
            CameraShakeFeedback.PlayDeath();
            Died?.Invoke();
            OnDied?.Invoke();
        }
    }
}
