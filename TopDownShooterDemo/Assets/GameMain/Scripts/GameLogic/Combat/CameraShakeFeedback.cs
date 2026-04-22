using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Lightweight camera shake without external dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShakeFeedback : MonoBehaviour
    {
        [SerializeField] private bool enableShake = true;
        [SerializeField] [Min(0f)] private float hitAmplitude = 0.045f;
        [SerializeField] [Min(0.01f)] private float hitDuration = 0.09f;
        [SerializeField] [Min(0f)] private float heavyHitAmplitude = 0.078f;
        [SerializeField] [Min(0.01f)] private float heavyHitDuration = 0.16f;
        [SerializeField] [Min(0f)] private float deathAmplitude = 0.12f;
        [SerializeField] [Min(0.01f)] private float deathDuration = 0.22f;
        [SerializeField] [Min(1f)] private float shakeFrequency = 36f;
        [SerializeField] [Range(0.1f, 5f)] private float dampingPower = 2.2f;
        [SerializeField] private bool useUnscaledTime = true;

        private float activeAmplitude;
        private float activeDuration;
        private float timer;
        private Vector3 appliedOffset;
        private bool hasAppliedOffset;

        public static CameraShakeFeedback Instance { get; private set; }

        public bool EnableShake
        {
            get => enableShake;
            set
            {
                enableShake = value;
                if (!enableShake)
                {
                    ResetCameraPose();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ResetCameraPose();
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!enableShake)
            {
                ClearAppliedOffset();
                return;
            }

            ClearAppliedOffset();
            if (timer <= 0f)
            {
                activeAmplitude = 0f;
                activeDuration = 0f;
                return;
            }

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer = Mathf.Max(0f, timer - deltaTime);
            var life01 = activeDuration > 0f ? timer / activeDuration : 0f;
            var damper = Mathf.Pow(Mathf.Clamp01(life01), dampingPower);
            var amplitude = activeAmplitude * damper;

            var time = useUnscaledTime ? Time.unscaledTime : Time.time;
            var x = (Mathf.PerlinNoise(time * shakeFrequency, 0.21f) - 0.5f) * 2f;
            var y = (Mathf.PerlinNoise(0.73f, time * shakeFrequency) - 0.5f) * 2f;
            var offset = new Vector3(x * amplitude, y * amplitude, 0f);
            if (offset.sqrMagnitude > 0f)
            {
                transform.localPosition += offset;
                appliedOffset = offset;
                hasAppliedOffset = true;
            }

            if (timer <= 0f)
            {
                activeAmplitude = 0f;
                activeDuration = 0f;
            }
        }

        public void ShakeHit()
        {
            Shake(hitAmplitude, hitDuration);
        }

        public void ShakeDeath()
        {
            Shake(deathAmplitude, deathDuration);
        }

        public void Shake(float amplitude, float duration)
        {
            if (!enableShake)
            {
                return;
            }

            activeAmplitude = Mathf.Max(activeAmplitude, Mathf.Max(0f, amplitude));
            activeDuration = Mathf.Max(activeDuration, Mathf.Max(0.01f, duration));
            timer = Mathf.Max(timer, activeDuration);
        }

        public static void PlayHit()
        {
            Instance?.ShakeHit();
        }

        public static void PlayDamage(float normalizedDamage)
        {
            if (Instance == null)
            {
                return;
            }

            var normalized = Mathf.Clamp01(normalizedDamage);
            var amplitude = Mathf.Lerp(Instance.hitAmplitude, Instance.heavyHitAmplitude, normalized);
            var duration = Mathf.Lerp(Instance.hitDuration, Instance.heavyHitDuration, normalized);
            Instance.Shake(amplitude, duration);
        }

        public static void PlayDeath()
        {
            Instance?.ShakeDeath();
        }

        public void ResetCameraPose()
        {
            ClearAppliedOffset();
            timer = 0f;
            activeAmplitude = 0f;
            activeDuration = 0f;
        }

        private void ClearAppliedOffset()
        {
            if (!hasAppliedOffset)
            {
                return;
            }

            transform.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
            hasAppliedOffset = false;
        }
    }
}
