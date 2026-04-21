// Path: Assets/_Scripts/UI/DemoRewardPopupPanel.cs
// Minimal reward popup panel for chest/item feedback.
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    [DisallowMultipleComponent]
    public class DemoRewardPopupPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button closeButton;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private KeyCode closeHotkey = KeyCode.Q;

        private static DemoRewardPopupPanel instance;
        private static int lastHotkeyCloseFrame = -1;
        private bool hasAwakened;
        private bool suppressHideOnAwakeOnce;
        private int lastShownFrame = -1;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public static bool WasHotkeyCloseTriggeredThisFrame => lastHotkeyCloseFrame == Time.frameCount;
        public static DemoRewardPopupPanel Instance
        {
            get
            {
                if (instance == null)
                {
                    DemoRewardPopupPanel[] all = FindObjectsOfType<DemoRewardPopupPanel>(true);
                    if (all != null && all.Length > 0)
                    {
                        instance = all[0];
                    }
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            closeHotkey = KeyCode.Q;
            ResolveReferencesIfMissing();
            EnsureCloseButtonBinding();

            if (hideOnAwake && !suppressHideOnAwakeOnce)
            {
                Hide();
            }

            hasAwakened = true;
            suppressHideOnAwakeOnce = false;
        }

        private void ResolveReferencesIfMissing()
        {
            if (titleText == null)
            {
                Transform t = transform.Find("RewardTitle");
                if (t != null)
                {
                    titleText = t.GetComponent<Text>();
                }
            }

            if (messageText == null)
            {
                Transform t = transform.Find("RewardMessage");
                if (t != null)
                {
                    messageText = t.GetComponent<Text>();
                }
            }

            if (closeButton == null)
            {
                Transform t = transform.Find("RewardCloseButton");
                if (t != null)
                {
                    closeButton = t.GetComponent<Button>();
                }
            }
        }

        private void OnEnable()
        {
            ResolveReferencesIfMissing();
            EnsureCloseButtonBinding();
        }

        private void Update()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            if (Time.frameCount == lastShownFrame)
            {
                return;
            }

            // Reward popup uses Q to close so R remains dedicated to inventory toggle.
            if (!InputCompat.IsDown(closeHotkey))
            {
                return;
            }

            lastHotkeyCloseFrame = Time.frameCount;
            Hide();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void ConfigureRuntime(GameObject root, Text title, Text message, Button close)
        {
            panelRoot = root != null ? root : gameObject;
            titleText = title;
            messageText = message;
            closeButton = close;
            EnsureCloseButtonBinding();
        }

        public void Show(string title, string message)
        {
            ResolveReferencesIfMissing();
            EnsureCloseButtonBinding();

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelRoot != null)
            {
                if (!panelRoot.activeSelf && !hasAwakened)
                {
                    suppressHideOnAwakeOnce = true;
                }

                panelRoot.SetActive(true);
                lastShownFrame = Time.frameCount;
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(title) ? "Reward" : title;
            }

            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void EnsureCloseButtonBinding()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.interactable = true;
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }
}
