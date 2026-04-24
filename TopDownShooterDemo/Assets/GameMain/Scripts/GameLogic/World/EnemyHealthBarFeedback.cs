using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Display-only world-space health bar for slice enemies.
    /// Reads SliceEnemyController health; never owns damage, death, or flow state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarFeedback : MonoBehaviour
    {
        [Header("Read-Only Source")]
        [SerializeField] private SliceEnemyController target;

        [Header("Display")]
        [SerializeField] private SpriteRenderer fillSprite;
        [SerializeField] private SpriteRenderer backgroundSprite;

        private Vector3 initialFillScale = Vector3.one;
        private bool hasInitialFillScale;

        private void Awake()
        {
            ResolveTarget();
            CacheInitialScale();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveTarget();
            CacheInitialScale();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void ResolveTarget()
        {
            if (target == null)
            {
                target = GetComponentInParent<SliceEnemyController>();
            }
        }

        private void CacheInitialScale()
        {
            if (!hasInitialFillScale && fillSprite != null)
            {
                initialFillScale = fillSprite.transform.localScale;
                hasInitialFillScale = true;
            }
        }

        private void Refresh()
        {
            if (target == null || fillSprite == null)
            {
                SetVisible(false);
                return;
            }

            var maxHealth = Mathf.Max(0f, target.MaxHealth);
            var currentHealth = Mathf.Max(0f, target.CurrentHealth);
            if (maxHealth <= 0f || currentHealth <= 0f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            var ratio = Mathf.Clamp01(currentHealth / maxHealth);
            var scale = initialFillScale;
            scale.x = initialFillScale.x * ratio;
            fillSprite.transform.localScale = scale;
        }

        private void SetVisible(bool visible)
        {
            if (fillSprite != null)
            {
                fillSprite.enabled = visible;
            }

            if (backgroundSprite != null)
            {
                backgroundSprite.enabled = visible;
            }
        }
    }
}
