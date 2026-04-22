using UnityEngine;

namespace GameMain.GameLogic.Combat
{
    /// <summary>
    /// Short-lifetime hit flash visual spawned at projectile impact position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactFlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] [Min(0.02f)] private float lifeTime = 0.15f;
        [SerializeField] [Min(0.01f)] private float startScale = 0.22f;
        [SerializeField] [Min(0.01f)] private float endScale = 0.62f;

        private ImpactFlashEffectSpawner ownerSpawner;
        private Color activeColor;
        private float timer;
        private float duration;
        private bool isPlaying;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnDisable()
        {
            isPlaying = false;
        }

        public void SetRenderer(SpriteRenderer renderer)
        {
            spriteRenderer = renderer;
            EnsureRenderer();
        }

        public void Play(Color color, ImpactFlashEffectSpawner spawner)
        {
            EnsureRenderer();
            ownerSpawner = spawner;
            activeColor = color;
            duration = Mathf.Max(0.02f, lifeTime);
            timer = duration;
            isPlaying = true;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, startScale);
            spriteRenderer.color = color;
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            timer -= Time.deltaTime;
            var normalized = 1f - Mathf.Clamp01(timer / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, normalized);

            var color = activeColor;
            color.a = Mathf.Lerp(activeColor.a, 0f, normalized);
            spriteRenderer.color = color;

            if (timer <= 0f)
            {
                isPlaying = false;
                ownerSpawner?.Release(this);
            }
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
    }
}
