using System;
using GameMain.GameLogic.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Runtime-built portfolio entry scene. It only routes to existing scenes and opens display panels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenuScene";
        private const string CharacterSelectSceneName = "CharacterSelectScene";
        private const string MenuCanvasName = "MainMenuCanvas";

        private static bool sceneCallbacksRegistered;
        private bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            sceneCallbacksRegistered = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallbacks()
        {
            if (sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneCallbacksRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            EnsureMainMenu(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureMainMenu(scene);
        }

        private static void EnsureMainMenu(Scene scene)
        {
            if (!scene.IsValid() || !string.Equals(scene.name, MainMenuSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (FindObjectOfType<MainMenuController>() != null)
            {
                return;
            }

            var root = new GameObject("MainMenuController");
            root.AddComponent<MainMenuController>();
        }

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (built)
            {
                return;
            }

            built = true;
            PlayerProfileService.Load();
            SettingsPanel.ApplySavedSettings();
            EnsureMenuCamera();
            MetaUiFactory.EnsureEventSystem();

            var canvas = MetaUiFactory.EnsureCanvas(MenuCanvasName, 10);
            var background = MetaUiFactory.CreatePanel(
                canvas.transform,
                "Background",
                Vector2.zero,
                Vector2.zero,
                new Color(0.028f, 0.038f, 0.055f, 1f));
            MetaUiFactory.StretchToParent(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            var hotZone = MetaUiFactory.CreatePanel(
                canvas.transform,
                "StartHotZone",
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 1f, 1f, 0f));
            MetaUiFactory.StretchToParent(hotZone.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            var hotZoneImage = MetaUiFactory.GetOrAddComponent<Image>(hotZone);
            hotZoneImage.raycastTarget = true;
            var hotZoneButton = MetaUiFactory.GetOrAddComponent<Button>(hotZone);
            hotZoneButton.targetGraphic = hotZoneImage;
            hotZoneButton.transition = Selectable.Transition.None;
            hotZoneButton.onClick.RemoveListener(StartGame);
            hotZoneButton.onClick.AddListener(StartGame);

            var vignette = MetaUiFactory.CreatePanel(
                canvas.transform,
                "BackgroundVignette",
                new Vector2(0f, -30f),
                new Vector2(1560f, 820f),
                new Color(0.055f, 0.08f, 0.12f, 0.72f));

            for (var i = 0; i < 9; i++)
            {
                var tile = MetaUiFactory.CreatePanel(
                    vignette.transform,
                    "FloorTile_" + i,
                    new Vector2((i % 3 - 1) * 380f, (i / 3 - 1) * 190f),
                    new Vector2(330f, 150f),
                    i % 2 == 0 ? new Color(0.08f, 0.115f, 0.16f, 0.58f) : new Color(0.045f, 0.07f, 0.105f, 0.58f));
                MetaUiFactory.GetOrAddComponent<Image>(tile).raycastTarget = false;
            }

            var sideBar = MetaUiFactory.CreatePanel(
                canvas.transform,
                "LeftNavigation",
                new Vector2(-760f, 0f),
                new Vector2(260f, 720f),
                new Color(0.045f, 0.07f, 0.1f, 0.94f));
            MetaUiFactory.CreateText(sideBar.transform, "NavTitle", "DEMO", new Vector2(0f, 292f), new Vector2(210f, 42f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);

            var settingsPanel = SettingsPanel.Create(canvas.transform);
            var helpPanel = DialogPanel.Create(canvas.transform);

            var navStart = MetaUiFactory.CreateButton(sideBar.transform, "NavStartButton", "开始", new Vector2(0f, 176f), new Vector2(190f, 54f));
            navStart.onClick.RemoveListener(StartGame);
            navStart.onClick.AddListener(StartGame);

            var navSettings = MetaUiFactory.CreateButton(sideBar.transform, "NavSettingsButton", "设置", new Vector2(0f, 94f), new Vector2(190f, 54f));
            navSettings.onClick.RemoveAllListeners();
            navSettings.onClick.AddListener(settingsPanel.Show);

            var navHelp = MetaUiFactory.CreateButton(sideBar.transform, "NavHelpButton", "帮助", new Vector2(0f, 12f), new Vector2(190f, 54f));
            navHelp.onClick.RemoveAllListeners();
            navHelp.onClick.AddListener(() => helpPanel.ShowDialog(
                "帮助",
                new[]
                {
                    "从主菜单进入选角大厅，确认角色后走向传送门。",
                    "战斗中使用 WASD 移动，Space 闪避，F 技能，Q 切枪。",
                    "本项目展示客户端 UI、角色选择、Lua 配置和双关卡战斗切片。",
                }));

            MetaUiFactory.CreateText(sideBar.transform, "ProfileSummary", BuildProfileSummary(), new Vector2(0f, -258f), new Vector2(220f, 96f), 18, TextAnchor.MiddleCenter, FontStyle.Normal);

            var titleBand = MetaUiFactory.CreatePanel(
                canvas.transform,
                "TitleBand",
                new Vector2(120f, 160f),
                new Vector2(980f, 340f),
                new Color(0.035f, 0.055f, 0.08f, 0.68f));
            MetaUiFactory.CreateText(titleBand.transform, "Title", "TopDown Shooter Demo", new Vector2(0f, 84f), new Vector2(860f, 82f), 54, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaUiFactory.CreateText(titleBand.transform, "SubTitle", "Unity 2D Vertical Slice", new Vector2(0f, 24f), new Vector2(760f, 42f), 26, TextAnchor.MiddleCenter, FontStyle.Normal);
            MetaUiFactory.CreateText(titleBand.transform, "Summary", "像素动作射击大厅 / 角色选择 / 双关卡战斗 Demo", new Vector2(0f, -38f), new Vector2(860f, 42f), 22, TextAnchor.MiddleCenter, FontStyle.Normal);

            var startButton = MetaUiFactory.CreateButton(canvas.transform, "StartButton", "开始游戏", new Vector2(120f, -226f), new Vector2(360f, 70f));
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);

            var quitButton = MetaUiFactory.CreateButton(sideBar.transform, "QuitButton", "退出", new Vector2(0f, -70f), new Vector2(190f, 48f));
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);

            MetaUiFactory.CreateText(
                canvas.transform,
                "Footer",
                "操作：WASD 移动 / Space 闪避 / F 技能 / Q 切枪 / E 交互 / I 信息面板",
                new Vector2(120f, -454f),
                new Vector2(1040f, 42f),
                18,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);

            MetaUiFactory.CreateText(
                canvas.transform,
                "StartHint",
                "点击开始游戏，或点击主界面空白区域进入选角大厅",
                new Vector2(120f, -302f),
                new Vector2(620f, 36f),
                18,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
        }

        private static string BuildProfileSummary()
        {
            var profile = PlayerProfileService.Current;
            return "玩家：" + profile.PlayerName + "\n蓝币：" + profile.Coin;
        }

        private static void StartGame()
        {
            SceneManager.LoadScene(CharacterSelectSceneName);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("MainMenu Quit clicked. Application.Quit is skipped in Editor.");
#else
            Application.Quit();
#endif
        }

        private static void EnsureMenuCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();
        }
    }

    internal static class MetaUiFactory
    {
        private static Font cachedFont;

        public static Canvas EnsureCanvas(string name, int sortingOrder)
        {
            var existing = GameObject.Find(name);
            var canvasObject = existing != null ? existing : new GameObject(name, typeof(RectTransform));
            var canvas = GetOrAddComponent<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            GetOrAddComponent<GraphicRaycaster>(canvasObject);

            var scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        public static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var panel = FindOrCreateUiChild(parent, name);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = GetOrAddComponent<Image>(panel);
            image.color = color;
            image.raycastTarget = true;
            return panel;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            FontStyle style)
        {
            var textObject = FindOrCreateUiChild(parent, name);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = GetOrAddComponent<Text>(textObject);
            text.font = GetFont();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = style;
            text.color = new Color(0.92f, 0.97f, 1f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var buttonObject = FindOrCreateUiChild(parent, name);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = GetOrAddComponent<Image>(buttonObject);
            image.color = new Color(0.18f, 0.38f, 0.52f, 0.96f);

            var button = GetOrAddComponent<Button>(buttonObject);
            button.targetGraphic = image;

            var labelText = CreateText(buttonObject.transform, "Label", label, Vector2.zero, size, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            labelText.raycastTarget = false;
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var sliderObject = FindOrCreateUiChild(parent, name);
            var rect = sliderObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var background = FindOrCreateUiChild(sliderObject.transform, "Background");
            StretchToParent(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            GetOrAddComponent<Image>(background).color = new Color(0.08f, 0.12f, 0.16f, 1f);

            var fillArea = FindOrCreateUiChild(sliderObject.transform, "Fill Area");
            StretchToParent(fillArea.GetComponent<RectTransform>(), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var fill = FindOrCreateUiChild(fillArea.transform, "Fill");
            StretchToParent(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            var fillImage = GetOrAddComponent<Image>(fill);
            fillImage.color = new Color(0.3f, 0.72f, 0.92f, 1f);

            var handle = FindOrCreateUiChild(sliderObject.transform, "Handle");
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 32f);
            var handleImage = GetOrAddComponent<Image>(handle);
            handleImage.color = new Color(0.94f, 0.98f, 1f, 1f);

            var slider = GetOrAddComponent<Slider>(sliderObject);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var toggleObject = FindOrCreateUiChild(parent, name);
            var rect = toggleObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var box = FindOrCreateUiChild(toggleObject.transform, "Box");
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(30f, 30f);
            var boxImage = GetOrAddComponent<Image>(box);
            boxImage.color = new Color(0.08f, 0.12f, 0.16f, 1f);

            var check = FindOrCreateUiChild(box.transform, "Checkmark");
            StretchToParent(check.GetComponent<RectTransform>(), new Vector2(6f, 6f), new Vector2(-6f, -6f));
            var checkImage = GetOrAddComponent<Image>(check);
            checkImage.color = new Color(0.38f, 0.86f, 0.72f, 1f);

            var labelText = CreateText(toggleObject.transform, "Label", label, new Vector2(36f, 0f), new Vector2(size.x - 40f, size.y), 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            labelText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            labelText.rectTransform.pivot = new Vector2(0f, 0.5f);

            var toggle = GetOrAddComponent<Toggle>(toggleObject);
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            return toggle;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var imageObject = FindOrCreateUiChild(parent, name);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = GetOrAddComponent<Image>(imageObject);
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            return image;
        }

        public static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static Sprite LoadSprite(string resourcesPath)
        {
            return string.IsNullOrWhiteSpace(resourcesPath) ? null : Resources.Load<Sprite>(resourcesPath.Trim());
        }

        public static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        public static GameObject FindOrCreateUiChild(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) : null;
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                child.transform.SetParent(parent, false);
            }

            return child;
        }

        public static Font GetFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
            }

            return cachedFont;
        }
    }
}

