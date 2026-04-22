using System.Collections;
using System.Collections.Generic;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Single-scene linear vertical slice flow:
    /// Start area (Menu) -> start portal -> corridor/encounter room (Battle) -> two enemy waves -> corridor -> boss room trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VerticalSliceFlowController : MonoBehaviour
    {
        private enum FlowStage
        {
            None = 0,
            StartArea = 1,
            RunStarted = 2,
            EncounterWaveOne = 3,
            EncounterWaveTwo = 4,
            EncounterCleared = 5,
            BossRoomActivated = 6,
        }

        [Header("Core")]
        [SerializeField] private ProcedureManager procedureManager;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private WeaponController playerWeapon;
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private BossController bossController;
        [SerializeField] private BossBrain bossBrain;
        [SerializeField] private WeaponController bossWeapon;

        [Header("Start Area")]
        [SerializeField] private Transform startAreaSpawnPoint;
        [SerializeField] private Transform startAreaCameraFocusPoint;
        [SerializeField] private RoomPortalTrigger startAreaPortal;
        [SerializeField] private bool allowStartPortalWithoutConfirm = false;
        [SerializeField] private bool roleConfirmed;

        [Header("Linear Progress Triggers")]
        [SerializeField] private RoomPortalTrigger encounterEntrySensor;
        [SerializeField] private RoomPortalTrigger bossRoomEntrySensor;
        [SerializeField] private RoomGateController encounterEntryGate;
        [SerializeField] private RoomGateController encounterExitGate;

        [Header("Encounter Wave")]
        [SerializeField] private SliceEnemyController encounterEnemyTemplate;
        [SerializeField] private Transform encounterEnemyRoot;
        [SerializeField] private Transform encounterWaveSpawnRoot;
        [SerializeField] [Min(1)] private int waveOneEnemyCount = 3;
        [SerializeField] [Min(1)] private int waveTwoEnemyCount = 4;
        [SerializeField] [Min(1f)] private float encounterEnemyHealth = 54f;
        [SerializeField] [Min(0.1f)] private float encounterEnemyMoveSpeed = 3.1f;
        [SerializeField] [Min(0f)] private float encounterEnemyContactDamage = 10f;

        [Header("Transition")]
        [SerializeField] private Image transitionOverlayImage;
        [SerializeField] [Min(0.05f)] private float transitionFadeDuration = 0.22f;
        [SerializeField] [Min(0f)] private float transitionMiddleHold = 0.08f;

        [Header("Camera")]
        [SerializeField] private bool enableCameraFollow = true;
        [SerializeField] [Min(0f)] private float cameraFollowLerp = 8f;
        [SerializeField] private float cameraMinX = -72f;
        [SerializeField] private float cameraMaxX = 52f;
        [SerializeField] private Vector2 cameraYClamp = new Vector2(-3f, 3f);

        private readonly List<SliceEnemyController> activeEncounterEnemies = new List<SliceEnemyController>(16);
        private readonly List<Transform> waveOneSpawnPoints = new List<Transform>(8);
        private readonly List<Transform> waveTwoSpawnPoints = new List<Transform>(8);

        private FlowStage flowStage = FlowStage.None;
        private bool transitionBusy;
        private bool encounterTriggered;
        private bool waveTwoSpawned;
        private bool bossActivated;

        public bool IsRoleConfirmed => roleConfirmed;

        public event System.Action<string> RolePortalBlocked;

        private void Awake()
        {
            ResolveReferences();
            CacheWaveSpawnPoints();
            BindPortalEvents();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheWaveSpawnPoints();
            BindProcedureEvents();
            BindPortalEvents();
            ApplyProcedureState(procedureManager != null ? procedureManager.CurrentProcedureType : ProcedureType.None, true);
        }

        private void OnDisable()
        {
            UnbindProcedureEvents();
            UnbindPortalEvents();
            ClearEncounterEnemies();
        }

        private void Update()
        {
            if (procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Battle)
            {
                return;
            }

            TickEncounterProgress();
        }

        private void LateUpdate()
        {
            if (!enableCameraFollow || playerHealth == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var target = playerHealth.transform.position;
            var targetPosition = new Vector3(
                Mathf.Clamp(target.x, Mathf.Min(cameraMinX, cameraMaxX), Mathf.Max(cameraMinX, cameraMaxX)),
                Mathf.Clamp(target.y, Mathf.Min(cameraYClamp.x, cameraYClamp.y), Mathf.Max(cameraYClamp.x, cameraYClamp.y)),
                camera.transform.position.z);

            var lerp = cameraFollowLerp <= 0f ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * cameraFollowLerp);
            camera.transform.position = Vector3.Lerp(camera.transform.position, targetPosition, lerp);
        }

        public void Configure(
            ProcedureManager manager,
            PlayerHealth player,
            PlayerController playerMove,
            WeaponController playerGun,
            BossHealth bossHp,
            BossController bossMove,
            BossBrain brain,
            WeaponController bossGun,
            Transform startSpawn,
            Transform startCameraFocus,
            RoomPortalTrigger startRunPortal,
            RoomPortalTrigger encounterSensor,
            RoomPortalTrigger bossSensor,
            RoomGateController entryGate,
            RoomGateController exitGate,
            SliceEnemyController enemyTemplate,
            Transform enemyRoot,
            Transform waveSpawnRoot,
            Image transitionOverlay,
            float minCameraX,
            float maxCameraX)
        {
            UnbindProcedureEvents();
            UnbindPortalEvents();

            procedureManager = manager;
            playerHealth = player;
            playerController = playerMove;
            playerWeapon = playerGun;
            bossHealth = bossHp;
            bossController = bossMove;
            bossBrain = brain;
            bossWeapon = bossGun;

            startAreaSpawnPoint = startSpawn;
            startAreaCameraFocusPoint = startCameraFocus;
            startAreaPortal = startRunPortal;
            encounterEntrySensor = encounterSensor;
            bossRoomEntrySensor = bossSensor;
            encounterEntryGate = entryGate;
            encounterExitGate = exitGate;
            encounterEnemyTemplate = enemyTemplate;
            encounterEnemyRoot = enemyRoot;
            encounterWaveSpawnRoot = waveSpawnRoot;
            transitionOverlayImage = transitionOverlay;
            cameraMinX = minCameraX;
            cameraMaxX = maxCameraX;

            ResolveReferences();
            CacheWaveSpawnPoints();
            BindPortalEvents();
            BindProcedureEvents();
            ApplyProcedureState(procedureManager != null ? procedureManager.CurrentProcedureType : ProcedureType.None, true);
        }

        public void SetRoleConfirmed(bool value)
        {
            roleConfirmed = value;
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

            if (bossBrain != null)
            {
                bossBrain.SetTargetPlayer(playerHealth);
            }
        }

        public bool IsCameraFollowingFormalPlayer(PlayerHealth formalPlayer)
        {
            return formalPlayer != null && playerHealth == formalPlayer;
        }

        private void ResolveReferences()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (playerHealth == null && RuntimeSceneHooks.Active != null)
            {
                playerHealth = RuntimeSceneHooks.Active.PlayerHealth;
            }

            if (bossHealth == null && RuntimeSceneHooks.Active != null)
            {
                bossHealth = RuntimeSceneHooks.Active.BossHealth;
            }

            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (playerWeapon == null && playerHealth != null)
            {
                playerWeapon = playerHealth.GetComponent<WeaponController>();
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

        private void CacheWaveSpawnPoints()
        {
            waveOneSpawnPoints.Clear();
            waveTwoSpawnPoints.Clear();
            if (encounterWaveSpawnRoot == null)
            {
                return;
            }

            for (var i = 0; i < encounterWaveSpawnRoot.childCount; i++)
            {
                var child = encounterWaveSpawnRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("Wave1", System.StringComparison.OrdinalIgnoreCase))
                {
                    waveOneSpawnPoints.Add(child);
                }
                else if (child.name.StartsWith("Wave2", System.StringComparison.OrdinalIgnoreCase))
                {
                    waveTwoSpawnPoints.Add(child);
                }
            }

            if (waveOneSpawnPoints.Count == 0 || waveTwoSpawnPoints.Count == 0)
            {
                for (var i = 0; i < encounterWaveSpawnRoot.childCount; i++)
                {
                    var child = encounterWaveSpawnRoot.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    if (waveOneSpawnPoints.Count == 0)
                    {
                        waveOneSpawnPoints.Add(child);
                    }

                    if (waveTwoSpawnPoints.Count == 0)
                    {
                        waveTwoSpawnPoints.Add(child);
                    }
                }
            }
        }

        private void BindProcedureEvents()
        {
            if (procedureManager == null)
            {
                return;
            }

            procedureManager.ProcedureChanged -= OnProcedureChanged;
            procedureManager.ProcedureChanged += OnProcedureChanged;
        }

        private void UnbindProcedureEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }
        }

        private void BindPortalEvents()
        {
            if (startAreaPortal != null)
            {
                startAreaPortal.PortalTriggered -= OnStartAreaPortalTriggered;
                startAreaPortal.PortalTriggered += OnStartAreaPortalTriggered;
            }

            if (encounterEntrySensor != null)
            {
                encounterEntrySensor.PortalTriggered -= OnEncounterEntrySensorTriggered;
                encounterEntrySensor.PortalTriggered += OnEncounterEntrySensorTriggered;
            }

            if (bossRoomEntrySensor != null)
            {
                bossRoomEntrySensor.PortalTriggered -= OnBossRoomEntrySensorTriggered;
                bossRoomEntrySensor.PortalTriggered += OnBossRoomEntrySensorTriggered;
            }
        }

        private void UnbindPortalEvents()
        {
            if (startAreaPortal != null)
            {
                startAreaPortal.PortalTriggered -= OnStartAreaPortalTriggered;
            }

            if (encounterEntrySensor != null)
            {
                encounterEntrySensor.PortalTriggered -= OnEncounterEntrySensorTriggered;
            }

            if (bossRoomEntrySensor != null)
            {
                bossRoomEntrySensor.PortalTriggered -= OnBossRoomEntrySensorTriggered;
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            ApplyProcedureState(current, false);
        }

        private void ApplyProcedureState(ProcedureType currentProcedure, bool immediate)
        {
            switch (currentProcedure)
            {
                case ProcedureType.Menu:
                    EnterStartArea(immediate);
                    break;
                case ProcedureType.Battle:
                    EnterRunStart(immediate);
                    break;
                default:
                    SetTriggerAvailability(false, false, false);
                    break;
            }
        }

        private void EnterStartArea(bool immediate)
        {
            flowStage = FlowStage.StartArea;
            roleConfirmed = false;
            RoleSelectionRuntimeState.Clear();
            ResetEncounterState();
            SetBossCombatEnabled(false);
            MovePlayerTo(startAreaSpawnPoint);
            FocusCamera(startAreaCameraFocusPoint != null ? startAreaCameraFocusPoint : startAreaSpawnPoint);
            SetTriggerAvailability(true, false, false);
            SetEncounterGatesLocked(true);
            if (immediate)
            {
                SetOverlayAlpha(0f);
            }

            Debug.Log("[VerticalSliceFlow] EnterStartArea: waiting for role confirmation + start portal.");
        }

        private void EnterRunStart(bool immediate)
        {
            flowStage = FlowStage.RunStarted;
            ResetEncounterState();
            SetBossCombatEnabled(false);
            MovePlayerTo(startAreaSpawnPoint);
            FocusCamera(startAreaCameraFocusPoint != null ? startAreaCameraFocusPoint : startAreaSpawnPoint);
            SetTriggerAvailability(false, true, true);
            SetEncounterGatesLocked(false);
            if (immediate)
            {
                SetOverlayAlpha(0f);
            }

            Debug.Log("[VerticalSliceFlow] Battle started. Proceed to encounter room sensor.");
        }

        private void ResetEncounterState()
        {
            encounterTriggered = false;
            waveTwoSpawned = false;
            bossActivated = false;
            ClearEncounterEnemies();
            if (encounterEntrySensor != null)
            {
                encounterEntrySensor.SetPortalEnabled(true);
            }

            if (bossRoomEntrySensor != null)
            {
                bossRoomEntrySensor.SetPortalEnabled(true);
            }
        }

        private void OnStartAreaPortalTriggered(RoomPortalTrigger portal, PlayerHealth player)
        {
            if (transitionBusy || flowStage != FlowStage.StartArea || procedureManager == null)
            {
                return;
            }

            if (!allowStartPortalWithoutConfirm && !roleConfirmed)
            {
                const string blockedMessage = "Confirm role first, then use portal.";
                Debug.Log("Start portal blocked: role has not been confirmed yet.", this);
                RolePortalBlocked?.Invoke(blockedMessage);
                return;
            }

            StartCoroutine(RunTransition(() =>
            {
                var menuProcedure = procedureManager.CurrentProcedure as ProcedureMenu;
                if (menuProcedure != null)
                {
                    menuProcedure.StartBattleFromPortal("StartAreaPortal");
                }
                else
                {
                    procedureManager.ChangeProcedure(ProcedureType.Battle);
                }
            }));
        }

        private void OnEncounterEntrySensorTriggered(RoomPortalTrigger portal, PlayerHealth player)
        {
            if (transitionBusy || procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Battle)
            {
                return;
            }

            if (encounterTriggered)
            {
                return;
            }

            encounterTriggered = true;
            flowStage = FlowStage.EncounterWaveOne;
            SetEncounterGatesLocked(true);
            if (encounterEntrySensor != null)
            {
                encounterEntrySensor.SetPortalEnabled(false);
            }

            SpawnEncounterWave(1);
            Debug.Log("[VerticalSliceFlow] Encounter locked. Wave 1 started.");
        }

        private void OnBossRoomEntrySensorTriggered(RoomPortalTrigger portal, PlayerHealth player)
        {
            if (transitionBusy || procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Battle)
            {
                return;
            }

            if (bossActivated)
            {
                return;
            }

            if (flowStage != FlowStage.EncounterCleared)
            {
                Debug.Log("[VerticalSliceFlow] Boss room trigger ignored: encounter not cleared yet.");
                return;
            }

            ActivateBossRoom();
        }

        private void TickEncounterProgress()
        {
            if (!encounterTriggered)
            {
                return;
            }

            for (var i = activeEncounterEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEncounterEnemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    activeEncounterEnemies.RemoveAt(i);
                }
            }

            if (activeEncounterEnemies.Count > 0)
            {
                return;
            }

            if (flowStage == FlowStage.EncounterWaveOne && !waveTwoSpawned)
            {
                waveTwoSpawned = true;
                flowStage = FlowStage.EncounterWaveTwo;
                SpawnEncounterWave(2);
                Debug.Log("[VerticalSliceFlow] Encounter wave 1 cleared. Wave 2 started.");
                return;
            }

            if (flowStage == FlowStage.EncounterWaveTwo)
            {
                flowStage = FlowStage.EncounterCleared;
                SetEncounterGatesLocked(false);
                Debug.Log("[VerticalSliceFlow] Encounter cleared. Gates opened, proceed to boss room.");
            }
        }

        private void SpawnEncounterWave(int waveIndex)
        {
            if (encounterEnemyTemplate == null)
            {
                Debug.LogWarning("Cannot spawn encounter wave: enemy template is missing.", this);
                return;
            }

            var spawnPoints = waveIndex == 2 ? waveTwoSpawnPoints : waveOneSpawnPoints;
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("Cannot spawn encounter wave: no spawn points found.", this);
                return;
            }

            var enemyCount = waveIndex == 2 ? Mathf.Max(1, waveTwoEnemyCount) : Mathf.Max(1, waveOneEnemyCount);
            var health = waveIndex == 2 ? encounterEnemyHealth * 1.12f : encounterEnemyHealth;
            var speed = waveIndex == 2 ? encounterEnemyMoveSpeed * 1.08f : encounterEnemyMoveSpeed;
            var touchDamage = waveIndex == 2 ? encounterEnemyContactDamage * 1.1f : encounterEnemyContactDamage;
            var parent = encounterEnemyRoot != null ? encounterEnemyRoot : transform;

            for (var i = 0; i < enemyCount; i++)
            {
                var spawnPoint = spawnPoints[i % spawnPoints.Count];
                if (spawnPoint == null)
                {
                    continue;
                }

                var enemyObject = Instantiate(
                    encounterEnemyTemplate.gameObject,
                    spawnPoint.position,
                    Quaternion.identity,
                    parent);
                enemyObject.name = "EncounterEnemy_W" + waveIndex + "_" + (i + 1);
                enemyObject.SetActive(true);

                var enemy = enemyObject.GetComponent<SliceEnemyController>();
                if (enemy == null)
                {
                    Debug.LogWarning("Encounter enemy prefab is missing SliceEnemyController.", enemyObject);
                    Destroy(enemyObject);
                    continue;
                }

                enemy.Initialize(playerHealth, health, speed, touchDamage);
                enemy.Defeated -= OnEncounterEnemyDefeated;
                enemy.Defeated += OnEncounterEnemyDefeated;
                activeEncounterEnemies.Add(enemy);
            }
        }

        private void OnEncounterEnemyDefeated(SliceEnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.Defeated -= OnEncounterEnemyDefeated;
            activeEncounterEnemies.Remove(enemy);
        }

        private void ActivateBossRoom()
        {
            bossActivated = true;
            flowStage = FlowStage.BossRoomActivated;
            if (bossRoomEntrySensor != null)
            {
                bossRoomEntrySensor.SetPortalEnabled(false);
            }

            SetBossCombatEnabled(true);
            bossWeapon?.ResetFireCooldown();
            if (bossBrain != null)
            {
                if (playerHealth != null)
                {
                    bossBrain.SetTargetPlayer(playerHealth);
                }

                bossBrain.ResetForBattle();
            }

            Debug.Log("[VerticalSliceFlow] Boss room entered. Boss combat activated.");
        }

        private void SetEncounterGatesLocked(bool locked)
        {
            encounterEntryGate?.SetLocked(locked);
            encounterExitGate?.SetLocked(locked);
        }

        private void ClearEncounterEnemies()
        {
            for (var i = activeEncounterEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEncounterEnemies[i];
                if (enemy != null)
                {
                    enemy.Defeated -= OnEncounterEnemyDefeated;
                    Destroy(enemy.gameObject);
                }
            }

            activeEncounterEnemies.Clear();
        }

        private void MovePlayerTo(Transform spawnPoint)
        {
            if (playerHealth == null || spawnPoint == null)
            {
                return;
            }

            var playerTransform = playerHealth.transform;
            playerTransform.position = spawnPoint.position;
            playerTransform.rotation = spawnPoint.rotation;

            var body = playerHealth.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void FocusCamera(Transform focusPoint)
        {
            if (focusPoint == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var target = focusPoint.position;
            camera.transform.position = new Vector3(target.x, target.y, camera.transform.position.z);
        }

        private void SetBossCombatEnabled(bool enabled)
        {
            if (bossHealth == null)
            {
                return;
            }

            var bossObject = bossHealth.gameObject;
            if (bossObject.activeSelf != enabled)
            {
                bossObject.SetActive(enabled);
            }

            if (bossController != null)
            {
                bossController.enabled = enabled;
            }

            if (bossBrain != null)
            {
                bossBrain.enabled = enabled;
            }

            if (bossWeapon != null)
            {
                bossWeapon.enabled = enabled;
            }
        }

        private void SetTriggerAvailability(bool startPortalEnabled, bool encounterSensorEnabled, bool bossSensorEnabled)
        {
            startAreaPortal?.SetPortalEnabled(startPortalEnabled);
            encounterEntrySensor?.SetPortalEnabled(encounterSensorEnabled);
            bossRoomEntrySensor?.SetPortalEnabled(bossSensorEnabled);
        }

        private IEnumerator RunTransition(System.Action middleAction)
        {
            transitionBusy = true;

            if (transitionOverlayImage != null)
            {
                yield return FadeOverlay(0f, 1f, transitionFadeDuration);
                if (transitionMiddleHold > 0f)
                {
                    yield return new WaitForSecondsRealtime(transitionMiddleHold);
                }
            }

            middleAction?.Invoke();
            yield return null;

            if (transitionOverlayImage != null)
            {
                yield return FadeOverlay(1f, 0f, transitionFadeDuration);
            }

            transitionBusy = false;
        }

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            if (transitionOverlayImage == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            SetOverlayAlpha(from);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                SetOverlayAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetOverlayAlpha(to);
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (transitionOverlayImage == null)
            {
                return;
            }

            var color = transitionOverlayImage.color;
            color.a = Mathf.Clamp01(alpha);
            transitionOverlayImage.color = color;
            transitionOverlayImage.raycastTarget = color.a > 0.01f;
        }
    }
}
