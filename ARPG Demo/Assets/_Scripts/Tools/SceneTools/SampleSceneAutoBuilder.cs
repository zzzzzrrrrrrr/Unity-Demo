// Path: Assets/_Scripts/Tools/SceneTools/SampleSceneAutoBuilder.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ARPGDemo.Core;
using ARPGDemo.Game;
using ARPGDemo.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARPGDemo.Tools.SceneTools
{
    public static class SampleSceneAutoBuilder
    {
        private const string StabilizeMenuPath = "Tools/ARPG/Stabilize Sample Scene (P0)";
        private const string MenuPath = "Tools/ARPG/Build Sample Scene";
        private const string ScenePathScene = "Assets/Scenes/SampleScene.scene";
        private const string ScenePathUnity = "Assets/Scenes/SampleScene.unity";
        private const string PlayerViewPrefabPath = "Assets/_Prefabs/Player_View.prefab";
        private const string GeneratedAnimFolder = "Assets/_Anim/Player/Generated";
        private const string PlayerControllerPath = GeneratedAnimFolder + "/Player_Auto.controller";
        private const string EnemyControllerPath = GeneratedAnimFolder + "/Enemy_Auto.controller";
        private const string GeneratedArtFolder = "Assets/_Art/Generated";
        private const string SolidSpritePath = GeneratedArtFolder + "/SolidWhite_1x1.png";

        [MenuItem(MenuPath, false, 2001)]
        public static void BuildSampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            StringBuilder log = new StringBuilder(4096);
            int warningCount = 0;

            try
            {
                Scene scene = OpenOrCreateSampleScene(log, ref warningCount);
                if (!scene.IsValid())
                {
                    Debug.LogError("[SampleSceneAutoBuilder] SampleScene is invalid.");
                    return;
                }

                SceneManager.SetActiveScene(scene);
                RemoveMissingScripts(scene, log);

                int groundLayer = EnsureLayer("Ground", log, ref warningCount);
                int playerLayer = EnsureLayer("Player", log, ref warningCount);
                int enemyLayer = EnsureLayer("Enemy", log, ref warningCount);
                Sprite solidSprite = EnsureSolidSpriteAsset(log, ref warningCount);

                GameObject cameraGo = EnsureRoot(scene, "Main Camera", log);
                cameraGo.tag = "MainCamera";
                Camera camera = EnsureComponent<Camera>(cameraGo, log);
                camera.orthographic = true;
                camera.orthographicSize = 6.8f;
                cameraGo.transform.position = new Vector3(0f, -0.5f, -10f);
                _ = EnsureComponent<AudioListener>(cameraGo, log);

                GameObject gameRoot = EnsureRoot(scene, "GameRoot", log);
                ARPGDemo.Core.GameManager gameManager = EnsureComponent<ARPGDemo.Core.GameManager>(gameRoot, log);
                ARPGDemo.Core.UIManager uiManager = EnsureComponent<ARPGDemo.Core.UIManager>(gameRoot, log);
                string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                SetString(gameManager, "gameplaySceneName", sceneName, log);
                SetString(gameManager, "mainMenuSceneName", sceneName, log);
                SetBool(gameManager, "enterMainMenuOnStart", false, log);
                SetBool(gameManager, "resetTimeScaleOnStart", true, log);
                SetBool(gameManager, "autoVictoryWhenAllEnemiesDead", false, log);
                SetBool(gameManager, "logEnemyDeathWithoutVictory", true, log);
                SetBool(uiManager, "hideAllPanelsOnAwake", true, log);

                BuildGreyboxLayout(scene, groundLayer, solidSprite, log);

                GameObject player = EnsureRoot(scene, "Player", log);
                if (playerLayer >= 0)
                {
                    player.layer = playerLayer;
                }

                player.transform.position = new Vector3(-12f, -0.6f, 0f);
                Rigidbody2D playerRb = EnsureComponent<Rigidbody2D>(player, log);
                playerRb.gravityScale = Mathf.Max(1f, playerRb.gravityScale);
                playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                CapsuleCollider2D playerCollider = EnsureComponent<CapsuleCollider2D>(player, log);
                playerCollider.direction = CapsuleDirection2D.Vertical;
                playerCollider.size = new Vector2(0.8f, 1.8f);
                playerCollider.offset = Vector2.zero;

                ActorStats playerStats = EnsureComponent<ActorStats>(player, log);
                SetEnum(playerStats, "team", (int)ActorTeam.Player, log);
                SetString(playerStats, "actorId", "Player", log);
                SetBool(playerStats, "autoGenerateActorId", false, log);
                SetFloat(playerStats, "maxHealth", 260f, log);
                SetFloat(playerStats, "defensePower", 6f, log);
                SetFloat(playerStats, "hurtInvincibleDuration", 0.45f, log);
                SetBool(playerStats, "enableHitFlash", true, log);
                SetFloat(playerStats, "hitFlashDuration", 0.05f, log);
                SetBool(playerStats, "enableDeathFade", true, log);
                SetFloat(playerStats, "deathFadeDuration", 0.35f, log);

                PlayerController playerController = EnsureComponent<PlayerController>(player, log);
                SetString(playerController, "hurtTrigger", "Hurt", log);
                SetString(playerController, "deathTrigger", "Death", log);
                SetString(playerController, "deathTriggerFallback", "Die", log);
                SetString(playerController, "legacyAttackButton", "Fire1", log);
                SetInt(playerController, "attackPrimaryKey", (int)KeyCode.J, log);
                SetBool(playerController, "controllerHandlesPause", false, log);
                EnsurePlayerComboFallback(playerController, log);

                DisableLegacyPlayerComponents(player, log);

                GameObject playerView = EnsurePlayerView(scene, player.transform, log, ref warningCount);
                playerView.transform.localPosition = Vector3.zero;
                Animator playerAnimator = EnsureComponent<Animator>(playerView, log);
                SpriteRenderer playerRenderer = EnsureComponent<SpriteRenderer>(playerView, log);
                ApplyPlayerVisual(playerView.transform, playerRenderer, solidSprite);
                DisableDuplicateRootPlayerViews(scene, playerView, log);

                AnimatorController playerAnimatorController = EnsurePlayerAnimatorController(log, ref warningCount);
                if (playerAnimatorController != null)
                {
                    playerAnimator.runtimeAnimatorController = playerAnimatorController;
                }

                GameObject playerHitboxGo = EnsureChild(player.transform, "Player_Hitbox", log);
                playerHitboxGo.transform.localPosition = Vector3.zero;
                AttackHitbox2D playerHitbox = EnsureComponent<AttackHitbox2D>(playerHitboxGo, log);
                GameObject playerHitPoint = EnsureChild(playerHitboxGo.transform, "HitPoint", log);
                playerHitPoint.transform.localPosition = new Vector3(0.9f, 0f, 0f);

                GameObject groundCheck = EnsureChild(player.transform, "GroundCheck", log);
                groundCheck.transform.localPosition = new Vector3(0f, -0.95f, 0f);

                SetObject(playerController, "rb", playerRb, log);
                SetObject(playerController, "stats", playerStats, log);
                SetObject(playerController, "attackHitbox", playerHitbox, log);
                SetObject(playerController, "groundCheck", groundCheck.transform, log);
                SetObject(playerController, "animator", playerAnimator, log);
                SetMask(playerController, "groundMask", groundLayer >= 0 ? (1 << groundLayer) : ~0, log);

                SetObject(playerHitbox, "ownerStats", playerStats, log);
                SetObject(playerHitbox, "hitPoint", playerHitPoint.transform, log);
                SetMask(playerHitbox, "targetLayers", enemyLayer >= 0 ? (1 << enemyLayer) : ~0, log);
                SetBool(playerHitbox, "enableHitKnockback", true, log);
                SetFloat(playerHitbox, "hitKnockbackForce", 3.2f, log);
                SetFloat(playerHitbox, "hitKnockbackUpwardBias", 0.16f, log);
                AnimatorController enemyAnimatorController = EnsureEnemyAnimatorController(log, ref warningCount);
                EnsureEnemyUnit(
                    scene,
                    "Enemy_01",
                    new Vector3(-7f, -0.6f, 0f),
                    enemyLayer,
                    playerLayer,
                    player.transform,
                    enemyAnimatorController,
                    70f,   // maxHealth (tutorial)
                    12f,   // attackPower
                    3f,    // defensePower
                    0f,    // patrolSpeed
                    0f,    // patrolRange
                    1.8f,  // chaseSpeed
                    4.2f,  // detectRange
                    1.15f, // attackRange
                    1.35f, // attackCooldown
                    0.8f,  // attackDamageMultiplier
                    0f,    // attackFlatBonus
                    solidSprite,
                    log);
                EnsureEnemyUnit(
                    scene,
                    "Enemy_02",
                    new Vector3(-1.5f, -0.6f, 0f),
                    enemyLayer,
                    playerLayer,
                    player.transform,
                    enemyAnimatorController,
                    95f, 15f, 4f,
                    0f, 0f,
                    2.2f, 5.2f, 1.2f, 1.35f, 0.78f, 0f,
                    solidSprite,
                    log);
                EnsureEnemyUnit(
                    scene,
                    "Enemy_03",
                    new Vector3(3.8f, -0.6f, 0f),
                    enemyLayer,
                    playerLayer,
                    player.transform,
                    enemyAnimatorController,
                    105f, 17f, 4.5f,
                    0f, 0f,
                    2.35f, 5.8f, 1.25f, 1.45f, 0.82f, 0.2f,
                    solidSprite,
                    log);
                EnsureEnemyUnit(
                    scene,
                    "Enemy_04",
                    new Vector3(9.2f, -0.6f, 0f),
                    enemyLayer,
                    playerLayer,
                    player.transform,
                    enemyAnimatorController,
                    220f, 21f, 8f, // ending elite (tankier, less burst)
                    0f, 0f,
                    2.45f, 6.5f, 1.3f, 1.2f, 0.95f, 1f,
                    solidSprite,
                    log);
                RelocateDummyEnemy(log);

                GameObject canvasGo = EnsureRoot(scene, "Canvas", log);
                Canvas canvas = EnsureComponent<Canvas>(canvasGo, log);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _ = EnsureComponent<CanvasScaler>(canvasGo, log);
                _ = EnsureComponent<GraphicRaycaster>(canvasGo, log);
                NormalizeCanvasRoot(canvasGo, log);

                GameObject hudPanel = EnsureChild(canvasGo.transform, "HUD", log);
                SetupPanelRect(hudPanel, true);

                GameObject mainMenuPanel = EnsureChild(canvasGo.transform, "MainMenuPanel", log);
                SetupPanelRect(mainMenuPanel, false);
                EnsurePanelImage(mainMenuPanel, new Color(0f, 0f, 0f, 0.6f));

                GameObject pausePanel = EnsureChild(canvasGo.transform, "PausePanel", log);
                SetupPanelRect(pausePanel, false);
                EnsurePanelImage(pausePanel, new Color(0f, 0f, 0f, 0.6f));

                GameObject resultPanel = EnsureChild(canvasGo.transform, "ResultPanel", log);
                SetupPanelRect(resultPanel, false);
                EnsurePanelImage(resultPanel, new Color(0f, 0f, 0f, 0.65f));

                GameObject hpSliderGo = EnsureSlider(hudPanel.transform, "HP_Slider", new Vector2(210f, -30f), new Vector2(280f, 24f));
                GameObject hpSmoothSliderGo = EnsureSlider(hudPanel.transform, "HP_SmoothSlider", new Vector2(210f, -30f), new Vector2(280f, 24f));
                GameObject mpSliderGo = EnsureSlider(hudPanel.transform, "MP_Slider", new Vector2(210f, -62f), new Vector2(280f, 20f));
                GameObject mpSmoothSliderGo = EnsureSlider(hudPanel.transform, "MP_SmoothSlider", new Vector2(210f, -62f), new Vector2(280f, 20f));

                Text hpText = EnsureText(hudPanel.transform, "HP_Text", "HP", new Vector2(390f, -30f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, 18);
                Text mpText = EnsureText(hudPanel.transform, "MP_Text", "MP", new Vector2(390f, -62f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, 18);
                Text resultText = EnsureText(resultPanel.transform, "ResultText", "Result", new Vector2(0f, 40f), new Vector2(460f, 80f), TextAnchor.MiddleCenter, 40);

                PlayerHUDController hudController = EnsureComponent<PlayerHUDController>(hudPanel, log);
                MainMenuUIController mainMenuController = EnsureComponent<MainMenuUIController>(mainMenuPanel, log);
                PauseUIController pauseController = EnsureComponent<PauseUIController>(pausePanel, log);
                ResultUIController resultController = EnsureComponent<ResultUIController>(resultPanel, log);

                SetObject(hudController, "hpSlider", hpSliderGo.GetComponent<Slider>(), log);
                SetObject(hudController, "hpSmoothSlider", hpSmoothSliderGo.GetComponent<Slider>(), log);
                SetObject(hudController, "mpSlider", mpSliderGo.GetComponent<Slider>(), log);
                SetObject(hudController, "mpSmoothSlider", mpSmoothSliderGo.GetComponent<Slider>(), log);
                SetObject(hudController, "hpText", hpText, log);
                SetObject(hudController, "mpText", mpText, log);
                SetString(hudController, "playerActorId", "Player", log);
                SetBool(hudController, "autoBindFirstPlayer", true, log);
                SetBool(hudController, "followPlayer", false, log);

                SetObject(mainMenuController, "panelRoot", mainMenuPanel, log);
                SetObject(pauseController, "panelRoot", pausePanel, log);
                SetObject(resultController, "panelRoot", resultPanel, log);
                SetObject(resultController, "resultText", resultText, log);

                EnsureEventSystem(scene, log, ref warningCount);
                DisableLegacyManagersAndControllers(log);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[SampleSceneAutoBuilder] Build done.\n" + log + "\nWarnings: " + warningCount);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SampleSceneAutoBuilder] Build failed:\n" + ex);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuildSampleScene()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem(StabilizeMenuPath, false, 2000)]
        public static void StabilizeSampleSceneP0()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            StringBuilder log = new StringBuilder(2048);
            int warningCount = 0;

            try
            {
                Scene scene = OpenOrCreateSampleScene(log, ref warningCount);
                if (!scene.IsValid())
                {
                    Debug.LogError("[SampleSceneAutoBuilder] P0 stabilize failed: invalid scene.");
                    return;
                }

                SceneManager.SetActiveScene(scene);
                RemoveMissingScripts(scene, log);

                int enemyLayer = LayerMask.NameToLayer("Enemy");
                GameObject gameRoot = EnsureRoot(scene, "GameRoot", log);
                ARPGDemo.Core.GameManager gameManager = EnsureComponent<ARPGDemo.Core.GameManager>(gameRoot, log);
                ARPGDemo.Core.UIManager uiManager = EnsureComponent<ARPGDemo.Core.UIManager>(gameRoot, log);
                SetBool(gameManager, "autoVictoryWhenAllEnemiesDead", false, log);
                SetBool(gameManager, "autoEnsureBattleHud", true, log);
                SetBool(uiManager, "hideAllPanelsOnAwake", true, log);

                GameObject canvasGo = EnsureRoot(scene, "Canvas", log);
                Canvas canvas = EnsureComponent<Canvas>(canvasGo, log);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _ = EnsureComponent<CanvasScaler>(canvasGo, log);
                _ = EnsureComponent<GraphicRaycaster>(canvasGo, log);
                NormalizeCanvasRoot(canvasGo, log);

                EnsureEventSystem(scene, log, ref warningCount);

                GameObject hudRoot = EnsureChild(canvasGo.transform, "HUD", log);
                SetupPanelRect(hudRoot, true);
                PlayerHUDController playerHud = EnsureComponent<PlayerHUDController>(hudRoot, log);
                playerHud.enabled = true;
                SetBool(playerHud, "autoCreateMinimalHud", true, log);
                SetBool(playerHud, "runtimeSyncFromStats", true, log);
                SetBool(playerHud, "debugHudLog", true, log);
                DisableDuplicatePlayerHudControllersInScene(playerHud, log);

                string[] enemyHudIds = { "Enemy_01", "Enemy_02", "Enemy_03", "Enemy_04" };
                string[] keepEnemyBarNames = new string[enemyHudIds.Length];
                int keepEnemyBarCount = 0;
                for (int i = 0; i < enemyHudIds.Length; i++)
                {
                    string actorId = enemyHudIds[i];
                    ActorStats enemyStats = FindActorStatsById(actorId);
                    if (enemyStats == null)
                    {
                        warningCount++;
                        log.AppendLine("[WARN] " + actorId + " not found, skip world bar binding.");
                        continue;
                    }

                    string barName = actorId + "_WorldBar";
                    GameObject enemyBarRoot = EnsureChild(canvasGo.transform, barName, log);
                    enemyBarRoot.SetActive(true);
                    RectTransform enemyBarRt = enemyBarRoot.GetComponent<RectTransform>() ?? enemyBarRoot.AddComponent<RectTransform>();
                    enemyBarRt.anchorMin = new Vector2(0.5f, 0.5f);
                    enemyBarRt.anchorMax = new Vector2(0.5f, 0.5f);
                    enemyBarRt.pivot = new Vector2(0.5f, 0.5f);
                    enemyBarRt.localScale = Vector3.one;

                    UIFollowTarget2D follow = EnsureComponent<UIFollowTarget2D>(enemyBarRoot, log);
                    EnemyWorldBarController enemyBar = EnsureComponent<EnemyWorldBarController>(enemyBarRoot, log);
                    enemyBar.enabled = true;

                    Transform worldBarTarget = EnsureEnemyBarAnchor(enemyStats.transform, actorId, enemyStats.GetComponent<CapsuleCollider2D>(), log);
                    follow.WorldTarget = worldBarTarget != null ? worldBarTarget : enemyStats.transform;
                    follow.WorldOffset = Vector3.zero;
                    EditorUtility.SetDirty(follow);
                    SetObject(follow, "parentCanvas", canvas, log);
                    SetObject(enemyBar, "targetStats", enemyStats, log);
                    SetString(enemyBar, "targetActorId", actorId, log);
                    SetObject(enemyBar, "root", enemyBarRoot, log);
                    SetObject(enemyBar, "followTarget", follow, log);
                    SetBool(enemyBar, "runtimeSyncFromStats", true, log);
                    SetBool(enemyBar, "debugHudLog", true, log);
                    enemyBar.BindToTarget(enemyStats);

                    keepEnemyBarNames[keepEnemyBarCount++] = barName;
                }

                DisableUnexpectedEnemyBarsInScene(keepEnemyBarNames, keepEnemyBarCount, log);

                EnsureFinishZoneTrigger(scene, log);
                ResolvePlayerHitboxConflict(log, enemyLayer);
                DisableLegacyManagersAndControllers(log);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[SampleSceneAutoBuilder] P0 stabilize done.\n" + log + "\nWarnings: " + warningCount);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SampleSceneAutoBuilder] P0 stabilize failed:\n" + ex);
            }
        }

        [MenuItem(StabilizeMenuPath, true)]
        private static bool ValidateStabilizeSampleSceneP0()
        {
            return !EditorApplication.isPlaying;
        }

        private static Scene OpenOrCreateSampleScene(StringBuilder log, ref int warnings)
        {
            string path = ResolveSampleScenePath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(path))
            {
                log.AppendLine("[INFO] Open scene: " + path);
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool ok = EditorSceneManager.SaveScene(scene, path);
            if (!ok)
            {
                warnings++;
                ok = EditorSceneManager.SaveScene(scene, ScenePathUnity);
            }

            if (ok)
            {
                string finalPath = File.Exists(path) ? path : ScenePathUnity;
                log.AppendLine("[INFO] Create scene: " + finalPath);
                return EditorSceneManager.OpenScene(finalPath, OpenSceneMode.Single);
            }

            warnings++;
            log.AppendLine("[WARN] Scene save failed, continue in unsaved scene.");
            return scene;
        }

        private static string ResolveSampleScenePath()
        {
            if (File.Exists(ScenePathScene)) return ScenePathScene;
            if (File.Exists(ScenePathUnity)) return ScenePathUnity;

            string[] guids = AssetDatabase.FindAssets("SampleScene t:Scene");
            if (guids.Length > 0)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (!string.IsNullOrEmpty(p)) return p;
            }

            return ScenePathScene;
        }

        private static void EnsureFinishZoneTrigger(Scene scene, StringBuilder log)
        {
            GameObject groundRoot = EnsureRoot(scene, "Ground", log);
            GameObject finishZone = EnsureChild(groundRoot.transform, "FinishZone", log);
            BoxCollider2D triggerCol = EnsureComponent<BoxCollider2D>(finishZone, log);
            triggerCol.isTrigger = true;
            if (triggerCol.size == Vector2.zero)
            {
                triggerCol.size = new Vector2(1.5f, 3f);
            }

            LevelFinishZoneTrigger trigger = EnsureComponent<LevelFinishZoneTrigger>(finishZone, log);
            trigger.enabled = true;
            SetString(trigger, "playerActorId", "Player", log);
            SetEnum(trigger, "interactKey", (int)KeyCode.E, log);
            SetBool(trigger, "logWhenBlocked", true, log);
            SetBool(trigger, "logWhenReady", true, log);
            SetFloat(trigger, "logCooldownSeconds", 1f, log);
            SetBool(trigger, "showWorldPrompt", true, log);
            SetFloat(trigger, "promptCharacterSize", 0.14f, log);
        }

        private static void ResolvePlayerHitboxConflict(StringBuilder log, int enemyLayer)
        {
            PlayerController playerController = UnityEngine.Object.FindObjectOfType<PlayerController>(true);
            if (playerController == null)
            {
                log.AppendLine("[WARN] PlayerController not found, skip hitbox conflict check.");
                return;
            }

            GameObject player = playerController.gameObject;
            AttackHitbox2D[] allHitboxes = player.GetComponentsInChildren<AttackHitbox2D>(true);
            if (allHitboxes == null || allHitboxes.Length == 0)
            {
                log.AppendLine("[WARN] No AttackHitbox2D found under Player.");
                return;
            }

            AttackHitbox2D keep = null;
            for (int i = 0; i < allHitboxes.Length; i++)
            {
                if (allHitboxes[i] != null && allHitboxes[i].gameObject.name == "Player_Hitbox")
                {
                    keep = allHitboxes[i];
                    break;
                }
            }

            if (keep == null)
            {
                keep = allHitboxes[0];
            }

            for (int i = 0; i < allHitboxes.Length; i++)
            {
                AttackHitbox2D hb = allHitboxes[i];
                if (hb == null || hb == keep)
                {
                    continue;
                }

                hb.enabled = false;
                log.AppendLine("[UPDATE] Disable duplicated Player hitbox -> " + hb.gameObject.name);
            }

            keep.enabled = true;
            SetObject(playerController, "attackHitbox", keep, log);

            ActorStats playerStats = player.GetComponent<ActorStats>();
            if (playerStats != null)
            {
                SetObject(keep, "ownerStats", playerStats, log);
            }

            GameObject hitPointGo = EnsureChild(keep.transform, "HitPoint", log);
            if (hitPointGo.transform.localPosition == Vector3.zero)
            {
                hitPointGo.transform.localPosition = new Vector3(0.9f, 0f, 0f);
            }
            SetObject(keep, "hitPoint", hitPointGo.transform, log);
            if (enemyLayer >= 0)
            {
                SetMask(keep, "targetLayers", 1 << enemyLayer, log);
            }

            log.AppendLine("[INFO] Player effective hitbox -> " + keep.gameObject.name);
        }

        private static void DisableDuplicatePlayerHudControllersInScene(PlayerHUDController keep, StringBuilder log)
        {
            PlayerHUDController[] all = UnityEngine.Object.FindObjectsOfType<PlayerHUDController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                PlayerHUDController hud = all[i];
                if (hud == null || hud == keep)
                {
                    continue;
                }

                hud.enabled = false;
                if (hud.gameObject.activeSelf)
                {
                    hud.gameObject.SetActive(false);
                }
                log.AppendLine("[UPDATE] Disable duplicate PlayerHUDController -> " + hud.gameObject.name);
            }
        }

        private static void DisableUnexpectedEnemyBarsInScene(string[] keepBarNames, int keepCount, StringBuilder log)
        {
            EnemyWorldBarController[] all = UnityEngine.Object.FindObjectsOfType<EnemyWorldBarController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                EnemyWorldBarController bar = all[i];
                if (bar == null)
                {
                    continue;
                }

                string goName = bar.gameObject != null ? bar.gameObject.name : string.Empty;
                if (string.IsNullOrEmpty(goName) || !goName.StartsWith("Enemy_") || !goName.EndsWith("_WorldBar"))
                {
                    continue;
                }

                bool keep = false;
                for (int k = 0; k < keepCount; k++)
                {
                    if (keepBarNames[k] == goName)
                    {
                        keep = true;
                        break;
                    }
                }

                if (keep)
                {
                    continue;
                }

                bar.enabled = false;
                if (bar.gameObject.activeSelf)
                {
                    bar.gameObject.SetActive(false);
                }
                log.AppendLine("[UPDATE] Disable stale EnemyWorldBarController -> " + bar.gameObject.name);
            }
        }

        private static ActorStats FindActorStatsById(string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
            {
                return null;
            }

            ActorStats[] allStats = UnityEngine.Object.FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < allStats.Length; i++)
            {
                if (allStats[i] != null && allStats[i].ActorId == actorId)
                {
                    return allStats[i];
                }
            }

            return null;
        }
        private static int EnsureLayer(string name, StringBuilder log, ref int warnings)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            UnityEngine.Object tagManagerObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tagManagerObj == null)
            {
                warnings++;
                log.AppendLine("[WARN] TagManager missing for layer: " + name);
                return -1;
            }

            SerializedObject tagManager = new SerializedObject(tagManagerObj);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                warnings++;
                log.AppendLine("[WARN] TagManager.layers missing for: " + name);
                return -1;
            }

            for (int i = 8; i < 32; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (sp != null && string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    int idx = LayerMask.NameToLayer(name);
                    log.AppendLine("[INFO] Create layer: " + name + " (" + idx + ")");
                    return idx;
                }
            }

            warnings++;
            log.AppendLine("[WARN] No empty layer slot for: " + name);
            return -1;
        }

        private static AnimatorController EnsurePlayerAnimatorController(StringBuilder log, ref int warnings)
        {
            EnsureFolder("Assets/_Anim");
            EnsureFolder("Assets/_Anim/Player");
            EnsureFolder(GeneratedAnimFolder);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
                if (controller == null)
                {
                    warnings++;
                    log.AppendLine("[WARN] Create animator controller failed: " + PlayerControllerPath);
                    return null;
                }
                log.AppendLine("[INFO] Create animator controller: " + PlayerControllerPath);
            }

            ConfigurePlayerAnimator(controller, log, ref warnings);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorController EnsureEnemyAnimatorController(StringBuilder log, ref int warnings)
        {
            EnsureFolder("Assets/_Anim");
            EnsureFolder("Assets/_Anim/Enemy");
            EnsureFolder(GeneratedAnimFolder);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
                if (controller == null)
                {
                    warnings++;
                    log.AppendLine("[WARN] Create enemy animator controller failed: " + EnemyControllerPath);
                    return null;
                }

                log.AppendLine("[CREATE] Enemy animator controller: " + EnemyControllerPath);
            }

            ConfigureEnemyAnimator(controller, log, ref warnings);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigurePlayerAnimator(AnimatorController controller, StringBuilder log, ref int warnings)
        {
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "VerticalSpeed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attacking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack1", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack2", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack3", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            AnimationClip idleClip = ResolveOrCreateClip("Idle", true, log, ref warnings);
            AnimationClip attack1Clip = ResolveOrCreateClip("Attack1", false, log, ref warnings);
            AnimationClip attack2Clip = ResolveOrCreateClip("Attack2", false, log, ref warnings);
            AnimationClip attack3Clip = ResolveOrCreateClip("Attack3", false, log, ref warnings);
            AnimationClip hurtClip = ResolveOrCreateClip("Hurt", false, log, ref warnings);
            AnimationClip deathClip = ResolveOrCreateClip("Death", false, log, ref warnings);

            AnimatorState idle = sm.AddState("Idle", new Vector3(120f, 120f, 0f));
            idle.motion = idleClip;
            sm.defaultState = idle;

            AnimatorState attack1 = sm.AddState("Attack1", new Vector3(420f, 40f, 0f));
            attack1.motion = attack1Clip;
            AnimatorState attack2 = sm.AddState("Attack2", new Vector3(620f, 40f, 0f));
            attack2.motion = attack2Clip;
            AnimatorState attack3 = sm.AddState("Attack3", new Vector3(820f, 40f, 0f));
            attack3.motion = attack3Clip;
            AnimatorState hurt = sm.AddState("Hurt", new Vector3(620f, 220f, 0f));
            hurt.motion = hurtClip;
            AnimatorState death = sm.AddState("Death", new Vector3(820f, 220f, 0f));
            death.motion = deathClip;

            AddAnyTriggerTransition(sm, attack1, "Attack1");
            AddAnyTriggerTransition(sm, hurt, "Hurt");
            AddAnyTriggerTransition(sm, death, "Die");
            AddAnyTriggerTransition(sm, death, "Death");

            AnimatorStateTransition a1a2 = attack1.AddTransition(attack2);
            a1a2.hasExitTime = false;
            a1a2.duration = 0f;
            a1a2.AddCondition(AnimatorConditionMode.If, 0f, "Attack2");

            AnimatorStateTransition a2a3 = attack2.AddTransition(attack3);
            a2a3.hasExitTime = false;
            a2a3.duration = 0f;
            a2a3.AddCondition(AnimatorConditionMode.If, 0f, "Attack3");

            AddExitToIdle(attack1, idle);
            AddExitToIdle(attack2, idle);
            AddExitToIdle(attack3, idle);
            AddExitToIdle(hurt, idle);
        }

        private static void ConfigureEnemyAnimator(AnimatorController controller, StringBuilder log, ref int warnings)
        {
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            AnimationClip idleClip = ResolveOrCreateEnemyClip("Idle", true, log, ref warnings);
            AnimationClip attackClip = ResolveOrCreateEnemyClip("Attack", false, log, ref warnings);
            AnimationClip hurtClip = ResolveOrCreateEnemyClip("Hurt", false, log, ref warnings);
            AnimationClip deathClip = ResolveOrCreateEnemyClip("Death", false, log, ref warnings);

            AnimatorState idle = sm.AddState("Idle", new Vector3(120f, 120f, 0f));
            idle.motion = idleClip;
            sm.defaultState = idle;

            AnimatorState attack = sm.AddState("Attack", new Vector3(360f, 40f, 0f));
            attack.motion = attackClip;
            AnimatorState hurt = sm.AddState("Hurt", new Vector3(360f, 220f, 0f));
            hurt.motion = hurtClip;
            AnimatorState death = sm.AddState("Death", new Vector3(560f, 220f, 0f));
            death.motion = deathClip;

            AddAnyTriggerTransition(sm, attack, "Attack");
            AddAnyTriggerTransition(sm, hurt, "Hurt");
            AddAnyTriggerTransition(sm, death, "Death");
            AddAnyTriggerTransition(sm, death, "Die");

            AddExitToIdle(attack, idle);
            AddExitToIdle(hurt, idle);
        }

        private static void AddAnyTriggerTransition(AnimatorStateMachine sm, AnimatorState state, string trigger)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitToIdle(AnimatorState from, AnimatorState idle)
        {
            AnimatorStateTransition t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.98f;
            t.duration = 0.05f;
        }

        private static AnimationClip ResolveOrCreateClip(string name, bool loop, StringBuilder log, ref int warnings)
        {
            string preferred = GeneratedAnimFolder + "/Player_" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(preferred);
            if (clip != null)
            {
                SetClipLoop(clip, loop);
                return clip;
            }

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip " + name, new[] { "Assets/_Anim/Player" });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    SetClipLoop(clip, loop);
                    log.AppendLine("[INFO] Use clip " + name + ": " + path);
                    return clip;
                }
            }

            warnings++;
            clip = new AnimationClip();
            clip.frameRate = 12f;
            string createPath = AssetDatabase.GenerateUniqueAssetPath(preferred);
            AssetDatabase.CreateAsset(clip, createPath);
            SetClipLoop(clip, loop);
            log.AppendLine("[WARN] Missing clip " + name + ", create placeholder: " + createPath);
            return clip;
        }

        private static AnimationClip ResolveOrCreateEnemyClip(string name, bool loop, StringBuilder log, ref int warnings)
        {
            string preferred = GeneratedAnimFolder + "/Enemy_" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(preferred);
            if (clip != null)
            {
                SetClipLoop(clip, loop);
                return clip;
            }

            string[] searchFolders = { "Assets/_Anim/Enemy", "Assets/_Anim/Player", GeneratedAnimFolder };
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip " + name, searchFolders);
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    SetClipLoop(clip, loop);
                    log.AppendLine("[INFO] Use enemy clip " + name + ": " + path);
                    return clip;
                }
            }

            warnings++;
            clip = new AnimationClip();
            clip.frameRate = 10f;
            string createPath = AssetDatabase.GenerateUniqueAssetPath(preferred);
            AssetDatabase.CreateAsset(clip, createPath);
            SetClipLoop(clip, loop);
            log.AppendLine("[WARN] Missing enemy clip " + name + ", create placeholder: " + createPath);
            return clip;
        }
        private static void SetClipLoop(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            SerializedObject so = new SerializedObject(clip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null) loopTime.boolValue = loop;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] ps = controller.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name != name) continue;
                if (ps[i].type != type)
                {
                    controller.RemoveParameter(ps[i]);
                    controller.AddParameter(name, type);
                }
                return;
            }
            controller.AddParameter(name, type);
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                sm.RemoveState(states[i].state);
            }
            AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
            for (int i = anyTransitions.Length - 1; i >= 0; i--)
            {
                sm.RemoveAnyStateTransition(anyTransitions[i]);
            }
        }

        private static void EnsureEventSystem(Scene scene, StringBuilder log, ref int warnings)
        {
            EventSystem es = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                SceneManager.MoveGameObjectToScene(esGo, scene);
                es = esGo.AddComponent<EventSystem>();
                log.AppendLine("[CREATE] EventSystem");
            }

            Type inputType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputType != null)
            {
                if (es.GetComponent(inputType) == null)
                {
                    es.gameObject.AddComponent(inputType);
                    log.AppendLine("[ADD] InputSystemUIInputModule");
                }
                StandaloneInputModule old = es.GetComponent<StandaloneInputModule>();
                if (old != null)
                {
                    UnityEngine.Object.DestroyImmediate(old, true);
                }
            }
            else
            {
                _ = EnsureComponent<StandaloneInputModule>(es.gameObject, log);
                warnings++;
                log.AppendLine("[WARN] Missing Unity.InputSystem UI module, fallback StandaloneInputModule.");
            }
        }

        private static void RemoveMissingScripts(Scene scene, StringBuilder log)
        {
            int removedCount = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                removedCount += RemoveMissingScriptsRecursive(roots[i], log);
            }

            if (removedCount == 0)
            {
                log.AppendLine("[INFO] Missing Script count: 0");
            }
        }

        private static int RemoveMissingScriptsRecursive(GameObject go, StringBuilder log)
        {
            if (go == null)
            {
                return 0;
            }

            int localMissing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (localMissing > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                log.AppendLine("[FIX] Removed Missing Script x" + localMissing + " on " + go.name);
            }

            int total = localMissing;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                total += RemoveMissingScriptsRecursive(go.transform.GetChild(i).gameObject, log);
            }

            return total;
        }

        private static void DisableLegacyPlayerComponents(GameObject player, StringBuilder log)
        {
            DisableSingleComponentByTypeName(player, "ARPGDemo.Game.PlayerController2D");
            DisableComponentsInChildrenByTypeName(player, "ARPGDemo.Game.PlayerSkillSystem");

            log.AppendLine("[INFO] Disabled legacy player components on Player.");
        }

        private static void DisableLegacyManagersAndControllers(StringBuilder log)
        {
            DisableAllComponentsByTypeName("ARPGDemo.Core.Managers.GameManager");
            DisableAllComponentsByTypeName("ARPGDemo.Core.Managers.UIManager");

            DisableAllComponentsByTypeName("ARPGDemo.Game.PlayerController2D");
            DisableAllComponentsByTypeName("ARPGDemo.Game.PlayerSkillSystem");

            log.AppendLine("[INFO] Disabled legacy managers/controllers.");
        }

        private static void DisableSingleComponentByTypeName(GameObject go, string fullTypeName)
        {
            if (go == null)
            {
                return;
            }

            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            {
                return;
            }

            Component component = go.GetComponent(type);
            if (component is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }
        }

        private static void DisableComponentsInChildrenByTypeName(GameObject root, string fullTypeName)
        {
            if (root == null)
            {
                return;
            }

            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            {
                return;
            }

            Component[] components = root.GetComponentsInChildren(type, true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void DisableAllComponentsByTypeName(string fullTypeName)
        {
            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            {
                return;
            }

            UnityEngine.Object[] components = UnityEngine.Object.FindObjectsOfType(type, true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static Type ResolveTypeByName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return null;
            }

            Type direct = Type.GetType(fullTypeName, false);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                Type candidate = assembly.GetType(fullTypeName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void DisableDuplicateRootPlayerViews(Scene scene, GameObject keepPlayerView, StringBuilder log)
        {
            if (keepPlayerView == null)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == keepPlayerView)
                {
                    continue;
                }

                if (root.transform.parent == null && root.name == "Player_View")
                {
                    root.SetActive(false);
                    root.name = "Player_View_Legacy_Disabled";
                    log.AppendLine("[UPDATE] Disabled duplicate root Player_View");
                }
            }
        }

        private static void NormalizeCanvasRoot(GameObject canvasGo, StringBuilder log)
        {
            if (canvasGo == null)
            {
                return;
            }

            RectTransform rt = canvasGo.GetComponent<RectTransform>() ?? canvasGo.AddComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            canvasGo.SetActive(true);
            log.AppendLine("[UPDATE] Canvas rect normalized (scale=1, visible=active).");
        }

        private static void EnsurePlayerComboFallback(PlayerController controller, StringBuilder log)
        {
            if (controller == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty comboStages = so.FindProperty("comboStages");
            if (comboStages == null || !comboStages.isArray)
            {
                log.AppendLine("[WARN] PlayerController.comboStages not found.");
                return;
            }

            if (comboStages.arraySize < 3)
            {
                comboStages.arraySize = 3;
            }

            string[] triggers = { "Attack1", "Attack2", "Attack3" };
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty stage = comboStages.GetArrayElementAtIndex(i);
                if (stage == null)
                {
                    continue;
                }

                SerializedProperty animatorTrigger = stage.FindPropertyRelative("animatorTrigger");
                if (animatorTrigger != null)
                {
                    animatorTrigger.stringValue = triggers[i];
                }

                SerializedProperty autoRequestHitbox = stage.FindPropertyRelative("autoRequestHitbox");
                if (autoRequestHitbox != null)
                {
                    autoRequestHitbox.boolValue = true;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void BuildGreyboxLayout(Scene scene, int groundLayer, Sprite solidSprite, StringBuilder log)
        {
            GameObject groundRoot = EnsureRoot(scene, "Ground", log);
            groundRoot.transform.position = Vector3.zero;
            if (groundLayer >= 0)
            {
                groundRoot.layer = groundLayer;
            }
            BoxCollider2D legacyRootCollider = groundRoot.GetComponent<BoxCollider2D>();
            if (legacyRootCollider != null)
            {
                legacyRootCollider.enabled = false;
            }

            EnsureGroundSegment(groundRoot.transform, "Ground_Spawn", new Vector3(-11f, -2f, 0f), new Vector2(6f, 1f), groundLayer, log);
            EnsureGroundSegment(groundRoot.transform, "Ground_Tutorial", new Vector3(-7f, -2f, 0f), new Vector2(4f, 1f), groundLayer, log);
            EnsureGroundSegment(groundRoot.transform, "Ground_Mid", new Vector3(1f, -2f, 0f), new Vector2(12f, 1f), groundLayer, log);
            EnsureGroundSegment(groundRoot.transform, "Ground_Final", new Vector3(9f, -2f, 0f), new Vector2(4f, 1f), groundLayer, log);
            EnsureGroundSegment(groundRoot.transform, "Ground_Goal", new Vector3(12f, -2f, 0f), new Vector2(2f, 1f), groundLayer, log);

            EnsureGroundSegment(groundRoot.transform, "Boundary_Left", new Vector3(-14f, 1f, 0f), new Vector2(1f, 8f), groundLayer, log);
            EnsureGroundSegment(groundRoot.transform, "Boundary_Right", new Vector3(14.8f, 1f, 0f), new Vector2(1f, 10f), groundLayer, log);

            GameObject dropZone = EnsureChild(groundRoot.transform, "DropZone_Marker", log);
            dropZone.transform.position = new Vector3(0f, -6f, 0f);
            BoxCollider2D dropCol = EnsureComponent<BoxCollider2D>(dropZone, log);
            dropCol.size = new Vector2(30f, 2f);
            dropCol.offset = Vector2.zero;
            dropCol.isTrigger = true;

            GameObject finishZone = EnsureChild(groundRoot.transform, "FinishZone", log);
            finishZone.transform.position = new Vector3(12.5f, -0.5f, 0f);
            BoxCollider2D finishCol = EnsureComponent<BoxCollider2D>(finishZone, log);
            finishCol.size = new Vector2(1.5f, 3f);
            finishCol.offset = Vector2.zero;
            finishCol.isTrigger = true;
            _ = EnsureComponentByTypeName(finishZone, "ARPGDemo.Game.LevelFinishZoneTrigger", log);

            GameObject zoneMarkers = EnsureChild(groundRoot.transform, "ZoneMarkers", log);
            EnsureZoneMarker(zoneMarkers.transform, "Spawn_Area", new Vector3(-11f, 1.4f, 0f));
            EnsureZoneMarker(zoneMarkers.transform, "Tutorial_Fight_Area", new Vector3(-7f, 1.4f, 0f));
            EnsureZoneMarker(zoneMarkers.transform, "Mid_Pressure_Area", new Vector3(1f, 1.4f, 0f));
            EnsureZoneMarker(zoneMarkers.transform, "Final_Elite_Area", new Vector3(9f, 1.4f, 0f));
            EnsureZoneMarker(zoneMarkers.transform, "Goal_Area", new Vector3(12.5f, 1.4f, 0f));

            ApplyEnvironmentPackaging(groundRoot.transform, solidSprite, log);
        }

        private static void EnsureGroundSegment(Transform parent, string name, Vector3 worldPos, Vector2 size, int layer, StringBuilder log)
        {
            GameObject segment = EnsureChild(parent, name, log);
            segment.transform.position = worldPos;
            if (layer >= 0)
            {
                segment.layer = layer;
            }

            BoxCollider2D col = EnsureComponent<BoxCollider2D>(segment, log);
            col.size = size;
            col.offset = Vector2.zero;
            col.isTrigger = false;
        }

        private static void ApplyEnvironmentPackaging(Transform groundRoot, Sprite solidSprite, StringBuilder log)
        {
            if (groundRoot == null || solidSprite == null)
            {
                return;
            }

            GameObject backdrop = EnsureRoot(groundRoot.gameObject.scene, "Backdrop", log);
            EnsureVisualRect(backdrop.transform, "BG_Far", new Vector3(0f, 1.8f, 8f), new Vector2(42f, 20f), new Color(0.16f, 0.2f, 0.31f, 1f), -40, solidSprite);
            EnsureVisualRect(backdrop.transform, "BG_Mid", new Vector3(0f, -0.2f, 7f), new Vector2(34f, 10f), new Color(0.23f, 0.31f, 0.44f, 1f), -35, solidSprite);
            EnsureVisualRect(backdrop.transform, "FG_Haze", new Vector3(0f, -2.4f, -1f), new Vector2(34f, 3f), new Color(0.12f, 0.16f, 0.22f, 0.35f), -1, solidSprite);

            ApplySegmentVisual(groundRoot, "Ground_Spawn", new Color(0.34f, 0.36f, 0.42f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Ground_Tutorial", new Color(0.35f, 0.38f, 0.45f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Ground_Mid", new Color(0.31f, 0.34f, 0.4f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Ground_Final", new Color(0.39f, 0.31f, 0.29f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Ground_Goal", new Color(0.45f, 0.36f, 0.26f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Boundary_Left", new Color(0.2f, 0.15f, 0.15f, 1f), solidSprite);
            ApplySegmentVisual(groundRoot, "Boundary_Right", new Color(0.2f, 0.15f, 0.15f, 1f), solidSprite);

            Transform dropZone = groundRoot.Find("DropZone_Marker");
            if (dropZone != null)
            {
                EnsureVisualRect(dropZone, "DropZone_Visual", new Vector3(0f, 0f, 0f), new Vector2(30f, 2f), new Color(0.66f, 0.12f, 0.12f, 0.25f), -5, solidSprite);
                EnsureTextMarker(dropZone, "DropZone_Text", "FALL ZONE", new Vector3(0f, 1.1f, 0f), new Color(1f, 0.6f, 0.6f, 1f), 0.18f);
            }

            Transform finishZone = groundRoot.Find("FinishZone");
            if (finishZone != null)
            {
                EnsureVisualRect(finishZone, "FinishZone_Visual", new Vector3(0f, 0f, 0f), new Vector2(1.5f, 3f), new Color(0.16f, 0.85f, 0.68f, 0.45f), 2, solidSprite);
                EnsureVisualRect(finishZone, "FinishZone_Glow", new Vector3(0f, 0f, 0f), new Vector2(2f, 3.6f), new Color(0.4f, 1f, 0.86f, 0.18f), 1, solidSprite);
                EnsureTextMarker(finishZone, "FinishZone_Label", "EXIT", new Vector3(0f, 1.9f, 0f), new Color(0.85f, 1f, 0.93f, 1f), 0.2f);
            }
        }

        private static void ApplySegmentVisual(Transform groundRoot, string segmentName, Color color, Sprite solidSprite)
        {
            if (groundRoot == null)
            {
                return;
            }

            Transform segment = groundRoot.Find(segmentName);
            if (segment == null)
            {
                return;
            }

            BoxCollider2D col = segment.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                return;
            }

            EnsureVisualRect(segment, "Visual", Vector3.zero, col.size, color, -10, solidSprite);
        }

        private static void EnsureVisualRect(Transform parent, string name, Vector3 localPos, Vector2 size, Color color, int sortingOrder, Sprite sprite)
        {
            if (parent == null)
            {
                return;
            }

            Transform child = parent.Find(name);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = child.gameObject;
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
            }

            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
        }

        private static void EnsureTextMarker(Transform parent, string name, string text, Vector3 localPos, Color color, float characterSize)
        {
            if (parent == null)
            {
                return;
            }

            Transform marker = parent.Find(name);
            GameObject markerGo;
            if (marker == null)
            {
                markerGo = new GameObject(name);
                markerGo.transform.SetParent(parent, false);
            }
            else
            {
                markerGo = marker.gameObject;
            }

            markerGo.transform.localPosition = localPos;
            markerGo.transform.localRotation = Quaternion.identity;
            markerGo.transform.localScale = Vector3.one;

            TextMesh tm = markerGo.GetComponent<TextMesh>();
            if (tm == null)
            {
                tm = markerGo.AddComponent<TextMesh>();
            }

            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = characterSize;
            tm.fontSize = 64;
            tm.color = color;
        }

        private static void EnsureZoneMarker(Transform parent, string name, Vector3 worldPos)
        {
            Transform marker = parent.Find(name);
            GameObject markerGo;
            if (marker == null)
            {
                markerGo = new GameObject(name);
                markerGo.transform.SetParent(parent, false);
            }
            else
            {
                markerGo = marker.gameObject;
            }

            markerGo.transform.position = worldPos;
            TextMesh tm = markerGo.GetComponent<TextMesh>();
            if (tm == null)
            {
                tm = markerGo.AddComponent<TextMesh>();
            }

            tm.text = name.Replace('_', ' ');
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.12f;
            tm.fontSize = 64;
            tm.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        private static void EnsureEnemyUnit(
            Scene scene,
            string enemyName,
            Vector3 position,
            int enemyLayer,
            int playerLayer,
            Transform playerTarget,
            AnimatorController enemyAnimatorController,
            float maxHealth,
            float attackPower,
            float defensePower,
            float patrolSpeed,
            float patrolRange,
            float chaseSpeed,
            float detectRange,
            float attackRange,
            float attackCooldown,
            float attackDamageMultiplier,
            float attackFlatBonus,
            Sprite solidSprite,
            StringBuilder log)
        {
            GameObject enemy = EnsureRoot(scene, enemyName, log);
            if (enemyLayer >= 0)
            {
                enemy.layer = enemyLayer;
            }

            enemy.transform.position = position;
            enemy.transform.localScale = Vector3.one;
            Rigidbody2D enemyRb = EnsureComponent<Rigidbody2D>(enemy, log);
            enemyRb.gravityScale = Mathf.Max(1f, enemyRb.gravityScale);
            enemyRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D enemyCollider = EnsureComponent<CapsuleCollider2D>(enemy, log);
            enemyCollider.enabled = true;
            enemyCollider.isTrigger = false;
            enemyCollider.direction = CapsuleDirection2D.Vertical;
            enemyCollider.size = new Vector2(0.8f, 1.8f);
            enemyCollider.offset = Vector2.zero;

            ActorStats enemyStats = EnsureComponent<ActorStats>(enemy, log);
            SetEnum(enemyStats, "team", (int)ActorTeam.Enemy, log);
            SetString(enemyStats, "actorId", enemyName, log);
            SetBool(enemyStats, "autoGenerateActorId", false, log);
            SetFloat(enemyStats, "maxHealth", maxHealth, log);
            SetFloat(enemyStats, "attackPower", attackPower, log);
            SetFloat(enemyStats, "defensePower", defensePower, log);
            SetFloat(enemyStats, "criticalChance", 0.08f, log);
            SetFloat(enemyStats, "criticalMultiplier", 1.4f, log);
            SetBool(enemyStats, "enableHitFlash", true, log);
            SetFloat(enemyStats, "hitFlashDuration", 0.05f, log);
            SetBool(enemyStats, "enableDeathFade", true, log);
            SetFloat(enemyStats, "deathFadeDuration", 0.35f, log);
            SetBool(enemyStats, "destroyOnDeath", false, log);
            SetBool(enemyStats, "autoHideEnemyCorpse", true, log);
            SetFloat(enemyStats, "enemyCorpseHideDelay", 0.9f, log);
            SetBool(enemyStats, "disablePhysicsOnDeath", true, log);

            EnemyAIController2D enemyAI = EnsureComponent<EnemyAIController2D>(enemy, log);
            enemyAI.enabled = true;
            SetString(enemyAI, "attackTrigger", "Attack", log);
            SetString(enemyAI, "hurtTrigger", "Hurt", log);
            SetString(enemyAI, "deathTrigger", "Death", log);
            SetFloat(enemyAI, "patrolSpeed", patrolSpeed, log);
            SetFloat(enemyAI, "patrolRange", patrolRange, log);
            SetFloat(enemyAI, "chaseSpeed", chaseSpeed, log);
            SetFloat(enemyAI, "detectRange", detectRange, log);
            SetFloat(enemyAI, "attackRange", attackRange, log);
            SetFloat(enemyAI, "attackCooldown", attackCooldown, log);
            SetFloat(enemyAI, "attackDamageMultiplier", attackDamageMultiplier, log);
            SetFloat(enemyAI, "attackFlatBonus", attackFlatBonus, log);

            GameObject enemyView = EnsureChild(enemy.transform, "Enemy_View", log);
            enemyView.transform.localPosition = Vector3.zero;
            enemyView.transform.localRotation = Quaternion.identity;
            if (enemyLayer >= 0)
            {
                enemyView.layer = enemyLayer;
            }
            Animator enemyAnimator = EnsureComponent<Animator>(enemyView, log);
            SpriteRenderer enemyRenderer = EnsureComponent<SpriteRenderer>(enemyView, log);
            ApplyEnemyVisual(enemyName, enemyView.transform, enemyRenderer, solidSprite);
            Transform visualProxy = enemyView.transform.Find("VisualProxy");
            if (visualProxy != null)
            {
                visualProxy.localPosition = Vector3.zero;
                visualProxy.localRotation = Quaternion.identity;
            }
            if (enemyAnimatorController != null)
            {
                enemyAnimator.runtimeAnimatorController = enemyAnimatorController;
            }

            GameObject enemyHitboxGo = EnsureChild(enemy.transform, "Enemy_Hitbox", log);
            enemyHitboxGo.transform.localPosition = Vector3.zero;
            if (enemyLayer >= 0)
            {
                enemyHitboxGo.layer = enemyLayer;
            }
            AttackHitbox2D enemyHitbox = EnsureComponent<AttackHitbox2D>(enemyHitboxGo, log);
            enemyHitbox.enabled = true;
            GameObject enemyHitPoint = EnsureChild(enemyHitboxGo.transform, "HitPoint", log);
            enemyHitPoint.transform.localPosition = new Vector3(0.8f, 0f, 0f);

            AlignEnemyNodeChain(enemy.transform, enemyName, enemyLayer, enemyCollider, log);

            SetObject(enemyAI, "rb", enemyRb, log);
            SetObject(enemyAI, "stats", enemyStats, log);
            SetObject(enemyAI, "attackHitbox", enemyHitbox, log);
            SetObject(enemyAI, "animator", enemyAnimator, log);
            SetObject(enemyAI, "playerTarget", playerTarget, log);

            SetObject(enemyHitbox, "ownerStats", enemyStats, log);
            SetObject(enemyHitbox, "hitPoint", enemyHitPoint.transform, log);
            SetMask(enemyHitbox, "targetLayers", playerLayer >= 0 ? (1 << playerLayer) : ~0, log);
            SetBool(enemyHitbox, "enableHitKnockback", true, log);
            SetFloat(enemyHitbox, "hitKnockbackForce", 2.6f, log);
            SetFloat(enemyHitbox, "hitKnockbackUpwardBias", 0.24f, log);
        }

        private static void AlignEnemyNodeChain(Transform enemyRoot, string enemyName, int enemyLayer, CapsuleCollider2D enemyCollider, StringBuilder log)
        {
            if (enemyRoot == null)
            {
                return;
            }

            Transform enemyView = enemyRoot.Find("Enemy_View");
            if (enemyView == null)
            {
                return;
            }

            enemyView.localPosition = Vector3.zero;
            enemyView.localRotation = Quaternion.identity;

            if (enemyLayer >= 0)
            {
                enemyView.gameObject.layer = enemyLayer;
            }

            Transform spritePivot = EnsureChild(enemyView, "SpritePivot", log).transform;
            spritePivot.localPosition = Vector3.zero;
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
            if (enemyLayer >= 0)
            {
                spritePivot.gameObject.layer = enemyLayer;
            }

            Transform visualProxy = EnsureChildUnderParent(enemyView, spritePivot, "VisualProxy", log);
            if (visualProxy != null)
            {
                visualProxy.localPosition = Vector3.zero;
                visualProxy.localRotation = Quaternion.identity;
                if (enemyLayer >= 0)
                {
                    visualProxy.gameObject.layer = enemyLayer;
                }
            }

            Transform visualShadow = EnsureChildUnderParent(enemyView, enemyView, "VisualShadow", log);
            if (visualShadow != null)
            {
                visualShadow.localPosition = new Vector3(0f, -0.55f, 0f);
                visualShadow.localRotation = Quaternion.identity;
                if (enemyLayer >= 0)
                {
                    visualShadow.gameObject.layer = enemyLayer;
                }
            }

            Transform eliteMark = FindDescendantByName(enemyView, "EliteMark");
            if (enemyName == "Enemy_04" && eliteMark == null)
            {
                eliteMark = EnsureChild(spritePivot, "EliteMark", log).transform;
            }

            if (eliteMark != null)
            {
                if (eliteMark.parent != spritePivot)
                {
                    eliteMark.SetParent(spritePivot, false);
                    log.AppendLine("[UPDATE] Reparent EliteMark -> SpritePivot");
                }

                eliteMark.gameObject.SetActive(enemyName == "Enemy_04");
                eliteMark.localPosition = new Vector3(0f, 1.18f, 0f);
                eliteMark.localRotation = Quaternion.identity;
                if (enemyLayer >= 0)
                {
                    eliteMark.gameObject.layer = enemyLayer;
                }
            }

            Transform eliteAura = FindDescendantByName(enemyView, "EliteAura");
            if (enemyName == "Enemy_04" && eliteAura == null)
            {
                eliteAura = EnsureChild(spritePivot, "EliteAura", log).transform;
            }

            if (eliteAura != null)
            {
                if (eliteAura.parent != spritePivot)
                {
                    eliteAura.SetParent(spritePivot, false);
                    log.AppendLine("[UPDATE] Reparent EliteAura -> SpritePivot");
                }

                eliteAura.gameObject.SetActive(enemyName == "Enemy_04");
                eliteAura.localPosition = new Vector3(0f, 0.05f, 0f);
                eliteAura.localRotation = Quaternion.identity;
                if (enemyLayer >= 0)
                {
                    eliteAura.gameObject.layer = enemyLayer;
                }
            }

            Transform hitboxRoot = enemyRoot.Find("Enemy_Hitbox");
            if (hitboxRoot != null)
            {
                hitboxRoot.localPosition = Vector3.zero;
                hitboxRoot.localRotation = Quaternion.identity;
                hitboxRoot.localScale = Vector3.one;
                if (enemyLayer >= 0)
                {
                    hitboxRoot.gameObject.layer = enemyLayer;
                }
            }

            NormalizeEnemyMainSpriteCarrier(enemyView, visualProxy, log);
            ApplyEnemySpritePivotCompensation(spritePivot, visualProxy);
            EnsureEnemyBarAnchor(enemyRoot, enemyName, enemyCollider, log);
        }

        private static Transform EnsureEnemyBarAnchor(Transform enemyRoot, string enemyName, CapsuleCollider2D enemyCollider, StringBuilder log)
        {
            if (enemyRoot == null)
            {
                return null;
            }

            Transform barAnchor = enemyRoot.Find("BarAnchor");
            if (barAnchor == null)
            {
                Transform legacyHeadAnchor = enemyRoot.Find("HeadAnchor");
                if (legacyHeadAnchor != null)
                {
                    legacyHeadAnchor.name = "BarAnchor";
                    barAnchor = legacyHeadAnchor;
                    log.AppendLine("[UPDATE] Rename " + enemyRoot.name + "/HeadAnchor -> BarAnchor");
                }
                else
                {
                    GameObject anchorGo = EnsureChild(enemyRoot, "BarAnchor", log);
                    barAnchor = anchorGo.transform;
                }
            }

            Transform enemyView = enemyRoot.Find("Enemy_View");
            Transform spritePivot = enemyView != null ? enemyView.Find("SpritePivot") : null;
            Transform proxy = enemyView != null ? FindDescendantByName(enemyView, "VisualProxy") : null;
            float proxyScaleY = proxy != null ? Mathf.Abs(proxy.localScale.y) : 1f;
            float spritePivotY = spritePivot != null ? spritePivot.localPosition.y : 0f;

            float halfBodyHeight = 0.9f;
            if (enemyCollider != null)
            {
                halfBodyHeight = Mathf.Max(0.6f, enemyCollider.offset.y + enemyCollider.size.y * 0.5f);
            }

            float localY = Mathf.Max(1.45f, halfBodyHeight + 0.55f + spritePivotY + Mathf.Max(0f, proxyScaleY - 1f) * 0.25f);
            SpriteRenderer proxyRenderer = proxy != null ? proxy.GetComponent<SpriteRenderer>() : null;
            if (proxyRenderer != null && proxyRenderer.sprite != null)
            {
                float ppu = Mathf.Max(1f, proxyRenderer.sprite.pixelsPerUnit);
                float topY = spritePivotY + ((proxyRenderer.sprite.rect.height - proxyRenderer.sprite.pivot.y) / ppu) * proxyScaleY;
                localY = Mathf.Max(localY, topY + 0.2f);
            }

            if (enemyName == "Enemy_04")
            {
                localY += 0.18f;
            }

            localY = Mathf.Clamp(localY, 1.3f, 2.2f);
            barAnchor.localPosition = new Vector3(0f, localY, 0f);
            barAnchor.localRotation = Quaternion.identity;
            barAnchor.localScale = Vector3.one;

            if (log != null)
            {
                log.AppendLine("[UPDATE] " + enemyRoot.name + "/BarAnchor localPosition=(0," + localY.ToString("F2") + ",0)");
            }

            return barAnchor;
        }

        private static void ApplyEnemySpritePivotCompensation(Transform spritePivot, Transform visualProxy)
        {
            if (spritePivot == null)
            {
                return;
            }

            float targetY = 0f;
            if (visualProxy != null)
            {
                SpriteRenderer proxyRenderer = visualProxy.GetComponent<SpriteRenderer>();
                if (proxyRenderer != null && proxyRenderer.sprite != null)
                {
                    float ppu = Mathf.Max(1f, proxyRenderer.sprite.pixelsPerUnit);
                    float pivotY = proxyRenderer.sprite.pivot.y / ppu;
                    targetY += pivotY * Mathf.Abs(visualProxy.localScale.y);
                }
            }

            spritePivot.localPosition = new Vector3(0f, Mathf.Clamp(targetY, -0.1f, 1.8f), 0f);
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
        }

        private static void NormalizeEnemyMainSpriteCarrier(Transform enemyView, Transform visualProxy, StringBuilder log)
        {
            if (enemyView == null || visualProxy == null)
            {
                return;
            }

            SpriteRenderer rootRenderer = enemyView.GetComponent<SpriteRenderer>();
            SpriteRenderer proxyRenderer = visualProxy.GetComponent<SpriteRenderer>();
            if (proxyRenderer == null && rootRenderer != null)
            {
                proxyRenderer = visualProxy.gameObject.AddComponent<SpriteRenderer>();
                CopySpriteRendererSettings(rootRenderer, proxyRenderer);
                log.AppendLine("[ADD] SpriteRenderer -> " + visualProxy.name);
            }

            if (rootRenderer != null && proxyRenderer != null && rootRenderer.enabled)
            {
                rootRenderer.enabled = false;
                log.AppendLine("[UPDATE] Disable duplicate renderer -> " + enemyView.name);
            }
        }

        private static Transform EnsureChildUnderParent(Transform searchRoot, Transform desiredParent, string childName, StringBuilder log)
        {
            if (searchRoot == null || desiredParent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform child = FindDescendantByName(searchRoot, childName);
            if (child == null)
            {
                return EnsureChild(desiredParent, childName, log).transform;
            }

            if (child.parent != desiredParent)
            {
                child.SetParent(desiredParent, false);
                log.AppendLine("[UPDATE] Reparent " + childName + " -> " + desiredParent.name);
            }

            return child;
        }

        private static Transform FindDescendantByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void CopySpriteRendererSettings(SpriteRenderer src, SpriteRenderer dst)
        {
            if (src == null || dst == null)
            {
                return;
            }

            dst.sprite = src.sprite;
            dst.color = src.color;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.drawMode = src.drawMode;
            dst.size = src.size;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
            dst.maskInteraction = src.maskInteraction;
            dst.material = src.sharedMaterial;
            dst.enabled = src.enabled;
        }

        private static void RelocateDummyEnemy(StringBuilder log)
        {
            ActorStats[] allStats = UnityEngine.Object.FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats == null)
                {
                    continue;
                }

                bool isDummy = stats.ActorId == "DummyEnemy" || stats.gameObject.name.Contains("DummyEnemy");
                if (!isDummy)
                {
                    continue;
                }

                stats.transform.position = new Vector3(24f, -0.6f, 0f);
                EnemyAIController2D ai = stats.GetComponent<EnemyAIController2D>();
                if (ai != null)
                {
                    ai.enabled = false;
                }

                log.AppendLine("[UPDATE] Move DummyEnemy out of playable lane -> " + stats.gameObject.name);
            }
        }

        private static GameObject EnsureRoot(Scene scene, string name, StringBuilder log)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    log.AppendLine("[UPDATE] " + name);
                    return roots[i];
                }
            }

            GameObject go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            log.AppendLine("[CREATE] " + name);
            return go;
        }

        private static GameObject EnsureChild(Transform parent, string name, StringBuilder log)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name)
                {
                    log.AppendLine("[UPDATE] " + parent.name + "/" + name);
                    return c.gameObject;
                }
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            log.AppendLine("[CREATE] " + parent.name + "/" + name);
            return go;
        }

        private static GameObject EnsurePlayerView(Scene scene, Transform parent, StringBuilder log, ref int warnings)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == "Player_View")
                {
                    log.AppendLine("[UPDATE] Player/Player_View");
                    return c.gameObject;
                }
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                if (root.name == "Player_View" && root.transform.parent == null)
                {
                    root.transform.SetParent(parent, false);
                    log.AppendLine("[UPDATE] Reparent root Player_View -> Player/Player_View");
                    return root;
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerViewPrefabPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null)
                {
                    instance.name = "Player_View";
                    instance.transform.SetParent(parent, false);
                    log.AppendLine("[CREATE] Player_View from prefab");
                    return instance;
                }
            }

            warnings++;
            log.AppendLine("[WARN] Missing Player_View prefab: " + PlayerViewPrefabPath);
            GameObject view = new GameObject("Player_View");
            view.transform.SetParent(parent, false);
            log.AppendLine("[CREATE] Player/Player_View");
            return view;
        }

        private static void ApplyPlayerVisual(Transform playerView, SpriteRenderer renderer, Sprite solidSprite)
        {
            if (playerView == null || renderer == null)
            {
                return;
            }

            if (renderer.sprite == null && solidSprite != null)
            {
                renderer.sprite = solidSprite;
            }

            renderer.color = new Color(0.78f, 0.9f, 1f, 1f);
            renderer.sortingOrder = 20;
            playerView.localScale = new Vector3(1.05f, 1.18f, 1f);
        }

        private static void ApplyEnemyVisual(string enemyName, Transform enemyView, SpriteRenderer renderer, Sprite solidSprite)
        {
            if (enemyView == null || renderer == null)
            {
                return;
            }

            if (renderer.sprite == null && solidSprite != null)
            {
                renderer.sprite = solidSprite;
            }

            Color tint = new Color(1f, 0.83f, 0.83f, 1f);
            Vector3 scale = new Vector3(1f, 1.12f, 1f);
            string badge = "Enemy";

            switch (enemyName)
            {
                case "Enemy_01":
                    tint = new Color(0.98f, 0.86f, 0.84f, 1f);
                    scale = new Vector3(0.95f, 1.08f, 1f);
                    badge = "Tutor";
                    break;
                case "Enemy_02":
                    tint = new Color(0.98f, 0.78f, 0.78f, 1f);
                    scale = new Vector3(1f, 1.12f, 1f);
                    badge = "Mob A";
                    break;
                case "Enemy_03":
                    tint = new Color(1f, 0.7f, 0.66f, 1f);
                    scale = new Vector3(1.06f, 1.16f, 1f);
                    badge = "Mob B";
                    break;
                case "Enemy_04":
                    tint = new Color(1f, 0.48f, 0.42f, 1f);
                    scale = new Vector3(1.22f, 1.34f, 1f);
                    badge = "ELITE";
                    break;
            }

            renderer.color = tint;
            renderer.sortingOrder = 18;
            enemyView.localScale = scale;
            EnsureTextMarker(enemyView, "EnemyBadge", badge, new Vector3(0f, 1.45f, 0f), Color.white, 0.12f);
        }

        private static T EnsureComponent<T>(GameObject go, StringBuilder log) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null)
            {
                c = go.AddComponent<T>();
                log.AppendLine("[ADD] " + typeof(T).Name + " -> " + go.name);
            }
            return c;
        }

        private static Component EnsureComponentByTypeName(GameObject go, string typeName, StringBuilder log)
        {
            if (go == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            Type t = Type.GetType(typeName + ", Assembly-CSharp");
            if (t == null)
            {
                log.AppendLine("[WARN] Type not found: " + typeName);
                return null;
            }

            Component c = go.GetComponent(t);
            if (c == null)
            {
                c = go.AddComponent(t);
                log.AppendLine("[ADD] " + t.Name + " -> " + go.name);
            }

            return c;
        }

        private static GameObject EnsureSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name && c.GetComponent<Slider>() != null)
                {
                    ConfigureRect(c.GetComponent<RectTransform>(), anchoredPos, size, false);
                    return c.gameObject;
                }
            }

            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), anchoredPos, size, false);
            Slider s = go.GetComponent<Slider>();
            s.minValue = 0f;
            s.maxValue = 100f;
            s.value = 100f;
            return go;
        }

        private static Text EnsureText(Transform parent, string name, string content, Vector2 anchoredPos, Vector2 size, TextAnchor anchor, int fontSize)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name)
                {
                    Text t = c.GetComponent<Text>() ?? c.gameObject.AddComponent<Text>();
                    t.text = content;
                    t.alignment = anchor;
                    t.fontSize = fontSize;
                    t.color = Color.white;
                    if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    ConfigureRect(c.GetComponent<RectTransform>() ?? c.gameObject.AddComponent<RectTransform>(), anchoredPos, size, anchor == TextAnchor.MiddleCenter);
                    return t;
                }
            }

            GameObject go = DefaultControls.CreateText(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.alignment = anchor;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConfigureRect(go.GetComponent<RectTransform>(), anchoredPos, size, anchor == TextAnchor.MiddleCenter);
            return text;
        }

        private static void ConfigureRect(RectTransform rt, Vector2 anchoredPos, Vector2 size, bool centered)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static void SetupPanelRect(GameObject go, bool active)
        {
            RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.SetActive(active);
        }

        private static void EnsurePanelImage(GameObject go, Color color)
        {
            Image img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = color;
        }

        private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBool(UnityEngine.Object target, string field, bool value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetString(UnityEngine.Object target, string field, string value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetInt(UnityEngine.Object target, string field, int value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetFloat(UnityEngine.Object target, string field, float value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(UnityEngine.Object target, string field, int index, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.enumValueIndex = index;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetMask(UnityEngine.Object target, string field, int value, StringBuilder log)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                log.AppendLine("[WARN] Missing field: " + target.GetType().Name + "." + field);
                return;
            }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static Sprite EnsureSolidSpriteAsset(StringBuilder log, ref int warnings)
        {
            EnsureFolder("Assets/_Art");
            EnsureFolder(GeneratedArtFolder);

            if (!File.Exists(SolidSpritePath))
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply(false, true);
                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                File.WriteAllBytes(SolidSpritePath, png);
                AssetDatabase.ImportAsset(SolidSpritePath, ImportAssetOptions.ForceUpdate);
                log.AppendLine("[CREATE] " + SolidSpritePath);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SolidSpritePath) as TextureImporter;
            if (importer != null)
            {
                bool importerChanged = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importerChanged = true;
                }

                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 1f))
                {
                    importer.spritePixelsPerUnit = 1f;
                    importerChanged = true;
                }

                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    importerChanged = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importerChanged = true;
                }

                if (importerChanged)
                {
                    importer.SaveAndReimport();
                    log.AppendLine("[UPDATE] Solid sprite importer settings.");
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
            if (sprite == null)
            {
                warnings++;
                log.AppendLine("[WARN] Solid sprite load failed: " + SolidSpritePath);
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] seg = path.Split('/');
            string cur = seg[0];
            for (int i = 1; i < seg.Length; i++)
            {
                string next = cur + "/" + seg[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, seg[i]);
                }
                cur = next;
            }
        }
    }
}
#endif
