// Path: Assets/_Scripts/Core/GameManager.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using ARPGDemo.Audio;
using ARPGDemo.Tools;
using ARPGDemo.UI;
using ARPGDemo.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Core
{
    public class GameManager : SingletonMono<GameManager>
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string nextGameplaySceneName = "SampleScene_02";
        [SerializeField] private bool loadNextSceneOnFinishZone = true;

        [Header("Boot")]
        [SerializeField] private bool enterMainMenuOnStart = true;
        [SerializeField] private bool resetTimeScaleOnStart = true;

        [Header("BGM Interface")]
        [SerializeField] private bool enableSceneBgm = true;
        [SerializeField] private AudioClip mainMenuBgmClip;
        [SerializeField] private AudioClip gameplayBgmClip;
        [SerializeField] private AudioClip nextGameplayBgmClip;
        [SerializeField] private AudioClip fallbackBgmClip;
        [SerializeField] [Range(0f, 1f)] private float bgmVolumeScale = 1f;
        [SerializeField] private bool bgmVerboseLog = false;

        [Header("SFX Interface")]
        [SerializeField] private bool enableSceneSfx = true;
        [SerializeField] [Range(0f, 1f)] private float sfxVolumeScale = 1f;
        [SerializeField] private bool sfxVerboseLog = false;
        [SerializeField] private AudioClip uiClickSfxClip;
        [SerializeField] private AudioClip playerAttackSfxClip;
        [SerializeField] private AudioClip enemyAttackSfxClip;
        [SerializeField] private AudioClip hitSfxClip;
        [SerializeField] private AudioClip criticalHitSfxClip;
        [SerializeField] private AudioClip chestOpenSfxClip;

        [Header("Combat Feedback")]
        [SerializeField] private bool enableCombatFeedback = true;

        [Header("Scene Runtime Safety")]
        [SerializeField] private bool autoEnsureSceneRuntimeReadiness = true;
        [SerializeField] private bool sceneRuntimeLog = true;
        [SerializeField] private float fallbackCameraOrthoSize = 7f;

        [Header("Global Hotkeys")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private KeyCode saveKey = KeyCode.F5;
        [SerializeField] private KeyCode loadKey = KeyCode.F9;

        [Header("Result Messages")]
        [SerializeField] private string victoryMessage = "Victory!";
        [SerializeField] private string defeatMessage = "Game Over";

        [Header("Result Rules")]
        [SerializeField] private bool autoVictoryWhenAllEnemiesDead = false;
        [SerializeField] private bool logEnemyDeathWithoutVictory = true;

        [Header("Save Keys")]
        [SerializeField] private string savePrefix = "ARPG_DEMO_SAVE_";

        [Header("HUD Auto Setup")]
        [SerializeField] private bool autoEnsureBattleHud = true;
        [SerializeField] private bool hudSetupLog = true;

        [Header("Enemy Visual Alignment")]
        [SerializeField] private bool autoAlignEnemyVisualChain = true;
        [SerializeField] private float enemyBarAnchorBaseY = 1.45f;
        [SerializeField] private float enemyBarAnchorEliteBonusY = 0.18f;
        [SerializeField] private float enemyShadowLocalY = -0.55f;
        [SerializeField] private float enemySpritePivotExtraY = 0f;
        [SerializeField] private bool suppressEnemySpriteVisualsAtRuntime = true;

        [Header("Combat Collision Isolation")]
        [SerializeField] private bool isolatePlayerEnemyBodyCollision = true;
        [SerializeField] private string playerBodyLayerName = "Player";
        [SerializeField] private string enemyBodyLayerName = "Enemy";
        [SerializeField] private bool collisionIsolationLog = true;

        [Header("Sample Combat Lane Auto Fix")]
        [SerializeField] private bool autoEnsureSampleEnemyCombatLane = true;
        [SerializeField] private bool combatLaneSetupLog = true;

        [Header("Player Survivability Tuning")]
        [SerializeField] private bool autoTunePlayerSurvivability = true;
        [SerializeField] private float tunedPlayerMaxHealth = 260f;
        [SerializeField] private float tunedPlayerDefensePower = 6f;
        [SerializeField] private float tunedPlayerHurtInvincibleDuration = 0.45f;

        [Header("Right Boundary Manual Fix")]
        [SerializeField] private bool autoEnsureRightBoundary = true;
        [SerializeField] private Vector2 rightBoundaryLocalPosition = new Vector2(14.8f, 1f);
        [SerializeField] private Vector2 rightBoundaryColliderSize = new Vector2(1f, 10f);

        [Header("Boundary Manual Setup")]
        [SerializeField] private bool autoEnsureSideBoundaries = false;
        [SerializeField] private float boundaryThickness = 1f;
        [SerializeField] private float boundaryHeight = 8f;
        [SerializeField] private float boundaryPadding = 0.6f;

        [Header("Boundary Safety Clamp (Disabled)")]
        [SerializeField] private bool enforceSafeBoundaryPlacement = false;
        [SerializeField] private float safeBoundaryLeftLocalX = -14.6f;
        [SerializeField] private float safeBoundaryRightLocalX = 14.6f;
        [SerializeField] private float safeBoundaryLocalY = 0f;
        [SerializeField] private float safeBoundaryColliderHeight = 8f;
        [SerializeField] private bool boundarySafetyLog = true;

        private GameFlowState currentState = GameFlowState.MainMenu;
        private ActorStats playerStats;
        private PlayerCoreData playerCoreData;
        private DemoBgmService demoBgmService;
        private DemoSfxService demoSfxService;
        private CombatFeedbackService combatFeedbackService;
        private const BindingFlags PrivateInstanceField = BindingFlags.Instance | BindingFlags.NonPublic;

        private struct EnemyLaneConfig
        {
            public EnemyLaneConfig(
                string actorId,
                Vector3 worldPosition,
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
                float attackFlatBonus)
            {
                ActorId = actorId;
                WorldPosition = worldPosition;
                MaxHealth = maxHealth;
                AttackPower = attackPower;
                DefensePower = defensePower;
                PatrolSpeed = patrolSpeed;
                PatrolRange = patrolRange;
                ChaseSpeed = chaseSpeed;
                DetectRange = detectRange;
                AttackRange = attackRange;
                AttackCooldown = attackCooldown;
                AttackDamageMultiplier = attackDamageMultiplier;
                AttackFlatBonus = attackFlatBonus;
            }

            public string ActorId { get; }
            public Vector3 WorldPosition { get; }
            public float MaxHealth { get; }
            public float AttackPower { get; }
            public float DefensePower { get; }
            public float PatrolSpeed { get; }
            public float PatrolRange { get; }
            public float ChaseSpeed { get; }
            public float DetectRange { get; }
            public float AttackRange { get; }
            public float AttackCooldown { get; }
            public float AttackDamageMultiplier { get; }
            public float AttackFlatBonus { get; }
        }

        private static readonly EnemyLaneConfig[] SampleEnemyLane =
        {
            new EnemyLaneConfig("Enemy_01", new Vector3(-7f, -0.6f, 0f), 70f, 12f, 3f, 0f, 0f, 1.8f, 4.2f, 1.15f, 1.35f, 0.8f, 0f),
            new EnemyLaneConfig("Enemy_02", new Vector3(-1.5f, -0.6f, 0f), 95f, 15f, 4f, 0f, 0f, 2.2f, 5.2f, 1.2f, 1.35f, 0.78f, 0f),
            new EnemyLaneConfig("Enemy_03", new Vector3(3.8f, -0.6f, 0f), 105f, 17f, 4.5f, 0f, 0f, 2.35f, 5.8f, 1.25f, 1.45f, 0.82f, 0.2f),
            new EnemyLaneConfig("Enemy_04", new Vector3(9.2f, -0.6f, 0f), 220f, 21f, 8f, 0f, 0f, 2.45f, 6.5f, 1.3f, 1.2f, 0.95f, 1f),
        };

        protected override bool DontDestroyEnabled => true;

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            EventCenter.AddListener<ActorDiedEvent>(OnActorDied);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<ActorDiedEvent>(OnActorDied);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            if (resetTimeScaleOnStart)
            {
                Time.timeScale = 1f;
            }

            EnsureSceneRuntimeReadiness(SceneManager.GetActiveScene());
            EnsureCoreUiManager();
            CachePlayerReference();
            EnsurePlayerSurvivabilityTuning();
            EnsurePlayerSkillCaster();
            ApplyPlayerEnemyBodyCollisionIsolation();
            EnsureSampleEnemyCombatLane();
            EnsureEnemyVisualSuppression();
            EnsureRightBoundaryManualFix();
            EnsureMinimalBattleHud();
            EnsureFinishZoneTriggerChain();
            EnsureDemoFeatureController();
            EnsureDemoBgmService();
            EnsureDemoSfxService();
            EnsureCombatFeedbackService();

            bool canEnterMainMenu = enterMainMenuOnStart && IsCurrentSceneMainMenu();
            if (canEnterMainMenu)
            {
                SetGameFlow(GameFlowState.MainMenu, string.Empty);
            }
            else
            {
                SetGameFlow(GameFlowState.Playing, string.Empty);
            }
        }

        private void Update()
        {
            if (InputCompat.IsDown(pauseKey))
            {
                if (currentState == GameFlowState.Playing || currentState == GameFlowState.Paused)
                {
                    TogglePause();
                }
            }

            if (InputCompat.IsDown(saveKey))
            {
                SaveGame();
            }

            if (InputCompat.IsDown(loadKey))
            {
                LoadGame();
            }
        }

        public void UI_StartGame()
        {
            if (IsCurrentSceneMainMenu())
            {
                SetGameFlow(GameFlowState.Playing, string.Empty);
                LoadGameplayScene();
                return;
            }

            SetGameFlow(GameFlowState.Playing, string.Empty);
        }

        public void UI_BackToMainMenu()
        {
            if (!IsCurrentSceneMainMenu())
            {
                SetGameFlow(GameFlowState.MainMenu, string.Empty);
                LoadMainMenuScene();
                return;
            }

            SetGameFlow(GameFlowState.MainMenu, string.Empty);
        }

        public void UI_ResumeGame()
        {
            if (currentState == GameFlowState.Paused)
            {
                SetGameFlow(GameFlowState.Playing, string.Empty);
            }
        }

        public void UI_RestartCurrentScene()
        {
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.name);
        }

        public void UI_QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void TryCompleteLevelAtFinishZone()
        {
            if (currentState != GameFlowState.Playing)
            {
                return;
            }

            if (!AreAllMajorEnemiesDefeated())
            {
                Debug.Log("[GameFlow] FinishZone reached, but enemies are not cleared yet.", this);
                return;
            }

            if (loadNextSceneOnFinishZone && TryLoadNextGameplayScene())
            {
                return;
            }

            SetGameFlow(GameFlowState.Victory, victoryMessage);
        }

        public bool AreAllMajorEnemiesDefeated()
        {
            ActorStats[] allStats = FindObjectsOfType<ActorStats>();

            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats == null || stats.Team != ActorTeam.Enemy)
                {
                    continue;
                }

                if (!IsMajorEnemy(stats))
                {
                    continue;
                }

                if (!stats.IsDead)
                {
                    return false;
                }
            }

            return true;
        }

        public void TogglePause()
        {
            if (currentState == GameFlowState.Playing)
            {
                SetGameFlow(GameFlowState.Paused, string.Empty);
            }
            else if (currentState == GameFlowState.Paused)
            {
                SetGameFlow(GameFlowState.Playing, string.Empty);
            }
        }

        public void LoadGameplayScene()
        {
            if (!string.IsNullOrEmpty(gameplaySceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(gameplaySceneName);
            }
        }

        public void LoadMainMenuScene()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private bool TryLoadNextGameplayScene()
        {
            if (string.IsNullOrEmpty(nextGameplaySceneName))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(nextGameplaySceneName))
            {
                Debug.LogWarning("[GameFlow] Next scene is not loadable: " + nextGameplaySceneName, this);
                return false;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(nextGameplaySceneName);
            return true;
        }

        private bool IsCurrentSceneMainMenu()
        {
            return string.Equals(SceneManager.GetActiveScene().name, mainMenuSceneName, StringComparison.Ordinal);
        }

        private bool IsCurrentSceneGameplay()
        {
            return string.Equals(SceneManager.GetActiveScene().name, gameplaySceneName, StringComparison.Ordinal);
        }

        private bool IsCurrentSceneNextGameplay()
        {
            return string.Equals(SceneManager.GetActiveScene().name, nextGameplaySceneName, StringComparison.Ordinal);
        }

        private void EnsureSceneRuntimeReadiness(Scene scene)
        {
            if (!autoEnsureSceneRuntimeReadiness || !scene.IsValid())
            {
                return;
            }

            EnsureSceneCamera(scene);
            EnsureSceneEventSystem(scene);
            UIManager.Instance?.CloseAll();
        }

        private void EnsureSceneCamera(Scene scene)
        {
            Camera sceneCamera = FindInactiveCameraInScene(scene);

            if (sceneCamera == null)
            {
                RuntimeLog("Missing camera in scene: " + scene.name);
                return;
            }

            if (!sceneCamera.gameObject.activeInHierarchy)
            {
                RuntimeLog("Camera is inactive in scene: " + scene.name);
            }

            if (!sceneCamera.enabled)
            {
                RuntimeLog("Camera component is disabled in scene: " + scene.name);
            }

            if (!sceneCamera.CompareTag("MainCamera"))
            {
                RuntimeLog("Camera tag is not MainCamera in scene: " + scene.name);
            }

            if (sceneCamera.targetDisplay != 0)
            {
                RuntimeLog("Camera targetDisplay is not Display 1 in scene: " + scene.name);
            }

            if (sceneCamera.transform.position.z > -0.1f)
            {
                RuntimeLog("Camera z-position is too close in scene: " + scene.name);
            }
        }

        private static Camera FindActiveCameraInScene(Scene scene)
        {
            Camera[] all = FindObjectsOfType<Camera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Camera cam = all[i];
                if (cam == null || cam.gameObject.scene != scene)
                {
                    continue;
                }

                if (!cam.gameObject.activeInHierarchy || !cam.enabled)
                {
                    continue;
                }

                return cam;
            }

            return null;
        }

        private static Camera FindInactiveCameraInScene(Scene scene)
        {
            Camera[] all = FindObjectsOfType<Camera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Camera cam = all[i];
                if (cam == null || cam.gameObject.scene != scene)
                {
                    continue;
                }

                return cam;
            }

            return null;
        }

        private void EnsureSceneEventSystem(Scene scene)
        {
            EventSystem[] all = FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < all.Length; i++)
            {
                EventSystem es = all[i];
                if (es == null || es.gameObject.scene != scene)
                {
                    continue;
                }

                if (es.GetComponent<StandaloneInputModule>() == null)
                {
                    RuntimeLog("EventSystem missing StandaloneInputModule in scene: " + scene.name);
                }

                if (!es.gameObject.activeInHierarchy)
                {
                    RuntimeLog("EventSystem is inactive in scene: " + scene.name);
                }

                return;
            }

            RuntimeLog("Missing EventSystem in scene: " + scene.name);
        }

        private void SanitizeMainMenuScene(Scene scene)
        {
            SetSceneObjectActive(scene, "Player", false);
            SetSceneObjectActive(scene, "Enemy_01", false);
            SetSceneObjectActive(scene, "Enemy_02", false);
            SetSceneObjectActive(scene, "Enemy_03", false);
            SetSceneObjectActive(scene, "Enemy_04", false);
            SetSceneObjectActive(scene, "HUD", false);
            SetSceneObjectActive(scene, "FinishZone", false);
        }

        private void SanitizeSecondGameplayScene(Scene scene)
        {
            SetSceneObjectActive(scene, "Player", true);
            SetSceneObjectActive(scene, "Ground", true);
            SetSceneObjectActive(scene, "Boundary_Left", true);
            SetSceneObjectActive(scene, "Boundary_Right", true);

            SetSceneObjectActive(scene, "Enemy_01", false);
            SetSceneObjectActive(scene, "Enemy_02", false);
            SetSceneObjectActive(scene, "Enemy_03", false);
            SetSceneObjectActive(scene, "Enemy_04", false);

            SetSceneObjectActive(scene, "FinishZone", false);
            SetSceneObjectActive(scene, "FinishZone_Visual", false);
            SetSceneObjectActive(scene, "FinishZone_Glow", false);
            SetSceneObjectActive(scene, "FinishZone_Label", false);

            EnsureGroundCollider(scene);
            EnsureBoundaryCollider(scene, "Boundary_Left");
            EnsureBoundaryCollider(scene, "Boundary_Right");
            EnsurePlayerSpawnAboveGround(scene);
        }

        private static void EnsureGroundCollider(Scene scene)
        {
            if (!TryFindSceneObject(scene, "Ground", out GameObject ground))
            {
                return;
            }

            BoxCollider2D col = ground.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = ground.AddComponent<BoxCollider2D>();
            }

            col.enabled = true;
            col.isTrigger = false;
            if (col.size.x < 8f)
            {
                col.size = new Vector2(30f, 1f);
            }
        }

        private static void EnsureBoundaryCollider(Scene scene, string boundaryName)
        {
            if (!TryFindSceneObject(scene, boundaryName, out GameObject boundary))
            {
                return;
            }

            BoxCollider2D col = boundary.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = boundary.AddComponent<BoxCollider2D>();
            }

            col.enabled = true;
            col.isTrigger = false;
            if (col.size.x < 0.4f || col.size.y < 4f)
            {
                col.size = new Vector2(Mathf.Max(1f, col.size.x), Mathf.Max(8f, col.size.y));
            }
        }

        private static void EnsurePlayerSpawnAboveGround(Scene scene)
        {
            if (!TryFindSceneObject(scene, "Player", out GameObject playerGo) || playerGo == null)
            {
                return;
            }

            if (!TryFindSceneObject(scene, "Ground", out GameObject groundGo) || groundGo == null)
            {
                return;
            }

            BoxCollider2D groundCol = groundGo.GetComponent<BoxCollider2D>();
            if (groundCol == null || !groundCol.enabled)
            {
                return;
            }

            float groundTopY = groundCol.bounds.max.y;
            float minSafeY = groundTopY + 0.9f;
            Vector3 playerPos = playerGo.transform.position;

            if (playerPos.y < minSafeY - 2f || float.IsNaN(playerPos.y))
            {
                playerGo.transform.position = new Vector3(playerPos.x, minSafeY, playerPos.z);
            }
        }

        private static void SetSceneObjectActive(Scene scene, string objectName, bool active)
        {
            if (!TryFindSceneObject(scene, objectName, out GameObject go) || go == null)
            {
                return;
            }

            if (go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private static bool TryFindSceneObject(Scene scene, string objectName, out GameObject result)
        {
            result = null;
            if (!scene.IsValid() || string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (TryFindSceneObjectRecursive(roots[i].transform, objectName, out result))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindSceneObjectRecursive(Transform root, string objectName, out GameObject result)
        {
            result = null;
            if (root == null)
            {
                return false;
            }

            if (root.name == objectName)
            {
                result = root.gameObject;
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryFindSceneObjectRecursive(root.GetChild(i), objectName, out result))
                {
                    return true;
                }
            }

            return false;
        }

        public void SaveGame()
        {
            if (!CachePlayerReference())
            {
                EventCenter.Broadcast(new SaveLoadEvent(true, false));
                return;
            }

            Vector3 pos = playerStats.transform.position;
            PlayerPrefs.SetFloat(savePrefix + "PX", pos.x);
            PlayerPrefs.SetFloat(savePrefix + "PY", pos.y);
            PlayerPrefs.SetFloat(savePrefix + "PZ", pos.z);
            PlayerPrefs.SetFloat(savePrefix + "HP", playerStats.CurrentHealth);
            PlayerPrefs.SetFloat(savePrefix + "MP", playerStats.CurrentMana);
            PlayerPrefs.SetInt(savePrefix + "DEAD", playerStats.IsDead ? 1 : 0);

            if (playerCoreData != null)
            {
                PlayerPrefs.SetInt(savePrefix + "REVIVE", playerCoreData.RemainingReviveCount);
            }

            PlayerPrefs.Save();
            EventCenter.Broadcast(new SaveLoadEvent(true, true));
        }

        public void LoadGame()
        {
            if (!CachePlayerReference())
            {
                EventCenter.Broadcast(new SaveLoadEvent(false, false));
                return;
            }

            if (!PlayerPrefs.HasKey(savePrefix + "PX"))
            {
                EventCenter.Broadcast(new SaveLoadEvent(false, false));
                return;
            }

            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat(savePrefix + "PX"),
                PlayerPrefs.GetFloat(savePrefix + "PY"),
                PlayerPrefs.GetFloat(savePrefix + "PZ"));

            float hp = PlayerPrefs.GetFloat(savePrefix + "HP", playerStats.MaxHealth);
            float mp = PlayerPrefs.GetFloat(savePrefix + "MP", playerStats.MaxMana);
            bool dead = PlayerPrefs.GetInt(savePrefix + "DEAD", 0) == 1;

            playerStats.transform.position = pos;
            playerStats.ForceSetRuntimeValues(hp, mp, dead);

            if (playerCoreData != null)
            {
                int reviveCount = PlayerPrefs.GetInt(savePrefix + "REVIVE", playerCoreData.RemainingReviveCount);
                playerCoreData.SetRemainingReviveCount(reviveCount);
            }

            if (dead)
            {
                if (playerCoreData != null && playerCoreData.TryConsumeReviveAndStart())
                {
                    SetGameFlow(GameFlowState.Playing, string.Empty);
                }
                else
                {
                    SetGameFlow(GameFlowState.Defeat, defeatMessage);
                }
            }
            else
            {
                SetGameFlow(GameFlowState.Playing, string.Empty);
            }

            EventCenter.Broadcast(new SaveLoadEvent(false, true));
        }

        private void SetGameFlow(GameFlowState nextState, string message)
        {
            GameFlowState previous = currentState;
            currentState = nextState;

            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.CloseAll();
            }

            switch (nextState)
            {
                case GameFlowState.MainMenu:
                    Time.timeScale = 1f;
                    uiManager?.ShowMainMenu(true);
                    break;

                case GameFlowState.Playing:
                    Time.timeScale = 1f;
                    break;

                case GameFlowState.Paused:
                    Time.timeScale = 0f;
                    uiManager?.ShowPause(true);
                    break;

                case GameFlowState.Victory:
                    Time.timeScale = 0f;
                    uiManager?.ShowResult(true, string.IsNullOrEmpty(message) ? victoryMessage : message);
                    break;

                case GameFlowState.Defeat:
                    Time.timeScale = 0f;
                    uiManager?.ShowResult(true, string.IsNullOrEmpty(message) ? defeatMessage : message);
                    break;
            }

            EventCenter.Broadcast(new GameFlowStateChangedEvent(previous, nextState, message));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSceneRuntimeReadiness(scene);
            EnsureCoreUiManager();
            playerStats = null;
            playerCoreData = null;
            CachePlayerReference();
            EnsurePlayerSurvivabilityTuning();
            EnsurePlayerSkillCaster();
            ApplyPlayerEnemyBodyCollisionIsolation();
            EnsureSampleEnemyCombatLane();
            EnsureEnemyVisualSuppression();
            EnsureRightBoundaryManualFix();
            EnsureMinimalBattleHud();
            EnsureFinishZoneTriggerChain();
            EnsureDemoFeatureController();
            EnsureDemoBgmService();
            EnsureDemoSfxService();
            EnsureCombatFeedbackService();
            Time.timeScale = 1f;
        }

        private void EnsurePlayerSurvivabilityTuning()
        {
            if (!autoTunePlayerSurvivability || playerStats == null)
            {
                return;
            }

            float tunedMaxHp = Mathf.Max(1f, tunedPlayerMaxHealth);
            SetPrivateField(playerStats, "maxHealth", tunedMaxHp);
            SetPrivateField(playerStats, "defensePower", Mathf.Max(0f, tunedPlayerDefensePower));
            SetPrivateField(playerStats, "hurtInvincibleDuration", Mathf.Max(0f, tunedPlayerHurtInvincibleDuration));

            if (!playerStats.IsDead)
            {
                SetPrivateField(playerStats, "currentHealth", tunedMaxHp);
            }

            playerStats.BroadcastVitals();
        }

        private void EnsureCoreUiManager()
        {
            UIManager existing = UIManager.Instance;
            if (existing != null)
            {
                return;
            }

            UIManager local = GetComponent<UIManager>();
            if (local == null)
            {
                local = gameObject.AddComponent<UIManager>();
                RuntimeLog("Attach UIManager for runtime UI event dispatch.");
            }

            if (local != null && !local.enabled)
            {
                local.enabled = true;
            }
        }

        private void EnsurePlayerSkillCaster()
        {
            Type casterType = ResolveTypeByName("ARPGDemo.Game.PlayerSkillCaster");
            if (casterType == null || !typeof(Component).IsAssignableFrom(casterType))
            {
                return;
            }

            bool ensuredAny = false;
            ActorStats[] allStats = FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats == null || stats.Team != ActorTeam.Player)
                {
                    continue;
                }

                if (EnsureSkillCasterOnPlayer(stats, casterType))
                {
                    ensuredAny = true;
                    if (playerStats == null || playerStats == stats)
                    {
                        playerStats = stats;
                    }
                }
            }

            if (!ensuredAny && playerStats != null)
            {
                EnsureSkillCasterOnPlayer(playerStats, casterType);
            }
        }

        private void EnsureDemoFeatureController()
        {
            DemoFeatureController featureController = GetComponent<DemoFeatureController>();
            if (featureController == null)
            {
                featureController = gameObject.AddComponent<DemoFeatureController>();
                RuntimeLog("Attach DemoFeatureController for pause/settings/chest/inventory runtime chain.");
            }

            if (featureController != null && !featureController.enabled)
            {
                featureController.enabled = true;
            }
        }

        private void EnsureDemoBgmService()
        {
            if (!enableSceneBgm)
            {
                return;
            }

            if (demoBgmService == null)
            {
                demoBgmService = GetComponent<DemoBgmService>();
            }

            if (demoBgmService == null)
            {
                demoBgmService = gameObject.AddComponent<DemoBgmService>();
                RuntimeLog("Attach DemoBgmService for scene BGM interface.");
            }

            if (demoBgmService == null)
            {
                return;
            }

            demoBgmService.SetVerboseLog(bgmVerboseLog);
            demoBgmService.SetGlobalVolumeScale(bgmVolumeScale);
            demoBgmService.SetFallbackClip(fallbackBgmClip);
            demoBgmService.SetTrackForScene(mainMenuSceneName, mainMenuBgmClip);
            demoBgmService.SetTrackForScene(gameplaySceneName, gameplayBgmClip);
            demoBgmService.SetTrackForScene(nextGameplaySceneName, nextGameplayBgmClip);
            demoBgmService.PlayForScene(SceneManager.GetActiveScene().name);
        }

        private void EnsureDemoSfxService()
        {
            if (!enableSceneSfx)
            {
                return;
            }

            if (demoSfxService == null)
            {
                demoSfxService = GetComponent<DemoSfxService>();
            }

            if (demoSfxService == null)
            {
                demoSfxService = gameObject.AddComponent<DemoSfxService>();
                RuntimeLog("Attach DemoSfxService for gameplay/UI SFX hooks.");
            }

            if (demoSfxService == null)
            {
                return;
            }

            demoSfxService.SetGlobalVolumeScale(sfxVolumeScale);
            demoSfxService.SetVerboseLog(sfxVerboseLog);
            demoSfxService.SetClips(
                uiClickSfxClip,
                playerAttackSfxClip,
                enemyAttackSfxClip,
                hitSfxClip,
                criticalHitSfxClip,
                chestOpenSfxClip);
        }

        private void EnsureCombatFeedbackService()
        {
            if (!enableCombatFeedback)
            {
                return;
            }

            if (combatFeedbackService == null)
            {
                combatFeedbackService = GetComponent<CombatFeedbackService>();
            }

            if (combatFeedbackService == null)
            {
                combatFeedbackService = gameObject.AddComponent<CombatFeedbackService>();
                RuntimeLog("Attach CombatFeedbackService for hit-stop/camera-shake.");
            }
        }

        private static bool EnsureSkillCasterOnPlayer(ActorStats player, Type casterType)
        {
            if (player == null || casterType == null || !typeof(Component).IsAssignableFrom(casterType))
            {
                return false;
            }

            Component caster = player.GetComponent(casterType);
            if (caster == null)
            {
                caster = player.gameObject.AddComponent(casterType);
            }

            if (caster is Behaviour behaviour && !behaviour.enabled)
            {
                behaviour.enabled = true;
            }

            return caster != null;
        }

        private static bool HasComponentTypeInScene(string fullTypeName)
        {
            Type type = ResolveTypeByName(fullTypeName);
            if (type == null)
            {
                return false;
            }

            return FindObjectOfType(type) != null;
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

        private void EnsureRightBoundaryManualFix()
        {
            if (!autoEnsureRightBoundary)
            {
                return;
            }

            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                RuntimeLog("Missing Ground root for right boundary check.");
                return;
            }

            Transform boundary = ground.transform.Find("Boundary_Right");
            if (boundary == null)
            {
                RuntimeLog("Missing Ground/Boundary_Right in scene.");
                return;
            }

            BoxCollider2D col = boundary.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                RuntimeLog("Boundary_Right missing BoxCollider2D.");
                return;
            }

            if (!col.enabled)
            {
                RuntimeLog("Boundary_Right collider is disabled.");
            }

            if (col.isTrigger)
            {
                RuntimeLog("Boundary_Right collider IsTrigger=true, expected false.");
            }
        }

        private void EnsureSampleEnemyCombatLane()
        {
            if (!autoEnsureSampleEnemyCombatLane)
            {
                return;
            }

            if (!IsCurrentSceneGameplay())
            {
                return;
            }

            Transform playerTransform = ResolvePlayerTransformForEnemyLane();
            if (playerTransform == null)
            {
                if (combatLaneSetupLog)
                {
                    Debug.LogWarning("[CombatLane] Missing Player transform, skip enemy lane auto-fix.", this);
                }

                return;
            }

            int enemyLayer = LayerMask.NameToLayer(enemyBodyLayerName);
            int playerLayer = LayerMask.NameToLayer(playerBodyLayerName);

            for (int i = 0; i < SampleEnemyLane.Length; i++)
            {
                EnsureEnemyCombatUnit(SampleEnemyLane[i], playerTransform, enemyLayer, playerLayer);
            }
        }

        private void EnsureEnemyVisualSuppression()
        {
            if (!suppressEnemySpriteVisualsAtRuntime)
            {
                return;
            }

            ActorStats[] allStats = FindObjectsOfType<ActorStats>(true);
            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats == null || stats.Team != ActorTeam.Enemy)
                {
                    continue;
                }

                SpriteRenderer[] renderers = stats.GetComponentsInChildren<SpriteRenderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    SpriteRenderer sr = renderers[j];
                    if (sr == null || !sr.enabled)
                    {
                        continue;
                    }

                    sr.enabled = false;
                }
            }
        }

        private Transform ResolvePlayerTransformForEnemyLane()
        {
            if (playerStats != null)
            {
                return playerStats.transform;
            }

            ActorStats[] allStats = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < allStats.Length; i++)
            {
                if (allStats[i] != null && allStats[i].Team == ActorTeam.Player)
                {
                    return allStats[i].transform;
                }
            }

            GameObject player = GameObject.Find("Player");
            return player != null ? player.transform : null;
        }

        private void EnsureEnemyCombatUnit(EnemyLaneConfig cfg, Transform playerTarget, int enemyLayer, int playerLayer)
        {
            GameObject enemy = GameObject.Find(cfg.ActorId);
            if (enemy == null)
            {
                enemy = new GameObject(cfg.ActorId);
                if (combatLaneSetupLog)
                {
                    Debug.Log("[CombatLane] Create missing enemy root -> " + cfg.ActorId, this);
                }
            }

            if (enemyLayer >= 0)
            {
                enemy.layer = enemyLayer;
            }

            enemy.transform.position = cfg.WorldPosition;
            enemy.transform.localScale = Vector3.one;

            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = enemy.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = Mathf.Max(1f, rb.gravityScale);
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            CapsuleCollider2D body = enemy.GetComponent<CapsuleCollider2D>();
            if (body == null)
            {
                body = enemy.AddComponent<CapsuleCollider2D>();
            }
            body.enabled = true;
            body.isTrigger = false;
            body.direction = CapsuleDirection2D.Vertical;
            body.size = new Vector2(0.8f, 1.8f);
            body.offset = Vector2.zero;

            ActorStats stats = enemy.GetComponent<ActorStats>();
            if (stats == null)
            {
                stats = enemy.AddComponent<ActorStats>();
            }
            ConfigureEnemyStats(stats, cfg);

            Transform enemyView = enemy.transform.Find("Enemy_View");
            if (enemyView == null)
            {
                GameObject viewGo = new GameObject("Enemy_View");
                viewGo.transform.SetParent(enemy.transform, false);
                enemyView = viewGo.transform;
            }
            if (enemyLayer >= 0)
            {
                enemyView.gameObject.layer = enemyLayer;
            }

            SpriteRenderer renderer = enemyView.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = enemyView.gameObject.AddComponent<SpriteRenderer>();
            }
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 18);

            Animator animator = enemyView.GetComponent<Animator>();
            if (animator == null)
            {
                animator = enemyView.gameObject.AddComponent<Animator>();
            }

            Transform hitboxRoot = enemy.transform.Find("Enemy_Hitbox");
            if (hitboxRoot == null)
            {
                GameObject hitboxGo = new GameObject("Enemy_Hitbox");
                hitboxGo.transform.SetParent(enemy.transform, false);
                hitboxRoot = hitboxGo.transform;
            }
            hitboxRoot.localPosition = Vector3.zero;
            if (enemyLayer >= 0)
            {
                hitboxRoot.gameObject.layer = enemyLayer;
            }

            AttackHitbox2D hitbox = hitboxRoot.GetComponent<AttackHitbox2D>();
            if (hitbox == null)
            {
                hitbox = hitboxRoot.gameObject.AddComponent<AttackHitbox2D>();
            }
            hitbox.enabled = true;

            Transform hitPoint = hitboxRoot.Find("HitPoint");
            if (hitPoint == null)
            {
                GameObject hitPointGo = new GameObject("HitPoint");
                hitPointGo.transform.SetParent(hitboxRoot, false);
                hitPoint = hitPointGo.transform;
            }
            hitPoint.localPosition = new Vector3(0.8f, 0f, 0f);

            ConfigureEnemyHitbox(hitbox, stats, hitPoint, playerLayer);

            EnemyAIController2D enemyAI = enemy.GetComponent<EnemyAIController2D>();
            if (enemyAI == null)
            {
                enemyAI = enemy.AddComponent<EnemyAIController2D>();
            }
            enemyAI.enabled = true;
            ConfigureEnemyAI(enemyAI, rb, animator, stats, hitbox, playerTarget, cfg);

            if (autoAlignEnemyVisualChain)
            {
                AlignEnemyNodeChain(enemy.transform, cfg.ActorId, enemyLayer, body);
            }

            stats.BroadcastVitals();

            if (combatLaneSetupLog)
            {
                Debug.Log("[CombatLane] Ready -> " + cfg.ActorId + " @ " + cfg.WorldPosition, enemy);
            }
        }

        private void AlignEnemyNodeChain(Transform enemyRoot, string actorId, int enemyLayer, CapsuleCollider2D bodyCollider)
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

            Transform spritePivot = EnsureDirectChild(enemyView, "SpritePivot");
            spritePivot.localPosition = Vector3.zero;
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
            if (enemyLayer >= 0)
            {
                spritePivot.gameObject.layer = enemyLayer;
            }

            Transform proxy = EnsureChildUnderParent(enemyView, spritePivot, "VisualProxy");
            if (proxy != null)
            {
                proxy.localPosition = Vector3.zero;
                proxy.localRotation = Quaternion.identity;

                if (enemyLayer >= 0)
                {
                    proxy.gameObject.layer = enemyLayer;
                }
            }

            Transform visualShadow = EnsureChildUnderParent(enemyView, enemyView, "VisualShadow");
            if (visualShadow != null)
            {
                visualShadow.localPosition = new Vector3(0f, enemyShadowLocalY, 0f);
                visualShadow.localRotation = Quaternion.identity;

                if (enemyLayer >= 0)
                {
                    visualShadow.gameObject.layer = enemyLayer;
                }
            }

            Transform eliteMark = FindDescendantByName(enemyView, "EliteMark");
            if (actorId == "Enemy_04" && eliteMark == null)
            {
                eliteMark = EnsureDirectChild(spritePivot, "EliteMark");
            }

            if (eliteMark != null)
            {
                if (eliteMark.parent != spritePivot)
                {
                    eliteMark.SetParent(spritePivot, false);
                }

                eliteMark.gameObject.SetActive(actorId == "Enemy_04");
                eliteMark.localPosition = new Vector3(0f, 1.18f, 0f);
                eliteMark.localRotation = Quaternion.identity;

                if (enemyLayer >= 0)
                {
                    eliteMark.gameObject.layer = enemyLayer;
                }
            }

            Transform eliteAura = FindDescendantByName(enemyView, "EliteAura");
            if (actorId == "Enemy_04" && eliteAura == null)
            {
                eliteAura = EnsureDirectChild(spritePivot, "EliteAura");
            }

            if (eliteAura != null)
            {
                if (eliteAura.parent != spritePivot)
                {
                    eliteAura.SetParent(spritePivot, false);
                }

                eliteAura.gameObject.SetActive(actorId == "Enemy_04");
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

            NormalizeEnemyMainSpriteCarrier(enemyView, proxy);
            ApplyEnemySpritePivotCompensation(spritePivot, proxy);

            Transform barAnchor = EnsureEnemyBarAnchor(enemyRoot, actorId, bodyCollider);
            if (barAnchor != null && enemyLayer >= 0)
            {
                barAnchor.gameObject.layer = enemyLayer;
            }
        }

        private Transform EnsureEnemyBarAnchor(Transform enemyRoot, string actorId, CapsuleCollider2D bodyCollider)
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
                }
                else
                {
                    GameObject anchorGo = new GameObject("BarAnchor");
                    anchorGo.transform.SetParent(enemyRoot, false);
                    barAnchor = anchorGo.transform;
                }
            }

            Transform enemyView = enemyRoot.Find("Enemy_View");
            Transform spritePivot = enemyView != null ? enemyView.Find("SpritePivot") : null;
            Transform proxy = enemyView != null ? FindDescendantByName(enemyView, "VisualProxy") : null;
            float proxyScaleY = proxy != null ? Mathf.Abs(proxy.localScale.y) : 1f;
            float spritePivotY = spritePivot != null ? spritePivot.localPosition.y : 0f;

            float halfBodyHeight = 0.9f;
            if (bodyCollider != null)
            {
                halfBodyHeight = Mathf.Max(0.6f, bodyCollider.offset.y + bodyCollider.size.y * 0.5f);
            }

            float localY = Mathf.Max(enemyBarAnchorBaseY, halfBodyHeight + 0.55f + spritePivotY + Mathf.Max(0f, proxyScaleY - 1f) * 0.25f);

            SpriteRenderer proxyRenderer = proxy != null ? proxy.GetComponent<SpriteRenderer>() : null;
            if (proxyRenderer != null && proxyRenderer.sprite != null)
            {
                float ppu = Mathf.Max(1f, proxyRenderer.sprite.pixelsPerUnit);
                float topY = spritePivotY
                    + ((proxyRenderer.sprite.rect.height - proxyRenderer.sprite.pivot.y) / ppu) * proxyScaleY;
                localY = Mathf.Max(localY, topY + 0.2f);
            }

            if (actorId == "Enemy_04")
            {
                localY += enemyBarAnchorEliteBonusY;
            }

            localY = Mathf.Clamp(localY, 1.3f, 2.2f);

            barAnchor.localPosition = new Vector3(0f, localY, 0f);
            barAnchor.localRotation = Quaternion.identity;
            barAnchor.localScale = Vector3.one;

            return barAnchor;
        }

        private Transform ResolveEnemyBarTargetTransform(ActorStats stats)
        {
            if (stats == null)
            {
                return null;
            }

            Transform enemyRoot = stats.transform;
            Transform barAnchor = enemyRoot.Find("BarAnchor");
            if (barAnchor == null)
            {
                Transform legacyHeadAnchor = enemyRoot.Find("HeadAnchor");
                if (legacyHeadAnchor != null)
                {
                    legacyHeadAnchor.name = "BarAnchor";
                    barAnchor = legacyHeadAnchor;
                }
            }

            if (barAnchor == null)
            {
                CapsuleCollider2D bodyCollider = enemyRoot.GetComponent<CapsuleCollider2D>();
                barAnchor = EnsureEnemyBarAnchor(enemyRoot, stats.ActorId, bodyCollider);
            }

            return barAnchor != null ? barAnchor : enemyRoot;
        }

        private void ApplyEnemySpritePivotCompensation(Transform spritePivot, Transform proxy)
        {
            if (spritePivot == null)
            {
                return;
            }

            float targetY = enemySpritePivotExtraY;
            if (proxy != null)
            {
                SpriteRenderer proxyRenderer = proxy.GetComponent<SpriteRenderer>();
                if (proxyRenderer != null && proxyRenderer.sprite != null)
                {
                    float ppu = Mathf.Max(1f, proxyRenderer.sprite.pixelsPerUnit);
                    float pivotY = proxyRenderer.sprite.pivot.y / ppu;
                    targetY += pivotY * Mathf.Abs(proxy.localScale.y);
                }
            }

            spritePivot.localPosition = new Vector3(0f, Mathf.Clamp(targetY, -0.1f, 1.8f), 0f);
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
        }

        private static void NormalizeEnemyMainSpriteCarrier(Transform enemyView, Transform proxy)
        {
            if (enemyView == null || proxy == null)
            {
                return;
            }

            SpriteRenderer rootRenderer = enemyView.GetComponent<SpriteRenderer>();
            SpriteRenderer proxyRenderer = proxy.GetComponent<SpriteRenderer>();

            if (proxyRenderer == null && rootRenderer != null)
            {
                proxyRenderer = proxy.gameObject.AddComponent<SpriteRenderer>();
                CopySpriteRendererSettings(rootRenderer, proxyRenderer);
            }

            if (rootRenderer != null && proxyRenderer != null && rootRenderer.enabled)
            {
                rootRenderer.enabled = false;
            }
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

        private static Transform EnsureDirectChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform EnsureChildUnderParent(Transform searchRoot, Transform desiredParent, string childName)
        {
            if (searchRoot == null || desiredParent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform child = FindDescendantByName(searchRoot, childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(desiredParent, false);
                return go.transform;
            }

            if (child.parent != desiredParent)
            {
                child.SetParent(desiredParent, false);
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

        private static void ConfigureEnemyStats(ActorStats stats, EnemyLaneConfig cfg)
        {
            SetPrivateField(stats, "actorId", cfg.ActorId);
            SetPrivateField(stats, "team", ActorTeam.Enemy);
            SetPrivateField(stats, "autoGenerateActorId", false);
            SetPrivateField(stats, "maxHealth", cfg.MaxHealth);
            SetPrivateField(stats, "attackPower", cfg.AttackPower);
            SetPrivateField(stats, "defensePower", cfg.DefensePower);
            SetPrivateField(stats, "criticalChance", 0.08f);
            SetPrivateField(stats, "criticalMultiplier", 1.4f);
            SetPrivateField(stats, "destroyOnDeath", false);
            SetPrivateField(stats, "autoHideEnemyCorpse", true);
            SetPrivateField(stats, "enemyCorpseHideDelay", 0.9f);
            SetPrivateField(stats, "disablePhysicsOnDeath", true);
            SetPrivateField(stats, "enableHitFlash", true);
            SetPrivateField(stats, "hitFlashDuration", 0.05f);
            SetPrivateField(stats, "enableDeathFade", true);
            SetPrivateField(stats, "deathFadeDuration", 0.35f);
            SetPrivateField(stats, "disableRenderersAfterFade", true);

            float maxMana = GetPrivateField(stats, "maxMana", 50f);
            SetPrivateField(stats, "currentHealth", cfg.MaxHealth);
            SetPrivateField(stats, "currentMana", maxMana);
            SetPrivateField(stats, "isDead", false);
            SetPrivateField(stats, "invincibleUntilTime", 0f);
            SetPrivateField(stats, "initialized", true);
        }

        private static void ConfigureEnemyHitbox(AttackHitbox2D hitbox, ActorStats owner, Transform hitPoint, int playerLayer)
        {
            SetPrivateField(hitbox, "ownerStats", owner);
            SetPrivateField(hitbox, "hitPoint", hitPoint);
            SetPrivateField(hitbox, "hitBoxSize", new Vector2(1.2f, 0.8f));
            SetPrivateField(hitbox, "localOffset", new Vector2(0.8f, 0f));

            LayerMask targetMask = playerLayer >= 0 ? (LayerMask)(1 << playerLayer) : (LayerMask)(~0);
            SetPrivateField(hitbox, "targetLayers", targetMask);

            SetPrivateField(hitbox, "hitOneTargetOnlyOncePerWindow", true);
            SetPrivateField(hitbox, "autoFrameEventSimulation", true);
            SetPrivateField(hitbox, "autoHitDelay", 0.08f);
            SetPrivateField(hitbox, "autoWindowDuration", 0.12f);
            SetPrivateField(hitbox, "enableHitKnockback", true);
            SetPrivateField(hitbox, "hitKnockbackForce", 2.6f);
            SetPrivateField(hitbox, "hitKnockbackUpwardBias", 0.24f);
        }

        private static void ConfigureEnemyAI(
            EnemyAIController2D enemyAI,
            Rigidbody2D rb,
            Animator animator,
            ActorStats stats,
            AttackHitbox2D hitbox,
            Transform playerTarget,
            EnemyLaneConfig cfg)
        {
            SetPrivateField(enemyAI, "rb", rb);
            SetPrivateField(enemyAI, "animator", animator);
            SetPrivateField(enemyAI, "stats", stats);
            SetPrivateField(enemyAI, "attackHitbox", hitbox);
            SetPrivateField(enemyAI, "playerTarget", playerTarget);

            SetPrivateField(enemyAI, "stateSwitchCooldown", 0.12f);
            SetPrivateField(enemyAI, "idleDuration", 0.9f);
            SetPrivateField(enemyAI, "hurtDuration", 0.25f);

            SetPrivateField(enemyAI, "patrolSpeed", cfg.PatrolSpeed);
            SetPrivateField(enemyAI, "patrolRange", cfg.PatrolRange);
            SetPrivateField(enemyAI, "chaseSpeed", cfg.ChaseSpeed);
            SetPrivateField(enemyAI, "detectRange", cfg.DetectRange);
            SetPrivateField(enemyAI, "attackRange", cfg.AttackRange);
            SetPrivateField(enemyAI, "attackCooldown", cfg.AttackCooldown);
            SetPrivateField(enemyAI, "attackDamageMultiplier", cfg.AttackDamageMultiplier);
            SetPrivateField(enemyAI, "attackFlatBonus", cfg.AttackFlatBonus);
            SetPrivateField(enemyAI, "attackIgnoreInvincible", false);

            SetPrivateField(enemyAI, "attackTrigger", "Attack");
            SetPrivateField(enemyAI, "hurtTrigger", "Hurt");
            SetPrivateField(enemyAI, "deathTrigger", "Death");

            SetPrivateField(enemyAI, "autoFindPlayer", true);
            SetPrivateField(enemyAI, "playerSearchInterval", 0.5f);
            SetPrivateField(enemyAI, "drawDebugGizmos", true);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstanceField);
            if (field == null)
            {
                return;
            }

            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName, T fallback)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return fallback;
            }

            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstanceField);
            if (field == null)
            {
                return fallback;
            }

            object value = field.GetValue(target);
            if (value is T typed)
            {
                return typed;
            }

            return fallback;
        }

        private void ApplyPlayerEnemyBodyCollisionIsolation()
        {
            if (!isolatePlayerEnemyBodyCollision)
            {
                return;
            }

            int playerLayer = LayerMask.NameToLayer(playerBodyLayerName);
            int enemyLayer = LayerMask.NameToLayer(enemyBodyLayerName);
            if (playerLayer < 0 || enemyLayer < 0)
            {
                if (collisionIsolationLog)
                {
                    Debug.LogWarning(
                        "[Combat][CollisionIsolation] Invalid layer name(s): Player=" + playerBodyLayerName +
                        ", Enemy=" + enemyBodyLayerName,
                        this);
                }

                return;
            }

            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

            if (collisionIsolationLog)
            {
                Debug.Log(
                    "[Combat][CollisionIsolation] Ignore Player-Enemy body collision. layers=(" +
                    playerLayer + "," + enemyLayer + ")",
                    this);
            }
        }

        private void OnActorDied(ActorDiedEvent evt)
        {
            if (evt.Team == ActorTeam.Player)
            {
                if (playerCoreData != null && playerCoreData.TryConsumeReviveAndStart())
                {
                    SetGameFlow(GameFlowState.Playing, string.Empty);
                    return;
                }

                SetGameFlow(GameFlowState.Defeat, defeatMessage);
                return;
            }

            if (evt.Team == ActorTeam.Enemy)
            {
                if (!autoVictoryWhenAllEnemiesDead)
                {
                    if (logEnemyDeathWithoutVictory && currentState == GameFlowState.Playing)
                    {
                        Debug.Log("[GameFlow] Enemy died (" + evt.ActorId + "), keep Playing. AutoVictoryWhenAllEnemiesDead=false", this);
                    }

                    return;
                }

                CheckVictoryCondition();
            }
        }

        private void CheckVictoryCondition()
        {
            if (!autoVictoryWhenAllEnemiesDead)
            {
                return;
            }

            if (currentState != GameFlowState.Playing)
            {
                return;
            }

            ActorStats[] allStats = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats.Team == ActorTeam.Enemy && !stats.IsDead)
                {
                    return;
                }
            }

            SetGameFlow(GameFlowState.Victory, victoryMessage);
        }

        private static bool IsMajorEnemy(ActorStats stats)
        {
            if (stats == null)
            {
                return false;
            }

            if (stats.ActorId == "DummyEnemy")
            {
                return false;
            }

            if (stats.gameObject != null && stats.gameObject.name.Contains("DummyEnemy"))
            {
                return false;
            }

            return true;
        }

        private void EnsureMinimalBattleHud()
        {
            if (!autoEnsureBattleHud)
            {
                return;
            }

            if (IsCurrentSceneMainMenu())
            {
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                HudLog("Missing Canvas.");
                return;
            }

            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
            }

            if (!canvas.enabled)
            {
                canvas.enabled = true;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null && HasZeroScale(canvasRect.localScale))
            {
                canvasRect.localScale = Vector3.one;
                HudLog("Canvas scale repaired to 1,1,1");
            }

            DisableStaleHudRoots(canvas);

            Transform hudRootTransform = canvas.transform.Find("HUD");
            if (hudRootTransform == null)
            {
                GameObject created = new GameObject("HUD", typeof(RectTransform));
                created.transform.SetParent(canvas.transform, false);
                hudRootTransform = created.transform;
                HudLog("Create HUD root");
            }

            if (!hudRootTransform.gameObject.activeSelf)
            {
                hudRootTransform.gameObject.SetActive(true);
            }

            PlayerHUDController playerHud = hudRootTransform.GetComponent<PlayerHUDController>();
            if (playerHud == null)
            {
                playerHud = hudRootTransform.gameObject.AddComponent<PlayerHUDController>();
                HudLog("Attach PlayerHUDController");
            }

            if (!playerHud.enabled)
            {
                playerHud.enabled = true;
            }

            DisableDuplicatePlayerHudControllers(playerHud);
            HudLog("Active PlayerHUDController -> " + GetPath(playerHud.transform));

            ActorStats[] enemyStats = FindEnemiesForWorldBars();
            if (enemyStats == null || enemyStats.Length == 0)
            {
                DisableOrphanEnemyWorldBars(canvas, Array.Empty<string>());
                HudLog("No major enemies found for world bar.");
                return;
            }

            string[] keepBars = new string[enemyStats.Length];
            for (int i = 0; i < enemyStats.Length; i++)
            {
                ActorStats stats = enemyStats[i];
                if (stats == null)
                {
                    continue;
                }

                string barName = stats.ActorId + "_WorldBar";
                keepBars[i] = barName;

                Transform enemyBarTransform = canvas.transform.Find(barName);
                if (enemyBarTransform == null)
                {
                    GameObject created = new GameObject(barName, typeof(RectTransform));
                    created.transform.SetParent(canvas.transform, false);
                    enemyBarTransform = created.transform;
                    HudLog("Create " + barName + " root");
                }

                GameObject enemyBarObject = enemyBarTransform.gameObject;
                if (!enemyBarObject.activeSelf)
                {
                    enemyBarObject.SetActive(true);
                }

                RectTransform enemyBarRect = enemyBarObject.GetComponent<RectTransform>();
                if (enemyBarRect != null)
                {
                    enemyBarRect.anchorMin = new Vector2(0.5f, 0.5f);
                    enemyBarRect.anchorMax = new Vector2(0.5f, 0.5f);
                    enemyBarRect.pivot = new Vector2(0.5f, 0.5f);
                    enemyBarRect.localScale = Vector3.one;
                }

                UIFollowTarget2D enemyFollow = enemyBarObject.GetComponent<UIFollowTarget2D>();
                if (enemyFollow == null)
                {
                    enemyFollow = enemyBarObject.AddComponent<UIFollowTarget2D>();
                    HudLog("Attach UIFollowTarget2D -> " + barName);
                }

                if (!enemyFollow.enabled)
                {
                    enemyFollow.enabled = true;
                }

                enemyFollow.WorldTarget = ResolveEnemyBarTargetTransform(stats);
                enemyFollow.WorldOffset = Vector3.zero;

                EnemyWorldBarController enemyBar = enemyBarObject.GetComponent<EnemyWorldBarController>();
                if (enemyBar == null)
                {
                    enemyBar = enemyBarObject.AddComponent<EnemyWorldBarController>();
                    HudLog("Attach EnemyWorldBarController -> " + barName);
                }

                if (!enemyBar.enabled)
                {
                    enemyBar.enabled = true;
                }

                enemyBar.BindToTarget(stats);
            }

            DisableOrphanEnemyWorldBars(canvas, keepBars);
        }

        private static bool HasZeroScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.x, 0f) ||
                   Mathf.Approximately(scale.y, 0f) ||
                   Mathf.Approximately(scale.z, 0f);
        }

        private static ActorStats[] FindEnemiesForWorldBars()
        {
            ActorStats[] allStats = FindObjectsOfType<ActorStats>();
            List<ActorStats> result = new List<ActorStats>();

            for (int i = 0; i < allStats.Length; i++)
            {
                ActorStats stats = allStats[i];
                if (stats == null || stats.Team != ActorTeam.Enemy)
                {
                    continue;
                }

                if (!IsMajorEnemy(stats))
                {
                    continue;
                }

                result.Add(stats);
            }

            result.Sort((a, b) =>
            {
                string aid = a != null ? a.ActorId : string.Empty;
                string bid = b != null ? b.ActorId : string.Empty;

                if (!string.IsNullOrEmpty(aid) && !string.IsNullOrEmpty(bid))
                {
                    int idCmp = string.CompareOrdinal(aid, bid);
                    if (idCmp != 0)
                    {
                        return idCmp;
                    }
                }

                float ax = a != null ? a.transform.position.x : 0f;
                float bx = b != null ? b.transform.position.x : 0f;
                return ax.CompareTo(bx);
            });

            return result.ToArray();
        }

        private void DisableOrphanEnemyWorldBars(Canvas canvas, string[] keepBars)
        {
            if (canvas == null)
            {
                return;
            }

            Transform[] all = canvas.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform tr = all[i];
                if (tr == null)
                {
                    continue;
                }

                string barName = tr.name;
                if (string.IsNullOrEmpty(barName) || !barName.EndsWith("_WorldBar"))
                {
                    continue;
                }

                if (!barName.StartsWith("Enemy_"))
                {
                    continue;
                }

                bool keep = false;
                for (int k = 0; k < keepBars.Length; k++)
                {
                    if (keepBars[k] == barName)
                    {
                        keep = true;
                        break;
                    }
                }

                if (keep)
                {
                    continue;
                }

                EnemyWorldBarController bar = tr.GetComponent<EnemyWorldBarController>();
                if (bar != null && bar.enabled)
                {
                    bar.enabled = false;
                }

                UIFollowTarget2D follow = tr.GetComponent<UIFollowTarget2D>();
                if (follow != null && follow.enabled)
                {
                    follow.enabled = false;
                }

                if (tr.gameObject.activeSelf)
                {
                    tr.gameObject.SetActive(false);
                }

                HudLog("Disable stale enemy world bar -> " + GetPath(tr));
            }
        }

        private void EnsureFinishZoneTriggerChain()
        {
            if (IsCurrentSceneNextGameplay())
            {
                return;
            }

            GameObject finishZone = GameObject.Find("Ground/FinishZone");
            if (finishZone == null)
            {
                finishZone = GameObject.Find("FinishZone");
            }

            if (finishZone == null)
            {
                RuntimeLog("Missing Ground/FinishZone.");
                return;
            }

            if (!finishZone.activeSelf)
            {
                finishZone.SetActive(true);
            }

            Collider2D triggerCollider = finishZone.GetComponent<Collider2D>();
            if (triggerCollider == null)
            {
                BoxCollider2D box = finishZone.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.5f, 3f);
                box.offset = Vector2.zero;
                triggerCollider = box;
            }

            if (!triggerCollider.enabled)
            {
                triggerCollider.enabled = true;
            }

            if (!triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }

            LevelFinishZoneTrigger finishTrigger = finishZone.GetComponent<LevelFinishZoneTrigger>();
            if (finishTrigger == null)
            {
                finishTrigger = finishZone.AddComponent<LevelFinishZoneTrigger>();
            }

            if (!finishTrigger.enabled)
            {
                finishTrigger.enabled = true;
            }
        }

        private void EnsureSideBoundaryCollisionChain()
        {
            // Manual fixed boundaries are now authored directly in scene.
            // Any runtime auto-reposition logic is intentionally disabled.
        }

        private void EnforceSafeBoundaryPlacement()
        {
            // Manual fixed boundaries are now authored directly in scene.
            // Any runtime auto-reposition logic is intentionally disabled.
        }

        private void EnsureSafeBoundary(Transform groundRoot, string boundaryName, float localX, float localY, int layer)
        {
            if (groundRoot == null)
            {
                return;
            }

            Transform boundary = groundRoot.Find(boundaryName);
            if (boundary == null)
            {
                GameObject created = new GameObject(boundaryName);
                created.transform.SetParent(groundRoot, false);
                boundary = created.transform;
            }

            boundary.gameObject.layer = layer;
            boundary.localPosition = new Vector3(localX, localY, 0f);
            boundary.localRotation = Quaternion.identity;
            boundary.localScale = Vector3.one;

            BoxCollider2D col = boundary.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = boundary.gameObject.AddComponent<BoxCollider2D>();
            }

            col.enabled = true;
            col.isTrigger = false;
            col.offset = Vector2.zero;
            col.size = new Vector2(
                Mathf.Max(0.2f, boundaryThickness),
                Mathf.Max(2f, safeBoundaryColliderHeight));

            if (boundarySafetyLog)
            {
                Debug.Log(
                    "[BoundarySafety] " + boundaryName +
                    " -> localPos=(" + localX + "," + localY + "), size=(" + col.size.x + "," + col.size.y + ")",
                    boundary);
            }
        }

        private void ResolvePlayableGroundRange(Transform groundRoot, ref float minX, ref float maxX, ref float baselineY)
        {
            if (groundRoot == null)
            {
                return;
            }

            string[] segmentNames = { "Ground_Spawn", "Ground_Tutorial", "Ground_Mid", "Ground_Final", "Ground_Goal" };
            bool hasAny = false;
            float lowestY = float.PositiveInfinity;

            for (int i = 0; i < segmentNames.Length; i++)
            {
                Transform seg = groundRoot.Find(segmentNames[i]);
                if (seg == null)
                {
                    continue;
                }

                BoxCollider2D col = seg.GetComponent<BoxCollider2D>();
                if (col == null || !col.enabled || col.isTrigger)
                {
                    continue;
                }

                hasAny = true;
                float absScaleX = Mathf.Abs(seg.lossyScale.x);
                float absScaleY = Mathf.Abs(seg.lossyScale.y);
                float centerX = seg.position.x + col.offset.x * absScaleX;
                float centerY = seg.position.y + col.offset.y * absScaleY;
                float halfW = col.size.x * absScaleX * 0.5f;
                float halfH = col.size.y * absScaleY * 0.5f;

                minX = Mathf.Min(minX, centerX - halfW);
                maxX = Mathf.Max(maxX, centerX + halfW);
                lowestY = Mathf.Min(lowestY, centerY - halfH);
            }

            if (hasAny && !float.IsPositiveInfinity(lowestY))
            {
                baselineY = lowestY;
            }
        }

        private void EnsureBoundary(Transform groundRoot, string boundaryName, float worldX, float baselineY, int layer, bool isLeft)
        {
            Transform boundary = groundRoot.Find(boundaryName);
            if (boundary == null)
            {
                GameObject created = new GameObject(boundaryName);
                created.transform.SetParent(groundRoot, false);
                boundary = created.transform;
            }

            boundary.gameObject.layer = layer;
            boundary.position = new Vector3(worldX, baselineY, 0f);
            boundary.rotation = Quaternion.identity;
            boundary.localScale = Vector3.one;

            BoxCollider2D col = boundary.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = boundary.gameObject.AddComponent<BoxCollider2D>();
            }

            col.enabled = true;
            col.isTrigger = false;
            col.size = new Vector2(Mathf.Max(0.2f, boundaryThickness), Mathf.Max(2f, boundaryHeight));
            col.offset = Vector2.zero;

            // If an old visual child was authored with a huge offset, normalize it so visual and collider stay aligned.
            Transform visual = boundary.Find("Visual");
            if (visual != null && Mathf.Abs(visual.localPosition.x) > 2f)
            {
                visual.localPosition = new Vector3(isLeft ? -0.1f : 0.1f, 0f, 0f);
            }
        }

        private bool CachePlayerReference()
        {
            if (playerStats != null)
            {
                return true;
            }

            ActorStats[] allStats = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < allStats.Length; i++)
            {
                if (allStats[i].Team == ActorTeam.Player)
                {
                    playerStats = allStats[i];
                    playerCoreData = playerStats.GetComponent<PlayerCoreData>();
                    return true;
                }
            }

            return false;
        }

        private void HudLog(string msg)
        {
            if (!hudSetupLog)
            {
                return;
            }

            Debug.Log("[HUD][Setup] " + msg, this);
        }

        private void RuntimeLog(string msg)
        {
            if (!sceneRuntimeLog)
            {
                return;
            }

            Debug.Log("[SceneRuntime] " + msg, this);
        }

        private void DisableStaleHudRoots(Canvas canvas)
        {
            Transform stale = canvas.transform.Find("BattleHUD_Auto");
            if (stale == null)
            {
                return;
            }

            stale.gameObject.SetActive(false);
            HudLog("Disable stale HUD root -> " + GetPath(stale));
        }

        private void DisableDuplicatePlayerHudControllers(PlayerHUDController keep)
        {
            PlayerHUDController[] all = FindObjectsOfType<PlayerHUDController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                PlayerHUDController hud = all[i];
                if (hud == null || hud == keep)
                {
                    continue;
                }

                if (hud.enabled)
                {
                    hud.enabled = false;
                }

                if (hud.gameObject.activeSelf)
                {
                    hud.gameObject.SetActive(false);
                }

                HudLog("Disable duplicate PlayerHUDController -> " + GetPath(hud.transform));
            }
        }

        private static string GetPath(Transform tr)
        {
            if (tr == null)
            {
                return "(null)";
            }

            string path = tr.name;
            Transform cur = tr.parent;
            while (cur != null)
            {
                path = cur.name + "/" + path;
                cur = cur.parent;
            }

            return path;
        }
    }
}
