// 路径: Assets/_Scripts/Tools/SceneTools/ArtSlot2D.cs
using UnityEngine;

namespace ARPGDemo.Tools.SceneTools
{
    [DisallowMultipleComponent]
    public class ArtSlot2D : MonoBehaviour
    {
        [SerializeField] private string slotKey = string.Empty;
        [SerializeField] private SpriteRenderer targetRenderer;

        public string SlotKey => slotKey;
        public SpriteRenderer TargetRenderer => targetRenderer;

        private void Reset()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (string.IsNullOrEmpty(slotKey))
            {
                slotKey = gameObject.name;
            }
        }

        public string ResolveKey()
        {
            if (!string.IsNullOrEmpty(slotKey))
            {
                return slotKey;
            }

            return gameObject.name;
        }

        public bool TryApplySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                return false;
            }

            targetRenderer.sprite = sprite;
            return true;
        }
    }
}
