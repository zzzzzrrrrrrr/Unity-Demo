// Path: Assets/_Scripts/Game/PlayerSkillCaster.cs
// Independent skill entry: press U to trigger a front-area skill hit.
using System.Collections.Generic;
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActorStats))]
    public class PlayerSkillCaster : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode castKey = KeyCode.U;
        [SerializeField] private bool useRuntimeInputBinding = true;
        [SerializeField] private string castActionName = "Skill";

        [Header("Skill Config")]
        [SerializeField] private float cooldown = 0.9f;
        [SerializeField] private float range = 1.8f;
        [SerializeField] private float radius = 1.05f;
        [SerializeField] private float damageMultiplier = 1.35f;
        [SerializeField] private float flatDamageBonus = 2f;
        [SerializeField] private bool ignoreTargetInvincible = false;
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("Debug")]
        [SerializeField] private bool logCast = true;
        [SerializeField] private bool drawGizmos = true;

        private ActorStats ownerStats;
        private PlayerController playerController;
        private float nextCastReadyTime;

        private void Awake()
        {
            ownerStats = GetComponent<ActorStats>();
            playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (ownerStats == null || ownerStats.IsDead || !isActiveAndEnabled || Time.timeScale <= 0f)
            {
                return;
            }

            bool castPressed = useRuntimeInputBinding
                ? InputCompat.IsActionDown(castActionName, castKey)
                : InputCompat.IsDown(castKey);

            if (!castPressed)
            {
                return;
            }

            if (logCast)
            {
                Debug.Log("[PlayerSkillCaster] Skill input received.", this);
            }

            TryCastSkill();
        }

        private void TryCastSkill()
        {
            if (Time.time < nextCastReadyTime)
            {
                if (logCast)
                {
                    float remain = nextCastReadyTime - Time.time;
                    Debug.Log("[PlayerSkillCaster] Cast blocked by cooldown, remain=" + remain.ToString("F2"), this);
                }

                return;
            }

            Vector2 center = ResolveCastCenter();
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, Mathf.Max(0.1f, radius), targetLayers);
            HashSet<ActorStats> processedTargets = new HashSet<ActorStats>();
            int affectedTargets = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                ActorStats targetStats = hit.GetComponentInParent<ActorStats>();
                if (targetStats == null || targetStats == ownerStats || targetStats.IsDead)
                {
                    continue;
                }

                if (targetStats.Team == ownerStats.Team)
                {
                    continue;
                }

                if (!processedTargets.Add(targetStats))
                {
                    continue;
                }

                int finalDamage = targetStats.TakeDamage(
                    ownerStats,
                    damageMultiplier,
                    flatDamageBonus,
                    hit.ClosestPoint(center),
                    ignoreTargetInvincible,
                    out bool _);

                if (finalDamage > 0)
                {
                    affectedTargets++;
                }
            }

            nextCastReadyTime = Time.time + Mathf.Max(0.05f, cooldown);

            if (logCast || affectedTargets > 0)
            {
                Debug.Log("[PlayerSkillCaster] Cast triggered at " + center + ", affected=" + affectedTargets, this);
            }
        }

        private Vector2 ResolveCastCenter()
        {
            float facingSign = 1f;
            if (playerController != null)
            {
                facingSign = Mathf.Approximately(playerController.FacingSign, 0f) ? 1f : playerController.FacingSign;
            }
            else if (transform.lossyScale.x < 0f)
            {
                facingSign = -1f;
            }

            return (Vector2)transform.position + new Vector2(Mathf.Max(0f, range) * facingSign, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Vector2 center = ResolveCastCenterForGizmos();
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireSphere(center, Mathf.Max(0.1f, radius));
        }

        private Vector2 ResolveCastCenterForGizmos()
        {
            float sign = transform.lossyScale.x < 0f ? -1f : 1f;
            return (Vector2)transform.position + new Vector2(Mathf.Max(0f, range) * sign, 0f);
        }
    }
}
