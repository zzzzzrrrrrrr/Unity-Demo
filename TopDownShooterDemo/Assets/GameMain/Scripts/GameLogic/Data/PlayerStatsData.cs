using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Basic player tuning data placeholder.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsData", menuName = "GameMain/Data/Player Stats")]
    public sealed class PlayerStatsData : ScriptableObject
    {
        [Min(0.1f)] public float moveSpeed = 6f;
        [Min(1f)] public float maxHealth = 100f;
        public float aimRotationOffset = 0f;

        [Header("Active Skill - Dodge")]
        public KeyCode dodgeKey = KeyCode.Space;
        [Min(0.1f)] public float dodgeDistance = 2.8f;
        [Min(0.05f)] public float dodgeDuration = 0.18f;
        [Min(0.1f)] public float dodgeCooldown = 1.6f;
        public bool dodgeInvulnerable = true;
        [Range(0f, 0.95f)] public float dodgeDamageReduction = 0.75f;
    }
}
