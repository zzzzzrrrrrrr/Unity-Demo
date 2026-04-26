using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Display-only NPC dialogue panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogPanel : BasePanel
    {
        [SerializeField] private Text npcNameText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button closeButton;

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public static DialogPanel Create(Transform parent)
        {
            var panel = MetaUiFactory.CreatePanel(parent, "DialogPanel", new Vector2(0f, -250f), new Vector2(820f, 230f), new Color(0.035f, 0.055f, 0.08f, 0.97f));
            panel.transform.SetAsLastSibling();
            var canvasGroup = MetaUiFactory.GetOrAddComponent<CanvasGroup>(panel);
            var controller = MetaUiFactory.GetOrAddComponent<DialogPanel>(panel);
            controller.BindPanelRoot(panel, canvasGroup);
            controller.BuildView(panel.transform);
            controller.Hide();
            return controller;
        }

        public void ShowDialog(string npcName, string[] lines)
        {
            transform.SetAsLastSibling();
            if (npcNameText != null)
            {
                npcNameText.text = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim();
            }

            if (bodyText != null)
            {
                bodyText.text = lines == null || lines.Length == 0
                    ? string.Empty
                    : string.Join("\n", lines);
            }

            Show();
            BindEvents();
        }

        private void BuildView(Transform root)
        {
            npcNameText = MetaUiFactory.CreateText(root, "NpcNameText", "NPC", new Vector2(-330f, 72f), new Vector2(130f, 42f), 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            bodyText = MetaUiFactory.CreateText(root, "BodyText", string.Empty, new Vector2(18f, 18f), new Vector2(700f, 112f), 22, TextAnchor.UpperLeft, FontStyle.Normal);
            closeButton = MetaUiFactory.CreateButton(root, "CloseButton", "关闭", new Vector2(330f, -72f), new Vector2(140f, 42f));
            BindEvents();
        }

        private void BindEvents()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void UnbindEvents()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }
    }
}
