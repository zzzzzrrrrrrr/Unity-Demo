// Path: Assets/_Scripts/Game/EnemyAIController2D.cs
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Game
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ActorStats))]
    public class EnemyAIController2D : MonoBehaviour
    {
        public enum EnemyCombatStyle
        {
            Default = 0,
            Dash = 1,
            Shield = 2,
            BossSample = 3
        }

        [Header("References")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Animator animator;
        [SerializeField] private ActorStats stats;
        [SerializeField] private AttackHitbox2D attackHitbox;
        [SerializeField] private Transform playerTarget;

        [Header("FSM Settings")]
        [SerializeField] private float stateSwitchCooldown = 0.12f;
        [SerializeField] private float idleDuration = 0.9f;
        [SerializeField] private float hurtDuration = 0.25f;

        [Header("Patrol")]
        [SerializeField] private float patrolSpeed = 1.2f;
        [SerializeField] private float patrolRange = 3f;

        [Header("Chase / Attack")]
        [SerializeField] private float chaseSpeed = 2.4f;
        [SerializeField] private float detectRange = 6f;
        [SerializeField] private float attackRange = 1.25f;
        [SerializeField] private float attackCooldown = 1f;

        [Header("Attack Damage")]
        [SerializeField] private float attackDamageMultiplier = 1f;
        [SerializeField] private float attackFlatBonus = 0f;
        [SerializeField] private bool attackIgnoreInvincible = false;
        [SerializeField] private string attackTrigger = "Attack";

        [Header("Animation Triggers")]
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string deathTrigger = "Death";

        [Header("Target Search")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private float playerSearchInterval = 0.5f;

        [Header("Combat Style (Optional)")]
        [SerializeField] private EnemyCombatStyle combatStyle = EnemyCombatStyle.Default;
        [SerializeField] private bool autoApplyDemoStyleByActorId = true;

        [Header("Dash Style")]
        [SerializeField] private float dashSpeed = 5.2f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 2.1f;

        [Header("Shield Style")]
        [SerializeField] private float shieldInvincibleDuration = 0.2f;
        [SerializeField] private float shieldCooldown = 2.8f;

        [Header("Boss Sample Style")]
        [SerializeField] [Range(0.1f, 1f)] private float bossEnrageHpRatio = 0.35f;
        [SerializeField] private float bossEnrageChaseMultiplier = 1.3f;
        [SerializeField] private float bossEnrageAttackCooldownMultiplier = 0.78f;
        [SerializeField] private float bossEnrageDamageMultiplier = 1.2f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private bool logStyleEvents;

        private int patrolDirection = 1;
        private float desiredVelocityX;
        private bool facingRight = true;
        private float nextStateSwitchTime;
        private float nextPlayerSearchTime;
        private float lastAttackTime = -999f;
        private bool pendingHurt;
        private bool canAIUpdate = true;
        private float patrolOriginX;
        private float dashEndTime;
        private float nextDashTime;
        private float dashDirection = 1f;
        private float nextShieldTime;
        private bool bossEnraged;
        private float runtimeAttackCooldown;
        private float runtimeAttackDamageMultiplier;
        private float runtimeChaseSpeed;

        private IEnemyState currentState;
        private IdleState idleState;
        private PatrolState patrolState;
        private ChaseState chaseState;
        private AttackState attackState;
        private HurtState hurtState;
        private DeathState deathState;

        private float stateTimer;

        private interface IEnemyState
        {
            ActorStateType StateType { get; }
            void Enter();
            void Tick();
            void Exit();
        }

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

            DisableAlternativeEnemyController();

            idleState = new IdleState(this);
            patrolState = new PatrolState(this);
            chaseState = new ChaseState(this);
            attackState = new AttackState(this);
            hurtState = new HurtState(this);
            deathState = new DeathState(this);
        }

        private void Start()
        {
            patrolOriginX = transform.position.x;
            ApplyDemoStyleIfNeeded();
            ResetRuntimeCombatValues();
            ChangeState(idleState, true);
        }

        private void OnEnable()
        {
            EventCenter.AddListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.AddListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<DamageAppliedEvent>(OnDamageApplied);
            EventCenter.RemoveListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void Update()
        {
            if (stats == null)
            {
                return;
            }

            if (autoFindPlayer)
            {
                TryAcquirePlayer();
            }

            if (stats.IsDead)
            {
                ChangeState(deathState, true);
                currentState?.Tick();
                UpdateAnimatorParameters();
                return;
            }

            if (!canAIUpdate)
            {
                desiredVelocityX = 0f;
                UpdateAnimatorParameters();
                return;
            }

            TryApplyBossEnrage();

            if (pendingHurt && currentState != hurtState)
            {
                pendingHurt = false;
                ChangeState(hurtState, true);
            }

            currentState?.Tick();
            UpdateAnimatorParameters();
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            if (!canAIUpdate || stats == null || stats.IsDead)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
                return;
            }

            rb.velocity = new Vector2(desiredVelocityX, rb.velocity.y);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (stats == null || stats.IsDead)
            {
                return;
            }

            if (evt.TargetId != stats.ActorId)
            {
                return;
            }

            if (evt.FinalDamage <= 0)
            {
                return;
            }

            pendingHurt = true;
            TryActivateShieldAfterHit();
        }

        private void OnGameFlowChanged(GameFlowStateChangedEvent evt)
        {
            canAIUpdate = evt.CurrentState == GameFlowState.Playing;
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

        private void ChangeState(IEnemyState next, bool force)
        {
            if (next == null)
            {
                return;
            }

            if (currentState == next)
            {
                return;
            }

            if (!force && Time.time < nextStateSwitchTime)
            {
                return;
            }

            currentState?.Exit();
            currentState = next;
            currentState.Enter();

            nextStateSwitchTime = Time.time + (force ? 0f : stateSwitchCooldown);
        }

        private bool HasValidPlayer()
        {
            if (playerTarget == null)
            {
                return false;
            }

            ActorStats playerStats = playerTarget.GetComponentInParent<ActorStats>();
            return playerStats != null && !playerStats.IsDead;
        }

        private void TryAcquirePlayer()
        {
            if (HasValidPlayer())
            {
                return;
            }

            if (Time.time < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);

            ActorStats[] all = FindObjectsOfType<ActorStats>();
            float nearestSqr = float.MaxValue;
            Transform nearest = null;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Team != ActorTeam.Player || all[i].IsDead)
                {
                    continue;
                }

                float sqr = (all[i].transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = all[i].transform;
                }
            }

            playerTarget = nearest;
        }

        private float DistanceToPlayer()
        {
            if (!HasValidPlayer())
            {
                return float.MaxValue;
            }

            return Vector2.Distance(transform.position, playerTarget.position);
        }

        private bool IsPlayerDetected()
        {
            return DistanceToPlayer() <= detectRange;
        }

        private bool IsPlayerInAttackRange()
        {
            return DistanceToPlayer() <= attackRange;
        }

        private void FaceTo(float worldX)
        {
            float dir = worldX >= transform.position.x ? 1f : -1f;
            facingRight = dir > 0f;
            transform.SetLocalScaleX(dir);
        }

        private void SetMoveDirection(float dir, float speed)
        {
            float sign = MathUtility2D.SignNonZero(dir, patrolDirection);
            desiredVelocityX = sign * Mathf.Max(0f, speed);
            FaceTo(transform.position.x + sign);
        }

        private void SetIdleMove()
        {
            desiredVelocityX = 0f;
        }

        private void TriggerAttack()
        {
            if (Time.time < lastAttackTime + runtimeAttackCooldown)
            {
                return;
            }

            lastAttackTime = Time.time;

            if (attackHitbox != null)
            {
                attackHitbox.SetAttackParams(runtimeAttackDamageMultiplier, attackFlatBonus, attackIgnoreInvincible);
                attackHitbox.RequestAttack();
            }

            if (stats != null && combatStyle == EnemyCombatStyle.BossSample)
            {
                EventCenter.Broadcast(new AttackPerformedEvent(stats.ActorId, stats.Team, true, transform.position));
            }

            TrySetAnimatorTriggerIfExists(attackTrigger);
        }

        private void ApplyDemoStyleIfNeeded()
        {
            if (!autoApplyDemoStyleByActorId || stats == null)
            {
                return;
            }

            if (stats.ActorId == "Enemy_02")
            {
                combatStyle = EnemyCombatStyle.Dash;
            }
            else if (stats.ActorId == "Enemy_03")
            {
                combatStyle = EnemyCombatStyle.Shield;
            }
            else if (stats.ActorId == "Enemy_04")
            {
                combatStyle = EnemyCombatStyle.BossSample;
            }
        }

        private void ResetRuntimeCombatValues()
        {
            runtimeAttackCooldown = Mathf.Max(0.05f, attackCooldown);
            runtimeAttackDamageMultiplier = Mathf.Max(0f, attackDamageMultiplier);
            runtimeChaseSpeed = Mathf.Max(0f, chaseSpeed);
            bossEnraged = false;
        }

        private bool TryDashMove()
        {
            if (combatStyle != EnemyCombatStyle.Dash && combatStyle != EnemyCombatStyle.BossSample)
            {
                return false;
            }

            if (Time.time < dashEndTime)
            {
                desiredVelocityX = dashDirection * Mathf.Max(0f, dashSpeed);
                FaceTo(transform.position.x + dashDirection);
                return true;
            }

            if (Time.time < nextDashTime || !HasValidPlayer())
            {
                return false;
            }

            float dx = playerTarget.position.x - transform.position.x;
            float absDx = Mathf.Abs(dx);
            if (absDx < attackRange * 1.25f || absDx > detectRange * 1.35f)
            {
                return false;
            }

            dashDirection = dx >= 0f ? 1f : -1f;
            dashEndTime = Time.time + Mathf.Max(0.05f, dashDuration);
            nextDashTime = Time.time + Mathf.Max(0.3f, dashCooldown);
            desiredVelocityX = dashDirection * Mathf.Max(0f, dashSpeed);
            FaceTo(transform.position.x + dashDirection);

            if (logStyleEvents)
            {
                Debug.Log($"[EnemyAI] DashStart id={stats.ActorId}, dir={dashDirection:F0}", this);
            }

            return true;
        }

        private void TryActivateShieldAfterHit()
        {
            if (stats == null)
            {
                return;
            }

            if (combatStyle != EnemyCombatStyle.Shield && combatStyle != EnemyCombatStyle.BossSample)
            {
                return;
            }

            if (Time.time < nextShieldTime)
            {
                return;
            }

            float duration = Mathf.Max(0f, shieldInvincibleDuration);
            if (duration <= 0f)
            {
                return;
            }

            stats.AddTemporaryInvincible(duration);
            nextShieldTime = Time.time + Mathf.Max(0.2f, shieldCooldown);

            if (logStyleEvents)
            {
                Debug.Log($"[EnemyAI] ShieldProc id={stats.ActorId}, duration={duration:F2}", this);
            }
        }

        private void TryApplyBossEnrage()
        {
            if (combatStyle != EnemyCombatStyle.BossSample || bossEnraged || stats == null || stats.MaxHealth <= 0f)
            {
                return;
            }

            float hpRatio = stats.CurrentHealth / stats.MaxHealth;
            if (hpRatio > Mathf.Clamp01(bossEnrageHpRatio))
            {
                return;
            }

            bossEnraged = true;
            runtimeChaseSpeed = Mathf.Max(0f, chaseSpeed * Mathf.Max(1f, bossEnrageChaseMultiplier));
            runtimeAttackCooldown = Mathf.Max(0.05f, attackCooldown * Mathf.Clamp(bossEnrageAttackCooldownMultiplier, 0.2f, 1f));
            runtimeAttackDamageMultiplier = Mathf.Max(0f, attackDamageMultiplier * Mathf.Max(1f, bossEnrageDamageMultiplier));

            if (logStyleEvents)
            {
                Debug.Log($"[EnemyAI] BossEnrage id={stats.ActorId}, hpRatio={hpRatio:F2}", this);
            }
        }

        private void EnterState(ActorStateType stateType)
        {
            if (stats != null)
            {
                stats.BroadcastState(stateType);
            }
        }

        private void UpdateAnimatorParameters()
        {
            if (animator == null || rb == null || stats == null)
            {
                return;
            }

            SetAnimatorFloatIfExists("Speed", Mathf.Abs(rb.velocity.x));
            SetAnimatorBoolIfExists("Dead", stats.IsDead);
        }

        private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(paramName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == paramName && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void TrySetAnimatorTriggerIfExists(string triggerName)
        {
            if (!HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            animator.SetTrigger(triggerName);
        }

        private void SetAnimatorBoolIfExists(string boolName, bool value)
        {
            if (!HasAnimatorParameter(boolName, AnimatorControllerParameterType.Bool))
            {
                return;
            }

            animator.SetBool(boolName, value);
        }

        private void SetAnimatorFloatIfExists(string floatName, float value)
        {
            if (!HasAnimatorParameter(floatName, AnimatorControllerParameterType.Float))
            {
                return;
            }

            animator.SetFloat(floatName, value);
        }

        private class IdleState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Idle;

            public IdleState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                owner.EnterState(StateType);
                owner.stateTimer = Time.time + owner.idleDuration;
                owner.SetIdleMove();
            }

            public void Tick()
            {
                if (owner.stats.IsDead)
                {
                    owner.ChangeState(owner.deathState, true);
                    return;
                }

                if (owner.IsPlayerInAttackRange())
                {
                    owner.ChangeState(owner.attackState, false);
                    return;
                }

                if (owner.IsPlayerDetected())
                {
                    owner.ChangeState(owner.chaseState, false);
                    return;
                }

                if (Time.time >= owner.stateTimer)
                {
                    owner.ChangeState(owner.patrolState, false);
                }
            }

            public void Exit()
            {
            }
        }

        private class PatrolState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Patrol;

            public PatrolState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                owner.EnterState(StateType);
            }

            public void Tick()
            {
                if (owner.stats.IsDead)
                {
                    owner.ChangeState(owner.deathState, true);
                    return;
                }

                if (owner.IsPlayerInAttackRange())
                {
                    owner.ChangeState(owner.attackState, false);
                    return;
                }

                if (owner.IsPlayerDetected())
                {
                    owner.ChangeState(owner.chaseState, false);
                    return;
                }

                float left = owner.patrolOriginX - owner.patrolRange;
                float right = owner.patrolOriginX + owner.patrolRange;

                if (owner.transform.position.x <= left)
                {
                    owner.patrolDirection = 1;
                }
                else if (owner.transform.position.x >= right)
                {
                    owner.patrolDirection = -1;
                }

                owner.SetMoveDirection(owner.patrolDirection, owner.patrolSpeed);
            }

            public void Exit()
            {
            }
        }

        private class ChaseState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Chase;

            public ChaseState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                owner.EnterState(StateType);
            }

            public void Tick()
            {
                if (owner.stats.IsDead)
                {
                    owner.ChangeState(owner.deathState, true);
                    return;
                }

                if (!owner.IsPlayerDetected())
                {
                    owner.ChangeState(owner.patrolState, false);
                    return;
                }

                if (owner.IsPlayerInAttackRange())
                {
                    owner.ChangeState(owner.attackState, false);
                    return;
                }

                if (!owner.HasValidPlayer())
                {
                    owner.ChangeState(owner.patrolState, false);
                    return;
                }

                if (owner.TryDashMove())
                {
                    return;
                }

                float dir = owner.playerTarget.position.x - owner.transform.position.x;
                owner.SetMoveDirection(dir, owner.runtimeChaseSpeed);
            }

            public void Exit()
            {
            }
        }

        private class AttackState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Attack;

            public AttackState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                owner.EnterState(StateType);
                owner.SetIdleMove();
            }

            public void Tick()
            {
                if (owner.stats.IsDead)
                {
                    owner.ChangeState(owner.deathState, true);
                    return;
                }

                if (!owner.IsPlayerDetected())
                {
                    owner.ChangeState(owner.patrolState, false);
                    return;
                }

                if (!owner.IsPlayerInAttackRange())
                {
                    owner.ChangeState(owner.chaseState, false);
                    return;
                }

                owner.SetIdleMove();
                if (owner.playerTarget != null)
                {
                    owner.FaceTo(owner.playerTarget.position.x);
                }

                owner.TriggerAttack();
            }

            public void Exit()
            {
            }
        }

        private class HurtState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Hurt;

            public HurtState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                owner.EnterState(StateType);
                owner.SetIdleMove();
                owner.stateTimer = Time.time + owner.hurtDuration;

                owner.TrySetAnimatorTriggerIfExists(owner.hurtTrigger);
            }

            public void Tick()
            {
                if (owner.stats.IsDead)
                {
                    owner.ChangeState(owner.deathState, true);
                    return;
                }

                if (Time.time < owner.stateTimer)
                {
                    return;
                }

                if (owner.IsPlayerInAttackRange())
                {
                    owner.ChangeState(owner.attackState, false);
                }
                else if (owner.IsPlayerDetected())
                {
                    owner.ChangeState(owner.chaseState, false);
                }
                else
                {
                    owner.ChangeState(owner.patrolState, false);
                }
            }

            public void Exit()
            {
            }
        }

        private class DeathState : IEnemyState
        {
            private readonly EnemyAIController2D owner;
            public ActorStateType StateType => ActorStateType.Death;
            private bool entered;

            public DeathState(EnemyAIController2D owner)
            {
                this.owner = owner;
            }

            public void Enter()
            {
                if (entered)
                {
                    return;
                }

                entered = true;
                owner.EnterState(StateType);
                owner.SetIdleMove();

                owner.TrySetAnimatorTriggerIfExists(owner.deathTrigger);
                owner.TrySetAnimatorTriggerIfExists("Die");
            }

            public void Tick()
            {
                owner.SetIdleMove();
            }

            public void Exit()
            {
            }
        }

        private void DisableAlternativeEnemyController()
        {
            System.Type legacyType = System.Type.GetType("ARPGDemo.Game.Enemy.EnemyFSM");
            if (legacyType == null)
            {
                System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    legacyType = assemblies[i].GetType("ARPGDemo.Game.Enemy.EnemyFSM");
                    if (legacyType != null)
                    {
                        break;
                    }
                }
            }

            if (legacyType == null || !typeof(Behaviour).IsAssignableFrom(legacyType))
            {
                return;
            }

            Component alternativeFsm = GetComponent(legacyType);
            if (alternativeFsm is Behaviour behaviour && behaviour.enabled)
            {
                behaviour.enabled = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.cyan;
            Vector3 left = new Vector3(transform.position.x - patrolRange, transform.position.y, transform.position.z);
            Vector3 right = new Vector3(transform.position.x + patrolRange, transform.position.y, transform.position.z);
            Gizmos.DrawLine(left, right);
        }
    }
}

