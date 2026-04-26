using GameMain.GameLogic.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// PlayerPrefs-backed settings panel. Audio page is functional; other pages are display-only portfolio sections.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanel : BasePanel
    {
        public const string MasterVolumeKey = "masterVolume";
        public const string MusicVolumeKey = "musicVolume";
        public const string SfxVolumeKey = "sfxVolume";
        public const string MutedKey = "muted";

        private enum SettingsCategory
        {
            Controls,
            Audio,
            Video,
            Language,
            Credits
        }

        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle mutedToggle;
        [SerializeField] private Text masterValueText;
        [SerializeField] private Text musicValueText;
        [SerializeField] private Text sfxValueText;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject controlsPage;
        [SerializeField] private GameObject audioPage;
        [SerializeField] private GameObject videoPage;
        [SerializeField] private GameObject languagePage;
        [SerializeField] private GameObject creditsPage;

        private bool suppressEvents;

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        private void OnEnable()
        {
            LoadFromPrefs();
            ShowCategory(SettingsCategory.Audio);
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public static SettingsPanel Create(Transform parent)
        {
            var panel = MetaUiFactory.CreatePanel(parent, "SettingsPanel", Vector2.zero, new Vector2(980f, 650f), new Color(0.035f, 0.055f, 0.08f, 0.98f));
            panel.transform.SetAsLastSibling();
            var canvasGroup = MetaUiFactory.GetOrAddComponent<CanvasGroup>(panel);
            var controller = MetaUiFactory.GetOrAddComponent<SettingsPanel>(panel);
            controller.BindPanelRoot(panel, canvasGroup);
            controller.BuildView(panel.transform);
            controller.LoadFromPrefs();
            controller.ShowCategory(SettingsCategory.Audio);
            controller.Hide();
            return controller;
        }

        public static void ApplySavedSettings()
        {
            var master = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            var muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            AudioListener.volume = muted ? 0f : Mathf.Clamp01(master);
        }

        public override void Show()
        {
            transform.SetAsLastSibling();
            base.Show();
        }

        private void BuildView(Transform root)
        {
            MetaUiFactory.CreateText(root, "TitleText", "设置", new Vector2(0f, 286f), new Vector2(880f, 44f), 32, TextAnchor.MiddleCenter, FontStyle.Bold);

            var nav = MetaUiFactory.CreatePanel(root, "CategoryBar", new Vector2(-355f, -10f), new Vector2(230f, 520f), new Color(0.055f, 0.085f, 0.12f, 0.96f));
            CreateCategoryButton(nav.transform, "ControlsButton", "键盘/鼠标", new Vector2(0f, 188f), SettingsCategory.Controls);
            CreateCategoryButton(nav.transform, "AudioButton", "声音", new Vector2(0f, 112f), SettingsCategory.Audio);
            CreateCategoryButton(nav.transform, "VideoButton", "视频", new Vector2(0f, 36f), SettingsCategory.Video);
            CreateCategoryButton(nav.transform, "LanguageButton", "语言", new Vector2(0f, -40f), SettingsCategory.Language);
            CreateCategoryButton(nav.transform, "CreditsButton", "制作名单", new Vector2(0f, -116f), SettingsCategory.Credits);

            var content = MetaUiFactory.CreatePanel(root, "ContentArea", new Vector2(110f, -10f), new Vector2(660f, 520f), new Color(0.045f, 0.07f, 0.105f, 0.96f));
            controlsPage = CreatePage(content.transform, "ControlsPage");
            audioPage = CreatePage(content.transform, "AudioPage");
            videoPage = CreatePage(content.transform, "VideoPage");
            languagePage = CreatePage(content.transform, "LanguagePage");
            creditsPage = CreatePage(content.transform, "CreditsPage");

            BuildControlsPage(controlsPage.transform);
            BuildAudioPage(audioPage.transform);
            BuildVideoPage(videoPage.transform);
            BuildLanguagePage(languagePage.transform);
            BuildCreditsPage(creditsPage.transform);

            closeButton = MetaUiFactory.CreateButton(root, "CloseButton", "关闭", new Vector2(390f, -282f), new Vector2(150f, 44f));
            BindEvents();
        }

        private static GameObject CreatePage(Transform parent, string name)
        {
            var page = MetaUiFactory.FindOrCreateUiChild(parent, name);
            MetaUiFactory.StretchToParent(page.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            return page;
        }

        private void CreateCategoryButton(Transform parent, string name, string label, Vector2 position, SettingsCategory category)
        {
            var button = MetaUiFactory.CreateButton(parent, name, label, position, new Vector2(178f, 48f));
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowCategory(category));
        }

        private static void BuildControlsPage(Transform root)
        {
            MetaUiFactory.CreateText(root, "PageTitle", "键盘 / 鼠标", new Vector2(0f, 198f), new Vector2(560f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaUiFactory.CreateText(
                root,
                "ControlsText",
                "WASD：移动\nSpace：闪避 / 翻滚\nF：角色技能\nQ：切换武器\nE：交互 / 拾取 / 传送门\n鼠标左键：射击\nI：战斗信息面板\nL：Lua Reload",
                new Vector2(0f, 20f),
                new Vector2(520f, 300f),
                23,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
        }

        private void BuildAudioPage(Transform root)
        {
            MetaUiFactory.CreateText(root, "PageTitle", "声音", new Vector2(0f, 198f), new Vector2(560f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            mutedToggle = MetaUiFactory.CreateToggle(root, "MutedToggle", "静音", new Vector2(-150f, 128f), new Vector2(260f, 42f));
            masterSlider = EnsureSliderRow(root, "Master", "主音量", new Vector2(0f, 58f), out masterValueText);
            musicSlider = EnsureSliderRow(root, "Music", "音乐音量", new Vector2(0f, -22f), out musicValueText);
            sfxSlider = EnsureSliderRow(root, "Sfx", "音效音量", new Vector2(0f, -102f), out sfxValueText);
            MetaUiFactory.CreateText(root, "AudioNote", "当前项目未引入 AudioMixer：主音量和静音会立即作用于 AudioListener，音乐/音效音量先保存为本地设置。", new Vector2(0f, -186f), new Vector2(560f, 74f), 18, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private static void BuildVideoPage(Transform root)
        {
            MetaUiFactory.CreateText(root, "PageTitle", "视频", new Vector2(0f, 198f), new Vector2(560f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaUiFactory.CreateText(root, "VideoText", "当前演示使用 Unity Game View / Build 分辨率。\n窗口模式、分辨率切换和画质档位作为后续正式项目扩展入口保留。", new Vector2(0f, 40f), new Vector2(560f, 180f), 22, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private static void BuildLanguagePage(Transform root)
        {
            MetaUiFactory.CreateText(root, "PageTitle", "语言", new Vector2(0f, 198f), new Vector2(560f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaUiFactory.CreateText(root, "LanguageText", "当前语言：中文\nUI 文案集中在展示层脚本，便于后续替换为配置化文本。", new Vector2(0f, 40f), new Vector2(560f, 180f), 22, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private static void BuildCreditsPage(Transform root)
        {
            MetaUiFactory.CreateText(root, "PageTitle", "制作名单", new Vector2(0f, 198f), new Vector2(560f, 40f), 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaUiFactory.CreateText(
                root,
                "CreditsText",
                "项目：TopDown Shooter Demo\n引擎：Unity / Tuanjie\n方向：客户端 / Gameplay / UI / Lua 配置热更新演示\n本地玩家：" + PlayerProfileService.Current.PlayerName,
                new Vector2(0f, 28f),
                new Vector2(560f, 220f),
                22,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
        }

        private static Slider EnsureSliderRow(Transform root, string id, string label, Vector2 position, out Text valueText)
        {
            MetaUiFactory.CreateText(root, id + "Label", label, new Vector2(-210f, position.y), new Vector2(120f, 32f), 20, TextAnchor.MiddleRight, FontStyle.Normal);
            var slider = MetaUiFactory.CreateSlider(root, id + "Slider", new Vector2(20f, position.y), new Vector2(320f, 30f));
            valueText = MetaUiFactory.CreateText(root, id + "Value", "100%", new Vector2(250f, position.y), new Vector2(90f, 32f), 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            return slider;
        }

        private void ShowCategory(SettingsCategory category)
        {
            SetPageActive(controlsPage, category == SettingsCategory.Controls);
            SetPageActive(audioPage, category == SettingsCategory.Audio);
            SetPageActive(videoPage, category == SettingsCategory.Video);
            SetPageActive(languagePage, category == SettingsCategory.Language);
            SetPageActive(creditsPage, category == SettingsCategory.Credits);
        }

        private static void SetPageActive(GameObject page, bool active)
        {
            if (page != null)
            {
                page.SetActive(active);
            }
        }

        private void LoadFromPrefs()
        {
            suppressEvents = true;
            SetSliderValue(masterSlider, PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            SetSliderValue(musicSlider, PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
            SetSliderValue(sfxSlider, PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            if (mutedToggle != null)
            {
                mutedToggle.isOn = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            }

            suppressEvents = false;
            RefreshLabels();
            ApplySavedSettings();
        }

        private void SaveToPrefs()
        {
            if (suppressEvents)
            {
                return;
            }

            PlayerPrefs.SetFloat(MasterVolumeKey, masterSlider != null ? masterSlider.value : 1f);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicSlider != null ? musicSlider.value : 1f);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxSlider != null ? sfxSlider.value : 1f);
            PlayerPrefs.SetInt(MutedKey, mutedToggle != null && mutedToggle.isOn ? 1 : 0);
            PlayerPrefs.Save();
            RefreshLabels();
            ApplySavedSettings();
        }

        private void RefreshLabels()
        {
            SetValueText(masterValueText, masterSlider);
            SetValueText(musicValueText, musicSlider);
            SetValueText(sfxValueText, sfxSlider);
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.value = Mathf.Clamp01(value);
            }
        }

        private static void SetValueText(Text label, Slider slider)
        {
            if (label != null)
            {
                label.text = Mathf.RoundToInt((slider != null ? slider.value : 1f) * 100f) + "%";
            }
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (mutedToggle != null)
            {
                mutedToggle.onValueChanged.AddListener(OnToggleChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void UnbindEvents()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveListener(OnSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(OnSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSliderChanged);
            }

            if (mutedToggle != null)
            {
                mutedToggle.onValueChanged.RemoveListener(OnToggleChanged);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        private void OnSliderChanged(float value)
        {
            SaveToPrefs();
        }

        private void OnToggleChanged(bool value)
        {
            SaveToPrefs();
        }
    }
}
