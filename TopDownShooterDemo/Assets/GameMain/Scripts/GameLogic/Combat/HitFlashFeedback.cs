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
        [Header("Shader Flash")]
        [SerializeField] private bool useShaderFlash = true;
        [SerializeField] private Shader flashShader;
        [SerializeField] private Color shaderFlashColor = Color.white;
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] [Range(0f, 4f)] private float outlineSize = 1.25f;

        private Color[] cachedBaseColors;
        private Material[] cachedBaseMaterials;
        private Material[] runtimeFlashMaterials;
        private Vector3 baseScale = Vector3.one;
        private Coroutine playRoutine;
        private bool loggedMissingRenderer;
        private bool shaderFlashActive;

        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineSizeId = Shader.PropertyToID("_OutlineSize");

        private void Awake()
        {
            baseScale = transform.localScale;
            CacheRenderers();
        }

        private void OnDisable()
        {
            RestoreVisuals();
        }

        private void OnDestroy()
        {
            DestroyRuntimeMaterials();
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
                RestoreVisuals();
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
            shaderFlashActive = TryBeginShaderFlash();
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

            if (shaderFlashActive)
            {
                ApplyShaderFlash(1f - t);
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

        private bool TryBeginShaderFlash()
        {
            if (!useShaderFlash || targetRenderers == null || targetRenderers.Length == 0)
            {
                return false;
            }

            var shader = ResolveFlashShader();
            if (shader == null || !shader.isSupported)
            {
                return false;
            }

            EnsureMaterialCaches();
            var applied = false;
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                cachedBaseMaterials[i] = renderer.sharedMaterial;
                if (runtimeFlashMaterials[i] == null || runtimeFlashMaterials[i].shader != shader)
                {
                    DestroyRuntimeMaterial(runtimeFlashMaterials[i]);
                    runtimeFlashMaterials[i] = new Material(shader)
                    {
                        name = "RuntimeHitFlashMaterial",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                }

                var material = runtimeFlashMaterials[i];
                material.SetFloat(FlashAmountId, 1f);
                material.SetColor(FlashColorId, shaderFlashColor);
                material.SetColor(OutlineColorId, outlineColor);
                material.SetFloat(OutlineSizeId, outlineSize);
                renderer.sharedMaterial = material;
                applied = true;
            }

            return applied;
        }

        private Shader ResolveFlashShader()
        {
            if (flashShader != null)
            {
                return flashShader;
            }

            flashShader = Resources.Load<Shader>("Shaders/HitFlashWhiteOutline");
            if (flashShader != null)
            {
                return flashShader;
            }

            flashShader = Shader.Find("GameMain/HitFlashWhiteOutline");
            return flashShader;
        }

        private void EnsureMaterialCaches()
        {
            if (targetRenderers == null)
            {
                cachedBaseMaterials = null;
                runtimeFlashMaterials = null;
                return;
            }

            if (cachedBaseMaterials == null || cachedBaseMaterials.Length != targetRenderers.Length)
            {
                cachedBaseMaterials = new Material[targetRenderers.Length];
            }

            if (runtimeFlashMaterials == null || runtimeFlashMaterials.Length != targetRenderers.Length)
            {
                DestroyRuntimeMaterials();
                runtimeFlashMaterials = new Material[targetRenderers.Length];
            }
        }

        private void ApplyShaderFlash(float amount)
        {
            if (runtimeFlashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < runtimeFlashMaterials.Length; i++)
            {
                var material = runtimeFlashMaterials[i];
                if (material == null)
                {
                    continue;
                }

                material.SetFloat(FlashAmountId, Mathf.Clamp01(amount));
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
                if (cachedBaseMaterials != null && i < cachedBaseMaterials.Length)
                {
                    renderer.sharedMaterial = cachedBaseMaterials[i];
                }
            }

            shaderFlashActive = false;
        }

        private void DestroyRuntimeMaterials()
        {
            if (runtimeFlashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < runtimeFlashMaterials.Length; i++)
            {
                DestroyRuntimeMaterial(runtimeFlashMaterials[i]);
                runtimeFlashMaterials[i] = null;
            }
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
