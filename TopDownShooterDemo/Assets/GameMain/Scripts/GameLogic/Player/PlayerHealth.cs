using System;
using GameMain.Builtin.Sound;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// Simple 2D health component for player.
    /// Ownership contract: runtime source of truth for HP, armor, and energy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] [Min(0f)] private float maxArmor = 0f;
        [SerializeField] [Min(0f)] private float maxEnergy = 100f;
        [SerializeField] private bool enableEnergyRegen = true;
        [SerializeField] [Min(0f)] private float energyRegenPerSecond = 8f;
        [SerializeField] [Min(0f)] private float energyRegenDelayAfterConsume = 1f;
        [SerializeField] private CombatTeam team = CombatTeam.Player;
        [SerializeField] private bool disableControlOnDeath = true;
        [SerializeField] private bool disableWeaponOnDeath = true;
        [Header("Feedback")]
        [SerializeField] private HitFlashFeedback hitFlashFeedback;
        [SerializeField] private DeathFadeFeedback deathFadeFeedback;
        [SerializeField] private bool showDamageText = true;
        [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 0.7f, 0f);

        private float currentHealth;
        private float currentArmor;
        private float currentEnergy;
        private float nextEnergyRegenTime;
        private bool isDeathHandled;
        private PlayerController playerController;
        private WeaponController weaponController;
        private bool loggedMissingDamageTextSpawner;
        private float nextDodgeBlockLogTime;
        private float temporaryDamageMultiplier = 1f;
        private float temporaryDamageMultiplierRemaining;

        public CombatTeam Team => team;

        public bool IsDead => currentHealth <= 0f;

        public Vector2 Position => transform.position;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public float CurrentArmor => currentArmor;

        public float MaxArmor => maxArmor;

        public float CurrentEnergy => currentEnergy;

        public float MaxEnergy => maxEnergy;

        public event Action Died;
        public event Action<float, float> HealthChanged;
        public event Action<float, float> ArmorChanged;
        public event Action<float, float> EnergyChanged;
        public event Action<float, float, GameObject> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
            currentArmor = Mathf.Clamp(maxArmor, 0f, maxArmor);
            currentEnergy = Mathf.Clamp(maxEnergy, 0f, maxEnergy);
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

        private void Update()
        {
            TickEnergyRegen(Time.deltaTime);
            TickTemporaryDamageMultiplier(Time.deltaTime);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            maxArmor = Mathf.Max(0f, maxArmor);
            maxEnergy = Mathf.Max(0f, maxEnergy);
            energyRegenPerSecond = Mathf.Max(0f, energyRegenPerSecond);
            energyRegenDelayAfterConsume = Mathf.Max(0f, energyRegenDelayAfterConsume);

            if (!Application.isPlaying)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            currentArmor = Mathf.Clamp(currentArmor, 0f, maxArmor);
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
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

            if (temporaryDamageMultiplierRemaining > 0f)
            {
                amount *= Mathf.Clamp(temporaryDamageMultiplier, 0f, 1f);
                if (amount <= 0f)
                {
                    return;
                }
            }

            var previousHealth = currentHealth;
            var previousArmor = currentArmor;
            var remainingDamage = amount;

            if (currentArmor > 0f)
            {
                var absorbedByArmor = Mathf.Min(currentArmor, remainingDamage);
                currentArmor -= absorbedByArmor;
                remainingDamage -= absorbedByArmor;
            }

            if (remainingDamage > 0f)
            {
                currentHealth = Mathf.Max(0f, currentHealth - remainingDamage);
            }

            var appliedHealthDamage = Mathf.Max(0f, previousHealth - currentHealth);
            var appliedArmorDamage = Mathf.Max(0f, previousArmor - currentArmor);
            var appliedDamage = appliedHealthDamage + appliedArmorDamage;

            if (appliedDamage > 0f)
            {
                AudioService.PlaySfxById(SoundIds.SfxPlayerHit);
                TriggerHitFeedback(appliedDamage);
            }

            if (previousArmor > 0f && currentArmor <= 0f)
            {
                AudioService.PlaySfxById(SoundIds.SfxArmorBreak);
            }

            if (!Mathf.Approximately(previousArmor, currentArmor))
            {
                ArmorChanged?.Invoke(currentArmor, maxArmor);
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

        public void SetMaxArmor(float value, bool resetCurrent)
        {
            maxArmor = Mathf.Max(0f, value);
            if (resetCurrent)
            {
                currentArmor = maxArmor;
                ArmorChanged?.Invoke(currentArmor, maxArmor);
            }
            else
            {
                currentArmor = Mathf.Clamp(currentArmor, 0f, maxArmor);
            }
        }

        public void SetMaxEnergy(float value, bool resetCurrent)
        {
            maxEnergy = Mathf.Max(0f, value);
            if (resetCurrent)
            {
                currentEnergy = maxEnergy;
                EnergyChanged?.Invoke(currentEnergy, maxEnergy);
            }
            else
            {
                currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
            }
        }

        public bool TryConsumeEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (IsDead || currentEnergy < amount)
            {
                return false;
            }

            currentEnergy = Mathf.Max(0f, currentEnergy - amount);
            nextEnergyRegenTime = Time.time + Mathf.Max(0f, energyRegenDelayAfterConsume);
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
            return true;
        }

        public void RestoreArmor(float amount)
        {
            if (IsDead || amount <= 0f || maxArmor <= 0f)
            {
                return;
            }

            var previousArmor = currentArmor;
            currentArmor = Mathf.Clamp(currentArmor + amount, 0f, maxArmor);
            if (!Mathf.Approximately(previousArmor, currentArmor))
            {
                ArmorChanged?.Invoke(currentArmor, maxArmor);
            }
        }

        public bool RestoreHealth(float amount)
        {
            if (IsDead || amount <= 0f || currentHealth >= maxHealth)
            {
                return false;
            }

            var previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
            if (Mathf.Approximately(previousHealth, currentHealth))
            {
                return false;
            }

            HealthChanged?.Invoke(currentHealth, maxHealth);
            return true;
        }

        public void ApplyTemporaryDamageMultiplier(float multiplier, float duration)
        {
            if (IsDead || duration <= 0f)
            {
                return;
            }

            temporaryDamageMultiplier = Mathf.Clamp(multiplier, 0f, 1f);
            temporaryDamageMultiplierRemaining = Mathf.Max(temporaryDamageMultiplierRemaining, duration);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            currentArmor = maxArmor;
            currentEnergy = maxEnergy;
            nextEnergyRegenTime = 0f;
            temporaryDamageMultiplier = 1f;
            temporaryDamageMultiplierRemaining = 0f;
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
            ArmorChanged?.Invoke(currentArmor, maxArmor);
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }

        public bool Revive(float healthRatio, float armorAmount, float energyRatio, float protectionDuration)
        {
            if (!IsDead)
            {
                return false;
            }

            currentHealth = Mathf.Clamp(maxHealth * Mathf.Clamp01(healthRatio), 1f, maxHealth);
            currentArmor = Mathf.Clamp(Mathf.Max(0f, armorAmount), 0f, maxArmor);
            currentEnergy = Mathf.Clamp(maxEnergy * Mathf.Clamp01(energyRatio), 0f, maxEnergy);
            nextEnergyRegenTime = 0f;
            temporaryDamageMultiplier = 1f;
            temporaryDamageMultiplierRemaining = 0f;
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
            ArmorChanged?.Invoke(currentArmor, maxArmor);
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);

            if (protectionDuration > 0f)
            {
                ApplyTemporaryDamageMultiplier(0f, protectionDuration);
            }

            return true;
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

        private void TickEnergyRegen(float deltaTime)
        {
            if (IsDead || !enableEnergyRegen || maxEnergy <= 0f)
            {
                return;
            }

            if (currentEnergy >= maxEnergy)
            {
                return;
            }

            if (Time.time < nextEnergyRegenTime)
            {
                return;
            }

            var previousEnergy = currentEnergy;
            currentEnergy = Mathf.Min(
                maxEnergy,
                currentEnergy + Mathf.Max(0f, energyRegenPerSecond) * Mathf.Max(0f, deltaTime));

            if (!Mathf.Approximately(previousEnergy, currentEnergy))
            {
                EnergyChanged?.Invoke(currentEnergy, maxEnergy);
            }
        }

        private void TickTemporaryDamageMultiplier(float deltaTime)
        {
            if (temporaryDamageMultiplierRemaining <= 0f)
            {
                return;
            }

            temporaryDamageMultiplierRemaining = Mathf.Max(0f, temporaryDamageMultiplierRemaining - Mathf.Max(0f, deltaTime));
            if (temporaryDamageMultiplierRemaining <= 0f)
            {
                temporaryDamageMultiplier = 1f;
            }
        }
    }
}
