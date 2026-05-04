using System.Collections;
using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class StarterARPGPrototypeVisualFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Color attackMarkerColor = new Color(1f, 0.62f, 0.12f, 1f);
        [SerializeField] private Color stateFlashColor = new Color(0.35f, 0.8f, 1f, 1f);

        private Coroutine poseRoutine;
        private Coroutine flashRoutine;
        private bool hasWarnedMissingVisualRoot;
        private Vector3 defaultLocalPosition;
        private Quaternion defaultLocalRotation;
        private Vector3 defaultLocalScale;

        private void Awake()
        {
            ResolveVisualRoot();
            CacheDefaultPose();
        }

        private void Reset()
        {
            ResolveVisualRoot();
            CacheDefaultPose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResolveVisualRoot();
                CacheDefaultPose();
            }
        }
#endif

        public void PlayAttack(int comboIndex)
        {
            if (!EnsureVisualRoot())
            {
                return;
            }

            RestartPoseRoutine(AttackRoutine(Mathf.Clamp(comboIndex, 1, 3)));
            SpawnAttackMarker(comboIndex);
        }

        public void PlayDodge()
        {
            if (!EnsureVisualRoot())
            {
                return;
            }

            RestartPoseRoutine(DodgeRoutine());
        }

        public void PlayStateFlash(Color color)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(StateFlashRoutine(color));
        }

        public void PlayStateFlash()
        {
            PlayStateFlash(stateFlashColor);
        }

        private void RestartPoseRoutine(IEnumerator routine)
        {
            if (poseRoutine != null)
            {
                StopCoroutine(poseRoutine);
                RestorePose();
            }

            poseRoutine = StartCoroutine(routine);
        }

        private IEnumerator AttackRoutine(int comboIndex)
        {
            const float duration = 0.18f;
            float elapsed = 0f;
            float sideLean = comboIndex == 2 ? -4f : comboIndex == 3 ? 6f : 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float punch = Mathf.Sin(t * Mathf.PI);
                visualRoot.localPosition = defaultLocalPosition + new Vector3(0f, 0f, 0.04f * punch);
                visualRoot.localRotation = defaultLocalRotation * Quaternion.Euler(-8f * punch, sideLean * punch, 0f);
                visualRoot.localScale = Vector3.Lerp(defaultLocalScale, defaultLocalScale * 1.04f, punch);
                elapsed += Time.deltaTime;
                yield return null;
            }

            RestorePose();
            poseRoutine = null;
        }

        private IEnumerator DodgeRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float squash = Mathf.Sin(t * Mathf.PI);
                visualRoot.localRotation = defaultLocalRotation * Quaternion.Euler(10f * squash, 0f, 0f);
                visualRoot.localScale = new Vector3(
                    defaultLocalScale.x * (1f + 0.08f * squash),
                    defaultLocalScale.y * (1f - 0.12f * squash),
                    defaultLocalScale.z * (1f + 0.08f * squash));
                elapsed += Time.deltaTime;
                yield return null;
            }

            RestorePose();
            poseRoutine = null;
        }

        private IEnumerator StateFlashRoutine(Color color)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            var originalColors = new Color[renderers.Length];
            var hadColor = new bool[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].material;
                hadColor[i] = material.HasProperty("_BaseColor") || material.HasProperty("_Color");
                if (!hadColor[i])
                {
                    continue;
                }

                originalColors[i] = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.GetColor("_Color");
                SetMaterialColor(material, color);
            }

            yield return new WaitForSeconds(0.08f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && hadColor[i])
                {
                    SetMaterialColor(renderers[i].material, originalColors[i]);
                }
            }

            flashRoutine = null;
        }

        private void SpawnAttackMarker(int comboIndex)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"ARPG_Attack{comboIndex}_Fallback";
            marker.transform.SetPositionAndRotation(
                transform.position + transform.forward * (0.9f + comboIndex * 0.12f) + Vector3.up * 0.95f,
                Quaternion.LookRotation(transform.forward, Vector3.up));
            marker.transform.localScale = new Vector3(0.75f + comboIndex * 0.12f, 0.08f, 0.18f);

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                markerRenderer.material = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
                SetMaterialColor(markerRenderer.material, attackMarkerColor);
            }

            Destroy(marker, 0.16f);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Transform[] children = GetComponentsInChildren<Transform>();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "Geometry" || children[i].name == "Armature_Mesh")
                {
                    visualRoot = children[i];
                    return;
                }
            }

            Animator animator = GetComponentInChildren<Animator>();
            visualRoot = animator != null ? animator.transform : transform;
        }

        private void CacheDefaultPose()
        {
            if (visualRoot == null)
            {
                return;
            }

            defaultLocalPosition = visualRoot.localPosition;
            defaultLocalRotation = visualRoot.localRotation;
            defaultLocalScale = visualRoot.localScale;
        }

        private bool EnsureVisualRoot()
        {
            if (visualRoot != null)
            {
                return true;
            }

            ResolveVisualRoot();
            CacheDefaultPose();

            if (visualRoot != null)
            {
                return true;
            }

            if (!hasWarnedMissingVisualRoot)
            {
                hasWarnedMissingVisualRoot = true;
                Debug.LogWarning("StarterARPGPrototypeVisualFeedback could not find a visual root.", this);
            }

            return false;
        }

        private void RestorePose()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = defaultLocalPosition;
            visualRoot.localRotation = defaultLocalRotation;
            visualRoot.localScale = defaultLocalScale;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

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
