using System.Collections;
using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class TrainingDummy3D : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float currentHP;
        [SerializeField] private bool isDefeated;
        [SerializeField] private bool autoReset = true;
        [SerializeField] private float resetDelay = 3f;

        [Header("Feedback")]
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private Color normalColor = new Color(0.95f, 0.72f, 0.25f, 1f);
        [SerializeField] private Color hitColor = new Color(1f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color defeatedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private Transform visualRoot;

        private Coroutine feedbackRoutine;
        private Coroutine resetRoutine;
        private Vector3 defaultVisualScale = Vector3.one;

        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public bool IsDefeated => isDefeated;
        public float NormalizedHP => maxHP <= 0f ? 0f : Mathf.Clamp01(currentHP / maxHP);

        private void Awake()
        {
            maxHP = Mathf.Max(1f, maxHP);
            EnsureVisual();
            CacheVisualDefaults();
            ResetDummy();
        }

        private void Reset()
        {
            maxHP = 100f;
            currentHP = maxHP;
            autoReset = true;
            resetDelay = 3f;
            ResolveVisualReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHP = Mathf.Max(1f, maxHP);
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
            resetDelay = Mathf.Max(0.1f, resetDelay);

            if (!Application.isPlaying)
            {
                ResolveVisualReferences();
            }
        }
#endif

        public void ApplyDamage(DamageInfo info)
        {
            if (isDefeated || info.Amount <= 0f)
            {
                return;
            }

            currentHP = Mathf.Max(0f, currentHP - info.Amount);

            if (currentHP <= 0f)
            {
                EnterDefeated();
                return;
            }

            PlayHitFeedback(info.HitDirection);
        }

        public void ResetDummy()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            if (resetRoutine != null)
            {
                StopCoroutine(resetRoutine);
                resetRoutine = null;
            }

            currentHP = maxHP;
            isDefeated = false;
            RestoreVisualScale();
            SetRendererColor(normalColor);
        }

        private void EnterDefeated()
        {
            isDefeated = true;

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            SetRendererColor(defeatedColor);
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(defaultVisualScale.x * 1.08f, defaultVisualScale.y * 0.55f, defaultVisualScale.z * 1.08f);
            }

            if (autoReset)
            {
                resetRoutine = StartCoroutine(AutoResetRoutine());
            }
        }

        private void PlayHitFeedback(Vector3 hitDirection)
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(HitFeedbackRoutine(hitDirection));
        }

        private IEnumerator HitFeedbackRoutine(Vector3 hitDirection)
        {
            SetRendererColor(hitColor);

            if (visualRoot != null)
            {
                Vector3 direction = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : transform.forward;
                float elapsed = 0f;
                const float duration = 0.16f;

                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    float punch = Mathf.Sin(t * Mathf.PI);
                    visualRoot.localScale = Vector3.Lerp(defaultVisualScale, defaultVisualScale * 1.12f, punch);
                    visualRoot.localPosition = direction * (0.05f * punch);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                RestoreVisualScale();
            }
            else
            {
                yield return new WaitForSeconds(0.08f);
            }

            SetRendererColor(normalColor);
            feedbackRoutine = null;
        }

        private IEnumerator AutoResetRoutine()
        {
            yield return new WaitForSeconds(resetDelay);
            ResetDummy();
        }

        private void EnsureVisual()
        {
            ResolveVisualReferences();

            if (visualRoot != null && renderers.Length > 0)
            {
                return;
            }

            if (visualRoot == null)
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(transform, false);
                visualRoot = visual.transform;
            }

            if (renderers.Length == 0)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(visualRoot, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);
                body.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);

                Collider bodyCollider = body.GetComponent<Collider>();
                if (bodyCollider != null)
                {
                    Destroy(bodyCollider);
                }

                renderers = body.GetComponentsInChildren<Renderer>();
            }
        }

        private void ResolveVisualReferences()
        {
            if (visualRoot == null)
            {
                Transform visual = transform.Find("Visual");
                visualRoot = visual != null ? visual : transform;
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);
            }
        }

        private void CacheVisualDefaults()
        {
            if (visualRoot != null)
            {
                defaultVisualScale = visualRoot.localScale;
            }
        }

        private void RestoreVisualScale()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = defaultVisualScale;
        }

        private void SetRendererColor(Color color)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Material material = renderers[i].material;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }
        }
    }
}
