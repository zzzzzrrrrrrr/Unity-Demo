using System;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Boss
{
    /// <summary>
    /// Runtime selector for boss behavior presets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPresetController : MonoBehaviour
    {
        public enum BossPresetType
        {
            Normal = 0,
            Frenzy = 1,
        }

        [SerializeField] private BossPresetType defaultPreset = BossPresetType.Normal;
        [SerializeField] private BossStatsData normalPreset;
        [SerializeField] private BossStatsData frenzyPreset;
        [SerializeField] private bool autoApplyOnStart = true;
        [SerializeField] private bool autoResolveTargets = true;

        [Header("Runtime Targets")]
        [SerializeField] private BossController bossController;
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private BossBrain bossBrain;
        [SerializeField] private WeaponController bossWeapon;

        private bool hasInitializedPreset;
        private BossPresetType currentPreset;
        private bool loggedMissingPresetData;

        public static BossPresetController Instance { get; private set; }

        public BossPresetType CurrentPreset => currentPreset;

        public string CurrentPresetName => currentPreset.ToString();

        public event Action<BossPresetType, string> PresetChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (autoResolveTargets)
            {
                ResolveTargets();
            }

            InitializeDefaultPreset();
        }

        private void Start()
        {
            if (autoApplyOnStart)
            {
                ApplyCurrentPreset();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResolveTargets()
        {
            if (bossHealth == null && RuntimeSceneHooks.Active != null)
            {
                bossHealth = RuntimeSceneHooks.Active.BossHealth;
            }

            if (bossHealth == null)
            {
                bossHealth = FindObjectOfType<BossHealth>();
            }

            if (bossController == null && bossHealth != null)
            {
                bossController = bossHealth.GetComponent<BossController>();
            }

            if (bossBrain == null && bossHealth != null)
            {
                bossBrain = bossHealth.GetComponent<BossBrain>();
            }

            if (bossWeapon == null && bossHealth != null)
            {
                bossWeapon = bossHealth.GetComponent<WeaponController>();
            }
        }

        public void SetTargets(
            BossController controller,
            BossHealth health,
            BossBrain brain,
            WeaponController weapon)
        {
            bossController = controller;
            bossHealth = health;
            bossBrain = brain;
            bossWeapon = weapon;
        }

        public void SetPresetData(
            BossStatsData normal,
            BossStatsData frenzy,
            BossPresetType defaultPresetType = BossPresetType.Normal)
        {
            normalPreset = normal;
            frenzyPreset = frenzy;
            defaultPreset = defaultPresetType;
            hasInitializedPreset = false;
            InitializeDefaultPreset();
            NotifyPresetChanged();
        }

        public void SelectNormalPreset()
        {
            SetPreset(BossPresetType.Normal, true);
        }

        public void SelectFrenzyPreset()
        {
            SetPreset(BossPresetType.Frenzy, true);
        }

        public void SetPreset(BossPresetType presetType, bool applyImmediately)
        {
            InitializeDefaultPreset();
            currentPreset = presetType;

            if (applyImmediately)
            {
                ApplyCurrentPreset();
            }
            else
            {
                NotifyPresetChanged();
            }
        }

        public void ApplyCurrentPreset()
        {
            InitializeDefaultPreset();
            if (autoResolveTargets)
            {
                ResolveTargets();
            }

            var preset = GetPresetData(currentPreset);
            if (preset == null)
            {
                if (!loggedMissingPresetData)
                {
                    loggedMissingPresetData = true;
                    Debug.LogWarning("BossPresetController is missing preset ScriptableObject data.", this);
                }

                return;
            }

            if (bossController != null)
            {
                bossController.SetMoveSpeed(preset.moveSpeed);
            }

            if (bossHealth != null)
            {
                bossHealth.SetMaxHealth(preset.maxHealth, false);
            }

            if (bossBrain != null)
            {
                bossBrain.Configure(
                    preset.idleDuration,
                    preset.burstDuration,
                    preset.burstFireInterval,
                    preset.cooldownDuration,
                    preset.engageDistanceMin,
                    preset.engageDistanceMax,
                    preset.retargetInterval);
                bossBrain.ConfigureFanSkill(
                    preset.enableFanShot,
                    preset.fanShotCount,
                    preset.fanSpreadAngle,
                    preset.fanSkillWindup,
                    preset.fanSkillRecovery,
                    preset.fanSkillInterval);
                bossBrain.ConfigureRadialSkill(
                    preset.enableRadialNova,
                    preset.radialNovaShotCount,
                    preset.radialNovaProjectileSpeedScale,
                    preset.radialNovaWindup,
                    preset.radialNovaRecovery,
                    preset.radialNovaInterval,
                    preset.radialNovaPulseCount,
                    preset.radialNovaPulseInterval);
                bossBrain.ConfigureLowHealthAggression(
                    preset.enableLowHealthAggression,
                    preset.lowHealthThresholdNormalized,
                    preset.lowHealthBurstFireIntervalScale,
                    preset.lowHealthCooldownScale,
                    preset.lowHealthChaseSpeedScale,
                    preset.lowHealthRadialNovaIntervalScale,
                    preset.lowHealthRadialNovaShotBonus);
            }

            if (bossWeapon != null)
            {
                bossWeapon.ResetFireCooldown();
            }

            NotifyPresetChanged();
        }

        private BossStatsData GetPresetData(BossPresetType presetType)
        {
            switch (presetType)
            {
                case BossPresetType.Frenzy:
                    return frenzyPreset != null ? frenzyPreset : normalPreset;
                default:
                    return normalPreset != null ? normalPreset : frenzyPreset;
            }
        }

        private void InitializeDefaultPreset()
        {
            if (hasInitializedPreset)
            {
                return;
            }

            hasInitializedPreset = true;
            currentPreset = defaultPreset;
        }

        private void NotifyPresetChanged()
        {
            PresetChanged?.Invoke(currentPreset, CurrentPresetName);
        }
    }
}
