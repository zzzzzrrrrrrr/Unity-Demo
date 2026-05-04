using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARPGDemo
{
    [DefaultExecutionOrder(-55)]
    [DisallowMultipleComponent]
    public sealed class PlayerSkillCaster3D : MonoBehaviour
    {
        private const int SkillSlotCount = 3;

        [Header("References")]
        [SerializeField] private StarterARPGBridge bridge;
        [SerializeField] private StarterARPGActionStateMachine stateMachine;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform castOrigin;

        [Header("Skills")]
        [SerializeField] private SkillDefinition3D[] skillDefinitions = new SkillDefinition3D[SkillSlotCount];
        [SerializeField] private LayerMask targetMask = 1;
        [SerializeField] private bool lockMovementDuringCast = true;
        [SerializeField] private bool turnToCameraForwardOnCast = true;

        [Header("Prototype VFX")]
        [SerializeField] private float vfxLifetime = 0.32f;
        [SerializeField] private float vfxHeightOffset = 1.05f;
        [SerializeField] private float vfxThickness = 0.18f;

        private readonly Collider[] overlapResults = new Collider[32];
        private readonly HashSet<IDamageable> hitCache = new HashSet<IDamageable>();

        private SkillRuntimeState3D[] runtimeStates = new SkillRuntimeState3D[SkillSlotCount];
        private float castEndsAt;
        private bool isCasting;
        private bool hasWarnedMissingDependencies;
#if !ENABLE_INPUT_SYSTEM
        private bool hasWarnedMissingInputSystem;
#endif

        public SkillRuntimeState3D[] RuntimeStates
        {
            get
            {
                EnsureRuntimeStates();
                return runtimeStates;
            }
        }

        public IReadOnlyList<SkillRuntimeState3D> SkillStates => RuntimeStates;

        private void Awake()
        {
            ResolveDependencies();
            EnsureRuntimeStates();
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

            if (skillDefinitions == null || skillDefinitions.Length != SkillSlotCount)
            {
                System.Array.Resize(ref skillDefinitions, SkillSlotCount);
            }

            vfxLifetime = Mathf.Max(0.05f, vfxLifetime);
            vfxHeightOffset = Mathf.Max(0f, vfxHeightOffset);
            vfxThickness = Mathf.Max(0.02f, vfxThickness);
        }
#endif

        private void Update()
        {
            EnsureRuntimeStates();
            TickCooldowns(Time.deltaTime);

            if (!EnsureDependencies())
            {
                return;
            }

            if (isCasting)
            {
                TickCast();
                return;
            }

            int slotIndex = GetPressedSkillSlotThisFrame();
            if (slotIndex >= 0)
            {
                TryCastSkill(slotIndex);
            }
        }

        private void ResolveDependencies()
        {
            bridge = bridge != null ? bridge : GetComponent<StarterARPGBridge>();
            stateMachine = stateMachine != null ? stateMachine : GetComponent<StarterARPGActionStateMachine>();
            animator = animator != null ? animator : GetComponentInChildren<Animator>();

            if (castOrigin == null)
            {
                Transform existingOrigin = transform.Find("SkillCastOrigin");
                castOrigin = existingOrigin != null ? existingOrigin : transform;
            }
        }

        private bool EnsureDependencies()
        {
            if (bridge == null || stateMachine == null)
            {
                ResolveDependencies();
            }

            bool hasDependencies = bridge != null && stateMachine != null && castOrigin != null;
            if (!hasDependencies && !hasWarnedMissingDependencies)
            {
                hasWarnedMissingDependencies = true;
                Debug.LogWarning("PlayerSkillCaster3D requires StarterARPGBridge, StarterARPGActionStateMachine, and a cast origin.", this);
            }

            return hasDependencies;
        }

        private void EnsureRuntimeStates()
        {
            if (runtimeStates == null || runtimeStates.Length != SkillSlotCount)
            {
                runtimeStates = new SkillRuntimeState3D[SkillSlotCount];
            }

            if (skillDefinitions == null || skillDefinitions.Length != SkillSlotCount)
            {
                System.Array.Resize(ref skillDefinitions, SkillSlotCount);
            }

            for (int i = 0; i < SkillSlotCount; i++)
            {
                if (runtimeStates[i] == null)
                {
                    runtimeStates[i] = new SkillRuntimeState3D(skillDefinitions[i]);
                }
                else
                {
                    runtimeStates[i].SetDefinition(skillDefinitions[i]);
                }
            }
        }

        private void TickCooldowns(float deltaTime)
        {
            for (int i = 0; i < runtimeStates.Length; i++)
            {
                runtimeStates[i]?.Tick(deltaTime);
            }
        }

        private void TickCast()
        {
            if (lockMovementDuringCast)
            {
                SuppressStarterMovementInput();
            }

            if (Time.time < castEndsAt)
            {
                return;
            }

            isCasting = false;
            if (stateMachine.CurrentState == StarterARPGActionState.Skill)
            {
                stateMachine.ReturnToLocomotion(IsMoveInputActive());
            }
        }

        private void TryCastSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= runtimeStates.Length)
            {
                return;
            }

            SkillRuntimeState3D state = runtimeStates[slotIndex];
            SkillDefinition3D definition = state?.Definition;
            if (definition == null || !state.IsReady || !stateMachine.CanCastSkill)
            {
                return;
            }

            if (!stateMachine.TryEnterSkill())
            {
                return;
            }

            isCasting = true;
            castEndsAt = Time.time + definition.CastDuration;
            state.StartCooldown();

            if (turnToCameraForwardOnCast)
            {
                TurnToCameraForward();
            }

            TriggerAnimator(slotIndex);
            SpawnVFX(definition);
            ApplySkillDamage(definition, slotIndex + 1);

            if (lockMovementDuringCast)
            {
                SuppressStarterMovementInput();
            }
        }

        private void TriggerAnimator(int slotIndex)
        {
            string triggerName = $"Skill{slotIndex + 1}";
            if (animator == null || !HasAnimatorTrigger(animator, triggerName))
            {
                return;
            }

            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
        }

        private void SpawnVFX(SkillDefinition3D definition)
        {
            Transform origin = GetCastOrigin();
            Quaternion rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
            GameObject vfxInstance = definition.VfxPrefab != null
                ? Instantiate(definition.VfxPrefab, origin.position, rotation)
                : new GameObject($"VFX_{definition.SkillName}");

            vfxInstance.transform.SetPositionAndRotation(origin.position, rotation);

            PrototypeSlashVFX3D slashVFX = vfxInstance.GetComponent<PrototypeSlashVFX3D>();
            if (slashVFX == null)
            {
                slashVFX = vfxInstance.AddComponent<PrototypeSlashVFX3D>();
            }

            slashVFX.Play(definition.VfxColor, vfxLifetime, definition.Range, definition.Angle, vfxHeightOffset, vfxThickness);
        }

        private void ApplySkillDamage(SkillDefinition3D definition, int comboIndex)
        {
            Transform origin = GetCastOrigin();
            Vector3 originPosition = origin.position;
            Vector3 forward = transform.forward;
            Vector3 overlapCenter = originPosition + forward * (definition.Range * 0.5f);
            float overlapRadius = Mathf.Max(definition.Radius, definition.Range * 0.5f);
            int hitCount = Physics.OverlapSphereNonAlloc(overlapCenter, overlapRadius, overlapResults, targetMask, QueryTriggerInteraction.Collide);

            hitCache.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = overlapResults[i];
                if (hitCollider == null || hitCollider.transform.root == transform.root)
                {
                    continue;
                }

                IDamageable damageable = ResolveDamageable(hitCollider);
                if (damageable == null || hitCache.Contains(damageable))
                {
                    continue;
                }

                Vector3 hitPoint = hitCollider.ClosestPoint(originPosition);
                Vector3 toTarget = hitPoint - originPosition;
                toTarget.y = 0f;

                if (!IsInsideSkillArc(toTarget, forward, definition))
                {
                    continue;
                }

                hitCache.Add(damageable);
                GameObject target = damageable is MonoBehaviour behaviour ? behaviour.gameObject : hitCollider.gameObject;
                Vector3 hitDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : forward;

                damageable.ApplyDamage(new DamageInfo
                {
                    Source = gameObject,
                    Target = target,
                    Amount = definition.Damage,
                    HitPoint = hitPoint,
                    HitDirection = hitDirection,
                    ComboIndex = comboIndex
                });
            }
        }

        private bool IsInsideSkillArc(Vector3 toTarget, Vector3 forward, SkillDefinition3D definition)
        {
            if (toTarget.sqrMagnitude < 0.001f)
            {
                return true;
            }

            float distance = toTarget.magnitude;
            if (distance > definition.Range + definition.Radius * 0.5f)
            {
                return false;
            }

            float halfAngle = definition.Angle * 0.5f;
            return Vector3.Angle(forward, toTarget.normalized) <= halfAngle;
        }

        private IDamageable ResolveDamageable(Collider hitCollider)
        {
            Hurtbox3D hurtbox = hitCollider.GetComponent<Hurtbox3D>();
            if (hurtbox == null)
            {
                hurtbox = hitCollider.GetComponentInParent<Hurtbox3D>();
            }

            if (hurtbox != null)
            {
                IDamageable owner = hurtbox.Owner ?? hurtbox.ResolveOwner();
                if (owner != null)
                {
                    return owner;
                }

                hurtbox.WarnMissingOwnerOnce();
            }

            MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private Transform GetCastOrigin()
        {
            return castOrigin != null ? castOrigin : transform;
        }

        private int GetPressedSkillSlotThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                return 0;
            }

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                return 1;
            }

            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                return 2;
            }

            return -1;
#else
            if (!hasWarnedMissingInputSystem)
            {
                hasWarnedMissingInputSystem = true;
                Debug.LogWarning("PlayerSkillCaster3D requires the Input System package for prototype skill input.", this);
            }

            return -1;
#endif
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

        private static bool HasAnimatorTrigger(Animator targetAnimator, string triggerName)
        {
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
