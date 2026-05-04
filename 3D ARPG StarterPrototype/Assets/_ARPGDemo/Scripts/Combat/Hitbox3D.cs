using System.Collections.Generic;
using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class Hitbox3D : MonoBehaviour
    {
        [SerializeField] private Transform origin;
        [SerializeField] private float radius = 1f;
        [SerializeField] private float forwardOffset = 0.7f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.25f, 0.05f, 0.3f);
        [SerializeField] private bool logHits = false;

        private readonly Collider[] overlapResults = new Collider[24];
        private readonly HashSet<IDamageable> hitCache = new HashSet<IDamageable>();

        private bool isActive;
        private float activeUntil;
        private int comboIndex;

        public bool IsActive => isActive;
        public float Radius => radius;
        public float ForwardOffset => forwardOffset;
        public LayerMask TargetMask => targetMask;

        private void Awake()
        {
            if (origin == null)
            {
                origin = transform;
            }
        }

        private void Reset()
        {
            origin = transform;
            targetMask = ~0;
            drawGizmos = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            radius = Mathf.Max(0.05f, radius);
            forwardOffset = Mathf.Max(0f, forwardOffset);
            damage = Mathf.Max(0f, damage);
        }
#endif

        private void Update()
        {
            if (!isActive)
            {
                return;
            }

            if (Time.time >= activeUntil)
            {
                Deactivate();
                return;
            }

            PerformHitCheck();
        }

        public void Configure(float newRadius, float newForwardOffset)
        {
            radius = Mathf.Max(0.05f, newRadius);
            forwardOffset = Mathf.Max(0f, newForwardOffset);
        }

        public void Activate(float duration, float attackDamage, int attackComboIndex)
        {
            damage = Mathf.Max(0f, attackDamage);
            comboIndex = attackComboIndex;
            activeUntil = Time.time + Mathf.Max(0.01f, duration);
            isActive = true;
            ClearHitCache();
            PerformHitCheck();
        }

        public void Deactivate()
        {
            isActive = false;
        }

        public void ClearHitCache()
        {
            hitCache.Clear();
        }

        public void PerformHitCheck()
        {
            Vector3 center = GetCenter();
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapResults, targetMask, QueryTriggerInteraction.Collide);

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

                hitCache.Add(damageable);
                DamageInfo info = BuildDamageInfo(hitCollider, damageable, center);
                damageable.ApplyDamage(info);

                if (logHits)
                {
                    Debug.Log($"Hitbox3D hit {info.Target.name} for {info.Amount:0.#}.", this);
                }
            }
        }

        private DamageInfo BuildDamageInfo(Collider hitCollider, IDamageable damageable, Vector3 center)
        {
            Vector3 hitPoint = hitCollider.ClosestPoint(center);
            Vector3 direction = hitCollider.transform.position - transform.root.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = GetOrigin().forward;
            }

            GameObject target = damageable is MonoBehaviour behaviour ? behaviour.gameObject : hitCollider.gameObject;

            return new DamageInfo
            {
                Source = transform.root.gameObject,
                Target = target,
                Amount = damage,
                HitPoint = hitPoint,
                HitDirection = direction.normalized,
                ComboIndex = comboIndex
            };
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

        private Vector3 GetCenter()
        {
            Transform originTransform = GetOrigin();
            return originTransform.position + originTransform.forward * forwardOffset;
        }

        private Transform GetOrigin()
        {
            return origin != null ? origin : transform;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(GetCenter(), radius);
        }
    }
}
