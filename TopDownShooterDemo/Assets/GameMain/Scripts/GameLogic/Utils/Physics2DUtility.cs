using UnityEngine;

namespace GameMain.GameLogic.Utils
{
    public static class Physics2DUtility
    {
        public static Vector2 SafeDirection(Vector3 from, Vector3 to, Vector2 fallback)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return fallback.sqrMagnitude < 0.0001f ? Vector2.right : fallback.normalized;
            }

            return ((Vector2)direction).normalized;
        }
    }
}
