using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.Builtin.Sound;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Projectiles;
using GameMain.GameLogic.UI;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Tools
{
    /// <summary>
    /// Optional runtime helper that applies ScriptableObject tuning data to scene components.
    /// </summary>
    public sealed class RuntimeSceneHooks : MonoBehaviour
    {
        public static RuntimeSceneHooks Active { get; private set; }

        [Header("Automation")]
        [SerializeField] private bool autoBindOnAwake = true;
        [SerializeField] private bool autoApplyOnStart = true;

        [Header("Core Refs")]
        [SerializeField] private ProcedureManager procedureManager;
        [SerializeField] private ProcedureBattle procedureBattle;
        [SerializeField] private ProcedureResult procedureResult;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private AudioService audioService;
        [SerializeField] private DamageTextSpawner damageTextSpawner;
        [SerializeField] private ImpactFlashEffectSpawner impactFlashEffectSpawner;
        [SerializeField] private BossPresetController bossPresetController;
        [SerializeField] private BattleHudController battleHudController;
        [SerializeField] private ResultPanelController resultPanelController;

        [Header("Runtime Refs")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private WeaponController playerWeapon;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BossController bossController;
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private BossBrain bossBrain;
        [SerializeField] private WeaponController bossWeapon;
        [SerializeField] private Transform bossTransform;

        [Header("Data Assets")]
        [SerializeField] private PlayerStatsData playerStats;
        [SerializeField] private WeaponStatsData playerWeaponStats;
        [SerializeField] private BossStatsData bossStats;
        [SerializeField] private WeaponStatsData bossWeaponStats;
        [SerializeField] private BattleConfigData battleConfig;
        [SerializeField] private AudioClipBindings audioClipBindings;

        public ProcedureManager ProcedureManager => procedureManager;

        public ProcedureBattle ProcedureBattle => procedureBattle;

        public ProcedureResult ProcedureResult => procedureResult;

        public PlayerHealth PlayerHealth => playerHealth;

        public BossHealth BossHealth => bossHealth;

        public BattleConfigData BattleConfig => battleConfig;

        public BossPresetController BossPresetController => bossPresetController;

        private void Awake()
        {
            Active = this;
            if (autoBindOnAwake)
            {
                AutoBindSceneReferences();
            }
        }

        private void Start()
        {
            if (autoApplyOnStart)
            {
                Apply();
            }

            if (battleConfig != null && battleConfig.autoEnterBattleOnPlay)
            {
                Debug.Log("RuntimeSceneHooks detected autoEnterBattleOnPlay=true. ProcedureMenu will auto transition to Battle.");
            }
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        [ContextMenu("Auto Bind Scene Refs")]
        public void AutoBindSceneReferences()
        {
            if (procedureManager == null)
            {
                procedureManager = GetComponent<ProcedureManager>();
            }

            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (procedureBattle == null)
            {
                procedureBattle = GetComponent<ProcedureBattle>();
            }

            if (procedureResult == null)
            {
                procedureResult = GetComponent<ProcedureResult>();
            }

            if (projectilePool == null)
            {
                projectilePool = GetComponentInChildren<ProjectilePool>(true);
            }

            if (audioService == null)
            {
                audioService = GetComponentInChildren<AudioService>(true);
            }

            if (damageTextSpawner == null)
            {
                damageTextSpawner = GetComponentInChildren<DamageTextSpawner>(true);
            }

            if (impactFlashEffectSpawner == null)
            {
                impactFlashEffectSpawner = GetComponentInChildren<ImpactFlashEffectSpawner>(true);
            }

            if (bossPresetController == null)
            {
                bossPresetController = GetComponent<BossPresetController>();
                if (bossPresetController == null)
                {
                    bossPresetController = GetComponentInChildren<BossPresetController>(true);
                }
            }

            if (battleHudController == null)
            {
                battleHudController = GetComponentInChildren<BattleHudController>(true);
            }

            if (resultPanelController == null)
            {
                resultPanelController = GetComponentInChildren<ResultPanelController>(true);
            }

            if (playerHealth == null)
            {
                playerHealth = Object.FindObjectOfType<PlayerHealth>();
            }

            if (bossHealth == null)
            {
                bossHealth = Object.FindObjectOfType<BossHealth>();
            }

            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (bossController == null && bossHealth != null)
            {
                bossController = bossHealth.GetComponent<BossController>();
            }

            if (playerWeapon == null && playerHealth != null)
            {
                playerWeapon = playerHealth.GetComponent<WeaponController>();
            }

            if (bossWeapon == null && bossHealth != null)
            {
                bossWeapon = bossHealth.GetComponent<WeaponController>();
            }

            if (bossBrain == null && bossHealth != null)
            {
                bossBrain = bossHealth.GetComponent<BossBrain>();
            }

            if (playerTransform == null && playerHealth != null)
            {
                playerTransform = playerHealth.transform;
            }

            if (bossTransform == null && bossHealth != null)
            {
                bossTransform = bossHealth.transform;
            }
        }

        [ContextMenu("Apply Data To Scene")]
        public void Apply()
        {
            AutoBindSceneReferences();

            if (playerController != null && playerStats != null)
            {
                playerController.SetMoveSpeed(playerStats.moveSpeed);
                playerController.SetAimRotationOffset(playerStats.aimRotationOffset);
                playerController.ConfigureDodge(
                    playerStats.dodgeKey,
                    playerStats.dodgeDistance,
                    playerStats.dodgeDuration,
                    playerStats.dodgeCooldown,
                    playerStats.dodgeInvulnerable,
                    playerStats.dodgeDamageReduction);
            }

            if (playerHealth != null && playerStats != null)
            {
                playerHealth.SetMaxHealth(playerStats.maxHealth, false);
            }

            if (playerHealth != null)
            {
                playerHealth.SetTeam(CombatTeam.Player);
            }

            if (playerWeapon != null && playerWeaponStats != null)
            {
                playerWeapon.Configure(
                    playerWeaponStats.fireInterval,
                    playerWeaponStats.projectileSpeed,
                    playerWeaponStats.projectileDamage,
                    playerWeaponStats.projectileLifetime);
                playerWeapon.SetOwnerTeam(playerWeaponStats.ownerTeam == CombatTeam.Neutral ? CombatTeam.Player : playerWeaponStats.ownerTeam);
            }

            if (bossController != null && bossStats != null)
            {
                bossController.SetMoveSpeed(bossStats.moveSpeed);
            }

            if (bossHealth != null && bossStats != null)
            {
                bossHealth.SetMaxHealth(bossStats.maxHealth, false);
            }

            if (bossHealth != null)
            {
                bossHealth.SetTeam(CombatTeam.Boss);
            }

            if (bossBrain != null && bossStats != null)
            {
                bossBrain.Configure(
                    bossStats.idleDuration,
                    bossStats.burstDuration,
                    bossStats.burstFireInterval,
                    bossStats.cooldownDuration,
                    bossStats.engageDistanceMin,
                    bossStats.engageDistanceMax,
                    bossStats.retargetInterval);
                bossBrain.ConfigureFanSkill(
                    bossStats.enableFanShot,
                    bossStats.fanShotCount,
                    bossStats.fanSpreadAngle,
                    bossStats.fanSkillWindup,
                    bossStats.fanSkillRecovery,
                    bossStats.fanSkillInterval);
                bossBrain.ConfigureRadialSkill(
                    bossStats.enableRadialNova,
                    bossStats.radialNovaShotCount,
                    bossStats.radialNovaProjectileSpeedScale,
                    bossStats.radialNovaWindup,
                    bossStats.radialNovaRecovery,
                    bossStats.radialNovaInterval,
                    bossStats.radialNovaPulseCount,
                    bossStats.radialNovaPulseInterval);
                bossBrain.ConfigureLowHealthAggression(
                    bossStats.enableLowHealthAggression,
                    bossStats.lowHealthThresholdNormalized,
                    bossStats.lowHealthBurstFireIntervalScale,
                    bossStats.lowHealthCooldownScale,
                    bossStats.lowHealthChaseSpeedScale,
                    bossStats.lowHealthRadialNovaIntervalScale,
                    bossStats.lowHealthRadialNovaShotBonus);
            }

            if (bossBrain != null && playerHealth != null)
            {
                bossBrain.SetTargetPlayer(playerHealth);
            }

            if (bossPresetController != null)
            {
                bossPresetController.ResolveTargets();
                bossPresetController.ApplyCurrentPreset();
            }

            if (bossWeapon != null && bossWeaponStats != null)
            {
                bossWeapon.Configure(
                    bossWeaponStats.fireInterval,
                    bossWeaponStats.projectileSpeed,
                    bossWeaponStats.projectileDamage,
                    bossWeaponStats.projectileLifetime);
                bossWeapon.SetOwnerTeam(bossWeaponStats.ownerTeam == CombatTeam.Neutral ? CombatTeam.Boss : bossWeaponStats.ownerTeam);
            }

            if (projectilePool != null)
            {
                if (playerWeapon != null)
                {
                    playerWeapon.SetProjectilePool(projectilePool);
                }

                if (bossWeapon != null)
                {
                    bossWeapon.SetProjectilePool(projectilePool);
                }
            }

            if (playerController != null)
            {
                playerController.SetWeaponController(playerWeapon);
                playerController.SetAimCamera(Camera.main);
            }

            if (playerWeapon != null)
            {
                playerWeapon.enabled = true;
                playerWeapon.EnsureRuntimeReferences();
                playerWeapon.ResetFireCooldown();
            }

            if (bossWeapon != null)
            {
                bossWeapon.enabled = true;
                bossWeapon.EnsureRuntimeReferences();
                bossWeapon.ResetFireCooldown();
            }

            if (procedureBattle != null)
            {
                procedureBattle.SetBattleParticipants(playerHealth, bossHealth);
                procedureBattle.SetBattleConfig(battleConfig);
            }

            if (battleHudController != null && procedureManager != null)
            {
                battleHudController.Configure(procedureManager, playerHealth, bossHealth);
            }

            if (resultPanelController != null && procedureManager != null)
            {
                resultPanelController.Configure(procedureManager);
            }

            if (audioService != null)
            {
                if (audioClipBindings != null)
                {
                    audioService.SetClipBindings(audioClipBindings);
                }

                audioService.BindProcedureManager(procedureManager);
            }

            if (battleConfig != null)
            {
                if (playerTransform != null)
                {
                    playerTransform.position = battleConfig.playerSpawnPosition;
                }

                if (bossTransform != null)
                {
                    bossTransform.position = battleConfig.bossSpawnPosition;
                }
            }

            Debug.Log(
                "[RuntimeSceneHooks] ApplySummary " +
                "playerWeapon=" + (playerWeapon != null ? playerWeapon.BuildRuntimeDebugSummary() : "null") +
                " bossWeapon=" + (bossWeapon != null ? bossWeapon.BuildRuntimeDebugSummary() : "null") +
                " playerHealth=" + (playerHealth != null ? playerHealth.CurrentHealth.ToString("0") : "null") +
                " bossHealth=" + (bossHealth != null ? bossHealth.CurrentHealth.ToString("0") : "null"));
        }

        public void SetCoreReferences(
            ProcedureManager manager,
            ProcedureBattle battleProcedure,
            ProcedureResult resultProcedure,
            ProjectilePool pool,
            AudioService service,
            BattleHudController hud,
            ResultPanelController resultPanel,
            PlayerController playerCtrl,
            PlayerHealth playerHp,
            WeaponController playerWpn,
            BossController bossCtrl,
            BossHealth bossHp,
            BossBrain brain,
            WeaponController bossWpn)
        {
            procedureManager = manager;
            procedureBattle = battleProcedure;
            procedureResult = resultProcedure;
            projectilePool = pool;
            audioService = service;
            battleHudController = hud;
            resultPanelController = resultPanel;
            playerController = playerCtrl;
            playerHealth = playerHp;
            playerWeapon = playerWpn;
            bossController = bossCtrl;
            bossHealth = bossHp;
            bossBrain = brain;
            bossWeapon = bossWpn;

            if (playerHealth != null)
            {
                playerTransform = playerHealth.transform;
            }

            if (bossHealth != null)
            {
                bossTransform = bossHealth.transform;
            }
        }

        public void BindFormalPlayer(PlayerHealth formalPlayer)
        {
            if (formalPlayer == null)
            {
                return;
            }

            playerHealth = formalPlayer;
            playerController = formalPlayer.GetComponent<PlayerController>();
            playerWeapon = formalPlayer.GetComponent<WeaponController>();
            playerTransform = formalPlayer.transform;

            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (bossHealth == null)
            {
                bossHealth = Object.FindObjectOfType<BossHealth>();
            }

            if (bossBrain == null && bossHealth != null)
            {
                bossBrain = bossHealth.GetComponent<BossBrain>();
            }

            if (playerController != null)
            {
                playerController.SetWeaponController(playerWeapon);
                playerController.SetAimCamera(Camera.main);
            }

            if (bossBrain != null)
            {
                bossBrain.SetTargetPlayer(playerHealth);
            }

            if (procedureBattle != null)
            {
                procedureBattle.SetBattleParticipants(playerHealth, bossHealth);
            }

            if (battleHudController != null && procedureManager != null)
            {
                battleHudController.Configure(procedureManager, playerHealth, bossHealth);
            }

            Debug.Log(
                "[RuntimeSceneHooks] Formal player rebound. name=" +
                playerHealth.gameObject.name +
                " hasController=" + (playerController != null) +
                " hasWeapon=" + (playerWeapon != null),
                playerHealth);
        }

        public void SetDataAssets(
            PlayerStatsData newPlayerStats,
            WeaponStatsData newPlayerWeaponStats,
            BossStatsData newBossStats,
            WeaponStatsData newBossWeaponStats,
            BattleConfigData newBattleConfig)
        {
            playerStats = newPlayerStats;
            playerWeaponStats = newPlayerWeaponStats;
            bossStats = newBossStats;
            bossWeaponStats = newBossWeaponStats;
            battleConfig = newBattleConfig;
        }

        public void SetAudioClipBindings(AudioClipBindings bindings)
        {
            audioClipBindings = bindings;
        }

        public void ResetTransientEffects()
        {
            projectilePool?.ReleaseAllActiveProjectiles();
            damageTextSpawner?.ReleaseAllActive();
            impactFlashEffectSpawner?.ReleaseAllActive();
            CameraShakeFeedback.Instance?.ResetCameraPose();
        }
    }
}
