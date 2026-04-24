using System.Collections.Generic;
using System.IO;
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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain.GameLogic.Tools.Editor
{
    /// <summary>
    /// Editor helper to create a minimum playable 2D boss rush scene setup.
    /// </summary>
    public static class BossRushSceneBootstrapper
    {
        private const string GeneratedRootFolder = "Assets/GameMain/Generated";
        private const string GeneratedPrefabFolder = "Assets/GameMain/Generated/Prefabs";
        private const string GeneratedDataFolder = "Assets/GameMain/Generated/Data";
        private const string DefaultProjectilePrefabPath = GeneratedPrefabFolder + "/DefaultProjectile2D.prefab";
        private const string DefaultPlayerStatsPath = GeneratedDataFolder + "/PlayerStats_Default.asset";
        private const string DefaultPlayerWeaponStatsPath = GeneratedDataFolder + "/PlayerWeaponStats_Default.asset";
        private const string DefaultBossStatsPath = GeneratedDataFolder + "/BossStats_Default.asset";
        private const string DefaultBossWeaponStatsPath = GeneratedDataFolder + "/BossWeaponStats_Default.asset";
        private const string DefaultBattleConfigPath = GeneratedDataFolder + "/BattleConfig_Default.asset";
        private const string DefaultAudioBindingsPath = GeneratedDataFolder + "/AudioClipBindings_Default.asset";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.scene";

        private struct BootstrapData
        {
            public PlayerStatsData PlayerStats;
            public WeaponStatsData PlayerWeaponStats;
            public BossStatsData BossStats;
            public WeaponStatsData BossWeaponStats;
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
        }

        [MenuItem("Tools/GameMain/Bootstrap 2D Boss Rush Scene")]
        public static void BootstrapScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("Open a scene before running bootstrap.");
                return;
            }

            BootstrapSceneInternal(scene, false);
        }

        [MenuItem("Tools/GameMain/Bootstrap SampleScene And Save")]
        public static void BootstrapSampleSceneAndSave()
        {
            if (!File.Exists(SampleScenePath))
            {
                Debug.LogError("Scene not found: " + SampleScenePath);
                return;
            }

            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            BootstrapSceneInternal(scene, true);
        }

        public static void BootstrapActiveSceneAndSave()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("No active scene to bootstrap.");
                return;
            }

            BootstrapSceneInternal(scene, true);
        }

        private static void BootstrapSceneInternal(Scene scene, bool saveScene)
        {
            EnsureGeneratedFolders();
            EnsureMainCamera();
            EnsureEventSystem();

            var data = EnsureDefaultDataAssets();
            var projectilePrefab = EnsureProjectilePrefab();
            var root = FindOrCreateRoot("GameMainRoot");
            SetupBattleEnvironment(root.transform);
            var entry = SetupEntryRoot(root);
            var pool = SetupProjectilePool(root.transform, projectilePrefab);
            SetupDamageTextSpawner(root.transform);
            SetupImpactEffectSpawner(root.transform);
            var combat = SetupCombat(root.transform, projectilePrefab, pool);
            var audioService = SetupAudio(root.transform, data.AudioBindings, entry.Manager);
            var ui = SetupUi(root.transform, entry.Manager);
            SetupRuntimeHooks(root, entry, combat, ui, pool, audioService, data);

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            Selection.activeGameObject = root;
            Debug.Log("Boss Rush bootstrap done. Play scene and press Space/Enter to start battle.");
        }

        [MenuItem("Tools/GameMain/Apply Runtime Hooks Data")]
        public static void ApplyRuntimeHooksData()
        {
            var hooks = Object.FindObjectOfType<RuntimeSceneHooks>();
            if (hooks == null)
            {
                Debug.LogWarning("RuntimeSceneHooks not found in current scene.");
                return;
            }

            var data = EnsureDefaultDataAssets();
            hooks.SetDataAssets(
                data.PlayerStats,
                data.PlayerWeaponStats,
                data.BossStats,
                data.BossWeaponStats,
                data.BattleConfig);
            hooks.SetAudioClipBindings(data.AudioBindings);
            hooks.AutoBindSceneReferences();
            hooks.Apply();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("RuntimeSceneHooks data applied.");
        }

        private static EntryRefs SetupEntryRoot(GameObject root)
        {
            AddComponentIfMissing<GameEntry>(root);
            var manager = AddComponentIfMissing<ProcedureManager>(root);
            AddComponentIfMissing<ProcedureLaunch>(root);
            AddComponentIfMissing<ProcedureMenu>(root);
            var battle = AddComponentIfMissing<ProcedureBattle>(root);
            var result = AddComponentIfMissing<ProcedureResult>(root);
            AddComponentIfMissing<DebugPanelController>(root);

            return new EntryRefs
            {
                Manager = manager,
                Battle = battle,
                Result = result,
            };
        }

        private static CombatRefs SetupCombat(Transform parent, Projectile projectilePrefab, ProjectilePool pool)
        {
            var player = FindOrCreateChild(parent, "Player");
            player.transform.position = new Vector3(-3f, 0f, 0f);
            EnsureCombatSprite(player, new Color(0.35f, 0.92f, 1f, 1f), 0.95f, 12);

            var playerRigidbody = AddComponentIfMissing<Rigidbody2D>(player);
            playerRigidbody.gravityScale = 0f;
            playerRigidbody.freezeRotation = true;

            var playerCollider = AddComponentIfMissing<CircleCollider2D>(player);
            playerCollider.isTrigger = false;
            playerCollider.radius = 0.45f;

            var playerController = AddComponentIfMissing<PlayerController>(player);
            var playerHealth = AddComponentIfMissing<PlayerHealth>(player);
            playerHealth.SetTeam(CombatTeam.Player);
            AddComponentIfMissing<HitFlashFeedback>(player);
            AddComponentIfMissing<DeathFadeFeedback>(player);
            var playerWeapon = AddComponentIfMissing<WeaponController>(player);
            playerWeapon.SetOwnerTeam(CombatTeam.Player);
            playerWeapon.SetProjectilePrefab(projectilePrefab);
            playerWeapon.SetProjectilePool(pool);
            playerWeapon.SetProjectileSpawnPoint(FindOrCreateFirePoint(player.transform, "PlayerFirePoint", new Vector3(0.6f, 0f, 0f)));

            var boss = FindOrCreateChild(parent, "Boss");
            boss.transform.position = new Vector3(3f, 0f, 0f);
            EnsureCombatSprite(boss, new Color(1f, 0.56f, 0.24f, 1f), 1.15f, 11);

            var bossRigidbody = AddComponentIfMissing<Rigidbody2D>(boss);
            bossRigidbody.gravityScale = 0f;
            bossRigidbody.freezeRotation = true;

            var bossCollider = AddComponentIfMissing<CircleCollider2D>(boss);
            bossCollider.isTrigger = false;
            bossCollider.radius = 0.55f;

            var bossController = AddComponentIfMissing<BossController>(boss);
            var bossHealth = AddComponentIfMissing<BossHealth>(boss);
            bossHealth.SetTeam(CombatTeam.Boss);
            AddComponentIfMissing<HitFlashFeedback>(boss);
            AddComponentIfMissing<DeathFadeFeedback>(boss);
            var bossWeapon = AddComponentIfMissing<WeaponController>(boss);
            bossWeapon.SetOwnerTeam(CombatTeam.Boss);
            bossWeapon.SetProjectilePrefab(projectilePrefab);
            bossWeapon.SetProjectilePool(pool);
            bossWeapon.SetProjectileSpawnPoint(FindOrCreateFirePoint(boss.transform, "BossFirePoint", new Vector3(-0.8f, 0f, 0f)));
            var bossBrain = AddComponentIfMissing<BossBrain>(boss);
            bossBrain.SetTargetPlayer(playerHealth);

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
            return spawner;
        }

        private static ProjectilePool SetupProjectilePool(Transform parent, Projectile projectilePrefab)
        {
            var poolObject = FindOrCreateChild(parent, "ProjectilePool");
            var pool = AddComponentIfMissing<ProjectilePool>(poolObject);
            pool.SetProjectilePrefab(projectilePrefab);
            pool.ConfigureCapacity(24, 8, 256);
            return pool;
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

        private static UiRefs SetupUi(Transform parent, ProcedureManager manager)
        {
            var canvas = EnsureCanvas(parent);
            var hud = EnsureBattleHud(canvas.transform, manager);
            var result = EnsureResultPanel(canvas.transform, manager);
            return new UiRefs
            {
                Hud = hud,
                ResultPanel = result,
            };
        }

        private static void SetupRuntimeHooks(
            GameObject root,
            EntryRefs entry,
            CombatRefs combat,
            UiRefs ui,
            ProjectilePool pool,
            AudioService audioService,
            BootstrapData data)
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
            hooks.SetDataAssets(
                data.PlayerStats,
                data.PlayerWeaponStats,
                data.BossStats,
                data.BossWeaponStats,
                data.BattleConfig);
            hooks.SetAudioClipBindings(data.AudioBindings);
            hooks.Apply();
        }

        private static BootstrapData EnsureDefaultDataAssets()
        {
            EnsureGeneratedFolders();

            var playerStats = LoadOrCreateAsset<PlayerStatsData>(DefaultPlayerStatsPath);
            var playerWeaponStats = LoadOrCreateAsset<WeaponStatsData>(DefaultPlayerWeaponStatsPath);
            var bossStats = LoadOrCreateAsset<BossStatsData>(DefaultBossStatsPath);
            var bossWeaponStats = LoadOrCreateAsset<WeaponStatsData>(DefaultBossWeaponStatsPath);
            var battleConfig = LoadOrCreateAsset<BattleConfigData>(DefaultBattleConfigPath);
            var audioBindings = LoadOrCreateAsset<AudioClipBindings>(DefaultAudioBindingsPath);

            playerWeaponStats.ownerTeam = CombatTeam.Player;
            bossWeaponStats.ownerTeam = CombatTeam.Boss;
            if (bossStats.fanShotCount < 1)
            {
                bossStats.fanShotCount = 3;
            }

            if (bossStats.fanSkillInterval <= 0f)
            {
                bossStats.fanSkillInterval = 4.5f;
            }

            if (battleConfig.battleTimeLimit <= 0f)
            {
                battleConfig.battleTimeLimit = 90f;
            }

            battleConfig.autoEnterBattleOnPlay = false;

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

            EditorUtility.SetDirty(playerWeaponStats);
            EditorUtility.SetDirty(bossWeaponStats);
            EditorUtility.SetDirty(bossStats);
            EditorUtility.SetDirty(battleConfig);
            EditorUtility.SetDirty(audioBindings);
            AssetDatabase.SaveAssets();

            return new BootstrapData
            {
                PlayerStats = playerStats,
                PlayerWeaponStats = playerWeaponStats,
                BossStats = bossStats,
                BossWeaponStats = bossWeaponStats,
                BattleConfig = battleConfig,
                AudioBindings = audioBindings,
            };
        }

        private static Projectile EnsureProjectilePrefab()
        {
            EnsureGeneratedFolders();

            var existing = AssetDatabase.LoadAssetAtPath<Projectile>(DefaultProjectilePrefabPath);
            if (existing != null)
            {
                var loaded = PrefabUtility.LoadPrefabContents(DefaultProjectilePrefabPath);
                UpgradeProjectileVisual(loaded);
                PrefabUtility.SaveAsPrefabAsset(loaded, DefaultProjectilePrefabPath);
                PrefabUtility.UnloadPrefabContents(loaded);
                AssetDatabase.SaveAssets();
                return AssetDatabase.LoadAssetAtPath<Projectile>(DefaultProjectilePrefabPath);
            }

            var template = new GameObject("DefaultProjectile2D");
            UpgradeProjectileVisual(template);

            var rigidbody = template.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            template.AddComponent<Projectile>();
            PrefabUtility.SaveAsPrefabAsset(template, DefaultProjectilePrefabPath);
            Object.DestroyImmediate(template);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Projectile>(DefaultProjectilePrefabPath);
        }

        private static void UpgradeProjectileVisual(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.transform.localScale = new Vector3(0.24f, 0.14f, 1f);

            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = target.AddComponent<SpriteRenderer>();
            }

            if (renderer.sprite == null)
            {
                renderer.sprite = GetBuiltinSprite();
            }

            renderer.color = new Color(1f, 0.95f, 0.8f, 1f);
            renderer.sortingOrder = 16;
        }

        private static void SetupBattleEnvironment(Transform parent)
        {
            var environmentRoot = FindOrCreateChild(parent, "Environment");
            environmentRoot.transform.localPosition = Vector3.zero;
            environmentRoot.transform.localRotation = Quaternion.identity;
            environmentRoot.transform.localScale = Vector3.one;
            environmentRoot.transform.SetSiblingIndex(0);

            var floor = FindOrCreateChild(environmentRoot.transform, "ArenaFloor");
            ConfigureArenaVisual(
                floor,
                new Color(0.12f, 0.14f, 0.18f, 1f),
                new Vector2(18f, 10f),
                -20,
                Vector3.zero);

            var centerMat = FindOrCreateChild(environmentRoot.transform, "ArenaCenter");
            ConfigureArenaVisual(
                centerMat,
                new Color(0.16f, 0.19f, 0.25f, 0.92f),
                new Vector2(12.8f, 7.2f),
                -19,
                Vector3.zero);

            var borderTop = FindOrCreateChild(environmentRoot.transform, "BorderTop");
            ConfigureArenaVisual(borderTop, new Color(0.22f, 0.66f, 0.82f, 0.95f), new Vector2(13.2f, 0.22f), -18, new Vector3(0f, 3.72f, 0f));
            var borderBottom = FindOrCreateChild(environmentRoot.transform, "BorderBottom");
            ConfigureArenaVisual(borderBottom, new Color(0.22f, 0.66f, 0.82f, 0.95f), new Vector2(13.2f, 0.22f), -18, new Vector3(0f, -3.72f, 0f));
            var borderLeft = FindOrCreateChild(environmentRoot.transform, "BorderLeft");
            ConfigureArenaVisual(borderLeft, new Color(0.22f, 0.66f, 0.82f, 0.95f), new Vector2(0.22f, 7.66f), -18, new Vector3(-6.6f, 0f, 0f));
            var borderRight = FindOrCreateChild(environmentRoot.transform, "BorderRight");
            ConfigureArenaVisual(borderRight, new Color(0.22f, 0.66f, 0.82f, 0.95f), new Vector2(0.22f, 7.66f), -18, new Vector3(6.6f, 0f, 0f));

            var decoTop = FindOrCreateChild(environmentRoot.transform, "DecoTop");
            ConfigureArenaVisual(decoTop, new Color(0.95f, 0.66f, 0.35f, 0.22f), new Vector2(4.6f, 0.38f), -17, new Vector3(0f, 3.2f, 0f));
            var decoBottom = FindOrCreateChild(environmentRoot.transform, "DecoBottom");
            ConfigureArenaVisual(decoBottom, new Color(0.95f, 0.66f, 0.35f, 0.22f), new Vector2(4.6f, 0.38f), -17, new Vector3(0f, -3.2f, 0f));
        }

        private static void ConfigureArenaVisual(GameObject target, Color color, Vector2 size, int sortingOrder, Vector3 localPosition)
        {
            var renderer = AddComponentIfMissing<SpriteRenderer>(target);
            if (renderer.sprite == null)
            {
                renderer.sprite = GetBuiltinSprite();
            }

            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static void EnsureMainCamera()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = Object.FindObjectOfType<Camera>();
            }

            if (targetCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
                targetCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = 6f;
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            AddComponentIfMissing<CameraShakeFeedback>(targetCamera.gameObject);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static Canvas EnsureCanvas(Transform parent)
        {
            var canvasObject = FindOrCreateUiChild(parent, "UIRoot");
            var canvas = AddComponentIfMissing<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddComponentIfMissing<CanvasScaler>(canvasObject).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            AddComponentIfMissing<GraphicRaycaster>(canvasObject);
            return canvas;
        }

        private static BattleHudController EnsureBattleHud(Transform canvasTransform, ProcedureManager manager)
        {
            var panel = FindOrCreateUiChild(canvasTransform, "BattleHudPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(320f, 126f);

            var panelImage = AddComponentIfMissing<Image>(panel);
            panelImage.color = new Color(0f, 0f, 0f, 0.45f);

            var procedureText = EnsureText(panel.transform, "ProcedureText", new Vector2(12f, -10f), 18, TextAnchor.UpperLeft);
            var playerText = EnsureText(panel.transform, "PlayerHealthText", new Vector2(12f, -38f), 20, TextAnchor.UpperLeft);
            var bossText = EnsureText(panel.transform, "BossHealthText", new Vector2(12f, -66f), 20, TextAnchor.UpperLeft);
            var timerText = EnsureText(panel.transform, "TimerText", new Vector2(12f, -94f), 20, TextAnchor.UpperLeft);

            var controller = AddComponentIfMissing<BattleHudController>(panel);
            controller.BindView(panel, procedureText, playerText, bossText, timerText);
            controller.Configure(manager, null, null);
            return controller;
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

        private static AudioSource EnsureAudioSource(Transform parent, string name, bool loop)
        {
            var sourceObject = FindOrCreateChild(parent, name);
            var source = AddComponentIfMissing<AudioSource>(sourceObject);
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private static void EnsureCombatSprite(GameObject target, Color tint, float uniformScale, int sortingOrder)
        {
            var renderer = AddComponentIfMissing<SpriteRenderer>(target);
            if (renderer.sprite == null)
            {
                renderer.sprite = GetBuiltinSprite();
            }

            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            target.transform.localScale = Vector3.one * Mathf.Max(0.2f, uniformScale);
        }

        private static Sprite GetBuiltinSprite()
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null)
            {
                return sprite;
            }

            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
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

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder(GeneratedRootFolder);
            EnsureFolder(GeneratedPrefabFolder);
            EnsureFolder(GeneratedDataFolder);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create GameMainRoot");
            return root;
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            var childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create " + childName);
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
            Undo.RegisterCreatedObjectUndo(childObject, "Create " + childName);
            childObject.transform.SetParent(parent);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject;
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
            Undo.RegisterCreatedObjectUndo(firePoint.gameObject, "Create " + name);
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

            return Undo.AddComponent<T>(target);
        }
    }
}


