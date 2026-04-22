// Path: Assets/_Scripts/UI/PauseUIController.cs
// Pause panel bridge: handles resume, open settings and back-to-menu actions.
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.UI
{
    public class PauseUIController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private MonoBehaviour settingsUI;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            panelRoot.SetActive(false);
        }

        public void ConfigureRuntime(GameObject runtimeRoot, MonoBehaviour runtimeSettingsUI)
        {
            panelRoot = runtimeRoot != null ? runtimeRoot : gameObject;
            settingsUI = runtimeSettingsUI;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventCenter.AddListener<UIRequestEvent>(OnUIRequest);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<UIRequestEvent>(OnUIRequest);
        }

        private void OnUIRequest(UIRequestEvent evt)
        {
            if (evt.PanelType != UIPanelType.Pause)
            {
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(evt.IsOpen);
            }
        }

        public void OnClickResume()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.UI_ResumeGame();
            }
        }

        public void OnClickSave()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.SaveGame();
            }
        }

        public void OnClickLoad()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.LoadGame();
            }
        }

        public void OnClickOpenSettings()
        {
            if (settingsUI != null)
            {
                settingsUI.Invoke("OpenSettings", 0f);
            }
        }

        public void OnClickBackToMenu()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.UI_BackToMainMenu();
            }
        }

        private static GameManager ResolveGameManager()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[PauseUIController] Missing ARPGDemo.Core.GameManager in scene.");
            }

            return manager;
        }
    }
}
