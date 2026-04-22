using UnityEngine;

namespace GameMain.GameLogic.Data
{
    /// <summary>
    /// Global battle config placeholder.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfigData", menuName = "GameMain/Data/Battle Config")]
    public sealed class BattleConfigData : ScriptableObject
    {
        public bool autoEnterBattleOnPlay = false;
        [Min(-1f)] public float battleTimeLimit = -1f;
        [Min(0f)] public float respawnDelay = 0f;
        public Vector2 playerSpawnPosition = new Vector2(-3f, 0f);
        public Vector2 bossSpawnPosition = new Vector2(3f, 0f);
    }
}
