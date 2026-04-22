using GameMain.GameLogic.Combat;
using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Basic weapon tuning data placeholder.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponStatsData", menuName = "GameMain/Data/Weapon Stats")]
    public sealed class WeaponStatsData : ScriptableObject
    {
        [Min(0.01f)] public float fireInterval = 0.25f;
        [Min(0.1f)] public float projectileSpeed = 12f;
        [Min(0f)] public float projectileDamage = 10f;
        [Min(0.1f)] public float projectileLifetime = 3f;
        public CombatTeam ownerTeam = CombatTeam.Neutral;
    }
}
