using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARPGDemo
{
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class StarterARPGCombatController : MonoBehaviour
    {
        private const int MaxComboIndex = 3;

        [Header("Combo")]
        [SerializeField] private float comboResetTime = 0.85f;
        [SerializeField] private float attackDuration = 0.38f;
        [SerializeField] private float comboInputBufferTime = 0.22f;

        [Header("Movement")]
        [SerializeField] private bool attackMoveLock = true;
        [SerializeField] private bool turnToCameraForwardOnAttack = true;

        [Header("Hitbox")]
        [SerializeField] private Hitbox3D attackHitbox;
        [SerializeField] private float attackHitWindowDuration = 0.18f;
        [SerializeField] private float attack1Damage = 10f;
        [SerializeField] private float attack2Damage = 12f;
        [SerializeField] private float attack3Damage = 18f;
        [SerializeField] private float attack1Radius = 1.05f;
        [SerializeField] private float attack2Radius = 1.08f;
        [SerializeField] private float attack3Radius = 1.18f;
        [SerializeField] private float attackForwardOffset = 0.75f;

        private StarterARPGBridge bridge;
        private StarterARPGActionStateMachine stateMachine;
        private StarterARPGPrototypeVisualFeedback visualFeedback;
        private Animator animator;

        private int currentComboIndex;
        private float lastAttackStartedAt = -999f;
        private float attackEndsAt;
        private bool bufferedAttack;
        private float bufferedAttackExpiresAt;
        private bool hasWarnedMissingDependencies;
        private bool hasWarnedMissingAttackTriggers;
#if !ENABLE_INPUT_SYSTEM
        private bool hasWarnedMissingInputSystem;
#endif

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Reset()
        {
            ResolveDependencies();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResolveDependencies();
            }

            comboResetTime = Mathf.Max(0.05f, comboResetTime);
            attackDuration = Mathf.Max(0.05f, attackDuration);
            comboInputBufferTime = Mathf.Max(0.01f, comboInputBufferTime);
            attackHitWindowDuration = Mathf.Clamp(attackHitWindowDuration, 0.01f, attackDuration);
            attack1Damage = Mathf.Max(0f, attack1Damage);
            attack2Damage = Mathf.Max(0f, attack2Damage);
            attack3Damage = Mathf.Max(0f, attack3Damage);
            attack1Radius = Mathf.Max(0.05f, attack1Radius);
            attack2Radius = Mathf.Max(0.05f, attack2Radius);
            attack3Radius = Mathf.Max(0.05f, attack3Radius);
            attackForwardOffset = Mathf.Max(0f, attackForwardOffset);
        }
#endif

        private void Update()
        {
            if (!EnsureDependencies())
            {
                return;
            }

            if (stateMachine.CanMoveNormally)
            {
                stateMachine.SetLocomotion(IsMoveInputActive());
            }

            if (AttackPressedThisFrame())
            {
                HandleAttackPressed();
            }

            TickAttackState();

            if (attackMoveLock && stateMachine.CurrentState == StarterARPGActionState.Attack)
            {
                SuppressStarterMovementInput();
            }
        }

        private void ResolveDependencies()
        {
            bridge = GetComponent<StarterARPGBridge>();
            stateMachine = GetComponent<StarterARPGActionStateMachine>();
            visualFeedback = GetComponent<StarterARPGPrototypeVisualFeedback>();
            attackHitbox = attackHitbox != null ? attackHitbox : GetComponentInChildren<Hitbox3D>(true);
            animator = bridge != null && bridge.Animator != null ? bridge.Animator : GetComponentInChildren<Animator>();
        }

        private bool EnsureDependencies()
        {
            if (bridge == null || stateMachine == null)
            {
                ResolveDependencies();
            }

            if (bridge != null)
            {
                bridge.ResolveReferences();
                animator = bridge.Animator != null ? bridge.Animator : animator;
            }

            bool hasDependencies = bridge != null && stateMachine != null;
            if (!hasDependencies && !hasWarnedMissingDependencies)
            {
                hasWarnedMissingDependencies = true;
                Debug.LogWarning("StarterARPGCombatController requires StarterARPGBridge and StarterARPGActionStateMachine on the same Player.", this);
            }

            return hasDependencies;
        }

        private void HandleAttackPressed()
        {
            if (stateMachine.CurrentState == StarterARPGActionState.Attack)
            {
                bufferedAttack = true;
                bufferedAttackExpiresAt = Time.time + comboInputBufferTime;
                return;
            }

            if (!stateMachine.CanAttack)
            {
                return;
            }

            int comboIndex = GetNextComboIndex();
            if (!stateMachine.TryEnterAttack())
            {
                return;
            }

            StartAttack(comboIndex);
        }

        private void TickAttackState()
        {
            if (stateMachine.CurrentState != StarterARPGActionState.Attack)
            {
                if (attackHitbox != null && attackHitbox.IsActive)
                {
                    attackHitbox.Deactivate();
                }

                if (currentComboIndex > 0 && Time.time - lastAttackStartedAt > comboResetTime)
                {
                    currentComboIndex = 0;
                }

                return;
            }

            if (Time.time < attackEndsAt)
            {
                return;
            }

            if (bufferedAttack && Time.time <= bufferedAttackExpiresAt && currentComboIndex < MaxComboIndex)
            {
                StartAttack(currentComboIndex + 1);
                return;
            }

            bufferedAttack = false;
            if (attackHitbox != null)
            {
                attackHitbox.Deactivate();
            }

            stateMachine.ReturnToLocomotion(IsMoveInputActive());

            if (currentComboIndex >= MaxComboIndex)
            {
                currentComboIndex = 0;
            }
        }

        private void StartAttack(int comboIndex)
        {
            currentComboIndex = Mathf.Clamp(comboIndex, 1, MaxComboIndex);
            lastAttackStartedAt = Time.time;
            attackEndsAt = Time.time + attackDuration;
            bufferedAttack = false;

            if (turnToCameraForwardOnAttack)
            {
                TurnToCameraForward();
            }

            TriggerAttackFeedback(currentComboIndex);
            ActivateHitbox(currentComboIndex);
            SuppressStarterMovementInput();
        }

        private void ActivateHitbox(int comboIndex)
        {
            if (attackHitbox == null)
            {
                return;
            }

            attackHitbox.Configure(GetAttackRadius(comboIndex), attackForwardOffset);
            attackHitbox.Activate(attackHitWindowDuration, GetAttackDamage(comboIndex), comboIndex);
        }

        private int GetNextComboIndex()
        {
            if (currentComboIndex <= 0 || Time.time - lastAttackStartedAt > comboResetTime)
            {
                return 1;
            }

            return currentComboIndex >= MaxComboIndex ? 1 : currentComboIndex + 1;
        }

        private float GetAttackDamage(int comboIndex)
        {
            switch (comboIndex)
            {
                case 2:
                    return attack2Damage;
                case 3:
                    return attack3Damage;
                default:
                    return attack1Damage;
            }
        }

        private float GetAttackRadius(int comboIndex)
        {
            switch (comboIndex)
            {
                case 2:
                    return attack2Radius;
                case 3:
                    return attack3Radius;
                default:
                    return attack1Radius;
            }
        }

        private void TriggerAttackFeedback(int comboIndex)
        {
            string triggerName = $"Attack{comboIndex}";

            if (animator != null && HasAnimatorTrigger(animator, triggerName))
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
                return;
            }

            if (!hasWarnedMissingAttackTriggers)
            {
                hasWarnedMissingAttackTriggers = true;
                Debug.LogWarning("Starter Assets Animator has no Attack1/Attack2/Attack3 triggers; using prototype attack visual feedback.", this);
            }

            if (visualFeedback != null)
            {
                visualFeedback.PlayAttack(comboIndex);
            }
        }

        private void TurnToCameraForward()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void SuppressStarterMovementInput()
        {
            StarterAssetsInputs inputs = bridge != null ? bridge.Inputs : null;
            if (inputs == null)
            {
                return;
            }

            inputs.move = Vector2.zero;
            inputs.jump = false;
            inputs.sprint = false;
        }

        private bool IsMoveInputActive()
        {
            return bridge != null && bridge.Inputs != null && bridge.Inputs.move.sqrMagnitude > 0.01f;
        }

        private bool AttackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            if (!hasWarnedMissingInputSystem)
            {
                hasWarnedMissingInputSystem = true;
                Debug.LogWarning("StarterARPGCombatController requires the Input System package for prototype attack input.", this);
            }

            return false;
#endif
        }

        private static bool HasAnimatorTrigger(Animator targetAnimator, string triggerName)
        {
            if (targetAnimator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = targetAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
