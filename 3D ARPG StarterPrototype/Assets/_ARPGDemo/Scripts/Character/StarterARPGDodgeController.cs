using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARPGDemo
{
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class StarterARPGDodgeController : MonoBehaviour
    {
        [Header("Dodge")]
        [SerializeField] private float dodgeDistance = 3.2f;
        [SerializeField] private float dodgeDuration = 0.32f;
        [SerializeField] private float dodgeCooldown = 0.55f;
        [SerializeField] private bool rotateToDodgeDirection = true;

        private StarterARPGBridge bridge;
        private StarterARPGActionStateMachine stateMachine;
        private StarterARPGPrototypeVisualFeedback visualFeedback;
        private CharacterController characterController;
        private Animator animator;

        private Vector3 dodgeDirection;
        private float dodgeEndsAt;
        private float nextDodgeAllowedAt;
        private float verticalVelocity;
        private bool hasWarnedMissingDependencies;
        private bool hasWarnedMissingDodgeTrigger;
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

            dodgeDistance = Mathf.Max(0.1f, dodgeDistance);
            dodgeDuration = Mathf.Max(0.05f, dodgeDuration);
            dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
        }
#endif

        private void Update()
        {
            if (!EnsureDependencies())
            {
                return;
            }

            if (stateMachine.CurrentState == StarterARPGActionState.Dodge)
            {
                TickDodge();
                SuppressStarterMovementInput();
                return;
            }

            if (stateMachine.CanMoveNormally)
            {
                stateMachine.SetLocomotion(IsMoveInputActive());
            }

            if (DodgePressedThisFrame())
            {
                TryStartDodge();
            }
        }

        private void ResolveDependencies()
        {
            bridge = GetComponent<StarterARPGBridge>();
            stateMachine = GetComponent<StarterARPGActionStateMachine>();
            visualFeedback = GetComponent<StarterARPGPrototypeVisualFeedback>();
            characterController = bridge != null && bridge.CharacterController != null
                ? bridge.CharacterController
                : GetComponent<CharacterController>();
            animator = bridge != null && bridge.Animator != null ? bridge.Animator : GetComponentInChildren<Animator>();
        }

        private bool EnsureDependencies()
        {
            if (bridge == null || stateMachine == null || characterController == null)
            {
                ResolveDependencies();
            }

            if (bridge != null)
            {
                bridge.ResolveReferences();
                characterController = bridge.CharacterController != null ? bridge.CharacterController : characterController;
                animator = bridge.Animator != null ? bridge.Animator : animator;
            }

            bool hasDependencies = bridge != null && stateMachine != null && characterController != null;
            if (!hasDependencies && !hasWarnedMissingDependencies)
            {
                hasWarnedMissingDependencies = true;
                Debug.LogWarning("StarterARPGDodgeController requires StarterARPGBridge, StarterARPGActionStateMachine, and CharacterController on the same Player.", this);
            }

            return hasDependencies;
        }

        private void TryStartDodge()
        {
            if (Time.time < nextDodgeAllowedAt || !stateMachine.TryEnterDodge())
            {
                return;
            }

            dodgeDirection = ResolveDodgeDirection();
            dodgeEndsAt = Time.time + dodgeDuration;
            nextDodgeAllowedAt = Time.time + dodgeCooldown;
            verticalVelocity = characterController.isGrounded ? -2f : verticalVelocity;

            if (rotateToDodgeDirection && dodgeDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);
            }

            TriggerDodgeFeedback();
            SuppressStarterMovementInput();
        }

        private void TickDodge()
        {
            if (Time.time >= dodgeEndsAt)
            {
                stateMachine.ReturnToLocomotion(IsMoveInputActive());
                verticalVelocity = characterController.isGrounded ? -2f : verticalVelocity;
                return;
            }

            float speed = dodgeDistance / dodgeDuration;
            Vector3 displacement = dodgeDirection * (speed * Time.deltaTime);

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            displacement.y = verticalVelocity * Time.deltaTime;
            characterController.Move(displacement);
        }

        private Vector3 ResolveDodgeDirection()
        {
            Vector2 move = bridge != null && bridge.Inputs != null ? bridge.Inputs.move : Vector2.zero;
            Vector3 direction = Vector3.zero;

            if (move.sqrMagnitude > 0.01f)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 cameraForward = mainCamera.transform.forward;
                    Vector3 cameraRight = mainCamera.transform.right;
                    cameraForward.y = 0f;
                    cameraRight.y = 0f;
                    cameraForward.Normalize();
                    cameraRight.Normalize();
                    direction = cameraForward * move.y + cameraRight * move.x;
                }
                else
                {
                    direction = transform.forward * move.y + transform.right * move.x;
                }
            }

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private void TriggerDodgeFeedback()
        {
            if (animator != null && HasAnimatorTrigger(animator, "Dodge"))
            {
                animator.ResetTrigger("Dodge");
                animator.SetTrigger("Dodge");
                return;
            }

            if (animator != null && HasAnimatorTrigger(animator, "Roll"))
            {
                animator.ResetTrigger("Roll");
                animator.SetTrigger("Roll");
                return;
            }

            if (!hasWarnedMissingDodgeTrigger)
            {
                hasWarnedMissingDodgeTrigger = true;
                Debug.LogWarning("Starter Assets Animator has no Dodge/Roll trigger; using prototype dodge visual feedback.", this);
            }

            if (visualFeedback != null)
            {
                visualFeedback.PlayDodge();
            }
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

        private bool DodgePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftAltKey.wasPressedThisFrame;
#else
            if (!hasWarnedMissingInputSystem)
            {
                hasWarnedMissingInputSystem = true;
                Debug.LogWarning("StarterARPGDodgeController requires the Input System package for prototype dodge input.", this);
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
