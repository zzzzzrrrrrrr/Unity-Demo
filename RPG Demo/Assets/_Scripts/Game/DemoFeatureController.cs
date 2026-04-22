// Path: Assets/_Scripts/Game/DemoFeatureController.cs
// Runtime feature bootstrap for demo-level reusable UI/chest/inventory chain.
using System.Collections.Generic;
using ARPGDemo.UI;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    public class DemoFeatureController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string firstGameplaySceneName = "SampleScene";

        [Header("Runtime Features")]
        [SerializeField] private bool enablePauseAndSettings = true;
        [SerializeField] private bool enableChestRewardAndInventory = true;
        [SerializeField] private bool createChestInventoryAtRuntime = false;
        [SerializeField] private bool verboseLog = false;

        [Header("Inventory")]
        [SerializeField] private KeyCode inventoryToggleKey = KeyCode.R;
        [SerializeField] private int inventorySlotCount = 8;

        [Header("Demo Chest")]
        [SerializeField] private Vector3 chestWorldPosition = new Vector3(1.8f, -0.6f, 0f);
        [SerializeField] private Vector2 chestColliderSize = new Vector2(1f, 1f);

        private DemoInventoryService inventoryService;
        private DemoInventoryPanel staticInventoryPanel;
        private static Font cachedBuiltinFont;

        private void Awake()
        {
            // Keep demo hotkeys stable even if older scene serialization has stale values.
            inventoryToggleKey = KeyCode.R;
            enableChestRewardAndInventory = true;

            inventoryService = GetComponent<DemoInventoryService>();
            if (inventoryService == null)
            {
                inventoryService = gameObject.AddComponent<DemoInventoryService>();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private void Update()
        {
            TryToggleStaticInventoryByHotkey();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            if (IsMainMenuScene(scene))
            {
                staticInventoryPanel = null;
                return;
            }

            Canvas canvas = FindCanvasInScene(scene);
            if (canvas == null)
            {
                canvas = CreateDefaultCanvas(scene);
            }

            EnsureEventSystem(scene);

            if (enablePauseAndSettings && canvas != null)
            {
                EnsurePauseAndSettingsUi(canvas);
            }

            if (enableChestRewardAndInventory && canvas != null)
            {
                BindExistingChestAndInventory(scene, canvas);
            }
        }

        private void BindExistingChestAndInventory(Scene scene, Canvas canvas)
        {
            if (createChestInventoryAtRuntime)
            {
                EnsureRewardPopupUi(canvas);
                EnsureInventoryUi(canvas);

                if (IsFirstGameplayScene(scene))
                {
                    EnsureDemoChest(scene);
                }

                return;
            }

            Transform canvasRoot = canvas != null ? canvas.transform : null;
            if (canvasRoot == null)
            {
                return;
            }

            Transform popup = canvasRoot.Find("RewardPopupPanel");
            Transform inventoryPanel = canvasRoot.Find("InventoryPanel");
            Transform toggleButton = canvasRoot.Find("InventoryToggleButton");

            if (popup == null && verboseLog)
            {
                Debug.LogWarning("[DemoFeatureController] Missing static RewardPopupPanel under Canvas.");
            }

            DemoInventoryPanel panel = inventoryPanel != null ? inventoryPanel.GetComponent<DemoInventoryPanel>() : null;
            staticInventoryPanel = panel;
            staticInventoryPanel?.SetSelfHotkeyEnabled(false);
            if (panel == null && verboseLog)
            {
                Debug.LogWarning("[DemoFeatureController] Missing static InventoryPanel with DemoInventoryPanel.");
            }

            if (toggleButton != null && panel != null)
            {
                Button button = toggleButton.GetComponent<Button>();
                if (button != null)
                {
                    BindButton(button, panel.TogglePanel);
                }
            }
            else if (verboseLog)
            {
                Debug.LogWarning("[DemoFeatureController] Missing static InventoryToggleButton or target panel.");
            }

            if (IsFirstGameplayScene(scene))
            {
                GameObject chest = FindObjectInScene(scene, "DemoChest_01");
                if (chest == null && verboseLog)
                {
                    Debug.LogWarning("[DemoFeatureController] Missing static DemoChest_01 in SampleScene.");
                }
            }
        }

        private void EnsurePauseAndSettingsUi(Canvas canvas)
        {
            GameObject pausePanel = EnsurePanel(canvas.transform, "PausePanel", new Vector2(500f, 320f), new Color(0f, 0f, 0f, 0.68f));
            GameObject settingsPanel = EnsurePanel(canvas.transform, "SettingsPanel", new Vector2(540f, 380f), new Color(0f, 0f, 0f, 0.75f));
            settingsPanel.SetActive(false);

            SettingsUIController settings = settingsPanel.GetComponent<SettingsUIController>();
            if (settings == null)
            {
                settings = settingsPanel.AddComponent<SettingsUIController>();
            }

            Text settingsTitle = EnsureLabel(settingsPanel.transform, "SettingsTitle", "Settings", new Vector2(0f, 150f), 34, TextAnchor.MiddleCenter);

            Slider masterSlider = EnsureSlider(settingsPanel.transform, "MasterVolumeSlider", new Vector2(0f, 78f), new Vector2(320f, 22f));
            Text masterLabel = EnsureLabel(settingsPanel.transform, "MasterVolumeLabel", "Master Volume", new Vector2(-185f, 78f), 22, TextAnchor.MiddleLeft);
            Toggle fullscreenToggle = EnsureToggle(settingsPanel.transform, "FullscreenToggle", "Fullscreen", new Vector2(0f, 28f));
            Button applyButton = EnsureButton(settingsPanel.transform, "ApplyButton", "Apply", new Vector2(-90f, -132f), new Vector2(140f, 42f));
            Button closeSettingsButton = EnsureButton(settingsPanel.transform, "CloseSettingsButton", "Close", new Vector2(90f, -132f), new Vector2(140f, 42f));

            settings.ConfigureRuntime(
                settingsPanel,
                masterSlider,
                masterLabel,
                fullscreenToggle,
                applyButton,
                closeSettingsButton);

            PauseUIController pause = pausePanel.GetComponent<PauseUIController>();
            if (pause == null)
            {
                pause = pausePanel.AddComponent<PauseUIController>();
            }
            pause.ConfigureRuntime(pausePanel, settings);

            EnsureLabel(pausePanel.transform, "PauseTitle", "Paused", new Vector2(0f, 120f), 34, TextAnchor.MiddleCenter);
            Button resumeButton = EnsureButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0f, 58f), new Vector2(220f, 44f));
            Button openSettingsButton = EnsureButton(pausePanel.transform, "OpenSettingsButton", "Settings", new Vector2(0f, 2f), new Vector2(220f, 44f));
            Button backMenuButton = EnsureButton(pausePanel.transform, "BackToMenuButton", "Main Menu", new Vector2(0f, -54f), new Vector2(220f, 44f));

            BindButton(resumeButton, pause.OnClickResume);
            BindButton(openSettingsButton, pause.OnClickOpenSettings);
            BindButton(backMenuButton, pause.OnClickBackToMenu);
        }

        private void EnsureRewardPopupUi(Canvas canvas)
        {
            GameObject popupPanel = EnsurePanel(canvas.transform, "RewardPopupPanel", new Vector2(480f, 240f), new Color(0f, 0f, 0f, 0.8f));
            popupPanel.SetActive(false);

            DemoRewardPopupPanel popup = popupPanel.GetComponent<DemoRewardPopupPanel>();
            if (popup == null)
            {
                popup = popupPanel.AddComponent<DemoRewardPopupPanel>();
            }

            Text title = EnsureLabel(popupPanel.transform, "RewardTitle", "Reward", new Vector2(0f, 76f), 30, TextAnchor.MiddleCenter);
            Text message = EnsureLabel(popupPanel.transform, "RewardMessage", "", new Vector2(0f, 20f), 24, TextAnchor.MiddleCenter);
            Button close = EnsureButton(popupPanel.transform, "RewardCloseButton", "OK", new Vector2(0f, -78f), new Vector2(140f, 42f));
            popup.ConfigureRuntime(popupPanel, title, message, close);
        }

        private void EnsureInventoryUi(Canvas canvas)
        {
            GameObject panel = EnsurePanel(canvas.transform, "InventoryPanel", new Vector2(520f, 340f), new Color(0f, 0f, 0f, 0.74f));
            panel.SetActive(false);
            EnsureLabel(panel.transform, "InventoryTitle", "Inventory (R)", new Vector2(0f, 138f), 30, TextAnchor.MiddleCenter);

            GameObject slotsRoot = EnsureRectObject(panel.transform, "SlotsRoot");
            RectTransform slotsRect = slotsRoot.GetComponent<RectTransform>();
            ConfigureRect(slotsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 230f), new Vector2(0f, -8f));

            List<Text> slotTexts = new List<Text>(Mathf.Max(4, inventorySlotCount));
            int safeSlots = Mathf.Clamp(inventorySlotCount, 4, 12);
            for (int i = 0; i < safeSlots; i++)
            {
                float y = 90f - i * 28f;
                Text slot = EnsureLabel(slotsRoot.transform, $"Slot_{i + 1:00}", $"{i + 1}. (empty)", new Vector2(-150f, y), 22, TextAnchor.MiddleLeft);
                slotTexts.Add(slot);
            }

            DemoInventoryPanel inventoryPanel = panel.GetComponent<DemoInventoryPanel>();
            if (inventoryPanel == null)
            {
                inventoryPanel = panel.AddComponent<DemoInventoryPanel>();
            }

            inventoryPanel.ConfigureRuntime(panel, slotsRoot.transform, slotTexts, inventoryService, inventoryToggleKey, safeSlots);
            staticInventoryPanel = inventoryPanel;
            staticInventoryPanel?.SetSelfHotkeyEnabled(false);
            EnsureInventoryToggleButton(canvas.transform, inventoryPanel);
        }

        private void TryToggleStaticInventoryByHotkey()
        {
            TryResolveStaticInventoryPanel();

            if (staticInventoryPanel == null)
            {
                return;
            }

            if (DemoRewardPopupPanel.WasHotkeyCloseTriggeredThisFrame)
            {
                return;
            }

            DemoRewardPopupPanel popup = DemoRewardPopupPanel.Instance;
            if (popup != null && popup.IsOpen)
            {
                return;
            }

            if (!InputCompat.IsDown(inventoryToggleKey))
            {
                return;
            }

            GameObject panel = staticInventoryPanel.gameObject;
            if (panel == null)
            {
                return;
            }

            staticInventoryPanel.TogglePanel();
        }

        private void TryResolveStaticInventoryPanel()
        {
            if (staticInventoryPanel != null)
            {
                return;
            }

            // First try component lookup (supports inactive panels).
            DemoInventoryPanel[] allPanels = FindObjectsOfType<DemoInventoryPanel>(true);
            if (allPanels != null && allPanels.Length > 0)
            {
                staticInventoryPanel = allPanels[0];
                staticInventoryPanel.SetSelfHotkeyEnabled(false);
                return;
            }

            // Fallback by name to recover from missing component references in old scene data.
            GameObject panel = GameObject.Find("InventoryPanel");
            if (panel == null)
            {
                return;
            }

            staticInventoryPanel = panel.GetComponent<DemoInventoryPanel>();
            if (staticInventoryPanel == null)
            {
                staticInventoryPanel = panel.AddComponent<DemoInventoryPanel>();
            }

            staticInventoryPanel.SetSelfHotkeyEnabled(false);
        }

        private void EnsureInventoryToggleButton(Transform canvasRoot, DemoInventoryPanel inventoryPanel)
        {
            if (canvasRoot == null || inventoryPanel == null)
            {
                return;
            }

            Button button = EnsureButton(
                canvasRoot,
                "InventoryToggleButton",
                "背包(R)",
                Vector2.zero,
                new Vector2(150f, 44f));

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(150f, 44f);
                rect.anchoredPosition = new Vector2(-24f, -20f);
            }

            button.transform.SetAsLastSibling();
            BindButton(button, inventoryPanel.TogglePanel);
        }

        private void EnsureDemoChest(Scene scene)
        {
            GameObject chest = FindObjectInScene(scene, "DemoChest_01");
            if (chest == null)
            {
                chest = new GameObject("DemoChest_01");
                SceneManager.MoveGameObjectToScene(chest, scene);
            }

            chest.SetActive(true);
            chest.transform.position = chestWorldPosition;
            chest.transform.rotation = Quaternion.identity;
            chest.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            SpriteRenderer renderer = chest.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = chest.AddComponent<SpriteRenderer>();
            }

            if (renderer.sprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                renderer.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            BoxCollider2D col = chest.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = chest.AddComponent<BoxCollider2D>();
            }
            col.isTrigger = true;
            col.size = chestColliderSize;
            col.offset = Vector2.zero;

            DemoChestInteractable interactable = chest.GetComponent<DemoChestInteractable>();
            if (interactable == null)
            {
                interactable = chest.AddComponent<DemoChestInteractable>();
            }

            if (verboseLog)
            {
                Debug.Log("[DemoFeatureController] Chest ready in " + scene.name, this);
            }
        }

        private static Canvas FindCanvasInScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas canvas = roots[i].GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static Canvas CreateDefaultCanvas(Scene scene)
        {
            GameObject canvasGo = new GameObject("Canvas");
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<EventSystem>(true) != null)
                {
                    return;
                }
            }

            GameObject eventSystemGo = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        private static GameObject EnsurePanel(Transform canvasRoot, string name, Vector2 size, Color color)
        {
            GameObject panel = EnsureRectObject(canvasRoot, name);
            RectTransform rect = panel.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, Vector2.zero);

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }
            image.color = color;
            return panel;
        }

        private static GameObject EnsureRectObject(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Button EnsureButton(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size)
        {
            GameObject buttonGo = EnsureRectObject(parent, name);
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, anchoredPos);

            Image image = buttonGo.GetComponent<Image>();
            if (image == null)
            {
                image = buttonGo.AddComponent<Image>();
            }
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            Button button = buttonGo.GetComponent<Button>();
            if (button == null)
            {
                button = buttonGo.AddComponent<Button>();
            }

            Text label = EnsureLabel(buttonGo.transform, "Text", text, Vector2.zero, 24, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }

        private static Slider EnsureSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject sliderGo = EnsureRectObject(parent, name);
            RectTransform rect = sliderGo.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, anchoredPos);

            Slider slider = sliderGo.GetComponent<Slider>();
            if (slider == null)
            {
                slider = sliderGo.AddComponent<Slider>();
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            GameObject backgroundGo = EnsureRectObject(sliderGo.transform, "Background");
            Image bg = backgroundGo.GetComponent<Image>();
            if (bg == null)
            {
                bg = backgroundGo.AddComponent<Image>();
            }
            bg.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            RectTransform bgRect = backgroundGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.35f);
            bgRect.anchorMax = new Vector2(1f, 0.65f);
            bgRect.sizeDelta = Vector2.zero;

            GameObject fillArea = EnsureRectObject(sliderGo.transform, "Fill Area");
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRect.sizeDelta = new Vector2(-16f, 0f);
            fillAreaRect.anchoredPosition = new Vector2(-8f, 0f);

            GameObject fillGo = EnsureRectObject(fillArea.transform, "Fill");
            Image fillImage = fillGo.GetComponent<Image>();
            if (fillImage == null)
            {
                fillImage = fillGo.AddComponent<Image>();
            }
            fillImage.color = new Color(0.28f, 0.75f, 0.95f, 1f);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            GameObject handleArea = EnsureRectObject(sliderGo.transform, "Handle Slide Area");
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20f, 0f);

            GameObject handleGo = EnsureRectObject(handleArea.transform, "Handle");
            Image handleImage = handleGo.GetComponent<Image>();
            if (handleImage == null)
            {
                handleImage = handleGo.AddComponent<Image>();
            }
            handleImage.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 30f);

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static Toggle EnsureToggle(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            GameObject toggleGo = EnsureRectObject(parent, name);
            RectTransform rect = toggleGo.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 30f), anchoredPos);

            Toggle toggle = toggleGo.GetComponent<Toggle>();
            if (toggle == null)
            {
                toggle = toggleGo.AddComponent<Toggle>();
            }

            GameObject backgroundGo = EnsureRectObject(toggleGo.transform, "Background");
            Image bg = backgroundGo.GetComponent<Image>();
            if (bg == null)
            {
                bg = backgroundGo.AddComponent<Image>();
            }
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            RectTransform bgRect = backgroundGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.sizeDelta = new Vector2(24f, 24f);
            bgRect.anchoredPosition = new Vector2(12f, 0f);

            GameObject checkmarkGo = EnsureRectObject(backgroundGo.transform, "Checkmark");
            Image checkmark = checkmarkGo.GetComponent<Image>();
            if (checkmark == null)
            {
                checkmark = checkmarkGo.AddComponent<Image>();
            }
            checkmark.color = new Color(0.30f, 0.85f, 0.38f, 1f);
            RectTransform checkRect = checkmarkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(14f, 14f);
            checkRect.anchoredPosition = Vector2.zero;

            Text text = EnsureLabel(toggleGo.transform, "Label", label, new Vector2(34f, 0f), 22, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(34f, 0f);
            text.rectTransform.offsetMax = Vector2.zero;

            toggle.targetGraphic = bg;
            toggle.graphic = checkmark;
            return toggle;
        }

        private static Text EnsureLabel(Transform parent, string name, string content, Vector2 anchoredPos, int fontSize, TextAnchor anchor)
        {
            GameObject textGo = EnsureRectObject(parent, name);
            Text text = textGo.GetComponent<Text>();
            if (text == null)
            {
                text = textGo.AddComponent<Text>();
            }

            if (cachedBuiltinFont == null)
            {
                cachedBuiltinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            text.font = cachedBuiltinFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;

            RectTransform rect = textGo.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 36f), anchoredPos);
            return text;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            rect.localScale = Vector3.one;
        }

        private static GameObject FindObjectInScene(Scene scene, string objectName)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindTransformRecursive(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransformRecursive(root.GetChild(i), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private bool IsMainMenuScene(Scene scene)
        {
            return string.Equals(scene.name, mainMenuSceneName, System.StringComparison.Ordinal);
        }

        private bool IsFirstGameplayScene(Scene scene)
        {
            return string.Equals(scene.name, firstGameplaySceneName, System.StringComparison.Ordinal);
        }
    }
}

