using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class Hurtbox3D : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour ownerBehaviour;

        private bool hasWarnedMissingOwner;

        public IDamageable Owner { get; private set; }

        private void Awake()
        {
            ResolveOwner();
            WarnMissingOwnerOnce();
        }

        private void Reset()
        {
            ResolveOwner();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResolveOwner();
            }
        }
#endif

        public IDamageable ResolveOwner()
        {
            if (ownerBehaviour is IDamageable configuredOwner)
            {
                Owner = configuredOwner;
                return Owner;
            }

            MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == this)
                {
                    continue;
                }

                if (behaviours[i] is IDamageable damageable)
                {
                    ownerBehaviour = behaviours[i];
                    Owner = damageable;
                    return Owner;
                }
            }

            Owner = null;
            return null;
        }

        public void WarnMissingOwnerOnce()
        {
            if (Owner != null || hasWarnedMissingOwner)
            {
                return;
            }

            hasWarnedMissingOwner = true;
            Debug.LogWarning("Hurtbox3D could not find an IDamageable owner in its parents.", this);
        }
    }
}
