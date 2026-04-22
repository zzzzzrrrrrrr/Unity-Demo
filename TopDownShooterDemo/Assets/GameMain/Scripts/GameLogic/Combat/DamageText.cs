using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// World-space floating damage number.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageText : MonoBehaviour
    {
        [SerializeField] private TextMesh textMesh;
        [SerializeField] [Min(0.1f)] private float lifeTime = 0.8f;
        [SerializeField] [Min(0f)] private float riseSpeed = 1.45f;
        [SerializeField] [Min(0f)] private float horizontalDriftSpeed = 0.35f;
        [SerializeField] [Min(0.01f)] private float startScale = 0.52f;
        [SerializeField] [Min(0.01f)] private float endScale = 0.85f;
        [SerializeField] private Color normalColor = new Color(1f, 0.93f, 0.45f, 1f);
        [SerializeField] private Color lethalColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private int sortingOrder = 33;

        private DamageTextSpawner ownerSpawner;
        private float timer;
        private float duration;
        private Vector3 velocity;
        private Color activeColor;
        private bool isPlaying;

        private void Awake()
        {
            EnsureTextMesh();
        }

        private void OnDisable()
        {
            isPlaying = false;
        }

        public void SetTextMesh(TextMesh mesh)
        {
            textMesh = mesh;
            EnsureTextMesh();
        }

        public void Play(float damage, bool isLethal, DamageTextSpawner spawner)
        {
            EnsureTextMesh();
            ownerSpawner = spawner;
            duration = Mathf.Max(0.1f, lifeTime);
            timer = duration;
            isPlaying = true;

            var displayDamage = Mathf.Max(0, Mathf.RoundToInt(damage));
            textMesh.text = displayDamage.ToString();
            activeColor = isLethal ? lethalColor : normalColor;
            textMesh.color = activeColor;
            textMesh.characterSize = isLethal ? 0.115f : 0.1f;

            var randomHorizontal = Random.Range(-horizontalDriftSpeed, horizontalDriftSpeed);
            velocity = new Vector3(randomHorizontal, riseSpeed, 0f);
            transform.localScale = Vector3.one * startScale * (isLethal ? 1.12f : 1f);
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            timer -= Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            var normalized = 1f - Mathf.Clamp01(timer / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, normalized);

            var color = activeColor;
            color.a = 1f - normalized;
            textMesh.color = color;

            if (timer <= 0f)
            {
                isPlaying = false;
                ownerSpawner?.Release(this);
            }
        }

        private void EnsureTextMesh()
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
            }

            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontSize = 56;
                textMesh.characterSize = 0.1f;
            }

            var renderer = textMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }
    }
}
