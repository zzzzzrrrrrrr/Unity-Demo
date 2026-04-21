// Path: Assets/_Scripts/Tools/MathUtility2D.cs
using UnityEngine;

namespace ARPGDemo.Tools
{
    /// <summary>
    /// </summary>
    public static class MathUtility2D
    {
        /// <summary>
        /// </summary>
        public static float ExpSmooth(float current, float target, float sharpness, float deltaTime)
        {
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * Mathf.Max(0f, deltaTime));
            return Mathf.Lerp(current, target, t);
        }

        /// <summary>
        /// </summary>
        public static float MoveTowards(float current, float target, float speed, float deltaTime)
        {
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// </summary>
        public static float SignNonZero(float value, float defaultSign = 1f)
        {
            if (value > 0f)
            {
                return 1f;
            }

            if (value < 0f)
            {
                return -1f;
            }

            return defaultSign >= 0f ? 1f : -1f;
        }

        /// <summary>
        /// </summary>
        public static int ClampMinInt(int value, int minValue)
        {
            return value < minValue ? minValue : value;
        }
    }
}

