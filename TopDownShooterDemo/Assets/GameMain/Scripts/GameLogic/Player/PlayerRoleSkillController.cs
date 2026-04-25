using GameMain.GameLogic.Data;
using UnityEngine;

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// Handles role active-skill input and delegates gameplay changes to the owning runtime components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRoleSkillController : MonoBehaviour
    {
        private const string SkillNone = "None";
        private const string SkillTacticalDodge = "TacticalDodge";
        private const string SkillBulwark = "Bulwark";
        private const string SkillOverclock = "Overclock";
        private const float BulwarkMinimumDamageMultiplier = 0.1f;

        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private string activeSkillId = SkillNone;
        [SerializeField] private KeyCode activeSkillKey = KeyCode.F;
        [SerializeField] [Min(0f)] private float activeSkillCooldown;
        [SerializeField] [Min(0f)] private float activeSkillEnergyCost;
        [SerializeField] [Min(0f)] private float activeSkillDuration;
        [SerializeField] [Min(0f)] private float activeSkillPower;

        private float cooldownRemaining;
        private float activeRemaining;
        private string runningSkillId = string.Empty;

        public string ActiveSkillId => activeSkillId;

        public KeyCode ActiveSkillKey => activeSkillKey;

        public bool IsCoolingDown => cooldownRemaining > 0f;

        public bool IsActive => activeRemaining > 0f;

        public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);

        public float CooldownDuration => Mathf.Max(0.01f, activeSkillCooldown);

        public float Cooldown01 => activeSkillCooldown > 0f
            ? 1f - Mathf.Clamp01(cooldownRemaining / Mathf.Max(0.01f, activeSkillCooldown))
            : 1f;

        private void Awake()
        {
            ResolveOwners();
        }

        private void OnEnable()
        {
            ResolveOwners();
        }

        private void OnDisable()
        {
            EndRunningSkill();
        }

        private void Update()
        {
            TickTimers(Time.deltaTime);

            if (!HasConfiguredSkill() || !Input.GetKeyDown(activeSkillKey))
            {
                return;
            }

            TryActivate();
        }

        public void Bind(PlayerHealth health, PlayerController controller)
        {
            playerHealth = health;
            playerController = controller;
            ResolveOwners();
        }

        public void Configure(CharacterData data)
        {
            if (data == null)
            {
                Configure(SkillNone, KeyCode.F, 0f, 0f, 0f, 0f);
                return;
            }

            Configure(
                data.activeSkillId,
                data.activeSkillKey,
                data.activeSkillCooldown,
                data.activeSkillEnergyCost,
                data.activeSkillDuration,
                data.activeSkillPower);
        }

        public void Configure(
            string skillId,
            KeyCode key,
            float cooldown,
            float energyCost,
            float duration,
            float power)
        {
            EndRunningSkill();
            activeSkillId = string.IsNullOrWhiteSpace(skillId) ? SkillNone : skillId.Trim();
            activeSkillKey = key;
            activeSkillCooldown = Mathf.Max(0f, cooldown);
            activeSkillEnergyCost = Mathf.Max(0f, energyCost);
            activeSkillDuration = Mathf.Max(0f, duration);
            activeSkillPower = Mathf.Max(0f, power);
            cooldownRemaining = 0f;
        }

        private void TryActivate()
        {
            ResolveOwners();
            if (cooldownRemaining > 0f || playerHealth == null || playerController == null)
            {
                return;
            }

            var activated = false;
            switch (activeSkillId)
            {
                case SkillTacticalDodge:
                    activated = ActivateTacticalDodge();
                    break;
                case SkillBulwark:
                    activated = ActivateBulwark();
                    break;
                case SkillOverclock:
                    activated = ActivateOverclock();
                    break;
            }

            if (activated)
            {
                cooldownRemaining = Mathf.Max(0f, activeSkillCooldown);
            }
        }

        private bool ActivateTacticalDodge()
        {
            if (!playerController.CanStartRoleSkillDodge)
            {
                return false;
            }

            if (!TryConsumeSkillEnergy())
            {
                return false;
            }

            var distanceMultiplier = Mathf.Max(1f, activeSkillPower);
            var durationMultiplier = Mathf.Max(0.1f, activeSkillDuration > 0f ? activeSkillDuration : 1f);
            return playerController.TryStartRoleSkillDodge(distanceMultiplier, durationMultiplier);
        }

        private bool ActivateBulwark()
        {
            if (!TryConsumeSkillEnergy())
            {
                return false;
            }

            var reductionRatio = Mathf.Clamp01(activeSkillPower);
            var damageMultiplier = Mathf.Max(BulwarkMinimumDamageMultiplier, 1f - reductionRatio);
            playerHealth.ApplyTemporaryDamageMultiplier(damageMultiplier, activeSkillDuration);

            var armorRestore = Mathf.Max(1f, playerHealth.MaxArmor * reductionRatio);
            playerHealth.RestoreArmor(armorRestore);
            StartRunningSkill(SkillBulwark, activeSkillDuration);
            return true;
        }

        private bool ActivateOverclock()
        {
            if (!TryConsumeSkillEnergy())
            {
                return false;
            }

            var fireIntervalMultiplier = Mathf.Clamp(activeSkillPower, 0.1f, 1f);
            playerController.ApplyTemporaryFireIntervalMultiplier(fireIntervalMultiplier);
            StartRunningSkill(SkillOverclock, activeSkillDuration);
            return true;
        }

        private void StartRunningSkill(string skillId, float duration)
        {
            runningSkillId = skillId;
            activeRemaining = Mathf.Max(0f, duration);
            if (activeRemaining <= 0f)
            {
                EndRunningSkill();
            }
        }

        private void EndRunningSkill()
        {
            if (string.Equals(runningSkillId, SkillOverclock, System.StringComparison.Ordinal) && playerController != null)
            {
                playerController.ClearTemporaryFireIntervalMultiplier();
            }

            runningSkillId = string.Empty;
            activeRemaining = 0f;
        }

        private void TickTimers(float deltaTime)
        {
            var safeDelta = Mathf.Max(0f, deltaTime);
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - safeDelta);
            }

            if (activeRemaining <= 0f)
            {
                return;
            }

            activeRemaining = Mathf.Max(0f, activeRemaining - safeDelta);
            if (activeRemaining <= 0f)
            {
                EndRunningSkill();
            }
        }

        private bool HasConfiguredSkill()
        {
            return !string.IsNullOrWhiteSpace(activeSkillId) &&
                   !string.Equals(activeSkillId, SkillNone, System.StringComparison.Ordinal);
        }

        private bool TryConsumeSkillEnergy()
        {
            return activeSkillEnergyCost <= 0f ||
                   (playerHealth != null && playerHealth.TryConsumeEnergy(activeSkillEnergyCost));
        }

        private void ResolveOwners()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }
    }
}
