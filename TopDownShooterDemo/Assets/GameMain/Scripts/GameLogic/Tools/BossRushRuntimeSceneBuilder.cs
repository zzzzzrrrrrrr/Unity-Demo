using System;
using System.Collections.Generic;
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
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain.GameLogic.Tools
{
    /// <summary>
    /// Runtime-only scene bootstrap so an empty scene can still become a playable demo after pressing Play.
    /// Ownership contract: runtime source of truth for environment hierarchy and runtime HUD layout.
    /// Builder-owned scene objects should be adjusted in this builder code, not by manual runtime transform edits.
    /// </summary>
        public static class BossRushRuntimeSceneBuilder
        {
        private const string RootName = "GameMainRoot";
        private const string RuntimeProjectileTemplateName = "RuntimeProjectileTemplate";
        private static readonly Vector2 BossArenaSize = new Vector2(42f, 24f);
        private static readonly Vector2 BossArenaInnerMatSize = new Vector2(36f, 18f);
        private static readonly Vector2 StartAreaSize = new Vector2(20f, 14f);
        private static readonly Vector2 CorridorOneSize = new Vector2(16f, 8f);
        private static readonly Vector2 EncounterRoomSize = new Vector2(28f, 18f);
        private static readonly Vector2 CorridorTwoSize = new Vector2(20f, 8f);
        private static readonly Vector2 StartAreaCenter = new Vector2(-64f, 0f);
        private static readonly Vector2 CorridorOneCenter = new Vector2(-46f, 0f);
        private static readonly Vector2 EncounterRoomCenter = new Vector2(-24f, 0f);
        private static readonly Vector2 CorridorTwoCenter = new Vector2(0f, 0f);
        private static readonly Vector2 BossArenaCenter = new Vector2(31f, 0f);
        private static readonly Vector2 StartAreaSpawn = new Vector2(-68f, -1.8f);
        private static readonly Vector2 BossSpawnPosition = new Vector2(43f, 5.8f);
        private const float ArenaWallThickness = 1.2f;
        private const float DoorOpeningHeight = 8f;
        private const float CameraMinFollowX = -74f;
        private const float CameraMaxFollowX = 52f;
        private static readonly Vector2 DefaultPlayerSpawn = StartAreaSpawn;
        private static readonly Vector2 DefaultBossSpawn = BossSpawnPosition;

        private static Sprite runtimeWhiteSprite;
        private static Font runtimeUiFont;
        private static RuntimeData cachedRuntimeData;
        private static RuntimePresentationBindings activePresentationBindings;
        private static bool loggedBootstrapSuccess;

        private sealed class RuntimeData
        {
            public PlayerStatsData PlayerStats;
            public WeaponStatsData PlayerWeaponStats;
            public BossStatsData BossStatsNormal;
            public BossStatsData BossStatsFrenzy;
            public WeaponStatsData BossWeaponStats;
            public RoleSelectionProfileData[] RoleProfiles;
            public BattleConfigData BattleConfig;
            public AudioClipBindings AudioBindings;
        }

        private struct EntryRefs
        {
            public ProcedureManager Manager;
            public ProcedureBattle Battle;
            public ProcedureResult Result;
        }

        private struct CombatRefs
        {
            public PlayerController PlayerController;
            public PlayerHealth PlayerHealth;
            public WeaponController PlayerWeapon;
            public BossController BossController;
            public BossHealth BossHealth;
            public BossBrain BossBrain;
            public WeaponController BossWeapon;
        }

        private struct UiRefs
        {
            public BattleHudController Hud;
            public ResultPanelController ResultPanel;
            public BattlePausePanelController PausePanel;
            public MenuPresetPanelController MenuPanel;
            public RoleSelectionPanelController RoleSelectionPanel;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!ShouldBootstrapCurrentScene())
            {
                return;
            }

            BootstrapCurrentScene();
        }

        public static void BootstrapCurrentSceneInEditor()
        {
            if (!ShouldBootstrapCurrentScene())
            {
                return;
            }

            try
            {
                BuildOrUpdateScene();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("BossRushRuntimeSceneBuilder editor bootstrap failed.\n" + exception);
            }
        }

        private static bool ShouldBootstrapCurrentScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return false;
            }

            return string.Equals(activeScene.name, "RunScene", StringComparison.Ordinal) ||
                   string.Equals(activeScene.name, "SampleScene", StringComparison.Ordinal);
        }

        public static void BootstrapCurrentScene()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                BuildOrUpdateScene();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("BossRushRuntimeSceneBuilder failed.\n" + exception);
            }
        }

        private static void BuildOrUpdateScene()
        {
            activePresentationBindings = null;
            RuntimeData runtimeData = null;
            GameObject root = null;
            RuntimePresentationBindings presentationBindings = null;
            Projectile projectileTemplate = null;
            var entry = default(EntryRefs);
            ProjectilePool pool = null;
            DamageTextSpawner damageSpawner = null;
            ImpactFlashEffectSpawner impactSpawner = null;
            SliceEnemyController encounterEnemyTemplate = null;
            var combat = default(CombatRefs);
            BossPresetController bossPresetController = null;
            AudioService audioService = null;
            VerticalSliceFlowController flowController = null;
            var ui = default(UiRefs);
            Canvas uiCanvas = null;

            TryBuildStep("Camera", EnsureMainCamera);
            TryBuildStep("EventSystem", EnsureEventSystem);

            TryBuildStep("RuntimeData", () => runtimeData = EnsureRuntimeData());
            TryBuildStep("Root", () => root = FindOrCreateRoot());
            if (root == null)
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] Abort: root creation failed.");
                return;
            }

            TryBuildStep("PresentationBindings", () => presentationBindings = EnsurePresentationBindings(root));
            activePresentationBindings = presentationBindings;

            // Runtime environment/layout ownership stays in builder code for stable rebuilds.
            TryBuildStep("Environment", () => SetupBattleEnvironment(root.transform));
            TryBuildStep("ProjectileTemplate", () => projectileTemplate = EnsureRuntimeProjectileTemplate(root.transform));
            TryBuildStep("Entry", () => entry = SetupEntryRoot(root));
            TryBuildStep("ProjectilePool", () => pool = SetupProjectilePool(root.transform, projectileTemplate));
            TryBuildStep("DamageTextSpawner", () => damageSpawner = SetupDamageTextSpawner(root.transform));
            TryBuildStep("ImpactFlashEffectSpawner", () => impactSpawner = SetupImpactEffectSpawner(root.transform));
            TryBuildStep("Combat", () => combat = SetupCombat(root.transform, projectileTemplate, pool));
            TryBuildStep("EncounterEnemyTemplate", () => encounterEnemyTemplate = EnsureRuntimeEncounterEnemyTemplate(root.transform));
            TryBuildStep("BossPreset", () => bossPresetController = SetupBossPresetController(root, runtimeData, combat));
            var resolvedAudioBindings = ResolveAudioBindings(runtimeData, presentationBindings);
            TryBuildStep("Audio", () => audioService = SetupAudio(root.transform, resolvedAudioBindings, entry.Manager));

            if (TryBuildStep("UI.Canvas", () => uiCanvas = EnsureCanvas(root.transform)) && uiCanvas != null)
            {
                TryBuildStep("UI.BattleHud", () => ui.Hud = EnsureBattleHud(uiCanvas.transform, entry.Manager));
                TryBuildStep("UI.ResultPanel", () => ui.ResultPanel = EnsureResultPanel(uiCanvas.transform, entry.Manager));
                TryBuildStep("UI.PausePanel", () => ui.PausePanel = EnsurePausePanel(uiCanvas.transform, entry.Manager));
                TryBuildStep("UI.MenuPanel", () => ui.MenuPanel = EnsureMenuPanel(uiCanvas.transform, entry.Manager, bossPresetController));
                TryBuildStep("UI.RoleSelectionPanel", () => ui.RoleSelectionPanel = EnsureRoleSelectionPanel(uiCanvas.transform, entry.Manager));
            }
            else
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] UI root canvas is missing. HUD/Result/Pause/Menu creation skipped.");
            }

            TryBuildStep(
                "RuntimeHooks",
                () => SetupRuntimeHooks(
                    root,
                    entry,
                    combat,
                    ui,
                    pool,
                    audioService,
                    runtimeData,
                    resolvedAudioBindings,
                    damageSpawner,
                    impactSpawner,
                    bossPresetController));

            TryBuildStep("VerticalSliceFlow", () => flowController = SetupVerticalSliceFlow(root, entry, combat, uiCanvas, encounterEnemyTemplate));
            TryBuildStep(
                "RoleSelection.Configure",
                () => ui.RoleSelectionPanel?.Configure(
                    entry.Manager,
                    flowController,
                    runtimeData != null ? runtimeData.RoleProfiles : null));

            LogPhysicsCollisionSummary(root.transform, combat, projectileTemplate);
            LogMainChainSummary(entry, combat, pool, projectileTemplate);

            if (!loggedBootstrapSuccess)
            {
                loggedBootstrapSuccess = true;
                Debug.Log("BossRush runtime bootstrap complete. Empty scene can now run a full demo loop.");
            }
        }

        private static bool TryBuildStep(string stepName, Action stepAction)
        {
            try
            {
                stepAction?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] Step failed: " + stepName + "\n" + exception);
                return false;
            }
        }

        private static void LogMainChainSummary(EntryRefs entry, CombatRefs combat, ProjectilePool pool, Projectile projectileTemplate)
        {
            var currentProcedure = entry.Manager != null ? entry.Manager.CurrentProcedureType.ToString() : "ManagerNull";
            var poolPrefabReady = pool != null && pool.ProjectilePrefab != null;
            var targetReady = combat.BossBrain != null && combat.PlayerHealth != null;
            var playerWeaponText = combat.PlayerWeapon != null ? combat.PlayerWeapon.BuildRuntimeDebugSummary() : "null";
            var bossWeaponText = combat.BossWeapon != null ? combat.BossWeapon.BuildRuntimeDebugSummary() : "null";

            Debug.Log(
                "[BossRushRuntimeSceneBuilder] MainChainSummary " +
                "procedure=" + currentProcedure +
                " player=" + (combat.PlayerHealth != null ? combat.PlayerHealth.name : "null") +
                " boss=" + (combat.BossHealth != null ? combat.BossHealth.name : "null") +
                " playerWeapon=" + playerWeaponText +
                " bossWeapon=" + bossWeaponText +
                " pool=" + (pool != null ? pool.name : "null") +
                " poolPrefab=" + (poolPrefabReady ? pool.ProjectilePrefab.name : "null") +
                " projectileTemplate=" + (projectileTemplate != null ? projectileTemplate.name : "null") +
                " bossTargetReady=" + targetReady);

            if (combat.PlayerWeapon != null && combat.BossWeapon != null)
            {
                Debug.Log(
                    "[BossRushRuntimeSceneBuilder] WeaponCompare " +
                    "player(team=" + combat.PlayerWeapon.OwnerTeam +
                    ", spawn=" + (combat.PlayerWeapon.ProjectileSpawnPoint != null ? combat.PlayerWeapon.ProjectileSpawnPoint.name : "null") +
                    ", fireInterval=" + combat.PlayerWeapon.FireInterval.ToString("0.###") +
                    ", pool=" + (combat.PlayerWeapon.ProjectilePool != null ? combat.PlayerWeapon.ProjectilePool.name : "null") +
                    ", prefab=" + (combat.PlayerWeapon.ProjectilePrefab != null ? combat.PlayerWeapon.ProjectilePrefab.name : "null") +
                    ") boss(team=" + combat.BossWeapon.OwnerTeam +
                    ", spawn=" + (combat.BossWeapon.ProjectileSpawnPoint != null ? combat.BossWeapon.ProjectileSpawnPoint.name : "null") +
                    ", fireInterval=" + combat.BossWeapon.FireInterval.ToString("0.###") +
                    ", pool=" + (combat.BossWeapon.ProjectilePool != null ? combat.BossWeapon.ProjectilePool.name : "null") +
                    ", prefab=" + (combat.BossWeapon.ProjectilePrefab != null ? combat.BossWeapon.ProjectilePrefab.name : "null") +
                    ")");
            }
        }

        private static void LogPhysicsCollisionSummary(Transform root, CombatRefs combat, Projectile projectileTemplate)
        {
            var playerCollider = combat.PlayerHealth != null ? combat.PlayerHealth.GetComponent<Collider2D>() : null;
            var bossCollider = combat.BossHealth != null ? combat.BossHealth.GetComponent<Collider2D>() : null;
            var playerRb = combat.PlayerHealth != null ? combat.PlayerHealth.GetComponent<Rigidbody2D>() : null;
            var bossRb = combat.BossHealth != null ? combat.BossHealth.GetComponent<Rigidbody2D>() : null;

            var centerObstacle = root != null ? root.Find("Environment/BossArena/ArenaObstacles/TopCenterCover") : null;
            var obstacleCollider = centerObstacle != null ? centerObstacle.GetComponent<Collider2D>() : null;
            var obstacleRb = centerObstacle != null ? centerObstacle.GetComponent<Rigidbody2D>() : null;

            var projectileCollider = projectileTemplate != null ? projectileTemplate.GetComponent<Collider2D>() : null;
            var projectileRb = projectileTemplate != null ? projectileTemplate.GetComponent<Rigidbody2D>() : null;
            var defaultCollisionIgnored = Physics2D.GetIgnoreLayerCollision(0, 0);

            Debug.Log(
                "[BossRushRuntimeSceneBuilder] PhysicsSummary " +
                "player(collider=" + DescribeCollider(playerCollider) +
                ", rb=" + DescribeBody(playerRb) +
                ") boss(collider=" + DescribeCollider(bossCollider) +
                ", rb=" + DescribeBody(bossRb) +
                ") obstacle(collider=" + DescribeCollider(obstacleCollider) +
                ", rb=" + DescribeBody(obstacleRb) +
                ") projectile(collider=" + DescribeCollider(projectileCollider) +
                ", rb=" + DescribeBody(projectileRb) +
                ") defaultLayerIgnored=" + defaultCollisionIgnored);
        }

        private static EntryRefs SetupEntryRoot(GameObject root)
        {
            var manager = AddComponentIfMissing<ProcedureManager>(root);
            AddComponentIfMissing<ProcedureLaunch>(root);
            AddComponentIfMissing<ProcedureMenu>(root);
            var battle = AddComponentIfMissing<ProcedureBattle>(root);
            var result = AddComponentIfMissing<ProcedureResult>(root);
            AddComponentIfMissing<DebugPanelController>(root);
            AddComponentIfMissing<GameEntry>(root);

            manager.Initialize();
            return new EntryRefs
            {
                Manager = manager,
                Battle = battle,
                Result = result,
            };
        }

        private static CombatRefs SetupCombat(Transform parent, Projectile projectileTemplate, ProjectilePool pool)
        {
            var player = FindOrReuseCharacter<PlayerHealth>(parent, "Player");
            player.layer = 0;
            player.transform.position = new Vector3(DefaultPlayerSpawn.x, DefaultPlayerSpawn.y, 0f);
            EnsureCombatSprite(
                player,
                new Color(0.35f, 0.92f, 1f, 1f),
                0.72f,
                20,
                activePresentationBindings != null ? activePresentationBindings.PlayerSprite : null);

            var playerRigidbody = AddComponentIfMissing<Rigidbody2D>(player);
            playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
            playerRigidbody.simulated = true;
            playerRigidbody.gravityScale = 0f;
            playerRigidbody.freezeRotation = true;
            playerRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            playerRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var playerCollider = AddComponentIfMissing<CircleCollider2D>(player);
            playerCollider.isTrigger = false;
            playerCollider.radius = 0.4f;
            playerCollider.enabled = true;

            var playerWeapon = AddComponentIfMissing<WeaponController>(player);
            playerWeapon.SetOwnerTeam(CombatTeam.Player);
            playerWeapon.SetProjectilePrefab(projectileTemplate);
            playerWeapon.SetProjectilePool(pool);
            playerWeapon.SetProjectileSpawnPoint(
                FindOrCreateFirePoint(player.transform, "PlayerFirePoint", new Vector3(1.02f, 0f, 0f)));
            playerWeapon.enabled = true;
            playerWeapon.EnsureRuntimeReferences();

            var playerController = AddComponentIfMissing<PlayerController>(player);
            playerController.SetWeaponController(playerWeapon);
            playerController.SetAimCamera(Camera.main);
            playerController.enabled = true;

            AddComponentIfMissing<HitFlashFeedback>(player);
            AddComponentIfMissing<DeathFadeFeedback>(player);
            var playerHealth = AddComponentIfMissing<PlayerHealth>(player);
            playerHealth.SetTeam(CombatTeam.Player);
            player.SetActive(true);

            var boss = FindOrReuseCharacter<BossHealth>(parent, "Boss");
            boss.layer = 0;
            boss.transform.position = new Vector3(DefaultBossSpawn.x, DefaultBossSpawn.y, 0f);
            EnsureCombatSprite(
                boss,
                new Color(1f, 0.39f, 0.3f, 1f),
                2.85f,
                22,
                activePresentationBindings != null ? activePresentationBindings.BossSprite : null);

            var bossRigidbody = AddComponentIfMissing<Rigidbody2D>(boss);
            bossRigidbody.bodyType = RigidbodyType2D.Dynamic;
            bossRigidbody.simulated = true;
            bossRigidbody.gravityScale = 0f;
            bossRigidbody.freezeRotation = true;
            bossRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            bossRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var bossCollider = AddComponentIfMissing<CircleCollider2D>(boss);
            bossCollider.isTrigger = false;
            bossCollider.radius = 0.46f;
            bossCollider.enabled = true;

            var bossController = AddComponentIfMissing<BossController>(boss);
            AddComponentIfMissing<HitFlashFeedback>(boss);
            AddComponentIfMissing<DeathFadeFeedback>(boss);
            var bossHealth = AddComponentIfMissing<BossHealth>(boss);
            bossHealth.SetTeam(CombatTeam.Boss);

            var bossWeapon = AddComponentIfMissing<WeaponController>(boss);
            bossWeapon.SetOwnerTeam(CombatTeam.Boss);
            bossWeapon.SetProjectilePrefab(projectileTemplate);
            bossWeapon.SetProjectilePool(pool);
            bossWeapon.SetProjectileSpawnPoint(
                FindOrCreateFirePoint(boss.transform, "BossFirePoint", new Vector3(-1.02f, 0f, 0f)));
            bossWeapon.enabled = true;
            bossWeapon.EnsureRuntimeReferences();

            var bossBrain = AddComponentIfMissing<BossBrain>(boss);
            bossBrain.SetTargetPlayer(playerHealth);
            bossController.enabled = true;
            bossBrain.enabled = true;
            boss.SetActive(true);

            return new CombatRefs
            {
                PlayerController = playerController,
                PlayerHealth = playerHealth,
                PlayerWeapon = playerWeapon,
                BossController = bossController,
                BossHealth = bossHealth,
                BossBrain = bossBrain,
                BossWeapon = bossWeapon,
            };
        }

        private static BossPresetController SetupBossPresetController(GameObject root, RuntimeData data, CombatRefs combat)
        {
            var presetController = AddComponentIfMissing<BossPresetController>(root);
            presetController.SetTargets(combat.BossController, combat.BossHealth, combat.BossBrain, combat.BossWeapon);
            presetController.SetPresetData(
                data != null ? data.BossStatsNormal : null,
                data != null ? data.BossStatsFrenzy : null,
                BossPresetController.BossPresetType.Normal);
            presetController.SetPreset(BossPresetController.BossPresetType.Normal, false);
            presetController.ApplyCurrentPreset();
            return presetController;
        }

        private static DamageTextSpawner SetupDamageTextSpawner(Transform parent)
        {
            var spawnerObject = FindOrCreateChild(parent, "DamageTextSpawner");
            var spawner = AddComponentIfMissing<DamageTextSpawner>(spawnerObject);
            spawner.ConfigurePool(20, 8, 120);
            return spawner;
        }

        private static ImpactFlashEffectSpawner SetupImpactEffectSpawner(Transform parent)
        {
            var spawnerObject = FindOrCreateChild(parent, "ImpactFlashEffectSpawner");
            var spawner = AddComponentIfMissing<ImpactFlashEffectSpawner>(spawnerObject);
            spawner.ConfigurePool(24, 8, 160);
            spawner.SetSortingOrder(31);
            return spawner;
        }

        private static ProjectilePool SetupProjectilePool(Transform parent, Projectile projectileTemplate)
        {
            var poolObject = FindOrCreateChild(parent, "ProjectilePool");
            var pool = AddComponentIfMissing<ProjectilePool>(poolObject);
            pool.SetProjectilePrefab(projectileTemplate);
            pool.ConfigureCapacity(24, 8, 256);
            return pool;
        }

        private static Projectile EnsureRuntimeProjectileTemplate(Transform parent)
        {
            var templateObject = FindOrCreateChild(parent, RuntimeProjectileTemplateName);
            templateObject.layer = 0;
            templateObject.transform.localPosition = Vector3.zero;
            templateObject.transform.localRotation = Quaternion.identity;
            templateObject.transform.localScale = new Vector3(0.26f, 0.16f, 1f);
            templateObject.SetActive(false);

            var renderer = AddComponentIfMissing<SpriteRenderer>(templateObject);
            var projectileSprite = activePresentationBindings != null ? activePresentationBindings.ProjectileSprite : null;
            if (projectileSprite != null)
            {
                renderer.sprite = projectileSprite;
            }
            else if (renderer.sprite == null)
            {
                renderer.sprite = GetRuntimeWhiteSprite();
            }

            renderer.color = new Color(1f, 0.95f, 0.8f, 1f);
            renderer.sortingOrder = 28;

            var rigidbody = AddComponentIfMissing<Rigidbody2D>(templateObject);
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            rigidbody.simulated = true;
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = AddComponentIfMissing<CircleCollider2D>(templateObject);
            collider.isTrigger = true;
            collider.radius = 0.48f;

            return AddComponentIfMissing<Projectile>(templateObject);
        }

        private static SliceEnemyController EnsureRuntimeEncounterEnemyTemplate(Transform parent)
        {
            var templateObject = FindOrCreateChild(parent, "RuntimeEncounterEnemyTemplate");
            templateObject.layer = 0;
            templateObject.transform.localPosition = Vector3.zero;
            templateObject.transform.localRotation = Quaternion.identity;
            templateObject.transform.localScale = new Vector3(0.78f, 0.78f, 1f);
            templateObject.SetActive(false);

            var renderer = AddComponentIfMissing<SpriteRenderer>(templateObject);
            var enemySprite = activePresentationBindings != null
                ? activePresentationBindings.BossSprite != null
                    ? activePresentationBindings.BossSprite
                    : activePresentationBindings.ObstacleSprite
                : null;
            if (enemySprite != null)
            {
                renderer.sprite = enemySprite;
            }
            else if (renderer.sprite == null)
            {
                renderer.sprite = GetRuntimeWhiteSprite();
            }

            renderer.color = new Color(0.95f, 0.62f, 0.42f, 1f);
            renderer.sortingOrder = 23;

            var rigidbody = AddComponentIfMissing<Rigidbody2D>(templateObject);
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            rigidbody.simulated = true;
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = AddComponentIfMissing<CircleCollider2D>(templateObject);
            collider.isTrigger = false;
            collider.radius = 0.56f;
            collider.enabled = true;

            AddComponentIfMissing<HitFlashFeedback>(templateObject);
            AddComponentIfMissing<DeathFadeFeedback>(templateObject);
            return AddComponentIfMissing<SliceEnemyController>(templateObject);
        }

        private static AudioService SetupAudio(Transform parent, AudioClipBindings bindings, ProcedureManager manager)
        {
            var audioRoot = FindOrCreateChild(parent, "AudioRoot");
            var service = AddComponentIfMissing<AudioService>(audioRoot);

            var bgmSource = EnsureAudioSource(audioRoot.transform, "BgmSource", true);
            var sfxSource = EnsureAudioSource(audioRoot.transform, "SfxSource", false);
            service.BindAudioSources(bgmSource, sfxSource);
            service.SetClipBindings(bindings);
            service.BindProcedureManager(manager);

            return service;
        }

        private static UiRefs SetupUi(Transform parent, ProcedureManager manager, BossPresetController presetController)
        {
            var canvas = EnsureCanvas(parent);
            var hud = EnsureBattleHud(canvas.transform, manager);
            var result = EnsureResultPanel(canvas.transform, manager);
            var pause = EnsurePausePanel(canvas.transform, manager);
            var menu = EnsureMenuPanel(canvas.transform, manager, presetController);
            return new UiRefs
            {
                Hud = hud,
                ResultPanel = result,
                PausePanel = pause,
                MenuPanel = menu,
            };
        }

        private static void SetupRuntimeHooks(
            GameObject root,
            EntryRefs entry,
            CombatRefs combat,
            UiRefs ui,
            ProjectilePool pool,
            AudioService audioService,
            RuntimeData data,
            AudioClipBindings resolvedAudioBindings,
            DamageTextSpawner damageTextSpawner,
            ImpactFlashEffectSpawner impactFlashEffectSpawner,
            BossPresetController presetController)
        {
            var hooks = AddComponentIfMissing<RuntimeSceneHooks>(root);
            hooks.SetCoreReferences(
                entry.Manager,
                entry.Battle,
                entry.Result,
                pool,
                audioService,
                ui.Hud,
                ui.ResultPanel,
                combat.PlayerController,
                combat.PlayerHealth,
                combat.PlayerWeapon,
                combat.BossController,
                combat.BossHealth,
                combat.BossBrain,
                combat.BossWeapon);

            if (data != null)
            {
                hooks.SetDataAssets(
                    data.PlayerStats,
                    data.PlayerWeaponStats,
                    data.BossStatsNormal,
                    data.BossWeaponStats,
                    data.BattleConfig);
            }
            else
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] RuntimeData is null. RuntimeSceneHooks fallback data will be unavailable.");
            }

            hooks.SetAudioClipBindings(resolvedAudioBindings);

            hooks.AutoBindSceneReferences();
            hooks.Apply();

            ui.PausePanel?.Configure(entry.Manager);
            ui.MenuPanel?.Configure(entry.Manager, presetController);

            if (damageTextSpawner == null || impactFlashEffectSpawner == null)
            {
                Debug.LogWarning("Runtime bootstrap could not resolve one or more feedback spawners.");
            }
        }

        private static RuntimePresentationBindings EnsurePresentationBindings(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            return AddComponentIfMissing<RuntimePresentationBindings>(root);
        }

        private static VerticalSliceFlowController SetupVerticalSliceFlow(
            GameObject root,
            EntryRefs entry,
            CombatRefs combat,
            Canvas uiCanvas,
            SliceEnemyController encounterEnemyTemplate)
        {
            if (root == null)
            {
                return null;
            }

            var environment = root.transform.Find("Environment");
            if (environment == null)
            {
                Debug.LogWarning("VerticalSliceFlow skipped: Environment root missing.");
                return null;
            }

            var startArea = environment.Find("StartArea");
            var encounterRoom = environment.Find("EncounterRoom");
            var bossArena = environment.Find("BossArena");
            if (startArea == null || encounterRoom == null || bossArena == null)
            {
                Debug.LogWarning("VerticalSliceFlow skipped: required room roots are missing.");
                return null;
            }

            var startSpawn = startArea.Find("SpawnPoint");
            var startCameraFocus = startArea.Find("CameraFocusPoint");
            var startPortal = startArea.Find("PortalToRun");

            var encounterSensor = encounterRoom.Find("EntrySensor");
            var waveSpawnRoot = encounterRoom.Find("WaveSpawnRoot");
            var runtimeEnemyRoot = encounterRoom.Find("RuntimeEnemies");
            var entryGate = encounterRoom.Find("EntryGate");
            var exitGate = encounterRoom.Find("ExitGate");

            var bossSensor = bossArena.Find("BossEntrySensor");
            var bossEntryGate = bossArena.Find("BossEntryGate");

            var transitionOverlay = uiCanvas != null ? EnsureTransitionOverlay(uiCanvas.transform) : null;
            var flow = AddComponentIfMissing<VerticalSliceFlowController>(root);
            flow.Configure(
                entry.Manager,
                combat.PlayerHealth,
                combat.PlayerController,
                combat.PlayerWeapon,
                combat.BossHealth,
                combat.BossController,
                combat.BossBrain,
                combat.BossWeapon,
                startSpawn,
                startCameraFocus,
                startPortal != null ? startPortal.GetComponent<RoomPortalTrigger>() : null,
                encounterSensor != null ? encounterSensor.GetComponent<RoomPortalTrigger>() : null,
                bossSensor != null ? bossSensor.GetComponent<RoomPortalTrigger>() : null,
                entryGate != null ? entryGate.GetComponent<RoomGateController>() : null,
                exitGate != null ? exitGate.GetComponent<RoomGateController>() : null,
                bossEntryGate != null ? bossEntryGate.GetComponent<RoomGateController>() : null,
                encounterEnemyTemplate,
                runtimeEnemyRoot,
                waveSpawnRoot,
                transitionOverlay,
                CameraMinFollowX,
                CameraMaxFollowX);
            flow.SetRoleConfirmed(false);
            return flow;
        }

        private static AudioClipBindings ResolveAudioBindings(RuntimeData runtimeData, RuntimePresentationBindings presentationBindings)
        {
            if (presentationBindings != null && presentationBindings.AudioClipBindingsOverride != null)
            {
                return presentationBindings.AudioClipBindingsOverride;
            }

            return runtimeData != null ? runtimeData.AudioBindings : null;
        }

        private static RuntimeData EnsureRuntimeData()
        {
            if (cachedRuntimeData != null)
            {
                return cachedRuntimeData;
            }

            var playerStats = ScriptableObject.CreateInstance<PlayerStatsData>();
            playerStats.name = "Runtime_PlayerStats";
            playerStats.moveSpeed = 8.4f;
            playerStats.maxHealth = 175f;
            playerStats.aimRotationOffset = 0f;
            playerStats.dodgeKey = KeyCode.Space;
            playerStats.dodgeDistance = 3f;
            playerStats.dodgeDuration = 0.18f;
            playerStats.dodgeCooldown = 1.55f;
            playerStats.dodgeInvulnerable = true;
            playerStats.dodgeDamageReduction = 0.75f;

            var playerWeaponStats = ScriptableObject.CreateInstance<WeaponStatsData>();
            playerWeaponStats.name = "Runtime_PlayerWeaponStats";
            playerWeaponStats.ownerTeam = CombatTeam.Player;
            playerWeaponStats.fireInterval = 0.13f;
            playerWeaponStats.projectileSpeed = 22.5f;
            playerWeaponStats.projectileDamage = 14f;
            playerWeaponStats.projectileLifetime = 3.35f;

            var bossWeaponStats = ScriptableObject.CreateInstance<WeaponStatsData>();
            bossWeaponStats.name = "Runtime_BossWeaponStats";
            bossWeaponStats.ownerTeam = CombatTeam.Boss;
            bossWeaponStats.fireInterval = 0.06f;
            bossWeaponStats.projectileSpeed = 14.2f;
            bossWeaponStats.projectileDamage = 6f;
            bossWeaponStats.projectileLifetime = 3.9f;

            var bossNormalStats = ScriptableObject.CreateInstance<BossStatsData>();
            bossNormalStats.name = "Runtime_BossStats_Normal";
            ApplyNormalBossDefaults(bossNormalStats);

            var bossFrenzyStats = ScriptableObject.CreateInstance<BossStatsData>();
            bossFrenzyStats.name = "Runtime_BossStats_Frenzy";
            ApplyFrenzyBossDefaults(bossFrenzyStats);

            var battleConfig = ScriptableObject.CreateInstance<BattleConfigData>();
            battleConfig.name = "Runtime_BattleConfig";
            battleConfig.autoEnterBattleOnPlay = false;
            battleConfig.battleTimeLimit = 150f;
            battleConfig.playerSpawnPosition = StartAreaSpawn;
            battleConfig.bossSpawnPosition = DefaultBossSpawn;

            var rangerProfile = CreateRuntimeRoleProfile(
                "RoleProfile_Ranger",
                "Ranger",
                "Ranger",
                20,
                10,
                100,
                "Tactical Roll",
                "Quick dodge with short invulnerability; ideal for threading through fan and nova gaps.",
                "Pulse Carbine",
                "Stable mid-range automatic fire with controllable recoil.",
                "Scatter Blaster",
                "Wide burst for close pressure and emergency peel.");
            var engineerProfile = CreateRuntimeRoleProfile(
                "RoleProfile_Engineer",
                "Engineer",
                "Engineer",
                22,
                8,
                120,
                "Overload Dash",
                "Short displacement with energy shielding; suited for aggressive repositioning.",
                "Arc Pistol",
                "Fast sidearm that chains light damage at medium range.",
                "Micro Rocket",
                "Slow but hard-hitting projectile for punish windows.");
            var roleProfiles = new[] { rangerProfile, engineerProfile };

            var audioBindings = ScriptableObject.CreateInstance<AudioClipBindings>();
            audioBindings.name = "Runtime_AudioBindings";
            EnsureAudioBinding(audioBindings.bgm, SoundIds.BgmMenu);
            EnsureAudioBinding(audioBindings.bgm, SoundIds.BgmBattle);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxPlayerShoot);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxBossShoot);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxHit);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxPlayerDied);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxBossDied);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxWeaponSwitch);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxPlayerHit);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxArmorBreak);
            EnsureAudioBinding(audioBindings.sfx, SoundIds.SfxEnemyDied);

            cachedRuntimeData = new RuntimeData
            {
                PlayerStats = playerStats,
                PlayerWeaponStats = playerWeaponStats,
                BossStatsNormal = bossNormalStats,
                BossStatsFrenzy = bossFrenzyStats,
                BossWeaponStats = bossWeaponStats,
                RoleProfiles = roleProfiles,
                BattleConfig = battleConfig,
                AudioBindings = audioBindings,
            };
            return cachedRuntimeData;
        }

        private static RoleSelectionProfileData CreateRuntimeRoleProfile(
            string assetName,
            string roleId,
            string displayName,
            int redHealth,
            int blueArmor,
            int energy,
            string skillName,
            string skillDescription,
            string primaryWeaponName,
            string primaryWeaponDescription,
            string secondaryWeaponName,
            string secondaryWeaponDescription)
        {
            var profile = ScriptableObject.CreateInstance<RoleSelectionProfileData>();
            profile.name = assetName;
            profile.roleId = roleId;
            profile.displayName = displayName;
            profile.redHealth = Mathf.Max(1, redHealth);
            profile.blueArmor = Mathf.Max(0, blueArmor);
            profile.energy = Mathf.Max(0, energy);
            profile.skillName = skillName;
            profile.skillDescription = skillDescription;
            profile.primaryWeaponName = primaryWeaponName;
            profile.primaryWeaponDescription = primaryWeaponDescription;
            profile.secondaryWeaponName = secondaryWeaponName;
            profile.secondaryWeaponDescription = secondaryWeaponDescription;
            return profile;
        }

        private static void ApplyNormalBossDefaults(BossStatsData stats)
        {
            stats.maxHealth = 640f;
            stats.moveSpeed = 3.95f;
            stats.idleDuration = 0.58f;
            stats.burstDuration = 2.3f;
            stats.burstFireInterval = 0.39f;
            stats.cooldownDuration = 1.05f;
            stats.engageDistanceMin = 3.2f;
            stats.engageDistanceMax = 11.4f;
            stats.retargetInterval = 0.58f;
            stats.enableFanShot = true;
            stats.fanShotCount = 5;
            stats.fanSpreadAngle = 40f;
            stats.fanSkillWindup = 0.45f;
            stats.fanSkillRecovery = 0.68f;
            stats.fanSkillInterval = 4.9f;
            stats.enableRadialNova = true;
            stats.radialNovaShotCount = 18;
            stats.radialNovaProjectileSpeedScale = 0.9f;
            stats.radialNovaWindup = 0.88f;
            stats.radialNovaRecovery = 0.65f;
            stats.radialNovaInterval = 8.6f;
            stats.radialNovaPulseCount = 1;
            stats.radialNovaPulseInterval = 0.16f;
            stats.enableLowHealthAggression = true;
            stats.lowHealthThresholdNormalized = 0.45f;
            stats.lowHealthBurstFireIntervalScale = 0.6f;
            stats.lowHealthCooldownScale = 0.58f;
            stats.lowHealthChaseSpeedScale = 1.34f;
            stats.lowHealthRadialNovaIntervalScale = 0.66f;
            stats.lowHealthRadialNovaShotBonus = 4;
        }

        private static void ApplyFrenzyBossDefaults(BossStatsData stats)
        {
            stats.maxHealth = 720f;
            stats.moveSpeed = 4.95f;
            stats.idleDuration = 0.24f;
            stats.burstDuration = 2.8f;
            stats.burstFireInterval = 0.2f;
            stats.cooldownDuration = 0.45f;
            stats.engageDistanceMin = 2.6f;
            stats.engageDistanceMax = 12.8f;
            stats.retargetInterval = 0.3f;
            stats.enableFanShot = true;
            stats.fanShotCount = 7;
            stats.fanSpreadAngle = 68f;
            stats.fanSkillWindup = 0.26f;
            stats.fanSkillRecovery = 0.5f;
            stats.fanSkillInterval = 2.7f;
            stats.enableRadialNova = true;
            stats.radialNovaShotCount = 24;
            stats.radialNovaProjectileSpeedScale = 1.05f;
            stats.radialNovaWindup = 0.64f;
            stats.radialNovaRecovery = 0.48f;
            stats.radialNovaInterval = 6.3f;
            stats.radialNovaPulseCount = 2;
            stats.radialNovaPulseInterval = 0.2f;
            stats.enableLowHealthAggression = true;
            stats.lowHealthThresholdNormalized = 0.58f;
            stats.lowHealthBurstFireIntervalScale = 0.46f;
            stats.lowHealthCooldownScale = 0.32f;
            stats.lowHealthChaseSpeedScale = 1.64f;
            stats.lowHealthRadialNovaIntervalScale = 0.55f;
            stats.lowHealthRadialNovaShotBonus = 8;
        }

        private static void SetupBattleEnvironment(Transform parent)
        {
            var environmentRoot = FindOrCreateChild(parent, "Environment");
            environmentRoot.transform.localPosition = Vector3.zero;
            environmentRoot.transform.localRotation = Quaternion.identity;
            environmentRoot.transform.localScale = Vector3.one;
            environmentRoot.transform.SetSiblingIndex(0);
            RemoveChildrenExcept(
                environmentRoot.transform,
                "StartArea",
                "Corridor01",
                "EncounterRoom",
                "Corridor02",
                "BossArena");

            var startArea = FindOrCreateChild(environmentRoot.transform, "StartArea");
            var corridor01 = FindOrCreateChild(environmentRoot.transform, "Corridor01");
            var encounterRoom = FindOrCreateChild(environmentRoot.transform, "EncounterRoom");
            var corridor02 = FindOrCreateChild(environmentRoot.transform, "Corridor02");
            var bossArena = FindOrCreateChild(environmentRoot.transform, "BossArena");

            ConfigureLinearStartArea(startArea);
            ConfigureLinearCorridor(corridor01, CorridorOneCenter, CorridorOneSize, new Color(0.16f, 0.2f, 0.27f, 1f));
            ConfigureLinearEncounterRoom(encounterRoom);
            ConfigureLinearCorridor(corridor02, CorridorTwoCenter, CorridorTwoSize, new Color(0.15f, 0.19f, 0.26f, 1f));
            ConfigureLinearBossArena(bossArena);
        }

        private static void ConfigureLinearStartArea(GameObject room)
        {
            room.transform.localPosition = new Vector3(StartAreaCenter.x, StartAreaCenter.y, 0f);
            room.transform.localRotation = Quaternion.identity;
            room.transform.localScale = Vector3.one;
            RemoveChildrenExcept(
                room.transform,
                "Floor",
                "WallTop",
                "WallBottom",
                "WallLeft",
                "RightUpper",
                "RightLower",
                "SpawnPoint",
                "CameraFocusPoint",
                "PortalToRun",
                "RoleDisplayA",
                "RoleDisplayB");

            var wallColor = new Color(0.25f, 0.45f, 0.62f, 0.96f);
            var halfWidth = StartAreaSize.x * 0.5f;
            var halfHeight = StartAreaSize.y * 0.5f;

            ConfigureArenaVisual(
                FindOrCreateChild(room.transform, "Floor"),
                new Color(0.15f, 0.21f, 0.3f, 1f),
                StartAreaSize,
                -42,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.FloorSprite : null);

            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "WallTop"),
                wallColor,
                new Vector2(StartAreaSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -31,
                new Vector3(0f, halfHeight + ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "WallBottom"),
                wallColor,
                new Vector2(StartAreaSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -31,
                new Vector3(0f, -halfHeight - ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "WallLeft"),
                wallColor,
                new Vector2(ArenaWallThickness, StartAreaSize.y),
                -31,
                new Vector3(-halfWidth - ArenaWallThickness * 0.5f, 0f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureVerticalOpeningSegments(
                room.transform,
                "Right",
                halfWidth + ArenaWallThickness * 0.5f,
                halfHeight,
                DoorOpeningHeight,
                wallColor);

            var spawnPoint = FindOrCreateChild(room.transform, "SpawnPoint");
            spawnPoint.transform.localPosition = new Vector3(StartAreaSpawn.x - StartAreaCenter.x, StartAreaSpawn.y - StartAreaCenter.y, 0f);
            spawnPoint.transform.localRotation = Quaternion.identity;
            spawnPoint.transform.localScale = Vector3.one;

            var cameraFocus = FindOrCreateChild(room.transform, "CameraFocusPoint");
            cameraFocus.transform.localPosition = Vector3.zero;
            cameraFocus.transform.localRotation = Quaternion.identity;
            cameraFocus.transform.localScale = Vector3.one;

            ConfigureInteractivePortal(
                FindOrCreateChild(room.transform, "PortalToRun"),
                new Vector3(halfWidth - 1.6f, 0f, 0f),
                new Color(0.34f, 0.95f, 0.9f, 0.9f),
                "StartAreaPortal",
                true);

            ConfigureArenaVisual(
                FindOrCreateChild(room.transform, "RoleDisplayA"),
                new Color(0.4f, 0.75f, 0.96f, 0.84f),
                new Vector2(1.35f, 1.35f),
                -27,
                new Vector3(-5.8f, 2.3f, 0f),
                activePresentationBindings != null ? activePresentationBindings.PlayerSprite : null);
            ConfigureArenaVisual(
                FindOrCreateChild(room.transform, "RoleDisplayB"),
                new Color(0.84f, 0.65f, 0.34f, 0.84f),
                new Vector2(1.35f, 1.35f),
                -27,
                new Vector3(-3.8f, 2.3f, 0f),
                activePresentationBindings != null ? activePresentationBindings.PlayerSprite : null);
        }

        private static void ConfigureLinearCorridor(GameObject corridor, Vector2 center, Vector2 size, Color floorColor)
        {
            corridor.transform.localPosition = new Vector3(center.x, center.y, 0f);
            corridor.transform.localRotation = Quaternion.identity;
            corridor.transform.localScale = Vector3.one;
            RemoveChildrenExcept(
                corridor.transform,
                "Floor",
                "WallTop",
                "WallBottom");

            var halfHeight = size.y * 0.5f;
            ConfigureArenaVisual(
                FindOrCreateChild(corridor.transform, "Floor"),
                floorColor,
                size,
                -43,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.CombatZoneSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(corridor.transform, "WallTop"),
                new Color(0.22f, 0.34f, 0.48f, 0.95f),
                new Vector2(size.x, ArenaWallThickness),
                -31,
                new Vector3(0f, halfHeight + ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(corridor.transform, "WallBottom"),
                new Color(0.22f, 0.34f, 0.48f, 0.95f),
                new Vector2(size.x, ArenaWallThickness),
                -31,
                new Vector3(0f, -halfHeight - ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
        }

        private static void ConfigureLinearEncounterRoom(GameObject room)
        {
            room.transform.localPosition = new Vector3(EncounterRoomCenter.x, EncounterRoomCenter.y, 0f);
            room.transform.localRotation = Quaternion.identity;
            room.transform.localScale = Vector3.one;
            RemoveChildrenExcept(
                room.transform,
                "Floor",
                "InnerMat",
                "WallTop",
                "WallBottom",
                "LeftUpper",
                "LeftLower",
                "RightUpper",
                "RightLower",
                "EntryGate",
                "ExitGate",
                "EntrySensor",
                "WaveSpawnRoot",
                "RuntimeEnemies",
                "CoverUpper",
                "CoverLower");

            var halfWidth = EncounterRoomSize.x * 0.5f;
            var halfHeight = EncounterRoomSize.y * 0.5f;
            var wallColor = new Color(0.46f, 0.28f, 0.28f, 0.95f);

            ConfigureArenaVisual(
                FindOrCreateChild(room.transform, "Floor"),
                new Color(0.22f, 0.17f, 0.2f, 1f),
                EncounterRoomSize,
                -42,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.FloorSprite : null);
            ConfigureArenaVisual(
                FindOrCreateChild(room.transform, "InnerMat"),
                new Color(0.31f, 0.21f, 0.24f, 0.56f),
                new Vector2(EncounterRoomSize.x - 6f, EncounterRoomSize.y - 6f),
                -41,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.CenterMatSprite : null);

            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "WallTop"),
                wallColor,
                new Vector2(EncounterRoomSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -31,
                new Vector3(0f, halfHeight + ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "WallBottom"),
                wallColor,
                new Vector2(EncounterRoomSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -31,
                new Vector3(0f, -halfHeight - ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureVerticalOpeningSegments(
                room.transform,
                "Left",
                -halfWidth - ArenaWallThickness * 0.5f,
                halfHeight,
                DoorOpeningHeight,
                wallColor);
            ConfigureVerticalOpeningSegments(
                room.transform,
                "Right",
                halfWidth + ArenaWallThickness * 0.5f,
                halfHeight,
                DoorOpeningHeight,
                wallColor);

            ConfigureRoomGate(
                FindOrCreateChild(room.transform, "EntryGate"),
                new Vector3(-halfWidth - ArenaWallThickness * 0.5f, 0f, 0f),
                new Vector2(ArenaWallThickness, DoorOpeningHeight));
            ConfigureRoomGate(
                FindOrCreateChild(room.transform, "ExitGate"),
                new Vector3(halfWidth + ArenaWallThickness * 0.5f, 0f, 0f),
                new Vector2(ArenaWallThickness, DoorOpeningHeight));
            ConfigureSensorTrigger(
                FindOrCreateChild(room.transform, "EntrySensor"),
                new Vector3(-halfWidth + 2.2f, 0f, 0f),
                new Vector2(0.8f, DoorOpeningHeight + 1.2f),
                "EncounterEntrySensor");

            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "CoverUpper"),
                new Color(0.46f, 0.32f, 0.36f, 1f),
                new Vector2(3.4f, 1.2f),
                -26,
                new Vector3(-4.8f, 3.8f, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(room.transform, "CoverLower"),
                new Color(0.46f, 0.32f, 0.36f, 1f),
                new Vector2(3.4f, 1.2f),
                -26,
                new Vector3(4.8f, -3.8f, 0f));

            var runtimeEnemies = FindOrCreateChild(room.transform, "RuntimeEnemies");
            runtimeEnemies.transform.localPosition = Vector3.zero;
            runtimeEnemies.transform.localRotation = Quaternion.identity;
            runtimeEnemies.transform.localScale = Vector3.one;

            var spawnRoot = FindOrCreateChild(room.transform, "WaveSpawnRoot");
            spawnRoot.transform.localPosition = Vector3.zero;
            spawnRoot.transform.localRotation = Quaternion.identity;
            spawnRoot.transform.localScale = Vector3.one;
            RemoveChildrenExcept(
                spawnRoot.transform,
                "Wave1_A",
                "Wave1_B",
                "Wave1_C",
                "Wave2_A",
                "Wave2_B",
                "Wave2_C",
                "Wave2_D");

            CreateWaveSpawnPoint(spawnRoot.transform, "Wave1_A", new Vector3(-6f, 2.8f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave1_B", new Vector3(0f, -2.6f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave1_C", new Vector3(6f, 2.4f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave2_A", new Vector3(-8f, -3.6f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave2_B", new Vector3(-2.4f, 4.1f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave2_C", new Vector3(2.6f, -4.2f, 0f));
            CreateWaveSpawnPoint(spawnRoot.transform, "Wave2_D", new Vector3(8f, 3.4f, 0f));
        }

        private static void ConfigureLinearBossArena(GameObject bossArena)
        {
            bossArena.transform.localPosition = new Vector3(BossArenaCenter.x, BossArenaCenter.y, 0f);
            bossArena.transform.localRotation = Quaternion.identity;
            bossArena.transform.localScale = Vector3.one;
            RemoveChildrenExcept(
                bossArena.transform,
                "ArenaFloor",
                "ArenaCombatZone",
                "ArenaCenterMat",
                "WallTop",
                "WallBottom",
                "WallRight",
                "LeftUpper",
                "LeftLower",
                "BossEntryGate",
                "BossEntrySensor",
                "ArenaObstacles");

            var halfWidth = BossArenaSize.x * 0.5f;
            var halfHeight = BossArenaSize.y * 0.5f;
            var wallColor = new Color(0.22f, 0.66f, 0.82f, 0.96f);

            ConfigureArenaVisual(
                FindOrCreateChild(bossArena.transform, "ArenaFloor"),
                new Color(0.09f, 0.11f, 0.15f, 1f),
                BossArenaSize + new Vector2(ArenaWallThickness * 2f, ArenaWallThickness * 2f),
                -40,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.FloorSprite : null);
            ConfigureArenaVisual(
                FindOrCreateChild(bossArena.transform, "ArenaCombatZone"),
                new Color(0.14f, 0.18f, 0.24f, 0.96f),
                BossArenaSize,
                -39,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.CombatZoneSprite : null);
            ConfigureArenaVisual(
                FindOrCreateChild(bossArena.transform, "ArenaCenterMat"),
                new Color(0.19f, 0.24f, 0.31f, 0.64f),
                BossArenaInnerMatSize,
                -38,
                Vector3.zero,
                activePresentationBindings != null ? activePresentationBindings.CenterMatSprite : null);

            ConfigureArenaSolid(
                FindOrCreateChild(bossArena.transform, "WallTop"),
                wallColor,
                new Vector2(BossArenaSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -30,
                new Vector3(0f, halfHeight + ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(bossArena.transform, "WallBottom"),
                wallColor,
                new Vector2(BossArenaSize.x + ArenaWallThickness * 2f, ArenaWallThickness),
                -30,
                new Vector3(0f, -halfHeight - ArenaWallThickness * 0.5f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(bossArena.transform, "WallRight"),
                wallColor,
                new Vector2(ArenaWallThickness, BossArenaSize.y),
                -30,
                new Vector3(halfWidth + ArenaWallThickness * 0.5f, 0f, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureVerticalOpeningSegments(
                bossArena.transform,
                "Left",
                -halfWidth - ArenaWallThickness * 0.5f,
                halfHeight,
                DoorOpeningHeight,
                wallColor);
            ConfigureRoomGate(
                FindOrCreateChild(bossArena.transform, "BossEntryGate"),
                new Vector3(-halfWidth - ArenaWallThickness * 0.5f, 0f, 0f),
                new Vector2(ArenaWallThickness, DoorOpeningHeight));

            ConfigureSensorTrigger(
                FindOrCreateChild(bossArena.transform, "BossEntrySensor"),
                new Vector3(-halfWidth + 1.1f, 0f, 0f),
                new Vector2(1f, DoorOpeningHeight + 1.2f),
                "BossRoomEntrySensor");

            var obstaclesRoot = FindOrCreateChild(bossArena.transform, "ArenaObstacles");
            RemoveChildrenExcept(
                obstaclesRoot.transform,
                "TopLeftCover",
                "TopCenterCover",
                "TopRightCover",
                "MidLeftPillar",
                "MidRightPillar",
                "BottomLeftCover",
                "BottomCenterCover",
                "BottomRightCover",
                "UpperLeftRing",
                "UpperRightRing",
                "LowerLeftRing",
                "LowerRightRing");
            var ringX = halfWidth * 0.48f;
            var midRingX = halfWidth * 0.36f;
            var topY = halfHeight * 0.64f;
            var bottomY = -topY;
            var sideY = halfHeight * 0.38f;

            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "TopLeftCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.6f, 1.2f),
                -26,
                new Vector3(-10.4f, topY, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "TopCenterCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.2f, 1.2f),
                -26,
                new Vector3(0f, topY, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "TopRightCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.6f, 1.2f),
                -26,
                new Vector3(10.4f, topY, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "MidLeftPillar"),
                new Color(0.27f, 0.33f, 0.43f, 1f),
                new Vector2(1.85f, 6.4f),
                -26,
                new Vector3(-ringX, 0f, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "MidRightPillar"),
                new Color(0.27f, 0.33f, 0.43f, 1f),
                new Vector2(1.85f, 6.4f),
                -26,
                new Vector3(ringX, 0f, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "BottomLeftCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.6f, 1.2f),
                -26,
                new Vector3(-10.4f, bottomY, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "BottomCenterCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.2f, 1.2f),
                -26,
                new Vector3(0f, bottomY, 0f));
            ConfigureArenaSolid(
                FindOrCreateChild(obstaclesRoot.transform, "BottomRightCover"),
                new Color(0.28f, 0.35f, 0.45f, 1f),
                new Vector2(4.6f, 1.2f),
                -26,
                new Vector3(10.4f, bottomY, 0f));
            ConfigureArenaDecorative(
                FindOrCreateChild(obstaclesRoot.transform, "UpperLeftRing"),
                new Color(0.25f, 0.31f, 0.4f, 1f),
                new Vector2(2.2f, 2.2f),
                -26,
                new Vector3(-midRingX, sideY, 0f),
                null,
                true);
            ConfigureArenaDecorative(
                FindOrCreateChild(obstaclesRoot.transform, "UpperRightRing"),
                new Color(0.25f, 0.31f, 0.4f, 1f),
                new Vector2(2.2f, 2.2f),
                -26,
                new Vector3(midRingX, sideY, 0f),
                null,
                true);
            ConfigureArenaDecorative(
                FindOrCreateChild(obstaclesRoot.transform, "LowerLeftRing"),
                new Color(0.25f, 0.31f, 0.4f, 1f),
                new Vector2(2.2f, 2.2f),
                -26,
                new Vector3(-midRingX, -sideY, 0f),
                null,
                true);
            ConfigureArenaDecorative(
                FindOrCreateChild(obstaclesRoot.transform, "LowerRightRing"),
                new Color(0.25f, 0.31f, 0.4f, 1f),
                new Vector2(2.2f, 2.2f),
                -26,
                new Vector3(midRingX, -sideY, 0f),
                null,
                true);
        }

        private static void ConfigureVerticalOpeningSegments(
            Transform parent,
            string sidePrefix,
            float xPosition,
            float halfRoomHeight,
            float openingHeight,
            Color wallColor)
        {
            var safeHalfHeight = Mathf.Max(1f, halfRoomHeight);
            var safeOpening = Mathf.Clamp(openingHeight, 1.6f, safeHalfHeight * 2f - 1f);
            var segmentHeight = Mathf.Max(0.5f, safeHalfHeight - safeOpening * 0.5f);
            var offsetY = safeOpening * 0.5f + segmentHeight * 0.5f;

            ConfigureArenaSolid(
                FindOrCreateChild(parent, sidePrefix + "Upper"),
                wallColor,
                new Vector2(ArenaWallThickness, segmentHeight),
                -31,
                new Vector3(xPosition, offsetY, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
            ConfigureArenaSolid(
                FindOrCreateChild(parent, sidePrefix + "Lower"),
                wallColor,
                new Vector2(ArenaWallThickness, segmentHeight),
                -31,
                new Vector3(xPosition, -offsetY, 0f),
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);
        }

        private static void ConfigureInteractivePortal(
            GameObject portalObject,
            Vector3 localPosition,
            Color tint,
            string portalId,
            bool requireInteractKey)
        {
            if (portalObject == null)
            {
                return;
            }

            portalObject.layer = 0;
            ConfigureArenaVisual(
                portalObject,
                tint,
                new Vector2(1.75f, 1.75f),
                -27,
                localPosition,
                activePresentationBindings != null ? activePresentationBindings.ObstacleSprite : null);

            var rigidbody = AddComponentIfMissing<Rigidbody2D>(portalObject);
            rigidbody.bodyType = RigidbodyType2D.Static;
            rigidbody.simulated = true;
            rigidbody.gravityScale = 0f;

            var collider = AddComponentIfMissing<CircleCollider2D>(portalObject);
            collider.isTrigger = true;
            collider.enabled = true;
            collider.radius = 0.52f;

            var portal = AddComponentIfMissing<RoomPortalTrigger>(portalObject);
            portal.Configure(portalId, KeyCode.E, requireInteractKey);
            portal.SetPortalEnabled(true);
        }

        private static void ConfigureSensorTrigger(
            GameObject sensorObject,
            Vector3 localPosition,
            Vector2 size,
            string triggerId)
        {
            if (sensorObject == null)
            {
                return;
            }

            sensorObject.layer = 0;
            ConfigureArenaVisual(
                sensorObject,
                new Color(0.98f, 0.84f, 0.4f, 0.14f),
                size,
                -25,
                localPosition,
                activePresentationBindings != null ? activePresentationBindings.CombatZoneSprite : null);
            var sensorRenderer = sensorObject.GetComponent<SpriteRenderer>();
            if (sensorRenderer != null)
            {
                sensorRenderer.enabled = false;
            }

            var rigidbody = AddComponentIfMissing<Rigidbody2D>(sensorObject);
            rigidbody.bodyType = RigidbodyType2D.Static;
            rigidbody.simulated = true;
            rigidbody.gravityScale = 0f;

            var collider = AddComponentIfMissing<BoxCollider2D>(sensorObject);
            collider.isTrigger = true;
            collider.enabled = true;
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

            var portal = AddComponentIfMissing<RoomPortalTrigger>(sensorObject);
            portal.Configure(triggerId, KeyCode.None, false);
            portal.SetPortalEnabled(true);
        }

        private static void ConfigureRoomGate(GameObject gateObject, Vector3 localPosition, Vector2 size)
        {
            if (gateObject == null)
            {
                return;
            }

            ConfigureArenaSolid(
                gateObject,
                new Color(0.88f, 0.34f, 0.24f, 0.96f),
                size,
                -24,
                localPosition,
                activePresentationBindings != null ? activePresentationBindings.BorderSprite : null);

            var gate = AddComponentIfMissing<RoomGateController>(gateObject);
            gate.Configure(
                false,
                new Color(0.88f, 0.34f, 0.24f, 0.96f),
                new Color(0.32f, 0.9f, 0.62f, 0.24f));
        }

        private static void CreateWaveSpawnPoint(Transform root, string name, Vector3 localPosition)
        {
            var spawn = FindOrCreateChild(root, name);
            spawn.transform.localPosition = localPosition;
            spawn.transform.localRotation = Quaternion.identity;
            spawn.transform.localScale = Vector3.one;
        }

        private static void ConfigureArenaVisual(
            GameObject target,
            Color color,
            Vector2 size,
            int sortingOrder,
            Vector3 localPosition,
            Sprite spriteOverride = null)
        {
            var renderer = AddComponentIfMissing<SpriteRenderer>(target);
            if (spriteOverride != null)
            {
                renderer.sprite = spriteOverride;
            }
            else if (renderer.sprite == null)
            {
                renderer.sprite = GetRuntimeWhiteSprite();
            }

            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static void ConfigureArenaSolid(
            GameObject target,
            Color color,
            Vector2 size,
            int sortingOrder,
            Vector3 localPosition,
            Sprite spriteOverride = null)
        {
            target.layer = 0;
            if (spriteOverride == null && activePresentationBindings != null)
            {
                if (target.transform.parent != null && target.transform.parent.name == "ArenaObstacles")
                {
                    spriteOverride = activePresentationBindings.ObstacleSprite;
                }
                else if (target.name.StartsWith("Border", StringComparison.Ordinal))
                {
                    spriteOverride = activePresentationBindings.BorderSprite;
                }
            }

            ConfigureArenaVisual(target, color, size, sortingOrder, localPosition, spriteOverride);
            var rigidbody = AddComponentIfMissing<Rigidbody2D>(target);
            rigidbody.bodyType = RigidbodyType2D.Static;
            rigidbody.simulated = true;
            rigidbody.gravityScale = 0f;
            var collider = AddComponentIfMissing<BoxCollider2D>(target);
            collider.isTrigger = false;
            collider.enabled = true;
            collider.offset = Vector2.zero;
            collider.size = Vector2.one;
        }

        private static void ConfigureArenaDecorative(
            GameObject target,
            Color color,
            Vector2 size,
            int sortingOrder,
            Vector3 localPosition,
            Sprite spriteOverride = null,
            bool hideRenderer = false)
        {
            target.layer = 0;
            if (spriteOverride == null && activePresentationBindings != null)
            {
                if (target.transform.parent != null && target.transform.parent.name == "ArenaObstacles")
                {
                    spriteOverride = activePresentationBindings.ObstacleSprite;
                }
                else if (target.name.StartsWith("Border", StringComparison.Ordinal))
                {
                    spriteOverride = activePresentationBindings.BorderSprite;
                }
            }

            ConfigureArenaVisual(target, color, size, sortingOrder, localPosition, spriteOverride);
            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = !hideRenderer;
            }

            var rigidbody = target.GetComponent<Rigidbody2D>();
            if (rigidbody != null)
            {
                rigidbody.simulated = false;
            }

            var colliders = target.GetComponents<Collider2D>();
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private static string DescribeCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return "null";
            }

            return collider.GetType().Name + "(trigger=" + collider.isTrigger + ", enabled=" + collider.enabled + ")";
        }

        private static string DescribeBody(Rigidbody2D rigidbody)
        {
            if (rigidbody == null)
            {
                return "null";
            }

            return rigidbody.bodyType + "(simulated=" + rigidbody.simulated + ")";
        }

        private static void EnsureMainCamera()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Object.FindObjectOfType<Camera>();
            }

            if (targetCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                targetCamera = cameraObject.AddComponent<Camera>();
                if (cameraObject.GetComponent<AudioListener>() == null)
                {
                    cameraObject.AddComponent<AudioListener>();
                }
            }

            var safeAspect = targetCamera.aspect > 0.01f ? targetCamera.aspect : 16f / 9f;
            var targetHalfHeight = StartAreaSize.y * 0.5f + 2.2f;
            var targetHalfWidth = StartAreaSize.x * 0.5f + 4f;
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = Mathf.Max(targetHalfHeight, targetHalfWidth / safeAspect);
            targetCamera.transform.position = new Vector3(StartAreaCenter.x, StartAreaCenter.y, -10f);
            targetCamera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            AddComponentIfMissing<CameraShakeFeedback>(targetCamera.gameObject);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static Canvas EnsureCanvas(Transform parent)
        {
            var canvasObject = FindOrCreateUiChild(parent, "UIRoot");
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;
            var canvas = AddComponentIfMissing<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = AddComponentIfMissing<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            AddComponentIfMissing<GraphicRaycaster>(canvasObject);
            return canvas;
        }

        private static Image EnsureTransitionOverlay(Transform canvasTransform)
        {
            var overlay = FindOrCreateUiChild(canvasTransform, "TransitionOverlay");
            overlay.transform.SetAsLastSibling();
            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = AddComponentIfMissing<Image>(overlay);
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            return image;
        }

        private static BattleHudController EnsureBattleHud(Transform canvasTransform, ProcedureManager manager)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "BattleHudPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0f);

            var bossCard = FindOrCreateUiChild(panel.transform, "BossHudCard");
            var bossCardRect = bossCard.GetComponent<RectTransform>();
            bossCardRect.anchorMin = new Vector2(0.5f, 1f);
            bossCardRect.anchorMax = new Vector2(0.5f, 1f);
            bossCardRect.pivot = new Vector2(0.5f, 1f);
            bossCardRect.anchoredPosition = new Vector2(0f, -14f);
            bossCardRect.sizeDelta = new Vector2(860f, 122f);
            var bossCardImage = AddComponentIfMissing<Image>(bossCard);
            bossCardImage.color = new Color(0f, 0f, 0f, 0.56f);
            if (activePresentationBindings != null && activePresentationBindings.HudPanelSprite != null)
            {
                bossCardImage.sprite = activePresentationBindings.HudPanelSprite;
                bossCardImage.type = Image.Type.Sliced;
            }

            var bossText = EnsureText(bossCard.transform, "BossHealthText", new Vector2(20f, -14f), 22, TextAnchor.UpperLeft);
            var bossDangerText = EnsureText(bossCard.transform, "BossDangerText", new Vector2(20f, -42f), 20, TextAnchor.UpperLeft);
            bossDangerText.color = new Color(1f, 0.96f, 0.84f, 0.74f);

            var bossBarBackground = EnsureImage(
                bossCard.transform,
                "BossHealthBarBackground",
                new Vector2(20f, -76f),
                new Vector2(820f, 22f),
                new Color(0.12f, 0.12f, 0.12f, 0.92f),
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            var bossBarFill = EnsureImage(
                bossBarBackground.transform,
                "Fill",
                Vector2.zero,
                Vector2.zero,
                new Color(0.95f, 0.3f, 0.24f, 1f),
                true,
                activePresentationBindings != null ? activePresentationBindings.HudBarFillSprite : null);

            var playerCard = FindOrCreateUiChild(panel.transform, "PlayerHudCard");
            var playerCardRect = playerCard.GetComponent<RectTransform>();
            playerCardRect.anchorMin = new Vector2(0f, 0f);
            playerCardRect.anchorMax = new Vector2(0f, 0f);
            playerCardRect.pivot = new Vector2(0f, 0f);
            playerCardRect.anchoredPosition = new Vector2(16f, 16f);
            playerCardRect.sizeDelta = new Vector2(470f, 172f);
            var playerCardImage = AddComponentIfMissing<Image>(playerCard);
            playerCardImage.color = new Color(0f, 0f, 0f, 0.54f);
            if (activePresentationBindings != null && activePresentationBindings.HudPanelSprite != null)
            {
                playerCardImage.sprite = activePresentationBindings.HudPanelSprite;
                playerCardImage.type = Image.Type.Sliced;
            }

            var procedureText = EnsureText(playerCard.transform, "ProcedureText", new Vector2(16f, -12f), 18, TextAnchor.UpperLeft);
            var timerText = EnsureText(playerCard.transform, "TimerText", new Vector2(16f, -38f), 20, TextAnchor.UpperLeft);
            var playerText = EnsureText(playerCard.transform, "PlayerHealthText", new Vector2(16f, -68f), 21, TextAnchor.UpperLeft);

            var playerBarBackground = EnsureImage(
                playerCard.transform,
                "PlayerHealthBarBackground",
                new Vector2(16f, -102f),
                new Vector2(436f, 18f),
                new Color(0.12f, 0.12f, 0.12f, 0.9f),
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            var playerBarFill = EnsureImage(
                playerBarBackground.transform,
                "Fill",
                Vector2.zero,
                Vector2.zero,
                new Color(0.28f, 0.86f, 0.45f, 1f),
                true,
                activePresentationBindings != null ? activePresentationBindings.HudBarFillSprite : null);

            var dodgeIconBackground = EnsureImage(
                playerCard.transform,
                "DodgeIconBackground",
                new Vector2(16f, -128f),
                new Vector2(34f, 34f),
                new Color(0.12f, 0.12f, 0.12f, 0.9f),
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            var dodgeIcon = EnsureImage(
                dodgeIconBackground.transform,
                "DodgeIcon",
                Vector2.zero,
                Vector2.zero,
                new Color(0.3f, 0.9f, 1f, 1f),
                true,
                activePresentationBindings != null ? activePresentationBindings.DodgeSkillIconSprite : null);
            dodgeIcon.type = Image.Type.Simple;
            dodgeIcon.fillAmount = 1f;

            var dodgeText = EnsureText(playerCard.transform, "DodgeText", new Vector2(58f, -126f), 18, TextAnchor.UpperLeft);
            var dodgeCooldownBackground = EnsureImage(
                playerCard.transform,
                "DodgeCooldownBackground",
                new Vector2(58f, -150f),
                new Vector2(220f, 12f),
                new Color(0.12f, 0.12f, 0.12f, 0.9f),
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            var dodgeCooldownFill = EnsureImage(
                dodgeCooldownBackground.transform,
                "Fill",
                Vector2.zero,
                Vector2.zero,
                new Color(0.3f, 0.9f, 1f, 1f),
                true,
                activePresentationBindings != null ? activePresentationBindings.HudBarFillSprite : null);

            var hpBarFill = default(Image);
            var armorBarFill = default(Image);
            var energyBarFill = default(Image);
            EnsurePlayerResourceBars(canvasTransform, out hpBarFill, out armorBarFill, out energyBarFill);

            var controller = AddComponentIfMissing<BattleHudController>(panel);
            controller.BindView(
                panel,
                procedureText,
                playerText,
                bossText,
                timerText,
                playerBarFill,
                bossBarFill,
                dodgeText,
                dodgeCooldownFill,
                dodgeIcon,
                bossDangerText);
            controller.BindResourceBars(hpBarFill, armorBarFill, energyBarFill);
            controller.Configure(manager, null, null);
            return controller;
        }

        private static void EnsurePlayerResourceBars(
            Transform canvasTransform,
            out Image hpBarFill,
            out Image armorBarFill,
            out Image energyBarFill)
        {
            var playerHud = FindOrCreateUiChild(canvasTransform, "PlayerHUD");
            var playerHudRect = playerHud.GetComponent<RectTransform>();
            playerHudRect.anchorMin = new Vector2(0f, 1f);
            playerHudRect.anchorMax = new Vector2(0f, 1f);
            playerHudRect.pivot = new Vector2(0f, 1f);
            playerHudRect.anchoredPosition = new Vector2(16f, -16f);
            playerHudRect.sizeDelta = new Vector2(312f, 98f);

            hpBarFill = EnsurePlayerResourceBar(
                playerHud.transform,
                "HPBarRoot",
                "HPBarBg",
                "HPBarFill",
                new Vector2(0f, 0f),
                new Color(0.1f, 0.1f, 0.12f, 0.9f),
                new Color(0.26f, 0.9f, 0.45f, 1f));
            armorBarFill = EnsurePlayerResourceBar(
                playerHud.transform,
                "ArmorBarRoot",
                "ArmorBarBg",
                "ArmorBarFill",
                new Vector2(0f, -32f),
                new Color(0.1f, 0.1f, 0.12f, 0.9f),
                new Color(0.3f, 0.66f, 0.98f, 1f));
            energyBarFill = EnsurePlayerResourceBar(
                playerHud.transform,
                "EnergyBarRoot",
                "EnergyBarBg",
                "EnergyBarFill",
                new Vector2(0f, -64f),
                new Color(0.1f, 0.1f, 0.12f, 0.9f),
                new Color(1f, 0.84f, 0.28f, 1f));
        }

        private static Image EnsurePlayerResourceBar(
            Transform playerHudRoot,
            string rootName,
            string backgroundName,
            string fillName,
            Vector2 anchoredTopLeft,
            Color backgroundColor,
            Color fillColor)
        {
            var barRoot = FindOrCreateUiChild(playerHudRoot, rootName);
            var rootRect = barRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = anchoredTopLeft;
            rootRect.sizeDelta = new Vector2(296f, 20f);

            var background = EnsureImage(
                barRoot.transform,
                backgroundName,
                Vector2.zero,
                new Vector2(296f, 20f),
                backgroundColor,
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            background.raycastTarget = false;

            var fill = EnsureImage(
                barRoot.transform,
                fillName,
                Vector2.zero,
                Vector2.zero,
                fillColor,
                true,
                activePresentationBindings != null ? activePresentationBindings.HudBarFillSprite : null);
            fill.raycastTarget = false;
            return fill;
        }

        private static ResultPanelController EnsureResultPanel(Transform canvasTransform, ProcedureManager manager)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "ResultPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0.62f);

            var content = FindOrCreateUiChild(panel.transform, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(360f, 220f);

            var contentImage = AddComponentIfMissing<Image>(content);
            contentImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var title = EnsureText(content.transform, "TitleText", new Vector2(0f, -26f), 34, TextAnchor.UpperCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 44f);

            var detail = EnsureText(content.transform, "DetailText", new Vector2(0f, -88f), 22, TextAnchor.UpperCenter);
            var detailRect = detail.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 1f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.sizeDelta = new Vector2(0f, 34f);

            var button = EnsureButton(content.transform, "BackToMenuButton", new Vector2(0f, 30f), new Vector2(180f, 42f), "Back To Menu");
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 24f);

            var controller = AddComponentIfMissing<ResultPanelController>(panel);
            controller.BindView(panel, title, detail, button, contentRect);
            controller.Configure(manager);
            return controller;
        }

        private static BattlePausePanelController EnsurePausePanel(Transform canvasTransform, ProcedureManager manager)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "PausePanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0.5f);

            var content = FindOrCreateUiChild(panel.transform, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(340f, 250f);

            var contentImage = AddComponentIfMissing<Image>(content);
            contentImage.color = new Color(0.09f, 0.11f, 0.14f, 0.95f);

            var title = EnsureText(content.transform, "TitleText", new Vector2(0f, -24f), 32, TextAnchor.UpperCenter);
            title.text = "Paused";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 42f);

            var continueButton = EnsureButton(content.transform, "ContinueButton", new Vector2(0f, 50f), new Vector2(210f, 42f), "Continue");
            var restartButton = EnsureButton(content.transform, "RestartButton", new Vector2(0f, -2f), new Vector2(210f, 42f), "Restart Battle");
            var backButton = EnsureButton(content.transform, "BackToMenuButton", new Vector2(0f, -54f), new Vector2(210f, 42f), "Back To Menu");

            var controller = AddComponentIfMissing<BattlePausePanelController>(panel);
            controller.BindView(panel, continueButton, restartButton, backButton);
            controller.Configure(manager);
            return controller;
        }

        private static MenuPresetPanelController EnsureMenuPanel(
            Transform canvasTransform,
            ProcedureManager manager,
            BossPresetController presetController)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "MenuPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -12f);
            rect.sizeDelta = new Vector2(340f, 210f);

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0.52f);

            var title = EnsureText(panel.transform, "TitleText", new Vector2(14f, -10f), 18, TextAnchor.UpperLeft);
            title.text = "Boss Preset";
            title.fontStyle = FontStyle.Bold;

            var presetText = EnsureText(panel.transform, "PresetNameText", new Vector2(14f, -40f), 20, TextAnchor.UpperLeft);
            presetText.text = "Boss Preset: Normal";

            var normalButton = EnsureButton(panel.transform, "NormalButton", new Vector2(-76f, -8f), new Vector2(140f, 38f), "Normal");
            var frenzyButton = EnsureButton(panel.transform, "FrenzyButton", new Vector2(76f, -8f), new Vector2(140f, 38f), "Frenzy");
            var startButton = EnsureButton(panel.transform, "StartBattleButton", new Vector2(0f, -62f), new Vector2(296f, 42f), "Start Battle");

            var controller = AddComponentIfMissing<MenuPresetPanelController>(panel);
            controller.BindView(panel, presetText, normalButton, frenzyButton, startButton);
            controller.Configure(manager, presetController);
            return controller;
        }

        private static RoleSelectionPanelController EnsureRoleSelectionPanel(Transform canvasTransform, ProcedureManager manager)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "RoleSelectionPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(560f, 470f);

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0.62f);
            if (activePresentationBindings != null && activePresentationBindings.HudPanelSprite != null)
            {
                panelImage.sprite = activePresentationBindings.HudPanelSprite;
                panelImage.type = Image.Type.Sliced;
            }

            var title = EnsureText(panel.transform, "TitleText", new Vector2(16f, -12f), 24, TextAnchor.UpperLeft);
            title.text = "Role Selection";
            title.fontStyle = FontStyle.Bold;

            var roleButtonA = EnsureButton(panel.transform, "RoleButtonA", new Vector2(-122f, 170f), new Vector2(172f, 36f), "Ranger");
            var roleButtonB = EnsureButton(panel.transform, "RoleButtonB", new Vector2(64f, 170f), new Vector2(172f, 36f), "Engineer");

            var roleDisplayFrame = EnsureImage(
                panel.transform,
                "RoleDisplayFrame",
                new Vector2(16f, -84f),
                new Vector2(156f, 156f),
                new Color(0.1f, 0.1f, 0.12f, 0.95f),
                false,
                activePresentationBindings != null ? activePresentationBindings.HudBarBackgroundSprite : null);
            var roleDisplayImage = EnsureImage(
                roleDisplayFrame.transform,
                "RoleDisplayImage",
                Vector2.zero,
                Vector2.zero,
                new Color(0.28f, 0.62f, 0.92f, 0.96f),
                true,
                activePresentationBindings != null ? activePresentationBindings.PlayerSprite : null);
            roleDisplayImage.type = Image.Type.Simple;

            var roleName = EnsureText(panel.transform, "RoleNameText", new Vector2(188f, -84f), 24, TextAnchor.UpperLeft);
            var redHealth = EnsureText(panel.transform, "RedHealthText", new Vector2(188f, -118f), 20, TextAnchor.UpperLeft);
            var blueArmor = EnsureText(panel.transform, "BlueArmorText", new Vector2(188f, -146f), 20, TextAnchor.UpperLeft);
            var energy = EnsureText(panel.transform, "EnergyText", new Vector2(188f, -174f), 20, TextAnchor.UpperLeft);

            var skillName = EnsureText(panel.transform, "SkillNameText", new Vector2(16f, -252f), 20, TextAnchor.UpperLeft);
            var skillDesc = EnsureText(panel.transform, "SkillDescriptionText", new Vector2(16f, -278f), 18, TextAnchor.UpperLeft);
            skillDesc.rectTransform.sizeDelta = new Vector2(0f, 62f);

            var primaryWeapon = EnsureText(panel.transform, "PrimaryWeaponText", new Vector2(16f, -336f), 17, TextAnchor.UpperLeft);
            primaryWeapon.rectTransform.sizeDelta = new Vector2(0f, 46f);
            var secondaryWeapon = EnsureText(panel.transform, "SecondaryWeaponText", new Vector2(16f, -382f), 17, TextAnchor.UpperLeft);
            secondaryWeapon.rectTransform.sizeDelta = new Vector2(0f, 46f);

            var statusText = EnsureText(panel.transform, "StatusText", new Vector2(16f, -430f), 16, TextAnchor.UpperLeft);
            statusText.color = new Color(0.84f, 0.92f, 1f, 0.95f);

            var confirmButton = EnsureButton(panel.transform, "ConfirmRoleButton", new Vector2(202f, -206f), new Vector2(190f, 42f), "Confirm Role");
            var confirmButtonRect = confirmButton.GetComponent<RectTransform>();
            confirmButtonRect.anchorMin = new Vector2(1f, 0f);
            confirmButtonRect.anchorMax = new Vector2(1f, 0f);
            confirmButtonRect.pivot = new Vector2(1f, 0f);
            confirmButtonRect.anchoredPosition = new Vector2(-16f, 16f);

            var roleButtonALabel = roleButtonA.transform.Find("Label") != null
                ? roleButtonA.transform.Find("Label").GetComponent<Text>()
                : null;
            var roleButtonBLabel = roleButtonB.transform.Find("Label") != null
                ? roleButtonB.transform.Find("Label").GetComponent<Text>()
                : null;
            var confirmLabel = confirmButton.transform.Find("Label") != null
                ? confirmButton.transform.Find("Label").GetComponent<Text>()
                : null;

            var controller = AddComponentIfMissing<RoleSelectionPanelController>(panel);
            controller.BindView(
                panel,
                roleDisplayImage,
                roleName,
                redHealth,
                blueArmor,
                energy,
                skillName,
                skillDesc,
                primaryWeapon,
                secondaryWeapon,
                statusText,
                confirmButton,
                confirmLabel,
                roleButtonA,
                roleButtonALabel,
                roleButtonB,
                roleButtonBLabel);
            controller.Configure(manager, null, null);
            return controller;
        }

        private static AudioSource EnsureAudioSource(Transform parent, string name, bool loop)
        {
            var sourceObject = FindOrCreateChild(parent, name);
            var source = AddComponentIfMissing<AudioSource>(sourceObject);
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private static void EnsureCombatSprite(GameObject target, Color tint, float uniformScale, int sortingOrder, Sprite spriteOverride = null)
        {
            var renderer = AddComponentIfMissing<SpriteRenderer>(target);
            if (spriteOverride != null)
            {
                renderer.sprite = spriteOverride;
            }
            else if (renderer.sprite == null)
            {
                renderer.sprite = GetRuntimeWhiteSprite();
            }

            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            target.transform.localScale = Vector3.one * Mathf.Max(0.2f, uniformScale);
        }

        private static Text EnsureText(Transform parent, string name, Vector2 anchoredTopLeft, int fontSize, TextAnchor alignment)
        {
            var textObject = FindOrCreateUiChild(parent, name);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredTopLeft;
            rect.sizeDelta = new Vector2(-20f, 24f);

            var text = AddComponentIfMissing<Text>(textObject);
            var runtimeFont = GetRuntimeUiFont();
            if (runtimeFont != null)
            {
                text.font = runtimeFont;
            }

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image EnsureImage(
            Transform parent,
            string name,
            Vector2 anchoredTopLeft,
            Vector2 size,
            Color color,
            bool asFill = false,
            Sprite spriteOverride = null)
        {
            var imageObject = FindOrCreateUiChild(parent, name);
            var rect = imageObject.GetComponent<RectTransform>();
            if (asFill)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = anchoredTopLeft;
                rect.sizeDelta = size;
            }

            var image = AddComponentIfMissing<Image>(imageObject);
            image.color = color;
            if (spriteOverride != null)
            {
                image.sprite = spriteOverride;
            }
            if (asFill)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 1f;
            }
            else
            {
                image.type = Image.Type.Simple;
            }

            return image;
        }

        private static Button EnsureButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string buttonLabel)
        {
            var buttonObject = FindOrCreateUiChild(parent, name);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = AddComponentIfMissing<Image>(buttonObject);
            image.color = new Color(0.2f, 0.45f, 0.28f, 0.95f);
            if (activePresentationBindings != null && activePresentationBindings.ButtonSprite != null)
            {
                image.sprite = activePresentationBindings.ButtonSprite;
                image.type = Image.Type.Simple;
            }

            var button = AddComponentIfMissing<Button>(buttonObject);
            var label = EnsureText(buttonObject.transform, "Label", new Vector2(0f, -6f), 20, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = buttonLabel;
            return button;
        }

        private static void EnsureAudioBinding(List<AudioClipBindingEntry> list, int soundId)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].soundId == soundId)
                {
                    return;
                }
            }

            list.Add(new AudioClipBindingEntry
            {
                soundId = soundId,
                clip = null,
                volume = 1f,
            });
        }

        private static GameObject FindOrCreateRoot()
        {
            GameObject root = null;

            if (GameEntry.Instance != null)
            {
                root = GameEntry.Instance.gameObject;
            }

            if (root == null)
            {
                var existingEntry = UnityEngine.Object.FindObjectOfType<GameEntry>();
                if (existingEntry != null)
                {
                    root = existingEntry.gameObject;
                }
            }

            if (root == null)
            {
                root = GameObject.Find(RootName);
            }

            if (root == null)
            {
                root = new GameObject(RootName);
            }

            root.name = RootName;
            return root;
        }

        private static GameObject FindOrReuseCharacter<T>(Transform parent, string fallbackName) where T : Component
        {
            var child = parent.Find(fallbackName);
            if (child != null)
            {
                return child.gameObject;
            }

            var existing = UnityEngine.Object.FindObjectOfType<T>();
            if (existing != null)
            {
                var go = existing.gameObject;
                go.name = fallbackName;
                if (go.transform.parent != parent)
                {
                    go.transform.SetParent(parent, true);
                }

                return go;
            }

            return FindOrCreateChild(parent, fallbackName);
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject;
        }

        private static GameObject FindOrCreateUiChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(childName, typeof(RectTransform));
            childObject.transform.SetParent(parent);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject;
        }

        private static void RemoveChildrenExcept(Transform parent, params string[] keepNames)
        {
            if (parent == null)
            {
                return;
            }

            var keepSet = new HashSet<string>(keepNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null || keepSet.Contains(child.name))
                {
                    continue;
                }

                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static Transform FindOrCreateFirePoint(Transform parent, string name, Vector3 localPosition)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                existing.localPosition = localPosition;
                return existing;
            }

            var firePoint = new GameObject(name).transform;
            firePoint.SetParent(parent);
            firePoint.localPosition = localPosition;
            firePoint.localRotation = Quaternion.identity;
            firePoint.localScale = Vector3.one;
            return firePoint;
        }

        private static T AddComponentIfMissing<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }

        private static Font GetRuntimeUiFont()
        {
            if (runtimeUiFont != null)
            {
                return runtimeUiFont;
            }

            try
            {
                runtimeUiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception exception)
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] Failed to load built-in font LegacyRuntime.ttf.\n" + exception);
            }

            if (runtimeUiFont != null)
            {
                return runtimeUiFont;
            }

            try
            {
                runtimeUiFont = Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Arial",
                        "Segoe UI",
                        "Microsoft YaHei UI",
                        "Microsoft YaHei",
                    },
                    16);
            }
            catch (Exception exception)
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] Failed to create dynamic OS fallback font.\n" + exception);
            }

            if (runtimeUiFont == null)
            {
                Debug.LogError("[BossRushRuntimeSceneBuilder] Runtime UI font is unavailable. UI text will not render correctly.");
            }

            return runtimeUiFont;
        }

        private static Sprite GetRuntimeWhiteSprite()
        {
            if (runtimeWhiteSprite != null)
            {
                return runtimeWhiteSprite;
            }

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply(false, false);
            runtimeWhiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            runtimeWhiteSprite.name = "RuntimeWhiteSprite";
            return runtimeWhiteSprite;
        }
    }
}
