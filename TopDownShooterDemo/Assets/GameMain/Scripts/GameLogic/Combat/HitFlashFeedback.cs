using System.Collections;
using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Reusable hit feedback: color flash + optional scale pulse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlashFeedback : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField] private bool autoCollectFromChildren = true;
        [SerializeField] private Color hitColor = new Color(1f, 0.32f, 0.32f, 1f);
        [SerializeField] [Min(0.01f)] private float flashDuration = 0.08f;
        [SerializeField] [Range(1f, 1.6f)] private float pulseScaleMultiplier = 1.08f;
        [SerializeField] [Min(0f)] private float pulseDuration = 0.1f;

        private Color[] cachedBaseColors;
        private Vector3 baseScale = Vector3.one;
        private Coroutine playRoutine;
        private bool loggedMissingRenderer;

        private void Awake()
        {
            baseScale = transform.localScale;
            CacheRenderers();
        }

        private void OnDisable()
        {
            RestoreVisuals();
        }

        public void PlayHit()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            playRoutine = StartCoroutine(PlayRoutine());
        }

        public void CaptureCurrentAsBaseState()
        {
            baseScale = transform.localScale;
            CacheRenderers();
            CacheBaseColors();
        }

        private IEnumerator PlayRoutine()
        {
            CacheRenderers();
            CacheBaseColors();
            var effectiveFlashDuration = Mathf.Max(0.01f, flashDuration);
            var enablePulse = pulseDuration > 0f && pulseScaleMultiplier > 1f;
            var effectivePulseDuration = enablePulse ? Mathf.Max(0.01f, pulseDuration) : 0f;
            var maxDuration = Mathf.Max(effectiveFlashDuration, effectivePulseDuration);

            var elapsed = 0f;
            while (elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;

                var flashT = Mathf.Clamp01(elapsed / effectiveFlashDuration);
                ApplyFlash(flashT);

                if (enablePulse)
                {
                    var pulseT = Mathf.Clamp01(elapsed / effectivePulseDuration);
                    transform.localScale = Vector3.Lerp(baseScale * pulseScaleMultiplier, baseScale, pulseT);
                }

                yield return null;
            }

            RestoreVisuals();
            playRoutine = null;
        }

        private void CacheRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                if (autoCollectFromChildren)
                {
                    targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                }
            }

            if ((targetRenderers == null || targetRenderers.Length == 0) && !loggedMissingRenderer)
            {
                Debug.LogWarning("HitFlashFeedback has no SpriteRenderer targets on " + name + ".", this);
                loggedMissingRenderer = true;
            }
        }

        private void CacheBaseColors()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                cachedBaseColors = null;
                return;
            }

            if (cachedBaseColors == null || cachedBaseColors.Length != targetRenderers.Length)
            {
                cachedBaseColors = new Color[targetRenderers.Length];
            }

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                cachedBaseColors[i] = targetRenderers[i] != null ? targetRenderers[i].color : Color.white;
            }
        }

        private void ApplyFlash(float t)
        {
            if (targetRenderers == null || cachedBaseColors == null)
            {
                return;
            }

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = Color.Lerp(hitColor, cachedBaseColors[i], t);
            }
        }

        private void RestoreVisuals()
        {
            transform.localScale = baseScale;

            if (targetRenderers == null || cachedBaseColors == null)
            {
                return;
            }

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = cachedBaseColors[i];
            }
        }
    }
}
