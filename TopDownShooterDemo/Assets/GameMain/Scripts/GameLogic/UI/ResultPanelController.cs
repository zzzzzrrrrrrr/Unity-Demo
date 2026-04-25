using System.Collections;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Formal result panel for battle end states.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;
        [SerializeField] private Button backToMenuButton;
        [Header("Show Animation")]
        [SerializeField] private bool animateOnShow = true;
        [SerializeField] [Min(0.05f)] private float showDuration = 0.3f;
        [SerializeField] [Range(0.4f, 1f)] private float contentStartScale = 0.82f;
        [Header("Title Emphasis")]
        [SerializeField] private Color victoryTitleColor = new Color(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color defeatTitleColor = new Color(1f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color abortTitleColor = new Color(1f, 0.75f, 0.32f, 1f);
        [SerializeField] private Color defaultTitleColor = Color.white;
        [SerializeField] private int emphasizedTitleFontSize = 42;
        [SerializeField] private int normalTitleFontSize = 34;

        private ProcedureManager procedureManager;
        private CanvasGroup panelCanvasGroup;
        private bool loggedMissingViewRefs;
        private Coroutine showAnimationRoutine;
        private Vector3 contentBaseScale = Vector3.one;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            EnsureCanvasGroup();
            ResolveContentRoot();
            CaptureVisibleContentScale();

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.RemoveListener(OnBackToMenuClicked);
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
            }
        }

        private void OnEnable()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                Configure(GameEntryBridge.Procedure);
            }
            else if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }

            ValidateViewReferences();
            ApplyVisibility(
                procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Result,
                procedureManager != null);
            if (procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Result)
            {
                UpdateResultText();
            }
        }

        private void OnDisable()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            StopShowAnimation();
        }

        public void BindView(GameObject root, Text title, Text detail, Button backButton, RectTransform content = null)
        {
            panelRoot = root;
            contentRoot = content;
            titleText = title;
            detailText = detail;
            backToMenuButton = backButton;
            ResolveContentRoot();
            EnsureCanvasGroup();
            CaptureVisibleContentScale();

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.RemoveListener(OnBackToMenuClicked);
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
            }
        }

        public void Configure(ProcedureManager manager)
        {
            if (procedureManager == manager)
            {
                return;
            }

            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            procedureManager = manager;
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }

            ApplyVisibility(
                procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Result,
                false);
            if (procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Result)
            {
                UpdateResultText();
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            var show = current == ProcedureType.Result;
            if (show)
            {
                UpdateResultText();
            }

            ApplyVisibility(show, true);
        }

        private void UpdateResultText()
        {
            if (procedureManager == null)
            {
                return;
            }

            var result = procedureManager.LastBattleResult;
            var resultProcedure = procedureManager.CurrentProcedure as ProcedureResult;
            if (resultProcedure != null)
            {
                result = resultProcedure.CurrentResult;
            }

            var title = "Battle Result";
            var detail = "返回角色选择。";
            var backButtonLabel = "返回角色选择";
            var titleColor = defaultTitleColor;
            var titleFontSize = normalTitleFontSize;
            switch (result)
            {
                case BattleResultType.Win:
                    title = "通关成功";
                    detail = "已击败 Boss。按 Enter / Space 或点击按钮返回角色选择。";
                    titleColor = victoryTitleColor;
                    titleFontSize = emphasizedTitleFontSize;
                    break;
                case BattleResultType.Lose:
                    title = "挑战失败";
                    detail = "角色已被击败。返回角色选择后可再次挑战。";
                    titleColor = defeatTitleColor;
                    titleFontSize = emphasizedTitleFontSize;
                    break;
                case BattleResultType.Abort:
                    title = "挑战结束";
                    titleColor = abortTitleColor;
                    titleFontSize = emphasizedTitleFontSize - 2;
                    break;
            }

            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = titleColor;
                titleText.fontStyle = FontStyle.Bold;
                titleText.fontSize = Mathf.Max(20, titleFontSize);
            }

            if (detailText != null)
            {
                detailText.text = detail;
            }

            SetBackButtonLabel(backButtonLabel);
        }

        private void OnBackToMenuClicked()
        {
            GameEntryBridge.SwitchProcedure(ProcedureType.Menu);
        }

        private void SetBackButtonLabel(string label)
        {
            if (backToMenuButton == null)
            {
                return;
            }

            var buttonLabel = backToMenuButton.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = label;
            }
        }

        private void ApplyVisibility(bool visible, bool allowAnimation)
        {
            if (panelRoot == null)
            {
                return;
            }

            EnsureCanvasGroup();
            ResolveContentRoot();

            if (!visible)
            {
                StopShowAnimation();
                SetImmediateState(false);
                return;
            }

            if (allowAnimation && animateOnShow && isActiveAndEnabled)
            {
                PlayShowAnimation();
                return;
            }

            StopShowAnimation();
            SetImmediateState(true);
        }

        private void ValidateViewReferences()
        {
            if (loggedMissingViewRefs)
            {
                return;
            }

            if (titleText == null || detailText == null || backToMenuButton == null)
            {
                loggedMissingViewRefs = true;
                Debug.LogWarning(
                    "ResultPanelController is missing title/detail/button reference. Result UI may not work fully.",
                    this);
            }
        }

        private void PlayShowAnimation()
        {
            StopShowAnimation();
            showAnimationRoutine = StartCoroutine(PlayShowAnimationRoutine());
        }

        private IEnumerator PlayShowAnimationRoutine()
        {
            EnsureCanvasGroup();
            ResolveContentRoot();
            EnsureVisibleContentScale();
            SetRootActive(true);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.blocksRaycasts = true;
                panelCanvasGroup.interactable = false;
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.interactable = false;
            }

            var fromScale = contentRoot != null ? contentBaseScale * Mathf.Clamp(contentStartScale, 0.4f, 1f) : Vector3.one;
            if (contentRoot != null)
            {
                contentRoot.localScale = fromScale;
            }

            var duration = Mathf.Max(0.05f, showDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = eased;
                }

                if (contentRoot != null)
                {
                    contentRoot.localScale = Vector3.LerpUnclamped(fromScale, contentBaseScale, eased);
                }

                yield return null;
            }

            SetImmediateState(true);
            showAnimationRoutine = null;
        }

        private void SetImmediateState(bool visible)
        {
            EnsureCanvasGroup();
            ResolveContentRoot();
            EnsureVisibleContentScale();
            SetRootActive(visible);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
                panelCanvasGroup.blocksRaycasts = visible;
                panelCanvasGroup.interactable = visible;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = visible ? contentBaseScale : contentBaseScale * Mathf.Clamp(contentStartScale, 0.4f, 1f);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.interactable = visible;
            }
        }

        private void StopShowAnimation()
        {
            if (showAnimationRoutine != null)
            {
                StopCoroutine(showAnimationRoutine);
                showAnimationRoutine = null;
            }
        }

        private void EnsureCanvasGroup()
        {
            if (panelRoot == null)
            {
                return;
            }

            var target = panelRoot;
            panelCanvasGroup = target.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = target.AddComponent<CanvasGroup>();
            }
        }

        private void ResolveContentRoot()
        {
            if (contentRoot != null || panelRoot == null)
            {
                return;
            }

            var contentTransform = panelRoot.transform.Find("Content");
            if (contentTransform != null)
            {
                contentRoot = contentTransform as RectTransform;
            }
        }

        private void CaptureVisibleContentScale()
        {
            contentBaseScale = contentRoot != null
                ? SanitizeVisibleContentScale(contentRoot.localScale)
                : Vector3.one;
        }

        private void EnsureVisibleContentScale()
        {
            contentBaseScale = SanitizeVisibleContentScale(contentBaseScale);
        }

        private static Vector3 SanitizeVisibleContentScale(Vector3 scale)
        {
            return Mathf.Abs(scale.x) < 0.05f || Mathf.Abs(scale.y) < 0.05f
                ? Vector3.one
                : scale;
        }

        private void SetRootActive(bool active)
        {
            if (panelRoot == null || panelRoot == gameObject)
            {
                return;
            }

            if (panelRoot.activeSelf != active)
            {
                panelRoot.SetActive(active);
            }
        }
    }
}
