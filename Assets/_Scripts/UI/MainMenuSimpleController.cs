// Path: Assets/_Scripts/UI/MainMenuSimpleController.cs
// Lightweight main menu controller for static scene UI (Start / Quit).
using ARPGDemo.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    [DisallowMultipleComponent]
    public class MainMenuSimpleController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameplaySceneName = "SampleScene";

        [Header("UI Refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text titleText;
        [SerializeField] private string title = "ARPG Demo";
        [SerializeField] private bool autoFindSceneReferences = true;

        [Header("Fallback")]
        [SerializeField] private bool useOnGuiFallback = false;

        private void Awake()
        {
            if (!IsCurrentMainMenuScene())
            {
                Destroy(gameObject);
                return;
            }

            if (autoFindSceneReferences)
            {
                TryAutoBindSceneReferences();
            }

            BindButtonEvents();
            RefreshUiState();
        }

        private void OnEnable()
        {
            if (!IsCurrentMainMenuScene())
            {
                return;
            }

            BindButtonEvents();
            RefreshUiState();
        }

        private void OnDisable()
        {
            UnbindButtonEvents();
        }

        private void OnGUI()
        {
            if (!useOnGuiFallback || HasStaticMenuReady() || !IsCurrentMainMenuScene())
            {
                return;
            }

            const float panelWidth = 320f;
            const float panelHeight = 190f;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            Rect titleRect = new Rect(panelX + 12f, panelY + 16f, panelWidth - 24f, 30f);
            Rect startRect = new Rect(panelX + 28f, panelY + 68f, panelWidth - 56f, 42f);
            Rect quitRect = new Rect(panelX + 28f, panelY + 122f, panelWidth - 56f, 42f);

            GUI.Box(panelRect, string.Empty);
            GUI.Label(titleRect, string.IsNullOrEmpty(title) ? "ARPG Demo" : title);

            if (GUI.Button(startRect, "开始游戏"))
            {
                OnClickStartGame();
            }

            if (GUI.Button(quitRect, "退出游戏"))
            {
                OnClickQuitGame();
            }
        }

        public void OnClickStartGame()
        {
            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                manager.UI_StartGame();
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OnClickQuitGame()
        {
            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                manager.UI_QuitGame();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void TryAutoBindSceneReferences()
        {
            if (panelRoot == null)
            {
                GameObject foundPanel = GameObject.Find("MainMenuPanel");
                if (foundPanel != null)
                {
                    panelRoot = foundPanel;
                }
            }

            if (startButton == null)
            {
                startButton = FindButtonByObjectName("StartButton");
            }

            if (quitButton == null)
            {
                quitButton = FindButtonByObjectName("QuitButton");
            }

            if (titleText == null)
            {
                titleText = FindTextByObjectName("MainMenuTitle");
            }
        }

        private Button FindButtonByObjectName(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private Text FindTextByObjectName(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<Text>() : null;
        }

        private void RefreshUiState()
        {
            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(title) ? "ARPG Demo" : title;
            }
        }

        private void BindButtonEvents()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStartGame);
                startButton.onClick.AddListener(OnClickStartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnClickQuitGame);
                quitButton.onClick.AddListener(OnClickQuitGame);
            }
        }

        private void UnbindButtonEvents()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnClickQuitGame);
            }
        }

        private bool HasStaticMenuReady()
        {
            return panelRoot != null && startButton != null && quitButton != null;
        }

        private bool IsCurrentMainMenuScene()
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                mainMenuSceneName,
                System.StringComparison.Ordinal);
        }
    }
}
