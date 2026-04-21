// Path: Assets/_Scripts/Tools/ComponentExtensions.cs
using UnityEngine;

namespace ARPGDemo.Tools
{
    /// <summary>
    /// </summary>
    public static class ComponentExtensions
    {
        /// <summary>
        /// </summary>
        public static bool TryGetInParent<T>(this Component component, out T value) where T : Component
        {
            value = null;
            if (component == null)
            {
                return false;
            }

            value = component.GetComponentInParent<T>();
            return value != null;
        }

        /// <summary>
        /// </summary>
        public static void SetLocalScaleX(this Transform transform, float directionSign)
        {
            if (transform == null)
            {
                return;
            }

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (directionSign >= 0f ? 1f : -1f);
            transform.localScale = scale;
        }

        /// <summary>
        /// </summary>
        public static void SetLayerRecursively(this GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;

            Transform rootTransform = root.transform;
            int count = rootTransform.childCount;
            for (int i = 0; i < count; i++)
            {
                rootTransform.GetChild(i).gameObject.SetLayerRecursively(layer);
            }
        }
    }
}

