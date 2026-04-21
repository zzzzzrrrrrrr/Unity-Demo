// Path: Assets/_Scripts/Game/AttackHitbox2D.cs
using System.Collections;
using System.Collections.Generic;
using ARPGDemo.Core;
using UnityEngine;

namespace ARPGDemo.Game
{
    public class AttackHitbox2D : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField] private ActorStats ownerStats;

        [Header("Hitbox")]
        [SerializeField] private Transform hitPoint;
        [SerializeField] private Vector2 hitBoxSize = new Vector2(1.2f, 0.8f);
        [SerializeField] private Vector2 localOffset = new Vector2(0.8f, 0f);
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private bool hitOneTargetOnlyOncePerWindow = true;

        [Header("Animation Event Fallback")]
        [Tooltip("If enabled, RequestAttack will simulate Begin->Hit->End when animation events are missing.")]
        [SerializeField] private bool autoFrameEventSimulation = true;
        [SerializeField] private float autoHitDelay = 0.08f;
        [SerializeField] private float autoWindowDuration = 0.12f;

        [Header("Hit Feedback")]
        [SerializeField] private bool enableHitKnockback = true;
        [SerializeField] private float hitKnockbackForce = 2.4f;
        [SerializeField] private float hitKnockbackUpwardBias = 0.2f;
        [SerializeField] private bool logHitFeedback;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private float runtimeSkillMultiplier = 1f;
        private float runtimeFlatBonus;
        private bool runtimeIgnoreInvincible;

        private bool attackWindowOpened;
        private readonly HashSet<string> hitCache = new HashSet<string>();
        private Coroutine autoRoutine;
        private int requestSequence;
        private int currentRequestId;
        private int lastHitRequestId = -1;
        private bool pendingAutoSimulation;

        private void Awake()
        {
            if (ownerStats == null)
            {
                ownerStats = GetComponentInParent<ActorStats>();
            }
        }

        private void OnDisable()
        {
            if (autoRoutine != null)
            {
                StopCoroutine(autoRoutine);
                autoRoutine = null;
            }

            attackWindowOpened = false;
            pendingAutoSimulation = false;
            currentRequestId = 0;
            lastHitRequestId = -1;
            hitCache.Clear();
        }

        public void SetAttackParams(float skillMultiplier, float flatBonus, bool ignoreInvincible)
        {
            runtimeSkillMultiplier = Mathf.Max(0f, skillMultiplier);
            runtimeFlatBonus = flatBonus;
            runtimeIgnoreInvincible = ignoreInvincible;
        }

        public void RequestAttack()
        {
            if (ownerStats == null)
            {
                ownerStats = GetComponentInParent<ActorStats>();
            }

            if (ownerStats == null || ownerStats.IsDead || !isActiveAndEnabled)
            {
                return;
            }

            currentRequestId = ++requestSequence;
            attackWindowOpened = false;
            hitCache.Clear();

            if (!autoFrameEventSimulation)
            {
                return;
            }

            pendingAutoSimulation = true;

            if (autoRoutine != null)
            {
                StopCoroutine(autoRoutine);
            }

            autoRoutine = StartCoroutine(AutoFrameRoutine(currentRequestId));
        }

        public void AnimEvent_BeginAttackWindow()
        {
            BeginAttackWindowInternal(true);
        }

        public void AnimEvent_DoHit()
        {
            DoHitInternal(true);
        }

        public void AnimEvent_EndAttackWindow()
        {
            EndAttackWindowInternal(true);
        }

        private IEnumerator AutoFrameRoutine(int requestId)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, autoHitDelay));

            if (!pendingAutoSimulation || requestId != currentRequestId)
            {
                autoRoutine = null;
                yield break;
            }

            BeginAttackWindowInternal(false);
            DoHitInternal(false);

            yield return new WaitForSeconds(Mathf.Max(0f, autoWindowDuration));

            if (requestId == currentRequestId)
            {
                EndAttackWindowInternal(false);
            }

            pendingAutoSimulation = false;
            autoRoutine = null;
        }

        private void BeginAttackWindowInternal(bool fromAnimationEvent)
        {
            if (fromAnimationEvent)
            {
                CancelPendingAutoSimulation();

                if (!attackWindowOpened)
                {
                    currentRequestId = ++requestSequence;
                }
            }

            attackWindowOpened = true;
            hitCache.Clear();
        }

        private void DoHitInternal(bool fromAnimationEvent)
        {
            if (fromAnimationEvent)
            {
                CancelPendingAutoSimulation();
            }

            if (!attackWindowOpened)
            {
                BeginAttackWindowInternal(fromAnimationEvent);
            }

            if (lastHitRequestId == currentRequestId)
            {
                return;
            }

            ExecuteHitDetection();
            lastHitRequestId = currentRequestId;
        }

        private void EndAttackWindowInternal(bool fromAnimationEvent)
        {
            if (fromAnimationEvent)
            {
                CancelPendingAutoSimulation();
            }

            attackWindowOpened = false;
        }

        private void CancelPendingAutoSimulation()
        {
            if (!pendingAutoSimulation)
            {
                return;
            }

            pendingAutoSimulation = false;
            if (autoRoutine != null)
            {
                StopCoroutine(autoRoutine);
                autoRoutine = null;
            }
        }

        private void ExecuteHitDetection()
        {
            if (ownerStats == null || ownerStats.IsDead)
            {
                return;
            }

            Vector2 center = ResolveHitCenter();
            Collider2D[] hitResults = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f, targetLayers);

            for (int i = 0; i < hitResults.Length; i++)
            {
                Collider2D hitCollider = hitResults[i];
                if (hitCollider == null)
                {
                    continue;
                }

                ActorStats targetStats = hitCollider.GetComponentInParent<ActorStats>();
                if (targetStats == null || targetStats == ownerStats || targetStats.IsDead)
                {
                    continue;
                }

                if (targetStats.Team == ownerStats.Team)
                {
                    continue;
                }

                string cacheKey = string.IsNullOrEmpty(targetStats.ActorId)
                    ? targetStats.GetInstanceID().ToString()
                    : targetStats.ActorId;

                if (hitOneTargetOnlyOncePerWindow && hitCache.Contains(cacheKey))
                {
                    continue;
                }

                bool isCritical;
                int finalDamage = targetStats.TakeDamage(
                    ownerStats,
                    runtimeSkillMultiplier,
                    runtimeFlatBonus,
                    hitCollider.ClosestPoint(center),
                    runtimeIgnoreInvincible,
                    out isCritical);

                if (finalDamage > 0 && enableHitKnockback)
                {
                    ApplyHitKnockback(targetStats);
                }

                if (hitOneTargetOnlyOncePerWindow)
                {
                    hitCache.Add(cacheKey);
                }
            }
        }

        private void ApplyHitKnockback(ActorStats targetStats)
        {
            if (targetStats == null || ownerStats == null)
            {
                return;
            }

            Rigidbody2D targetRb = targetStats.GetComponent<Rigidbody2D>();
            if (targetRb == null || targetRb.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            Vector2 dir = (targetStats.transform.position - ownerStats.transform.position);
            if (dir.sqrMagnitude < 0.0001f)
            {
                float sign = ResolveFacingSign();
                dir = Vector2.right * sign;
            }

            dir.Normalize();
            dir.y = Mathf.Max(dir.y, hitKnockbackUpwardBias);
            dir.Normalize();
            targetRb.AddForce(dir * Mathf.Max(0f, hitKnockbackForce), ForceMode2D.Impulse);

            if (logHitFeedback)
            {
                Debug.Log("[Combat][Knockback] Target=" + targetStats.ActorId + ", Force=" + hitKnockbackForce, this);
            }
        }

        private float ResolveFacingSign()
        {
            float hitboxScaleX = transform.lossyScale.x;
            if (Mathf.Abs(hitboxScaleX) > 0.001f)
            {
                return hitboxScaleX >= 0f ? 1f : -1f;
            }

            if (ownerStats != null)
            {
                return ownerStats.transform.localScale.x >= 0f ? 1f : -1f;
            }

            return 1f;
        }

        private Vector2 ResolveHitCenter()
        {
            if (hitPoint != null)
            {
                return hitPoint.position;
            }

            return transform.TransformPoint(localOffset);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Vector2 center = hitPoint != null ? (Vector2)hitPoint.position : (Vector2)transform.TransformPoint(localOffset);
            Gizmos.DrawWireCube(center, hitBoxSize);
        }
    }
}
