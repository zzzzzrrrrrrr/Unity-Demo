// Path: Assets/_Scripts/UI/MainMenuUIController.cs
// Main menu panel bridge for UIRequestEvent and button callbacks.
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.UI
{
    public class MainMenuUIController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private MonoBehaviour settingsUI;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            panelRoot.SetActive(showOnStart);
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
            if (evt.PanelType != UIPanelType.MainMenu)
            {
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(evt.IsOpen);
            }
        }

        public void OnClickStartGame()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.UI_StartGame();
            }
        }

        public void OnClickLoadGame()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.LoadGame();
                manager.UI_StartGame();
            }
        }

        public void OnClickOpenSettings()
        {
            if (settingsUI != null)
            {
                settingsUI.Invoke("OpenSettings", 0f);
            }
        }

        public void OnClickQuitGame()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.UI_QuitGame();
            }
        }

        private static GameManager ResolveGameManager()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[MainMenuUIController] Missing ARPGDemo.Core.GameManager in scene.");
            }

            return manager;
        }
    }
}
