using GameMain.GameLogic.Data;
using UnityEngine;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Clickable world target bound to one character data entry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectTarget : MonoBehaviour
    {
        [SerializeField] private CharacterData characterData;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer selectionRingRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.94f, 0.66f, 1f);
        [SerializeField] private Vector3 baseVisualScale = Vector3.zero;
        [SerializeField] private float selectedScaleMultiplier = 1.18f;
        [SerializeField] private float confirmedScaleMultiplier = 1.08f;

        private Vector3 initialLocalScale = Vector3.one;
        private bool isSelected;
        private bool isControllable;
        private bool isDimmed;

        public CharacterData CharacterData
        {
            get { return characterData; }
        }

        public SpriteRenderer BodyRenderer
        {
            get { return bodyRenderer; }
        }

        public Sprite BodySprite
        {
            get { return bodyRenderer != null ? bodyRenderer.sprite : null; }
        }

        public void Setup(CharacterData data, SpriteRenderer body, SpriteRenderer selectionRing)
        {
            characterData = data;
            bodyRenderer = body;
            selectionRingRenderer = selectionRing;
            if (bodyRenderer != null)
            {
                normalColor = bodyRenderer.color;
                selectedColor = Color.Lerp(normalColor, Color.white, 0.42f);
            }

            initialLocalScale = ResolveBaseVisualScale();
            isSelected = false;
            isControllable = false;
            isDimmed = false;
            ApplyVisualState();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyVisualState();
        }

        public void SetControlState(bool controllable, bool dimmed)
        {
            isControllable = controllable;
            isDimmed = dimmed;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (bodyRenderer != null)
            {
                var color = normalColor;
                if (isDimmed)
                {
                    color = Color.Lerp(color, Color.black, 0.48f);
                    color.a = normalColor.a;
                }

                if (isSelected)
                {
                    color = selectedColor;
                }

                if (isControllable)
                {
                    color = Color.Lerp(color, Color.white, 0.2f);
                }

                bodyRenderer.color = color;
            }

            if (selectionRingRenderer != null)
            {
                selectionRingRenderer.enabled = isSelected || isControllable;
            }

            var scaleMultiplier = 1f;
            if (isSelected)
            {
                scaleMultiplier *= Mathf.Max(1f, selectedScaleMultiplier);
            }

            if (isControllable)
            {
                scaleMultiplier *= Mathf.Max(1f, confirmedScaleMultiplier);
            }

            transform.localScale = initialLocalScale * scaleMultiplier;
        }

        private Vector3 ResolveBaseVisualScale()
        {
            var fallbackScale = transform.localScale;
            var resolvedScale = new Vector3(
                Mathf.Abs(baseVisualScale.x) > 0.0001f ? baseVisualScale.x : fallbackScale.x,
                Mathf.Abs(baseVisualScale.y) > 0.0001f ? baseVisualScale.y : fallbackScale.y,
                Mathf.Abs(baseVisualScale.z) > 0.0001f ? baseVisualScale.z : fallbackScale.z);

            if (Mathf.Abs(resolvedScale.x) < 0.01f)
            {
                resolvedScale.x = Mathf.Sign(fallbackScale.x == 0f ? 1f : fallbackScale.x) * 0.01f;
            }

            if (Mathf.Abs(resolvedScale.y) < 0.01f)
            {
                resolvedScale.y = Mathf.Sign(fallbackScale.y == 0f ? 1f : fallbackScale.y) * 0.01f;
            }

            if (Mathf.Abs(resolvedScale.z) < 0.01f)
            {
                resolvedScale.z = Mathf.Sign(fallbackScale.z == 0f ? 1f : fallbackScale.z) * 0.01f;
            }

            return resolvedScale;
        }
    }
}
