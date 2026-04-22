using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Basic boss and FSM tuning data placeholder.
    /// </summary>
    [CreateAssetMenu(fileName = "BossStatsData", menuName = "GameMain/Data/Boss Stats")]
    public sealed class BossStatsData : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 300f;
        [Min(0.1f)] public float moveSpeed = 3f;
        [Min(0f)] public float idleDuration = 0.8f;
        [Min(0.1f)] public float burstDuration = 1.8f;
        [Min(0.05f)] public float burstFireInterval = 0.3f;
        [Min(0f)] public float cooldownDuration = 1f;
        [Min(0.1f)] public float engageDistanceMin = 2.2f;
        [Min(0.1f)] public float engageDistanceMax = 5.2f;
        [Min(0.1f)] public float retargetInterval = 1f;

        [Header("Skill - Fan Shot")]
        public bool enableFanShot = true;
        [Range(1, 7)] public int fanShotCount = 3;
        [Min(0f)] public float fanSpreadAngle = 28f;
        [Min(0f)] public float fanSkillWindup = 0.25f;
        [Min(0f)] public float fanSkillRecovery = 0.65f;
        [Min(0.1f)] public float fanSkillInterval = 4.5f;

        [Header("Skill - Radial Nova")]
        public bool enableRadialNova = true;
        [Range(6, 72)] public int radialNovaShotCount = 20;
        [Min(0.1f)] public float radialNovaProjectileSpeedScale = 0.95f;
        [Min(0f)] public float radialNovaWindup = 0.8f;
        [Min(0f)] public float radialNovaRecovery = 0.72f;
        [Min(0.1f)] public float radialNovaInterval = 8.8f;
        [Range(1, 4)] public int radialNovaPulseCount = 1;
        [Min(0.01f)] public float radialNovaPulseInterval = 0.16f;

        [Header("Low HP Aggression")]
        public bool enableLowHealthAggression = true;
        [Range(0.05f, 1f)] public float lowHealthThresholdNormalized = 0.35f;
        [Range(0.2f, 1f)] public float lowHealthBurstFireIntervalScale = 0.65f;
        [Range(0.2f, 1f)] public float lowHealthCooldownScale = 0.6f;
        [Range(1f, 2f)] public float lowHealthChaseSpeedScale = 1.2f;
        [Range(0.2f, 1f)] public float lowHealthRadialNovaIntervalScale = 0.72f;
        [Range(0, 20)] public int lowHealthRadialNovaShotBonus = 4;
    }
}
