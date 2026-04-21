// Path: Assets/_Scripts/UI/SettingsUIController.cs
// Minimal settings panel: master volume + fullscreen toggle (+ optional quality/vsync).
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    [DisallowMultipleComponent]
    public class SettingsUIController : MonoBehaviour
    {
        private const string KeyMasterVolume = "SETTINGS_MASTER_VOLUME";
        private const string KeySfxVolume = "SETTINGS_SFX_VOLUME";
        private const string KeyMusicVolume = "SETTINGS_MUSIC_VOLUME";
        private const string KeyQuality = "SETTINGS_QUALITY";
        private const string KeyVSync = "SETTINGS_VSYNC";
        private const string KeyShowFPS = "SETTINGS_SHOW_FPS";
        private const string KeyFullscreen = "SETTINGS_FULLSCREEN";

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool hideOnAwake = true;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Text masterVolumeText;

        [Header("Display")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Toggle vSyncToggle;

        [Header("Debug")]
        [SerializeField] private Toggle showFpsToggle;
        [SerializeField] private Text fpsText;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;

        private bool showFPS;
        private float fpsTimer;
        private int fpsCounter;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (hideOnAwake)
            {
                panelRoot.SetActive(false);
            }

            BindButtons();
            LoadSettings();
            ApplySettings();
            RefreshUI();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void Update()
        {
            if (!showFPS || fpsText == null)
            {
                return;
            }

            fpsCounter++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                int fps = Mathf.RoundToInt(fpsCounter / Mathf.Max(0.0001f, fpsTimer));
                fpsText.text = $"FPS: {fps}";
                fpsCounter = 0;
                fpsTimer = 0f;
            }
        }

        public void ConfigureRuntime(
            GameObject runtimeRoot,
            Slider runtimeMasterSlider,
            Text runtimeMasterLabel,
            Toggle runtimeFullscreenToggle,
            Button runtimeApplyButton,
            Button runtimeCloseButton)
        {
            panelRoot = runtimeRoot != null ? runtimeRoot : gameObject;
            masterVolumeSlider = runtimeMasterSlider;
            masterVolumeText = runtimeMasterLabel;
            fullscreenToggle = runtimeFullscreenToggle;
            applyButton = runtimeApplyButton;
            closeButton = runtimeCloseButton;

            BindButtons();
            LoadSettings();
            ApplySettings();
            RefreshUI();
        }

        public void OpenSettings()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void CloseSettings()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void ToggleSettings()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(!panelRoot.activeSelf);
            }
        }

        public void OnClickApply()
        {
            ReadFromUI();
            ApplySettings();
            SaveSettings();
            RefreshUI();
        }

        public void OnClickResetDefault()
        {
            SetDefaultValues();
            ApplySettings();
            SaveSettings();
            RefreshUI();
        }

        private void BindButtons()
        {
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(OnClickApply);
                applyButton.onClick.AddListener(OnClickApply);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSettings);
                closeButton.onClick.AddListener(CloseSettings);
            }
        }

        private void ReadFromUI()
        {
            if (masterVolumeSlider != null)
            {
                PlayerPrefs.SetFloat(KeyMasterVolume, masterVolumeSlider.value);
            }

            if (sfxVolumeSlider != null)
            {
                PlayerPrefs.SetFloat(KeySfxVolume, sfxVolumeSlider.value);
            }

            if (musicVolumeSlider != null)
            {
                PlayerPrefs.SetFloat(KeyMusicVolume, musicVolumeSlider.value);
            }

            if (fullscreenToggle != null)
            {
                PlayerPrefs.SetInt(KeyFullscreen, fullscreenToggle.isOn ? 1 : 0);
            }

            if (qualityDropdown != null)
            {
                PlayerPrefs.SetInt(KeyQuality, qualityDropdown.value);
            }

            if (vSyncToggle != null)
            {
                PlayerPrefs.SetInt(KeyVSync, vSyncToggle.isOn ? 1 : 0);
            }

            if (showFpsToggle != null)
            {
                PlayerPrefs.SetInt(KeyShowFPS, showFpsToggle.isOn ? 1 : 0);
            }
        }

        private void LoadSettings()
        {
            if (!PlayerPrefs.HasKey(KeyMasterVolume))
            {
                SetDefaultValues();
            }
        }

        private void SetDefaultValues()
        {
            PlayerPrefs.SetFloat(KeyMasterVolume, 1f);
            PlayerPrefs.SetFloat(KeySfxVolume, 1f);
            PlayerPrefs.SetFloat(KeyMusicVolume, 1f);
            PlayerPrefs.SetInt(KeyQuality, QualitySettings.GetQualityLevel());
            PlayerPrefs.SetInt(KeyVSync, QualitySettings.vSyncCount > 0 ? 1 : 0);
            PlayerPrefs.SetInt(KeyShowFPS, 0);
            PlayerPrefs.SetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplySettings()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, 1f));
            int quality = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            bool vsync = PlayerPrefs.GetInt(KeyVSync, 0) == 1;
            bool fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            showFPS = PlayerPrefs.GetInt(KeyShowFPS, 0) == 1;

            AudioListener.volume = master;
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(quality, true);
            }

            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Screen.fullScreen = fullscreen;

            if (fpsText != null)
            {
                fpsText.gameObject.SetActive(showFPS);
            }
        }

        private void RefreshUI()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, 1f));
            float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfxVolume, 1f));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusicVolume, 1f));
            int quality = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            bool vsync = PlayerPrefs.GetInt(KeyVSync, 0) == 1;
            bool fps = PlayerPrefs.GetInt(KeyShowFPS, 0) == 1;
            bool fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = master;
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = sfx;
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = music;
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.value = quality;
            }

            if (vSyncToggle != null)
            {
                vSyncToggle.isOn = vsync;
            }

            if (showFpsToggle != null)
            {
                showFpsToggle.isOn = fps;
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = fullscreen;
            }

            if (masterVolumeText != null)
            {
                masterVolumeText.text = $"主音量: {Mathf.RoundToInt(master * 100f)}%";
            }
        }

        private static void SaveSettings()
        {
            PlayerPrefs.Save();
        }
    }
}
