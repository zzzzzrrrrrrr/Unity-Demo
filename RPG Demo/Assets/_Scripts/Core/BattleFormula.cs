// Path: Assets/_Scripts/Core/BattleFormula.cs
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Core
{
    /// <summary>
    /// </summary>
    public struct DamageContext
    {
        public float AttackerAttack;
        public float DefenderDefense;
        public float SkillMultiplier;
        public float FlatBonus;
        public float DefenseScale;
        public float CriticalChance;
        public float CriticalMultiplier;
        public int MinimumDamage;
    }

    /// <summary>
    /// </summary>
    public static class BattleFormula
    {
        /// <summary>
        /// </summary>
        public static int CalculateDamage(DamageContext context, out bool isCritical)
        {
            float rawDamage = context.AttackerAttack * context.SkillMultiplier + context.FlatBonus;

            rawDamage -= context.DefenderDefense * context.DefenseScale;

            int minDamage = Mathf.Max(1, context.MinimumDamage);
            int baseDamage = MathUtility2D.ClampMinInt(Mathf.RoundToInt(rawDamage), minDamage);

            float critChance = Mathf.Clamp01(context.CriticalChance);
            isCritical = Random.value <= critChance;

            if (isCritical)
            {
                float critMul = Mathf.Max(1f, context.CriticalMultiplier);
                baseDamage = Mathf.RoundToInt(baseDamage * critMul);
            }

            return Mathf.Max(minDamage, baseDamage);
        }
    }
}

