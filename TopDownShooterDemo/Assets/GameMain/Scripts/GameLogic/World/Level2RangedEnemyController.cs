using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Level2-only ranged attack companion for a SliceEnemyController health body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level2RangedEnemyController : MonoBehaviour
    {
        [SerializeField] private SliceEnemyController healthBody;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private PlayerHealth targetPlayer;
        [SerializeField] [Min(0.1f)] private float attackRange = 18f;

        private void Update()
        {
            if (healthBody == null || healthBody.IsDead)
            {
                return;
            }

            ResolveTargetPlayer();
            if (targetPlayer == null || targetPlayer.IsDead)
            {
                return;
            }

            var toTarget = (Vector2)(targetPlayer.transform.position - transform.position);
            if (toTarget.sqrMagnitude > attackRange * attackRange)
            {
                return;
            }

            weaponController?.TryFire(toTarget, gameObject);
        }

        public void Configure(
            PlayerHealth target,
            SliceEnemyController body,
            WeaponController weapon,
            float range)
        {
            targetPlayer = target;
            healthBody = body;
            weaponController = weapon;
            attackRange = Mathf.Max(0.1f, range);
        }

        private void ResolveTargetPlayer()
        {
            if (targetPlayer != null && !targetPlayer.IsDead)
            {
                return;
            }

            var hooksPlayer = RuntimeSceneHooks.Active != null ? RuntimeSceneHooks.Active.PlayerHealth : null;
            if (hooksPlayer != null && !hooksPlayer.IsDead)
            {
                targetPlayer = hooksPlayer;
                return;
            }

            targetPlayer = FindObjectOfType<PlayerHealth>();
        }
    }
}
