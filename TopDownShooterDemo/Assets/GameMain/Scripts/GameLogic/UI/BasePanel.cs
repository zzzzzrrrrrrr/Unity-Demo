using UnityEngine;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Minimal display-only panel base. It only controls visibility.
    /// </summary>
    [DisallowMultipleComponent]
    public class BasePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool visibleOnAwake;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            ResolveViewReferences();
            SetVisible(visibleOnAwake);
        }

        public virtual void Show()
        {
            SetVisible(true);
        }

        public virtual void Hide()
        {
            SetVisible(false);
        }

        public void Toggle()
        {
            SetVisible(!IsVisible);
        }

        public void BindPanelRoot(GameObject root, CanvasGroup group = null)
        {
            panelRoot = root != null ? root : gameObject;
            canvasGroup = group != null ? group : panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
        }

        protected void SetVisible(bool visible)
        {
            ResolveViewReferences();
            IsVisible = visible;

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
        }

        private void ResolveViewReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = panelRoot.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}
