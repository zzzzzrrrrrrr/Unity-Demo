using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Shared combat contract for all 2D damage receivers.
    /// </summary>
    public interface IDamageable
    {
        CombatTeam Team { get; }

        bool IsDead { get; }

        Vector2 Position { get; }

        void TakeDamage(float amount, GameObject source);

        // Legacy alias kept to avoid breaking old call sites.
        void ReceiveDamage(float amount, GameObject source);
    }
}
