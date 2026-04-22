using System.Collections;
using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Simple death fade + shrink feedback for 2D characters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathFadeFeedback : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField] private bool autoCollectFromChildren = true;
        [SerializeField] [Min(0.05f)] private float fadeDuration = 0.45f;
        [SerializeField] [Range(0.1f, 1f)] private float endScaleMultiplier = 0.68f;
        [SerializeField] private bool disableCollidersOnDeath = true;
        [SerializeField] private bool clearRigidBodyVelocityOnDeath = true;

        private Color[] baseColors;
        private Vector3 baseScale = Vector3.one;
        private Collider2D[] cachedColliders;
        private Rigidbody2D cachedRigidbody;
        private Coroutine playingRoutine;
        private bool warnedMissingRenderer;
        private bool hasBaseScale;

        private void Awake()
        {
            CacheReferences();
            ResetVisuals();
        }

        public void PlayDeath()
        {
            CacheReferences();

            if (playingRoutine != null)
            {
                StopCoroutine(playingRoutine);
            }

            if (clearRigidBodyVelocityOnDeath && cachedRigidbody != null)
            {
                cachedRigidbody.velocity = Vector2.zero;
            }

            if (disableCollidersOnDeath && cachedColliders != null)
            {
                for (var i = 0; i < cachedColliders.Length; i++)
                {
                    if (cachedColliders[i] != null)
                    {
                        cachedColliders[i].enabled = false;
                    }
                }
            }

            playingRoutine = StartCoroutine(PlayRoutine());
        }

        public void ResetVisuals()
        {
            CacheReferences();
            if (playingRoutine != null)
            {
                StopCoroutine(playingRoutine);
                playingRoutine = null;
            }

            transform.localScale = baseScale;
            ApplyColorLerp(0f);

            if (cachedColliders != null)
            {
                for (var i = 0; i < cachedColliders.Length; i++)
                {
                    if (cachedColliders[i] != null)
                    {
                        cachedColliders[i].enabled = true;
                    }
                }
            }
        }

        private IEnumerator PlayRoutine()
        {
            var duration = Mathf.Max(0.05f, fadeDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * Mathf.Clamp(endScaleMultiplier, 0.1f, 1f), t);
                ApplyColorLerp(t);
                yield return null;
            }

            transform.localScale = baseScale * Mathf.Clamp(endScaleMultiplier, 0.1f, 1f);
            ApplyColorLerp(1f);
            playingRoutine = null;
        }

        private void CacheReferences()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                if (autoCollectFromChildren)
                {
                    targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                }
            }

            if ((targetRenderers == null || targetRenderers.Length == 0) && !warnedMissingRenderer)
            {
                warnedMissingRenderer = true;
                Debug.LogWarning("DeathFadeFeedback has no SpriteRenderer target on " + name + ".", this);
            }

            if (targetRenderers != null)
            {
                if (baseColors == null || baseColors.Length != targetRenderers.Length)
                {
                    baseColors = new Color[targetRenderers.Length];
                    for (var i = 0; i < targetRenderers.Length; i++)
                    {
                        baseColors[i] = targetRenderers[i] != null ? targetRenderers[i].color : Color.white;
                    }
                }
            }

            if (cachedColliders == null || cachedColliders.Length == 0)
            {
                cachedColliders = GetComponentsInChildren<Collider2D>(true);
            }

            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody2D>();
            }

            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }
        }

        private void ApplyColorLerp(float t)
        {
            if (targetRenderers == null || baseColors == null)
            {
                return;
            }

            var clamped = Mathf.Clamp01(t);
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var color = baseColors[i];
                color.a = Mathf.Lerp(baseColors[i].a, 0f, clamped);
                renderer.color = color;
            }
        }
    }
}
