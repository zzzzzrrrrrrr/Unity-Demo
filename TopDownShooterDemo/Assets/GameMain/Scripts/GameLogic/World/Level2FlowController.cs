using GameMain.GameLogic.Boss;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Level2-local clear feedback. It does not own first-level gates, result, or portal flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level2FlowController : MonoBehaviour
    {
        [SerializeField] private Level2RangedEnemyController[] levelEnemies;
        [SerializeField] private BossHealth levelBoss;
        [SerializeField] private Level2BossAttackController bossAttack;
        [SerializeField] private Text clearText;

        private bool clearHandled;

        private void OnDisable()
        {
            if (levelBoss != null)
            {
                levelBoss.Defeated -= HandleBossDefeated;
            }
        }

        public void Configure(
            Level2RangedEnemyController[] enemies,
            BossHealth boss,
            Level2BossAttackController attack,
            Text text)
        {
            if (levelBoss != null)
            {
                levelBoss.Defeated -= HandleBossDefeated;
            }

            levelEnemies = enemies;
            levelBoss = boss;
            bossAttack = attack;
            clearText = text;
            clearHandled = false;

            if (clearText != null)
            {
                clearText.text = string.Empty;
                clearText.enabled = false;
            }

            if (levelBoss != null)
            {
                levelBoss.Defeated += HandleBossDefeated;
            }
        }

        private void HandleBossDefeated()
        {
            if (clearHandled)
            {
                return;
            }

            clearHandled = true;
            if (bossAttack != null)
            {
                bossAttack.enabled = false;
            }

            if (clearText != null)
            {
                clearText.text = "第二关清理完成";
                clearText.enabled = true;
            }

            Debug.Log("RunScene_Level2 cleared. Level2 boss defeated.", this);
        }
    }
}
