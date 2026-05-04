using System;
using UnityEngine;

namespace ARPGDemo
{
    [Serializable]
    public sealed class SkillRuntimeState3D
    {
        [SerializeField] private SkillDefinition3D definition;
        [SerializeField] private float cooldownRemaining;

        public SkillRuntimeState3D(SkillDefinition3D definition)
        {
            this.definition = definition;
        }

        public SkillDefinition3D Definition => definition;
        public float CooldownRemaining => cooldownRemaining;
        public bool IsReady => definition != null && cooldownRemaining <= 0f;

        public float Cooldown01
        {
            get
            {
                if (definition == null || definition.Cooldown <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(cooldownRemaining / definition.Cooldown);
            }
        }

        public void SetDefinition(SkillDefinition3D newDefinition)
        {
            if (definition == newDefinition)
            {
                return;
            }

            definition = newDefinition;
            cooldownRemaining = 0f;
        }

        public void StartCooldown()
        {
            cooldownRemaining = definition != null ? definition.Cooldown : 0f;
        }

        public void Tick(float deltaTime)
        {
            if (cooldownRemaining <= 0f)
            {
                cooldownRemaining = 0f;
                return;
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
        }
    }
}
