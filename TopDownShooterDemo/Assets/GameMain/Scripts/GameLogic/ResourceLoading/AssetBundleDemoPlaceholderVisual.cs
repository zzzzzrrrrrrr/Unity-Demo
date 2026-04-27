using UnityEngine;

namespace GameMain.GameLogic.ResourceLoading
{
    /// <summary>
    /// Runtime-only visual fallback for the AssetBundle demo prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AssetBundleDemoPlaceholderVisual : MonoBehaviour
    {
        [SerializeField] private Color tint = new Color(0.38f, 0.92f, 1f, 1f);
        [SerializeField] private int sortingOrder = 24;

        private static Sprite runtimeSprite;

        private void Awake()
        {
            ApplyVisual();
        }

        private void OnEnable()
        {
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GetRuntimeSprite();
            }

            spriteRenderer.color = tint;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetRuntimeSprite()
        {
            if (runtimeSprite != null)
            {
                return runtimeSprite;
            }

            const int size = 24;
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            FillRect(pixels, size, 9, 17, 15, 22, Color.white);
            FillRect(pixels, size, 6, 6, 18, 17, Color.white);
            FillRect(pixels, size, 4, 9, 6, 15, Color.white);
            FillRect(pixels, size, 18, 9, 20, 15, Color.white);
            FillRect(pixels, size, 8, 2, 11, 6, Color.white);
            FillRect(pixels, size, 13, 2, 16, 6, Color.white);

            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            runtimeSprite.name = "AssetBundleDemoPlaceholderSprite";
            runtimeSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeSprite;
        }

        private static void FillRect(Color[] pixels, int size, int xMin, int yMin, int xMax, int yMax, Color color)
        {
            for (var y = yMin; y < yMax; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    if (x < 0 || x >= size || y < 0 || y >= size)
                    {
                        continue;
                    }

                    pixels[y * size + x] = color;
                }
            }
        }
    }
}
