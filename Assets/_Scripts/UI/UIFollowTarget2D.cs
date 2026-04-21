// Path: Assets/_Scripts/UI/UIFollowTarget2D.cs
// UI follow helper: maps world target positions to canvas anchored position.
using UnityEngine;

namespace ARPGDemo.UI
{
    public class UIFollowTarget2D : MonoBehaviour
    {
        [SerializeField] private Transform worldTarget;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private bool hideWhenTargetBehindCamera = true;
        [SerializeField] private Vector2 screenPadding = new Vector2(8f, 8f);

        private RectTransform selfRect;
        private CanvasGroup canvasGroup;

        public Transform WorldTarget
        {
            get => worldTarget;
            set => worldTarget = value;
        }

        public Vector3 WorldOffset
        {
            get => worldOffset;
            set => worldOffset = value;
        }

        private void Awake()
        {
            selfRect = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();

            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
        }

        private void LateUpdate()
        {
            if (selfRect == null || parentCanvas == null)
            {
                return;
            }

            if (worldTarget == null)
            {
                SetVisible(false);
                return;
            }

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 worldPos = worldTarget.position + worldOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (hideWhenTargetBehindCamera && screenPos.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            screenPos.x = Mathf.Clamp(screenPos.x, screenPadding.x, Screen.width - screenPadding.x);
            screenPos.y = Mathf.Clamp(screenPos.y, screenPadding.y, Screen.height - screenPadding.y);

            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint);
            selfRect.anchoredPosition = localPoint;

            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                return;
            }

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
