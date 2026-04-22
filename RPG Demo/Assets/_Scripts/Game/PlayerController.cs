// Path: Assets/_Scripts/Game/PlayerController.cs
using System.Collections;
using System.Collections.Generic;
using System;
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARPGDemo.Game
{
    [System.Serializable]
    public class ComboAttackStage
    {
        [Header("Identity")]
        [Tooltip("Combo stage id used for events and debugging.")]
        public ComboStageType stageType = ComboStageType.Light1;

        [Tooltip("Animator trigger name for this combo stage.")]
        public string animatorTrigger = "Attack1";

        [Header("Damage")]
        [Tooltip("Mana cost of this stage.")]
        public float manaCost = 0f;

        [Tooltip("Damage multiplier used by BattleFormula.")]
        public float damageMultiplier = 1f;

        [Tooltip("Flat damage bonus used by BattleFormula.")]
        public float flatDamageBonus = 0f;

        [Tooltip("Whether this stage ignores target invincibility.")]
        public bool ignoreTargetInvincible = false;

        [Header("Feel Timing")]
        [Tooltip("Windup time in seconds. Windup is not interruptible.")]
        public float windupTime = 0.08f;

        [Tooltip("Recovery time in seconds. Recovery can be canceled by next input.")]
        public float recoveryTime = 0.18f;

        [Tooltip("Minimum interval before chaining to next stage.")]
        public float minChainInterval = 0.06f;

        [Tooltip("Move speed multiplier during this stage. 1=normal, 0=locked.")]
        [Range(0f, 1f)]
        public float moveSpeedMultiplier = 0.25f;

        [Header("Fallback")]
        [Tooltip("Auto request hitbox when animation events are not available.")]
        public bool autoRequestHitbox = false;
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ActorStats))]
    public class PlayerController : MonoBehaviour
    {
        private enum AttackPhase
        {
            None = 0,
            Windup = 1,
            Recovery = 2
        }

        [Header("References")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Animator animator;
        [SerializeField] private ActorStats stats;
        [SerializeField] private AttackHitbox2D attackHitbox;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer visualSprite;

        [Header("Move")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float moveAcceleration = 60f;
        [SerializeField] private float moveDeceleration = 80f;

        [Header("Jump")]
        [SerializeField] private float jumpVelocity = 12f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;
        [Tooltip("Small jump lock after landing to reduce false retrigger.")]
        [SerializeField] private float landingJumpLock = 0.05f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.16f;
        [SerializeField] private LayerMask groundMask;

        [Header("Combat - Combo Stages")]
        [Tooltip("Three combo stages, usually Attack1/Attack2/Attack3.")]
        [SerializeField] private ComboAttackStage[] comboStages = new ComboAttackStage[3];

        [Header("Combat - Input Buffer")]
        [Tooltip("Attack input buffer duration in seconds.")]
        [SerializeField] private float inputBufferDuration = 0.2f;
        [Tooltip("Maximum buffered attack inputs. 1 means cache only next stage.")]
        [SerializeField] private int maxBufferedInputCount = 1;
        [Tooltip("Input dedupe interval to avoid repeated triggers.")]
        [SerializeField] private float inputDeduplicateInterval = 0.06f;

        [Header("Combat - Flow Control")]
        [Tooltip("Reset combo to stage 1 if no follow-up input in this duration.")]
        [SerializeField] private float comboResetDuration = 0.9f;
        [Tooltip("Global cooldown after a combo finishes.")]
        [SerializeField] private float comboGlobalCooldown = 0.1f;
        [Tooltip("Allow canceling recovery by next combo input.")]
        [SerializeField] private bool enableRecoveryCancel = true;
        [Tooltip("Lock horizontal movement input during attack.")]
        [SerializeField] private bool lockMoveInputDuringAttack = true;
        [Tooltip("Lock jump input during attack.")]
        [SerializeField] private bool lockJumpInputDuringAttack = true;
        [Tooltip("Extra timeout for combo state rollback safety.")]
        [SerializeField] private float attackRollbackGraceTime = 0.35f;

        [Header("Hit Reaction")]
        [SerializeField] private float knockbackForceX = 5f;
        [SerializeField] private float knockbackForceY = 3f;
        [SerializeField] private float knockbackDuration = 0.16f;
        [Tooltip("Additional invincible duration when hurt.")]
        [SerializeField] private float hurtExtraInvincible = 0.08f;
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string deathTrigger = "Death";
        [Tooltip("Fallback death trigger name, e.g. Die.")]
        [SerializeField] private string deathTriggerFallback = "Die";

        [Header("Animator Parameters")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
        [SerializeField] private string deadParam = "Dead";
        [SerializeField] private string attackingParam = "Attacking";
        [SerializeField] private string isMovingParam = "IsMoving";
        [SerializeField] private string isGroundedParam = "IsGrounded";
        [SerializeField] private string isDeadParam = "IsDead";

        [Header("Input Mapping")]
        [SerializeField] private string legacyHorizontalAxis = "Horizontal";
        [SerializeField] private string legacyJumpButton = "Jump";
        [SerializeField] private string legacyAttackButton = "Fire1";
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode jumpPrimaryKey = KeyCode.Space;
        [SerializeField] private KeyCode jumpSecondaryKey = KeyCode.K;
        [SerializeField] private KeyCode attackPrimaryKey = KeyCode.J;
        [SerializeField] private KeyCode attackSecondaryKey = KeyCode.LeftShift;
        [SerializeField] private bool useRuntimeInputBinding = true;
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string pauseActionName = "Pause";
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private bool controllerHandlesPause = false;

        [Header("Debug")]
        [SerializeField] private bool verboseCombatLog = false;
        [SerializeField] private bool logFacingChain = true;

        private float moveInput;
        private bool isGrounded;
        private bool wasGrounded;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private float landingLockUntil;
        private bool facingRight = true;

        private float knockbackRemain;
        private Vector2 knockbackVelocity;
        private bool deathTriggered;
        private bool canControl = true;
        private float localInvincibleUntil;

        private readonly Queue<float> bufferedAttackExpireQueue = new Queue<float>(4);
        private Coroutine comboRoutine;
        private int currentStageIndex;
        private int activeStageIndex = -1;
        private bool isAttacking;
        private AttackPhase attackPhase = AttackPhase.None;
        private bool requestNextStage;
        private bool recoveryInterruptWindowOpen;
        private float currentMoveMultiplier = 1f;
        private float nextComboAllowedTime;
        private float lastComboFinishTime = -999f;
        private float lastAcceptedAttackInputTime = -999f;
        private float stageChainAllowedTime;
        private float comboRollbackTimeoutAt;

        public bool IsAttacking => isAttacking;
        public float CurrentMoveMultiplier => currentMoveMultiplier;
        public float FacingSign => facingRight ? 1f : -1f;

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (stats == null)
            {
                stats = GetComponent<ActorStats>();
            }

            if (attackHitbox == null)
            {
                attackHitbox = GetComponentInChildren<AttackHitbox2D>();
            }

            ResolveVisualReferences();
            SyncFacingFromVisual();
            ApplyFacingVisual(false);
            ForceDisableAnimatorRootMotion();

            EnsureDefaultComboSetup();
            DisableAlternativePlayerControllers();
            EnableCodeHitboxFallbackWhenAnimatorMissing();
        }

        private void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
        }

        private void OnEnable()
        {
            EventCenter.AddListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.AddListener<ActorRevivedEvent>(OnActorRevived);
            EventCenter.AddListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.RemoveListener<ActorRevivedEvent>(OnActorRevived);
            EventCenter.RemoveListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            moveAcceleration = Mathf.Max(0f, moveAcceleration);
            moveDeceleration = Mathf.Max(0f, moveDeceleration);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            landingJumpLock = Mathf.Max(0f, landingJumpLock);
            groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);

            inputBufferDuration = Mathf.Max(0.01f, inputBufferDuration);
            maxBufferedInputCount = Mathf.Max(1, maxBufferedInputCount);
            inputDeduplicateInterval = Mathf.Max(0f, inputDeduplicateInterval);
            comboResetDuration = Mathf.Max(0f, comboResetDuration);
            comboGlobalCooldown = Mathf.Max(0f, comboGlobalCooldown);
            attackRollbackGraceTime = Mathf.Max(0.1f, attackRollbackGraceTime);

            knockbackForceX = Mathf.Max(0f, knockbackForceX);
            knockbackForceY = Mathf.Max(0f, knockbackForceY);
            knockbackDuration = Mathf.Max(0f, knockbackDuration);
            hurtExtraInvincible = Mathf.Max(0f, hurtExtraInvincible);
        }

        private void Update()
        {
            if (controllerHandlesPause && InputCompat.IsActionDown(pauseActionName, pauseKey))
            {
                TryTogglePauseByGameManager();
            }

            if (Time.timeScale <= 0f)
            {
                moveInput = 0f;
                UpdateAnimator();
                return;
            }

            UpdateGroundedState();
            PruneExpiredBufferedInputs();
            MonitorComboStateSafety();

            if (stats == null)
            {
                return;
            }

            if (stats.IsDead)
            {
                TriggerDeathAnimationIfNeeded();
                moveInput = 0f;
                UpdateAnimator();
                return;
            }

            if (!canControl)
            {
                moveInput = 0f;
                UpdateAnimator();
                return;
            }

            ReadMoveInput();
            ReadJumpInput();
            ReadAttackInput();

            TryStartComboFromBufferedInput();

            TryRequestNextComboStageFromRecovery();

            EnsureRootScaleStable();
            UpdateFacing();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            if (stats != null && stats.IsDead)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
                return;
            }

            if (!canControl)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
                return;
            }

            if (knockbackRemain > 0f)
            {
                knockbackRemain -= Time.fixedDeltaTime;
                rb.velocity = new Vector2(knockbackVelocity.x, rb.velocity.y);
                return;
            }

            float inputX = moveInput;
            if (isAttacking && lockMoveInputDuringAttack)
            {
                inputX = 0f;
            }

            float stageMoveMul = isAttacking ? currentMoveMultiplier : 1f;
            float targetSpeed = inputX * moveSpeed * stageMoveMul;
            float accel = Mathf.Abs(targetSpeed) > Mathf.Abs(rb.velocity.x) ? moveAcceleration : moveDeceleration;
            float newVelX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newVelX, rb.velocity.y);
        }

        private void UpdateGroundedState()
        {
            wasGrounded = isGrounded;

            if (groundCheck != null)
            {
                isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask) != null;
            }
            else
            {
                isGrounded = false;
            }

            if (isGrounded)
            {
                coyoteCounter = coyoteTime;
                if (!wasGrounded)
                {
                    landingLockUntil = Time.time + landingJumpLock;
                }
            }
            else
            {
                coyoteCounter -= Time.deltaTime;
            }

            jumpBufferCounter -= Time.deltaTime;
        }

        private void ReadMoveInput()
        {
            moveInput = InputCompat.GetHorizontal(legacyHorizontalAxis, leftKey, rightKey);
        }

        private void ReadJumpInput()
        {
            if (lockJumpInputDuringAttack && isAttacking)
            {
                return;
            }

            KeyCode jumpPrimary = useRuntimeInputBinding ? InputCompat.GetActionBindingOrDefault(jumpActionName, jumpPrimaryKey) : jumpPrimaryKey;
            if (InputCompat.GetButtonDown(jumpPrimary, jumpSecondaryKey, legacyJumpButton))
            {
                jumpBufferCounter = jumpBufferTime;
            }

            bool canJump =
                jumpBufferCounter > 0f &&
                coyoteCounter > 0f &&
                knockbackRemain <= 0f &&
                Time.time >= landingLockUntil;

            if (!canJump)
            {
                return;
            }

            Vector2 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;

            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            isGrounded = false;

            ClearBufferedInputs();

            stats.BroadcastState(ActorStateType.Jump);
        }

        private void ReadAttackInput()
        {
            // Block attack input only when the click is aimed at UI; keyboard attack stays unchanged.
            if (IsUiPointerBlockingMouseAttack())
            {
                return;
            }

            KeyCode attackPrimary = useRuntimeInputBinding ? InputCompat.GetActionBindingOrDefault(attackActionName, attackPrimaryKey) : attackPrimaryKey;
            bool attackPressed = InputCompat.GetButtonDown(attackPrimary, attackSecondaryKey, legacyAttackButton);
            if (!attackPressed)
            {
                return;
            }

            RegisterAttackInputToBuffer();
        }

        private static bool IsUiPointerBlockingMouseAttack()
        {
            if (!IsMouseLeftButtonDownThisFrame())
            {
                return false;
            }

            EventSystem evt = EventSystem.current;
            if (evt == null)
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Pointer.current != null &&
                evt.IsPointerOverGameObject(UnityEngine.InputSystem.Pointer.current.deviceId))
            {
                return true;
            }
#endif

            return evt.IsPointerOverGameObject();
        }

        private static bool IsMouseLeftButtonDownThisFrame()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif

            return false;
        }

        private void RegisterAttackInputToBuffer()
        {
            if (!CanAcceptAttackInput())
            {
                return;
            }

            if (Time.time - lastAcceptedAttackInputTime <= inputDeduplicateInterval)
            {
                return;
            }

            lastAcceptedAttackInputTime = Time.time;
            PushAttackInputBuffer();
        }

        private bool CanAcceptAttackInput()
        {
            if (stats == null || stats.IsDead || !canControl)
            {
                return false;
            }

            if (knockbackRemain > 0f)
            {
                return false;
            }

            if (!isGrounded)
            {
                return false;
            }

            if (isAttacking)
            {
                return attackPhase == AttackPhase.Recovery;
            }

            return true;
        }

        private void PushAttackInputBuffer()
        {
            PruneExpiredBufferedInputs();

            int limit = Mathf.Max(1, maxBufferedInputCount);
            while (bufferedAttackExpireQueue.Count >= limit)
            {
                bufferedAttackExpireQueue.Dequeue();
            }

            bufferedAttackExpireQueue.Enqueue(Time.time + Mathf.Max(0.01f, inputBufferDuration));
        }

        private void PruneExpiredBufferedInputs()
        {
            while (bufferedAttackExpireQueue.Count > 0 && bufferedAttackExpireQueue.Peek() < Time.time)
            {
                bufferedAttackExpireQueue.Dequeue();
            }
        }

        private bool HasBufferedInput()
        {
            PruneExpiredBufferedInputs();
            return bufferedAttackExpireQueue.Count > 0;
        }

        private bool ConsumeOneBufferedInput()
        {
            PruneExpiredBufferedInputs();
            if (bufferedAttackExpireQueue.Count <= 0)
            {
                return false;
            }

            bufferedAttackExpireQueue.Dequeue();
            return true;
        }

        private void ClearBufferedInputs()
        {
            bufferedAttackExpireQueue.Clear();
        }

        private void TryStartComboFromBufferedInput()
        {
            if (isAttacking)
            {
                return;
            }

            if (!HasBufferedInput())
            {
                return;
            }

            if (Time.time < nextComboAllowedTime)
            {
                return;
            }

            if (!CanStartComboNow())
            {
                return;
            }

            if (Time.time - lastComboFinishTime > comboResetDuration)
            {
                currentStageIndex = 0;
            }

            if (!ConsumeOneBufferedInput())
            {
                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            comboRoutine = StartCoroutine(ComboRoutine());
        }

        private void TryRequestNextComboStageFromRecovery()
        {
            if (!isAttacking || attackPhase != AttackPhase.Recovery)
            {
                return;
            }

            if (!enableRecoveryCancel)
            {
                return;
            }

            if (!recoveryInterruptWindowOpen)
            {
                return;
            }

            if (Time.time < stageChainAllowedTime)
            {
                return;
            }

            if (currentStageIndex >= comboStages.Length - 1)
            {
                return;
            }

            if (!HasBufferedInput())
            {
                return;
            }

            if (ConsumeOneBufferedInput())
            {
                requestNextStage = true;
                if (verboseCombatLog)
                {
                    Debug.Log($"[PlayerController] Recovery cancel accepted {currentStageIndex} -> {currentStageIndex + 1}");
                }
            }
        }

        private bool CanStartComboNow()
        {
            if (stats == null || stats.IsDead)
            {
                return false;
            }

            if (!canControl || knockbackRemain > 0f)
            {
                return false;
            }

            return isGrounded;
        }

        private IEnumerator ComboRoutine()
        {
            isAttacking = true;
            requestNextStage = false;

            while (true)
            {
                ComboAttackStage stage = GetCurrentStage();
                if (stage == null)
                {
                    break;
                }

                if (stats != null && !stats.TryConsumeMana(stage.manaCost))
                {
                    break;
                }

                EnterStage(stage, currentStageIndex);

                float windup = Mathf.Max(0f, stage.windupTime);
                if (windup > 0f)
                {
                    yield return new WaitForSeconds(windup);
                }

                if (attackPhase != AttackPhase.Recovery)
                {
                    BeginRecoveryInternal();
                }

                float recovery = Mathf.Max(0f, stage.recoveryTime);
                float elapsed = 0f;
                while (elapsed < recovery)
                {
                    if (requestNextStage && currentStageIndex < comboStages.Length - 1)
                    {
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                ExitStage(stage);

                if (requestNextStage && currentStageIndex < comboStages.Length - 1)
                {
                    currentStageIndex++;
                    requestNextStage = false;
                    continue;
                }

                currentStageIndex = 0;
                break;
            }

            FinishComboRoutine();
        }

        private ComboAttackStage GetCurrentStage()
        {
            if (comboStages == null || comboStages.Length == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(currentStageIndex, 0, comboStages.Length - 1);
            return comboStages[safeIndex];
        }

        private void EnterStage(ComboAttackStage stage, int stageIndex)
        {
            activeStageIndex = stageIndex;
            attackPhase = AttackPhase.Windup;
            requestNextStage = false;
            recoveryInterruptWindowOpen = false;
            currentMoveMultiplier = Mathf.Clamp01(stage.moveSpeedMultiplier);

            stageChainAllowedTime = Time.time + Mathf.Max(0f, stage.minChainInterval);

            comboRollbackTimeoutAt = Time.time + Mathf.Max(0.1f, stage.windupTime + stage.recoveryTime + attackRollbackGraceTime);

            if (stats != null)
            {
                stats.BroadcastState(ActorStateType.Attack);
                EventCenter.Broadcast(new ComboStageChangedEvent(stats.ActorId, stage.stageType, true));
            }

            if (!string.IsNullOrEmpty(stage.animatorTrigger))
            {
                TrySetAnimatorTriggerIfExists(stage.animatorTrigger);
            }

            if (attackHitbox != null)
            {
                attackHitbox.SetAttackParams(stage.damageMultiplier, stage.flatDamageBonus, stage.ignoreTargetInvincible);
                attackHitbox.RequestAttack();
            }
        }

        private void ExitStage(ComboAttackStage stage)
        {
            if (stats != null)
            {
                EventCenter.Broadcast(new ComboStageChangedEvent(stats.ActorId, stage.stageType, false));
            }

            activeStageIndex = -1;
            attackPhase = AttackPhase.None;
            recoveryInterruptWindowOpen = false;
        }

        private void FinishComboRoutine()
        {
            comboRoutine = null;
            isAttacking = false;
            requestNextStage = false;
            attackPhase = AttackPhase.None;
            activeStageIndex = -1;
            recoveryInterruptWindowOpen = false;
            currentMoveMultiplier = 1f;

            nextComboAllowedTime = Time.time + Mathf.Max(0f, comboGlobalCooldown);
            lastComboFinishTime = Time.time;

            if (stats != null && !stats.IsDead)
            {
                ActorStateType nextState = Mathf.Abs(moveInput) > 0.01f ? ActorStateType.Move : ActorStateType.Idle;
                stats.BroadcastState(nextState);
            }
        }

        private void InterruptCombo(bool clearInputBuffer, bool forceIdleState)
        {
            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
                comboRoutine = null;
            }

            isAttacking = false;
            requestNextStage = false;
            attackPhase = AttackPhase.None;
            activeStageIndex = -1;
            recoveryInterruptWindowOpen = false;
            currentMoveMultiplier = 1f;
            currentStageIndex = 0;
            nextComboAllowedTime = Time.time + Mathf.Max(0f, comboGlobalCooldown);

            if (clearInputBuffer)
            {
                ClearBufferedInputs();
            }

            if (forceIdleState && stats != null && !stats.IsDead)
            {
                stats.BroadcastState(ActorStateType.Idle);
            }
        }

        private void MonitorComboStateSafety()
        {
            if (!isAttacking)
            {
                return;
            }

            if (Time.time <= comboRollbackTimeoutAt)
            {
                return;
            }

            if (verboseCombatLog)
            {
                Debug.LogWarning("[PlayerController] Combo timeout rollback -> Idle");
            }

            InterruptCombo(true, true);
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(moveInput) < 0.01f)
            {
                return;
            }

            if (moveInput > 0f && !facingRight)
            {
                Flip();
            }
            else if (moveInput < 0f && facingRight)
            {
                Flip();
            }
        }

        private void Flip()
        {
            if (logFacingChain)
            {
                LogFacingSnapshot("BeforeFlip");
            }

            facingRight = !facingRight;
            ApplyFacingVisual(false);

            if (logFacingChain)
            {
                LogFacingSnapshot("AfterFlip");
            }
        }

        private void ResolveVisualReferences()
        {
            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (visualRoot == null)
            {
                Transform fallbackView = transform.Find("Player_View");
                if (fallbackView != null)
                {
                    visualRoot = fallbackView;
                }
            }

            if (visualSprite == null && visualRoot != null)
            {
                visualSprite = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (visualSprite == null)
            {
                visualSprite = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void SyncFacingFromVisual()
        {
            if (visualRoot != null && Mathf.Abs(visualRoot.localScale.x) > 0.001f)
            {
                facingRight = visualRoot.localScale.x >= 0f;
                return;
            }

            if (visualSprite != null)
            {
                facingRight = !visualSprite.flipX;
                return;
            }

            facingRight = transform.localScale.x >= 0f;
        }

        private void ForceDisableAnimatorRootMotion()
        {
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void EnsureRootScaleStable()
        {
            if (transform.localScale.x <= 0f)
            {
                transform.SetLocalScaleX(1f);
                if (logFacingChain)
                {
                    LogFacingSnapshot("RootScaleCorrected");
                }
            }
        }

        private void ApplyFacingVisual(bool logState)
        {
            float sign = FacingSign;

            transform.SetLocalScaleX(1f);

            if (visualRoot != null)
            {
                visualRoot.SetLocalScaleX(sign);

                if (visualSprite != null)
                {
                    // Avoid double mirror when visualRoot already handles facing.
                    visualSprite.flipX = false;
                }
            }
            else if (visualSprite != null)
            {
                visualSprite.flipX = sign < 0f;
            }

            if (attackHitbox != null)
            {
                attackHitbox.transform.SetLocalScaleX(sign);
            }

            if (logState && logFacingChain)
            {
                LogFacingSnapshot("ApplyFacingVisual");
            }
        }

        private void LogFacingSnapshot(string phase)
        {
            Vector3 playerPos = transform.position;
            Vector3 playerScale = transform.localScale;
            Vector3 viewScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            string viewScaleText = visualRoot != null ? viewScale.ToString("F3") : "N/A";
            string spriteFlipText = visualSprite != null ? visualSprite.flipX.ToString() : "N/A";

            Debug.Log(
                $"[PlayerFacingDebug][{phase}] InputX={moveInput:F3}, FacingRight={facingRight}, " +
                $"PlayerPos={playerPos:F3}, PlayerScale={playerScale:F3}, " +
                $"PlayerViewScale={viewScaleText}, SpriteFlipX={spriteFlipText}",
                this);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (stats == null || stats.IsDead)
            {
                return;
            }

            if (evt.TargetId != stats.ActorId || evt.FinalDamage <= 0)
            {
                return;
            }

            if (Time.unscaledTime < localInvincibleUntil)
            {
                return;
            }

            InterruptCombo(true, false);

            if (hurtExtraInvincible > 0f)
            {
                localInvincibleUntil = Time.unscaledTime + hurtExtraInvincible;
                stats.AddTemporaryInvincible(hurtExtraInvincible);
            }

            Vector2 away = ResolveDamageKnockbackDirection(evt);
            knockbackVelocity = new Vector2(away.x * knockbackForceX, knockbackForceY);
            knockbackRemain = knockbackDuration;

            stats.BroadcastState(ActorStateType.Hurt);

            if (animator != null && !string.IsNullOrEmpty(hurtTrigger))
            {
                animator.SetTrigger(hurtTrigger);
            }
        }

        private void OnActorRevived(ActorRevivedEvent evt)
        {
            if (stats == null || evt.ActorId != stats.ActorId)
            {
                return;
            }

            deathTriggered = false;
            knockbackRemain = 0f;
            InterruptCombo(true, false);
            stats.BroadcastState(ActorStateType.Idle);
        }

        private Vector2 ResolveDamageKnockbackDirection(DamageAppliedEvent evt)
        {
            // Prefer hit point direction first; this is the most stable for close-range melee overlap.
            Vector2 away = (Vector2)transform.position - (Vector2)evt.HitPosition;
            if (away.sqrMagnitude >= 0.001f)
            {
                away.Normalize();
                return away;
            }

            // Fallback to source position from event payload.
            away = (Vector2)transform.position - (Vector2)evt.SourcePosition;
            if (away.sqrMagnitude >= 0.001f)
            {
                away.Normalize();
                return away;
            }

            // Final fallback: use attacker facing when stacked at the same point.
            Transform attacker;
            if (TryFindActorTransform(evt.AttackerId, out attacker) && attacker != null)
            {
                away = (Vector2)transform.position - (Vector2)attacker.position;
                if (away.sqrMagnitude >= 0.001f)
                {
                    away.Normalize();
                    return away;
                }

                float attackerFacing = attacker.localScale.x >= 0f ? 1f : -1f;
                return Vector2.right * -attackerFacing;
            }

            // Never bind fallback direction to player input/facing, to avoid left-right side swapping.
            return Vector2.right;
        }

        private static bool TryFindActorTransform(string actorId, out Transform actorTransform)
        {
            actorTransform = null;
            if (string.IsNullOrEmpty(actorId))
            {
                return false;
            }

            ActorStats[] all = FindObjectsOfType<ActorStats>();
            for (int i = 0; i < all.Length; i++)
            {
                ActorStats s = all[i];
                if (s == null || s.ActorId != actorId)
                {
                    continue;
                }

                actorTransform = s.transform;
                return actorTransform != null;
            }

            return false;
        }

        private void OnGameFlowChanged(GameFlowStateChangedEvent evt)
        {
            canControl = evt.CurrentState == GameFlowState.Playing;
            if (!canControl)
            {
                InterruptCombo(false, false);
            }
        }

        private void TriggerDeathAnimationIfNeeded()
        {
            if (deathTriggered)
            {
                return;
            }

            deathTriggered = true;
            canControl = false;
            InterruptCombo(true, false);

            if (animator == null)
            {
                return;
            }

            bool sent = TrySetAnimatorTriggerIfExists(deathTrigger);
            if (!sent && !string.IsNullOrEmpty(deathTriggerFallback) && deathTriggerFallback != deathTrigger)
            {
                TrySetAnimatorTriggerIfExists(deathTriggerFallback);
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            float absSpeed = rb != null ? Mathf.Abs(rb.velocity.x) : Mathf.Abs(moveInput) * moveSpeed;
            float verticalSpeed = rb != null ? rb.velocity.y : 0f;
            bool dead = stats != null && stats.IsDead;

            SetAnimatorFloat(speedParam, absSpeed);
            SetAnimatorBool(groundedParam, isGrounded);
            SetAnimatorFloat(verticalSpeedParam, verticalSpeed);
            SetAnimatorBool(deadParam, dead);
            SetAnimatorBool(attackingParam, isAttacking);
            SetAnimatorBool(isMovingParam, absSpeed > 0.05f);
            SetAnimatorBool(isGroundedParam, isGrounded);
            SetAnimatorBool(isDeadParam, dead);
        }

        private void SetAnimatorFloat(string paramName, float value)
        {
            if (animator == null || string.IsNullOrEmpty(paramName))
            {
                return;
            }

            animator.SetFloat(paramName, value);
        }

        private void SetAnimatorBool(string paramName, bool value)
        {
            if (animator == null || string.IsNullOrEmpty(paramName))
            {
                return;
            }

            animator.SetBool(paramName, value);
        }

        private bool TrySetAnimatorTriggerIfExists(string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == triggerName && parameters[i].type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(triggerName);
                    return true;
                }
            }

            return false;
        }


        public void AnimEvent_BeginAttackWindow()
        {
            attackHitbox?.AnimEvent_BeginAttackWindow();
        }

        public void AnimEvent_AttackHit()
        {
            attackHitbox?.AnimEvent_DoHit();
        }

        public void AnimEvent_EndAttackWindow()
        {
            attackHitbox?.AnimEvent_EndAttackWindow();
        }

        public void AnimEvent_BeginRecovery()
        {
            BeginRecoveryInternal();
        }

        public void AnimEvent_OpenComboWindow()
        {
            BeginRecoveryInternal();
        }

        public void AnimEvent_CloseComboWindow()
        {
            recoveryInterruptWindowOpen = false;
        }
        public void AnimEvent_AttackSfx()
        {
        }
        public void AnimEvent_AttackVfx()
        {
        }

        private void BeginRecoveryInternal()
        {
            if (!isAttacking)
            {
                return;
            }

            attackPhase = AttackPhase.Recovery;
            recoveryInterruptWindowOpen = true;
        }

        private void TryTogglePauseByGameManager()
        {
            ARPGDemo.Core.GameManager manager = ARPGDemo.Core.GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[PlayerController] Missing ARPGDemo.Core.GameManager in scene.");
                return;
            }

            manager.TogglePause();
        }

        private void EnsureDefaultComboSetup()
        {
            bool valid =
                comboStages != null &&
                comboStages.Length == 3 &&
                comboStages[0] != null &&
                comboStages[1] != null &&
                comboStages[2] != null;

            if (valid)
            {
                return;
            }

            comboStages = new[]
            {
                new ComboAttackStage
                {
                    stageType = ComboStageType.Light1,
                    animatorTrigger = "Attack1",
                    manaCost = 0f,
                    damageMultiplier = 1f,
                    flatDamageBonus = 0f,
                    windupTime = 0.06f,
                    recoveryTime = 0.14f,
                    minChainInterval = 0.05f,
                    moveSpeedMultiplier = 0.2f,
                    ignoreTargetInvincible = false,
                    autoRequestHitbox = false
                },
                new ComboAttackStage
                {
                    stageType = ComboStageType.Light2,
                    animatorTrigger = "Attack2",
                    manaCost = 0f,
                    damageMultiplier = 1.18f,
                    flatDamageBonus = 1.5f,
                    windupTime = 0.08f,
                    recoveryTime = 0.16f,
                    minChainInterval = 0.06f,
                    moveSpeedMultiplier = 0.15f,
                    ignoreTargetInvincible = false,
                    autoRequestHitbox = false
                },
                new ComboAttackStage
                {
                    stageType = ComboStageType.Heavy,
                    animatorTrigger = "Attack3",
                    manaCost = 0f,
                    damageMultiplier = 1.5f,
                    flatDamageBonus = 4f,
                    windupTime = 0.11f,
                    recoveryTime = 0.22f,
                    minChainInterval = 0.08f,
                    moveSpeedMultiplier = 0.1f,
                    ignoreTargetInvincible = false,
                    autoRequestHitbox = false
                }
            };
        }

        private void DisableAlternativePlayerControllers()
        {
            DisableLegacyBehavioursByTypeName("ARPGDemo.Game.PlayerSkillSystem");
        }

        private static void DisableLegacyBehavioursByTypeName(string fullTypeName)
        {
            Type legacyType = ResolveTypeByName(fullTypeName);
            if (legacyType == null || !typeof(Behaviour).IsAssignableFrom(legacyType))
            {
                return;
            }

            UnityEngine.Object[] legacyComponents = FindObjectsOfType(legacyType, true);
            for (int i = 0; i < legacyComponents.Length; i++)
            {
                if (legacyComponents[i] is Behaviour behaviour && behaviour.enabled)
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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type candidate = assemblies[i].GetType(fullTypeName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void EnableCodeHitboxFallbackWhenAnimatorMissing()
        {
            bool missingAnimatorController = animator == null || animator.runtimeAnimatorController == null;
            if (!missingAnimatorController || comboStages == null)
            {
                return;
            }

            for (int i = 0; i < comboStages.Length; i++)
            {
                if (comboStages[i] != null)
                {
                    comboStages[i].autoRequestHitbox = true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.75f);
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
