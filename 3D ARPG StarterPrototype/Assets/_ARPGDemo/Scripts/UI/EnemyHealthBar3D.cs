using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar3D : MonoBehaviour
    {
        [SerializeField] private TrainingDummy3D target;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.35f, 0f);
        [SerializeField] private float width = 1.25f;
        [SerializeField] private float height = 0.12f;
        [SerializeField] private bool hideWhenFull = false;

        private Transform background;
        private Transform fill;
        private Renderer backgroundRenderer;
        private Renderer fillRenderer;
        private bool hasWarnedMissingTarget;

        private void Awake()
        {
            ResolveTarget();
            EnsureBar();
            UpdateBar();
        }

        private void Reset()
        {
            ResolveTarget();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Max(0.1f, width);
            height = Mathf.Max(0.02f, height);

            if (!Application.isPlaying)
            {
                ResolveTarget();
            }
        }
#endif

        private void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                WarnMissingTargetOnce();
                return;
            }

            EnsureBar();
            FollowTarget();
            FaceCamera();
            UpdateBar();
        }

        private void ResolveTarget()
        {
            if (target == null)
            {
                target = GetComponentInParent<TrainingDummy3D>();
            }
        }

        private void EnsureBar()
        {
            if (background != null && fill != null)
            {
                return;
            }

            background = CreateBarPart("HealthBar_Background", new Color(0.08f, 0.08f, 0.08f, 1f));
            fill = CreateBarPart("HealthBar_Fill", new Color(0.15f, 0.95f, 0.25f, 1f));
            fill.SetParent(background, false);
            fill.localPosition = new Vector3(-0.5f, 0f, -0.01f);
            fill.localScale = new Vector3(1f, 0.72f, 0.6f);
        }

        private Transform CreateBarPart(string partName, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                Destroy(partCollider);
            }

            part.transform.SetParent(transform, false);

            Renderer partRenderer = part.GetComponent<Renderer>();
            if (partRenderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }

                partRenderer.material = material;
            }

            if (partName.Contains("Background"))
            {
                backgroundRenderer = partRenderer;
            }
            else
            {
                fillRenderer = partRenderer;
            }

            return part.transform;
        }

        private void FollowTarget()
        {
            transform.position = target.transform.position + worldOffset;
        }

        private void FaceCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Vector3 direction = transform.position - mainCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void UpdateBar()
        {
            if (target == null || background == null || fill == null)
            {
                return;
            }

            float normalizedHP = target.NormalizedHP;
            bool shouldShow = !hideWhenFull || normalizedHP < 0.999f || target.IsDefeated;

            background.localScale = new Vector3(width, height, 0.02f);
            fill.localScale = new Vector3(Mathf.Max(0.001f, normalizedHP), 0.72f, 0.6f);
            fill.localPosition = new Vector3(-0.5f + normalizedHP * 0.5f, 0f, -0.55f);

            SetRendererEnabled(backgroundRenderer, shouldShow);
            SetRendererEnabled(fillRenderer, shouldShow && normalizedHP > 0f);
        }

        private void WarnMissingTargetOnce()
        {
            if (hasWarnedMissingTarget)
            {
                return;
            }

            hasWarnedMissingTarget = true;
            Debug.LogWarning("EnemyHealthBar3D could not find a TrainingDummy3D target in its parents.", this);
        }

        private static void SetRendererEnabled(Renderer targetRenderer, bool isEnabled)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = isEnabled;
            }
        }
    }
}
