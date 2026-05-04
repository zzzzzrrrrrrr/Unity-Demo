using UnityEngine;

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class PrototypeSlashVFX3D : MonoBehaviour
    {
        [SerializeField] private Color color = Color.cyan;
        [SerializeField] private float lifetime = 0.32f;
        [SerializeField] private float radius = 2f;
        [SerializeField] private float angle = 70f;
        [SerializeField] private float heightOffset = 1.05f;
        [SerializeField] private float thickness = 0.18f;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private LineRenderer lineRenderer;
        private Material meshMaterial;
        private Material lineMaterial;
        private float startedAt;

        private void Awake()
        {
            EnsureComponents();
        }

        private void Update()
        {
            float elapsed = Time.time - startedAt;
            float normalized = lifetime <= 0f ? 1f : Mathf.Clamp01(elapsed / lifetime);
            float alpha = 1f - normalized;
            float scale = Mathf.Lerp(0.55f, 1.15f, Mathf.Sin(normalized * Mathf.PI * 0.5f));
            transform.localScale = new Vector3(scale, scale, scale);
            SetAlpha(alpha);

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
            }

            if (meshMaterial != null)
            {
                Destroy(meshMaterial);
            }

            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        public void Play(Color vfxColor, float vfxLifetime, float vfxRadius, float vfxAngle, float vfxHeightOffset, float vfxThickness)
        {
            color = vfxColor;
            lifetime = Mathf.Max(0.05f, vfxLifetime);
            radius = Mathf.Max(0.1f, vfxRadius);
            angle = Mathf.Clamp(vfxAngle, 1f, 180f);
            heightOffset = Mathf.Max(0f, vfxHeightOffset);
            thickness = Mathf.Max(0.02f, vfxThickness);
            startedAt = Time.time;

            EnsureComponents();
            BuildMesh();
            BuildLine();
            SetAlpha(color.a);
        }

        private void EnsureComponents()
        {
            meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            lineRenderer = lineRenderer != null ? lineRenderer : GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            if (meshMaterial == null)
            {
                meshMaterial = CreateTransparentMaterial();
                meshRenderer.material = meshMaterial;
            }

            if (lineMaterial == null)
            {
                lineMaterial = CreateTransparentMaterial();
                lineRenderer.material = lineMaterial;
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            lineRenderer.useWorldSpace = false;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.widthMultiplier = thickness;
        }

        private void BuildMesh()
        {
            const int segments = 24;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            Color[] colors = new Color[vertices.Length];

            float innerRadius = Mathf.Max(0.05f, radius - thickness * 2.5f);
            float halfAngle = angle * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float yaw = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                vertices[i * 2] = direction * innerRadius + Vector3.up * heightOffset;
                vertices[i * 2 + 1] = direction * radius + Vector3.up * heightOffset;
                colors[i * 2] = color;
                colors[i * 2 + 1] = color;
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int tri = i * 6;
                triangles[tri] = v;
                triangles[tri + 1] = v + 1;
                triangles[tri + 2] = v + 2;
                triangles[tri + 3] = v + 1;
                triangles[tri + 4] = v + 3;
                triangles[tri + 5] = v + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "PrototypeSlashVFXMesh",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }

        private void BuildLine()
        {
            const int points = 25;
            lineRenderer.positionCount = points;
            lineRenderer.widthMultiplier = thickness;

            float halfAngle = angle * 0.5f;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float yaw = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                lineRenderer.SetPosition(i, direction * radius + Vector3.up * heightOffset);
            }
        }

        private void SetAlpha(float alpha)
        {
            Color meshColor = new Color(color.r, color.g, color.b, 0.28f * alpha);
            Color lineColor = new Color(color.r, color.g, color.b, 0.95f * alpha);
            SetMaterialColor(meshMaterial, meshColor);
            SetMaterialColor(lineMaterial, lineColor);
        }

        private static Material CreateTransparentMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                renderQueue = 3000
            };
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private static void SetMaterialColor(Material material, Color targetColor)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", targetColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", targetColor);
            }
        }
    }
}
