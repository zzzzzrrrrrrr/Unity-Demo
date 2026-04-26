using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Boss
{
    /// <summary>
    /// Level2-only boss pattern: repeated rotating radial volleys.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level2BossAttackController : MonoBehaviour
    {
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private PlayerHealth targetPlayer;
        [SerializeField] [Min(0.1f)] private float attackRange = 26f;
        [SerializeField] [Min(0.2f)] private float radialInterval = 1.65f;
        [SerializeField] [Range(4, 24)] private int radialShotCount = 10;

        private float nextAttackTime;
        private float angleOffset;

        private void OnEnable()
        {
            nextAttackTime = Time.time + 0.8f;
            angleOffset = 0f;
        }

        private void Update()
        {
            if (bossHealth == null || bossHealth.IsDead)
            {
                return;
            }

            ResolveTargetPlayer();
            if (targetPlayer == null || targetPlayer.IsDead)
            {
                return;
            }

            var toTarget = (Vector2)(targetPlayer.transform.position - transform.position);
            if (toTarget.sqrMagnitude > attackRange * attackRange || Time.time < nextAttackTime)
            {
                return;
            }

            FireRadialVolley();
            nextAttackTime = Time.time + Mathf.Max(0.2f, radialInterval);
            angleOffset += 360f / Mathf.Max(1, radialShotCount) * 0.5f;
        }

        public void Configure(
            PlayerHealth target,
            BossHealth health,
            WeaponController weapon,
            float range,
            float interval,
            int shotCount)
        {
            targetPlayer = target;
            bossHealth = health;
            weaponController = weapon;
            attackRange = Mathf.Max(0.1f, range);
            radialInterval = Mathf.Max(0.2f, interval);
            radialShotCount = Mathf.Clamp(shotCount, 4, 24);
            nextAttackTime = Time.time + 0.8f;
        }

        private void FireRadialVolley()
        {
            if (weaponController == null)
            {
                return;
            }

            var count = Mathf.Clamp(radialShotCount, 4, 24);
            for (var i = 0; i < count; i++)
            {
                var angle = angleOffset + (360f / count) * i;
                var radians = angle * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                weaponController.FireImmediate(direction, gameObject);
            }
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
