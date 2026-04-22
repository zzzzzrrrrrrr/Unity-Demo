using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Utils;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Boss
{
    /// <summary>
    /// Lightweight custom FSM for boss combat behavior.
    /// </summary>
    [RequireComponent(typeof(BossController), typeof(BossHealth))]
    [DisallowMultipleComponent]
    public sealed class BossBrain : MonoBehaviour
    {
        private enum BossState
        {
            Idle = 0,
            Chase = 1,
            Burst = 2,
            SkillFanShot = 3,
            SkillRadialNova = 4,
            Cooldown = 5,
            Dead = 6,
        }

        [Header("FSM Timing")]
        [SerializeField] private float idleDuration = 0.8f;
        [SerializeField] private float burstDuration = 1.8f;
        [SerializeField] private float burstFireInterval = 0.3f;
        [SerializeField] private float cooldownDuration = 1.0f;
        [SerializeField] private float retargetInterval = 1.0f;

        [Header("FSM Distance")]
        [SerializeField] private float engageDistanceMin = 2.2f;
        [SerializeField] private float engageDistanceMax = 5.2f;

        [Header("Skill - Fan Shot")]
        [SerializeField] private bool enableFanShot = true;
        [SerializeField] [Range(1, 7)] private int fanShotCount = 3;
        [SerializeField] private float fanSpreadAngle = 28f;
        [SerializeField] private float fanSkillWindup = 0.25f;
        [SerializeField] private float fanSkillRecovery = 0.65f;
        [SerializeField] private float fanSkillInterval = 4.5f;
        [SerializeField] [Range(1f, 1.5f)] private float fanWindupScaleMultiplier = 1.13f;
        [SerializeField] [Min(1f)] private float fanWindupPulseFrequency = 8f;
        [SerializeField] private Color fanWindupColor = new Color(1f, 0.52f, 0.4f, 1f);

        [Header("Skill - Radial Nova")]
        [SerializeField] private bool enableRadialNova = true;
        [SerializeField] [Range(6, 72)] private int radialNovaShotCount = 20;
        [SerializeField] private float radialNovaProjectileSpeedScale = 0.95f;
        [SerializeField] private float radialNovaWindup = 0.8f;
        [SerializeField] private float radialNovaRecovery = 0.72f;
        [SerializeField] private float radialNovaInterval = 8.8f;
        [SerializeField] [Range(1, 4)] private int radialNovaPulseCount = 1;
        [SerializeField] [Min(0.01f)] private float radialNovaPulseInterval = 0.16f;
        [SerializeField] [Range(1f, 1.7f)] private float radialWindupScaleMultiplier = 1.24f;
        [SerializeField] [Min(1f)] private float radialWindupPulseFrequency = 5.2f;
        [SerializeField] private Color radialWindupColor = new Color(1f, 0.22f, 0.12f, 1f);

        [Header("Low HP Aggression")]
        [SerializeField] private bool enableLowHealthAggression = true;
        [SerializeField] [Range(0.05f, 1f)] private float lowHealthThresholdNormalized = 0.35f;
        [SerializeField] [Range(0.2f, 1f)] private float lowHealthBurstFireIntervalScale = 0.65f;
        [SerializeField] [Range(0.2f, 1f)] private float lowHealthCooldownScale = 0.6f;
        [SerializeField] [Range(1f, 2f)] private float lowHealthChaseSpeedScale = 1.2f;
        [SerializeField] [Range(0.2f, 1f)] private float lowHealthRadialNovaIntervalScale = 0.72f;
        [SerializeField] [Range(0, 20)] private int lowHealthRadialNovaShotBonus = 4;

        [Header("Refs")]
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private PlayerHealth initialTargetPlayer;
        [SerializeField] private bool autoFindPlayer = true;

        private BossController bossController;
        private BossHealth bossHealth;
        private Transform target;
        private PlayerHealth targetPlayerHealth;
        private BossState state;
        private float stateTimer;
        private float nextBurstFireTime;
        private bool hasState;
        private float nextRetargetTime;
        private float nextFanSkillReadyTime;
        private float nextRadialNovaReadyTime;
        private bool fanSkillFiredInState;
        private int radialNovaPulsesFiredInState;
        private float radialNovaElapsedInState;
        private bool lowHealthAggressionActive;
        private float activeBurstFireInterval;
        private float activeCooldownDuration;
        private float activeChaseSpeedScale = 1f;
        private float activeRadialNovaInterval;
        private int activeRadialNovaShotCount;
        private bool loggedMissingWeapon;
        private bool loggedNotInBattle;
        private bool loggedNoLiveTarget;
        private Vector3 baseScale = Vector3.one;
        private bool hasBaseScale;
        private SpriteRenderer cachedSpriteRenderer;
        private Color baseTintColor = Color.white;
        private bool hasBaseTintColor;

        public bool IsRadialNovaWindupActive =>
            state == BossState.SkillRadialNova &&
            radialNovaElapsedInState < Mathf.Max(0.01f, radialNovaWindup);

        public string CurrentHudDangerText
        {
            get
            {
                if (state == BossState.SkillRadialNova && IsRadialNovaWindupActive)
                {
                    return "DANGER: RADIAL NOVA";
                }

                if (state == BossState.SkillFanShot && stateTimer > fanSkillRecovery)
                {
                    return "Warning: Fan Shot";
                }

                return string.Empty;
            }
        }

        private void Awake()
        {
            bossController = GetComponent<BossController>();
            bossHealth = GetComponent<BossHealth>();

            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }

            if (weaponController == null)
            {
                Debug.LogWarning("BossBrain on " + name + " has no WeaponController assigned.", this);
                loggedMissingWeapon = true;
            }
            else
            {
                weaponController.EnsureRuntimeReferences();
            }

            if (initialTargetPlayer != null)
            {
                SetTargetPlayer(initialTargetPlayer);
            }

            if (!hasBaseScale)
            {
                hasBaseScale = true;
                baseScale = transform.localScale;
            }

            cachedSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (cachedSpriteRenderer != null)
            {
                baseTintColor = cachedSpriteRenderer.color;
                hasBaseTintColor = true;
            }

            RefreshAdaptiveValues();
        }

        private void OnEnable()
        {
            if (bossHealth != null)
            {
                bossHealth.Defeated += OnBossDefeated;
            }
        }

        private void Start()
        {
            TryResolveTargetPlayer();
            EnterState(BossState.Idle);
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.Defeated -= OnBossDefeated;
            }
        }

        private void Update()
        {
            if (bossController == null || bossHealth == null)
            {
                return;
            }

            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
                if (weaponController != null)
                {
                    loggedMissingWeapon = false;
                }
            }

            weaponController?.EnsureRuntimeReferences();

            if (bossHealth.IsDead)
            {
                EnterState(BossState.Dead);
            }

            if (!IsBattleRunning())
            {
                if (!loggedNotInBattle)
                {
                    loggedNotInBattle = true;
                    var procedureName = GameEntryBridge.IsReady ? GameEntryBridge.Procedure.CurrentProcedureType.ToString() : "GameEntryNotReady";
                    Debug.Log("BossBrain on " + name + " is waiting for Battle procedure. Current=" + procedureName, this);
                }

                bossController.Stop();
                return;
            }

            loggedNotInBattle = false;
            RefreshAdaptiveValues();

            switch (state)
            {
                case BossState.Idle:
                    TickIdle(Time.deltaTime);
                    break;
                case BossState.Chase:
                    TickChase(Time.deltaTime);
                    break;
                case BossState.Burst:
                    TickBurst(Time.deltaTime);
                    break;
                case BossState.SkillFanShot:
                    TickSkillFanShot(Time.deltaTime);
                    break;
                case BossState.SkillRadialNova:
                    TickSkillRadialNova(Time.deltaTime);
                    break;
                case BossState.Cooldown:
                    TickCooldown(Time.deltaTime);
                    break;
                case BossState.Dead:
                    bossController.Stop();
                    break;
            }
        }

        public void SetTargetPlayer(PlayerHealth player)
        {
            targetPlayerHealth = player;
            target = player != null ? player.transform : null;
            initialTargetPlayer = player;
        }

        public void Configure(
            float newIdleDuration,
            float newBurstDuration,
            float newBurstFireInterval,
            float newCooldownDuration,
            float newEngageDistanceMin,
            float newEngageDistanceMax,
            float newRetargetInterval)
        {
            idleDuration = Mathf.Max(0f, newIdleDuration);
            burstDuration = Mathf.Max(0.1f, newBurstDuration);
            burstFireInterval = Mathf.Max(0.05f, newBurstFireInterval);
            cooldownDuration = Mathf.Max(0f, newCooldownDuration);
            engageDistanceMin = Mathf.Max(0.1f, newEngageDistanceMin);
            engageDistanceMax = Mathf.Max(engageDistanceMin + 0.1f, newEngageDistanceMax);
            retargetInterval = Mathf.Max(0.1f, newRetargetInterval);
            RefreshAdaptiveValues();
        }

        public void ConfigureFanSkill(
            bool enabled,
            int shotCount,
            float spreadAngle,
            float windupDuration,
            float recoveryDuration,
            float interval)
        {
            enableFanShot = enabled;
            fanShotCount = Mathf.Clamp(shotCount, 1, 9);
            fanSpreadAngle = Mathf.Max(0f, spreadAngle);
            fanSkillWindup = Mathf.Max(0f, windupDuration);
            fanSkillRecovery = Mathf.Max(0f, recoveryDuration);
            fanSkillInterval = Mathf.Max(0.1f, interval);
        }

        public void ConfigureRadialSkill(
            bool enabled,
            int shotCount,
            float projectileSpeedScale,
            float windupDuration,
            float recoveryDuration,
            float interval,
            int pulseCount,
            float pulseInterval)
        {
            enableRadialNova = enabled;
            radialNovaShotCount = Mathf.Clamp(shotCount, 6, 96);
            radialNovaProjectileSpeedScale = Mathf.Max(0.1f, projectileSpeedScale);
            radialNovaWindup = Mathf.Max(0f, windupDuration);
            radialNovaRecovery = Mathf.Max(0f, recoveryDuration);
            radialNovaInterval = Mathf.Max(0.1f, interval);
            radialNovaPulseCount = Mathf.Clamp(pulseCount, 1, 8);
            radialNovaPulseInterval = Mathf.Max(0.01f, pulseInterval);
            RefreshAdaptiveValues();
        }

        public void ConfigureLowHealthAggression(
            bool enabled,
            float healthThresholdNormalized,
            float burstIntervalScale,
            float cooldownScale,
            float chaseSpeedScale,
            float radialNovaIntervalScale,
            int radialNovaShotBonus)
        {
            enableLowHealthAggression = enabled;
            lowHealthThresholdNormalized = Mathf.Clamp01(healthThresholdNormalized);
            lowHealthBurstFireIntervalScale = Mathf.Clamp(burstIntervalScale, 0.2f, 1f);
            lowHealthCooldownScale = Mathf.Clamp(cooldownScale, 0.2f, 1f);
            lowHealthChaseSpeedScale = Mathf.Clamp(chaseSpeedScale, 1f, 2f);
            lowHealthRadialNovaIntervalScale = Mathf.Clamp(radialNovaIntervalScale, 0.2f, 1f);
            lowHealthRadialNovaShotBonus = Mathf.Clamp(radialNovaShotBonus, 0, 20);
            RefreshAdaptiveValues();
        }

        public void ResetForBattle()
        {
            if (bossController == null)
            {
                bossController = GetComponent<BossController>();
            }

            if (bossHealth == null)
            {
                bossHealth = GetComponent<BossHealth>();
            }

            nextBurstFireTime = 0f;
            nextRetargetTime = 0f;
            nextFanSkillReadyTime = Time.time + Mathf.Max(0.1f, fanSkillInterval);
            nextRadialNovaReadyTime = Time.time + Mathf.Max(0.1f, radialNovaInterval);
            fanSkillFiredInState = false;
            radialNovaPulsesFiredInState = 0;
            radialNovaElapsedInState = 0f;
            hasState = false;
            RefreshAdaptiveValues();

            if (bossHealth != null && !bossHealth.IsDead)
            {
                EnterState(BossState.Idle);
            }
            else
            {
                EnterState(BossState.Dead);
            }
        }

        private bool IsBattleRunning()
        {
            if (!GameEntryBridge.IsReady)
            {
                return true;
            }

            return GameEntryBridge.Procedure.CurrentProcedureType == ProcedureType.Battle;
        }

        private void TickIdle(float deltaTime)
        {
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                EnterState(BossState.Chase);
            }
        }

        private void TickChase(float deltaTime)
        {
            if (!HasLiveTarget())
            {
                if (!loggedNoLiveTarget)
                {
                    loggedNoLiveTarget = true;
                    Debug.LogWarning("BossBrain on " + name + " has no live target player.", this);
                }

                EnterState(BossState.Idle);
                return;
            }

            loggedNoLiveTarget = false;

            FaceTarget(target.position);

            var distance = Vector2.Distance(transform.position, target.position);
            if (distance > engageDistanceMax)
            {
                bossController.MoveTowards(target.position, activeChaseSpeedScale);
                return;
            }

            if (distance < engageDistanceMin)
            {
                bossController.MoveAwayFrom(target.position, Mathf.Max(0.8f, activeChaseSpeedScale * 0.85f));
                return;
            }

            bossController.Stop();
            EnterState(BossState.Burst);
        }

        private void TickBurst(float deltaTime)
        {
            if (!HasLiveTarget())
            {
                if (!loggedNoLiveTarget)
                {
                    loggedNoLiveTarget = true;
                    Debug.LogWarning("BossBrain on " + name + " has no live target player.", this);
                }

                EnterState(BossState.Cooldown);
                return;
            }

            loggedNoLiveTarget = false;

            var distance = Vector2.Distance(transform.position, target.position);
            if (distance > engageDistanceMax * 1.35f || distance < engageDistanceMin * 0.65f)
            {
                EnterState(BossState.Chase);
                return;
            }

            bossController.Stop();
            FaceTarget(target.position);
            stateTimer -= deltaTime;

            if (Time.time >= nextBurstFireTime)
            {
                nextBurstFireTime = Time.time + activeBurstFireInterval;
                var fireDirection = Physics2DUtility.SafeDirection(transform.position, target.position, Vector2.left);
                weaponController?.FireImmediate(fireDirection, gameObject);
            }

            if (stateTimer <= 0f)
            {
                EnterState(BossState.Cooldown);
            }
        }

        private void TickSkillFanShot(float deltaTime)
        {
            if (!HasLiveTarget())
            {
                EnterState(BossState.Cooldown);
                return;
            }

            stateTimer -= deltaTime;
            bossController.Stop();
            FaceTarget(target.position);
            ApplyFanShotWindupVisual();

            if (!fanSkillFiredInState && stateTimer <= fanSkillRecovery)
            {
                fanSkillFiredInState = true;
                FireFanShot();
                RestoreFanShotVisual();
            }

            if (stateTimer <= 0f)
            {
                RestoreFanShotVisual();
                EnterState(BossState.Cooldown);
            }
        }

        private void TickSkillRadialNova(float deltaTime)
        {
            if (!HasLiveTarget())
            {
                EnterState(BossState.Cooldown);
                return;
            }

            stateTimer -= deltaTime;
            radialNovaElapsedInState += Mathf.Max(0f, deltaTime);
            bossController.Stop();
            FaceTarget(target.position);
            ApplyRadialNovaWindupVisual();

            var windup = Mathf.Max(0f, radialNovaWindup);
            while (radialNovaPulsesFiredInState < Mathf.Max(1, radialNovaPulseCount))
            {
                var pulseFireTime = windup + radialNovaPulsesFiredInState * Mathf.Max(0.01f, radialNovaPulseInterval);
                if (radialNovaElapsedInState < pulseFireTime)
                {
                    break;
                }

                FireRadialNovaPulse();
                radialNovaPulsesFiredInState++;
                RestoreRadialNovaVisual();
            }

            if (stateTimer <= 0f)
            {
                RestoreRadialNovaVisual();
                EnterState(BossState.Cooldown);
            }
        }

        private void TickCooldown(float deltaTime)
        {
            if (!HasLiveTarget())
            {
                EnterState(BossState.Idle);
                return;
            }

            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                if (CanUseRadialSkill())
                {
                    EnterState(BossState.SkillRadialNova);
                    return;
                }

                if (CanUseFanSkill())
                {
                    EnterState(BossState.SkillFanShot);
                    return;
                }

                EnterState(BossState.Chase);
            }
        }

        private void EnterState(BossState nextState)
        {
            if (hasState && state == nextState)
            {
                return;
            }

            if (hasState && state == BossState.SkillFanShot && nextState != BossState.SkillFanShot)
            {
                RestoreFanShotVisual();
            }

            if (hasState && state == BossState.SkillRadialNova && nextState != BossState.SkillRadialNova)
            {
                RestoreRadialNovaVisual();
            }

            state = nextState;
            hasState = true;
            switch (state)
            {
                case BossState.Idle:
                    stateTimer = idleDuration;
                    bossController.Stop();
                    break;
                case BossState.Chase:
                    stateTimer = 0f;
                    break;
                case BossState.Burst:
                    stateTimer = burstDuration;
                    nextBurstFireTime = Time.time;
                    break;
                case BossState.SkillFanShot:
                    stateTimer = Mathf.Max(0.05f, fanSkillWindup + fanSkillRecovery);
                    fanSkillFiredInState = false;
                    nextFanSkillReadyTime = Time.time + Mathf.Max(0.1f, fanSkillInterval);
                    bossController.Stop();
                    break;
                case BossState.SkillRadialNova:
                    radialNovaPulsesFiredInState = 0;
                    radialNovaElapsedInState = 0f;
                    stateTimer =
                        Mathf.Max(0.05f,
                            radialNovaWindup +
                            radialNovaRecovery +
                            Mathf.Max(0, radialNovaPulseCount - 1) * Mathf.Max(0.01f, radialNovaPulseInterval));
                    nextRadialNovaReadyTime = Time.time + Mathf.Max(0.1f, activeRadialNovaInterval);
                    bossController.Stop();
                    Debug.Log(
                        "BossBrain radial nova windup started. shots=" + activeRadialNovaShotCount +
                        " pulses=" + radialNovaPulseCount +
                        " windup=" + radialNovaWindup.ToString("0.##") +
                        " cooldown=" + activeRadialNovaInterval.ToString("0.##"),
                        this);
                    break;
                case BossState.Cooldown:
                    stateTimer = activeCooldownDuration;
                    bossController.Stop();
                    break;
                case BossState.Dead:
                    stateTimer = 0f;
                    bossController.Stop();
                    break;
            }
        }

        private void OnBossDefeated()
        {
            EnterState(BossState.Dead);
        }

        private bool CanUseFanSkill()
        {
            if (!enableFanShot || weaponController == null)
            {
                return false;
            }

            if (Time.time < nextFanSkillReadyTime)
            {
                return false;
            }

            return HasLiveTarget();
        }

        private bool CanUseRadialSkill()
        {
            if (!enableRadialNova || weaponController == null)
            {
                return false;
            }

            if (Time.time < nextRadialNovaReadyTime)
            {
                return false;
            }

            return HasLiveTarget();
        }

        private void FireFanShot()
        {
            if (weaponController == null)
            {
                if (!loggedMissingWeapon)
                {
                    Debug.LogWarning("BossBrain cannot cast fan skill because WeaponController is missing on " + name + ".", this);
                    loggedMissingWeapon = true;
                }

                return;
            }

            var baseDirection = target != null
                ? Physics2DUtility.SafeDirection(transform.position, target.position, Vector2.left)
                : (Vector2)transform.right;
            var shotCount = Mathf.Clamp(fanShotCount, 1, 9);
            if (shotCount == 1 || fanSpreadAngle <= 0.01f)
            {
                weaponController.FireImmediate(baseDirection, gameObject);
                return;
            }

            var half = fanSpreadAngle * 0.5f;
            var step = fanSpreadAngle / (shotCount - 1);
            for (var i = 0; i < shotCount; i++)
            {
                var angle = -half + step * i;
                var direction = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                weaponController.FireImmediate(direction, gameObject);
            }
        }

        private void FireRadialNovaPulse()
        {
            if (weaponController == null)
            {
                if (!loggedMissingWeapon)
                {
                    Debug.LogWarning("BossBrain cannot cast radial nova because WeaponController is missing on " + name + ".", this);
                    loggedMissingWeapon = true;
                }

                return;
            }

            var shotCount = Mathf.Clamp(activeRadialNovaShotCount, 6, 96);
            var angleStep = 360f / shotCount;
            var direction = Vector2.right;
            Debug.Log("BossBrain radial nova pulse fired. shots=" + shotCount + " speedScale=" + radialNovaProjectileSpeedScale.ToString("0.##"), this);
            for (var i = 0; i < shotCount; i++)
            {
                var angle = angleStep * i;
                var rotatedDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                weaponController.FireImmediateWithSpeedMultiplier(
                    rotatedDirection,
                    Mathf.Max(0.1f, radialNovaProjectileSpeedScale),
                    gameObject);
            }
        }

        private void TryResolveTargetPlayer()
        {
            var hooks = RuntimeSceneHooks.Active;
            if (hooks != null && hooks.PlayerHealth != null && !hooks.PlayerHealth.IsDead)
            {
                SetTargetPlayer(hooks.PlayerHealth);
                return;
            }

            if (!autoFindPlayer || Time.time < nextRetargetTime)
            {
                return;
            }

            nextRetargetTime = Time.time + Mathf.Max(0.1f, retargetInterval);
            var player = Object.FindObjectOfType<PlayerHealth>();
            if (player != null && !player.IsDead)
            {
                SetTargetPlayer(player);
            }
        }

        private bool HasLiveTarget()
        {
            if (targetPlayerHealth == null || target == null || targetPlayerHealth.IsDead)
            {
                TryResolveTargetPlayer();
            }

            return targetPlayerHealth != null && target != null && !targetPlayerHealth.IsDead;
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            var direction = Physics2DUtility.SafeDirection(transform.position, targetPosition, Vector2.left);
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void RefreshAdaptiveValues()
        {
            var useLowHealthAggression = false;
            if (enableLowHealthAggression && bossHealth != null && !bossHealth.IsDead && bossHealth.MaxHealth > 0f)
            {
                var ratio = bossHealth.CurrentHealth / bossHealth.MaxHealth;
                useLowHealthAggression = ratio <= Mathf.Clamp01(lowHealthThresholdNormalized);
            }

            if (useLowHealthAggression == lowHealthAggressionActive &&
                activeBurstFireInterval > 0f &&
                activeCooldownDuration >= 0f &&
                activeRadialNovaInterval > 0f &&
                activeRadialNovaShotCount > 0)
            {
                return;
            }

            lowHealthAggressionActive = useLowHealthAggression;
            if (lowHealthAggressionActive)
            {
                activeBurstFireInterval = Mathf.Max(0.05f, burstFireInterval * Mathf.Clamp(lowHealthBurstFireIntervalScale, 0.2f, 1f));
                activeCooldownDuration = Mathf.Max(0f, cooldownDuration * Mathf.Clamp(lowHealthCooldownScale, 0.2f, 1f));
                activeChaseSpeedScale = Mathf.Max(1f, lowHealthChaseSpeedScale);
                activeRadialNovaInterval = Mathf.Max(0.1f, radialNovaInterval * Mathf.Clamp(lowHealthRadialNovaIntervalScale, 0.2f, 1f));
                activeRadialNovaShotCount = Mathf.Clamp(radialNovaShotCount + Mathf.Max(0, lowHealthRadialNovaShotBonus), 6, 96);
            }
            else
            {
                activeBurstFireInterval = Mathf.Max(0.05f, burstFireInterval);
                activeCooldownDuration = Mathf.Max(0f, cooldownDuration);
                activeChaseSpeedScale = 1f;
                activeRadialNovaInterval = Mathf.Max(0.1f, radialNovaInterval);
                activeRadialNovaShotCount = Mathf.Clamp(radialNovaShotCount, 6, 96);
            }
        }

        private void ApplyFanShotWindupVisual()
        {
            var windup = Mathf.Max(0.01f, fanSkillWindup);
            var warmupRemaining = Mathf.Max(0f, stateTimer - fanSkillRecovery);
            if (warmupRemaining <= 0f)
            {
                transform.localScale = baseScale;
                return;
            }

            var windupProgress = 1f - Mathf.Clamp01(warmupRemaining / windup);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(1f, fanWindupPulseFrequency) * Mathf.PI * 2f);
            var blend = Mathf.Clamp01(0.35f + windupProgress * 0.65f * pulse);
            var scaleMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, fanWindupScaleMultiplier), blend);
            transform.localScale = baseScale * scaleMultiplier;
            ApplyTintColor(fanWindupColor, blend * 0.6f);
        }

        private void ApplyRadialNovaWindupVisual()
        {
            var windup = Mathf.Max(0.01f, radialNovaWindup);
            var normalized = Mathf.Clamp01(radialNovaElapsedInState / windup);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(1f, radialWindupPulseFrequency) * Mathf.PI * 2f);
            var blend = Mathf.Clamp01(0.25f + normalized * 0.75f * pulse);
            var scaleMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, radialWindupScaleMultiplier), blend);
            transform.localScale = baseScale * scaleMultiplier;
            ApplyTintColor(radialWindupColor, blend * 0.85f);
        }

        private void RestoreFanShotVisual()
        {
            RestoreVisualState();
        }

        private void RestoreRadialNovaVisual()
        {
            RestoreVisualState();
        }

        private void RestoreVisualState()
        {
            if (hasBaseScale)
            {
                transform.localScale = baseScale;
            }

            if (cachedSpriteRenderer != null && hasBaseTintColor)
            {
                cachedSpriteRenderer.color = baseTintColor;
            }
        }

        private void ApplyTintColor(Color targetColor, float strength)
        {
            if (cachedSpriteRenderer == null || !hasBaseTintColor)
            {
                return;
            }

            cachedSpriteRenderer.color = Color.Lerp(baseTintColor, targetColor, Mathf.Clamp01(strength));
        }
    }
}
