// Path: Assets/_Scripts/UI/ResultUIController.cs
// Result panel controller for victory/defeat messages and follow-up actions.
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    public class ResultUIController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text resultText;
        [SerializeField] private string defaultVictoryText = "Victory!";
        [SerializeField] private string defaultDefeatText = "Game Over";

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventCenter.AddListener<UIRequestEvent>(OnUIRequest);
            EventCenter.AddListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void OnDisable()
        {
            EventCenter.RemoveListener<UIRequestEvent>(OnUIRequest);
            EventCenter.RemoveListener<GameFlowStateChangedEvent>(OnGameFlowChanged);
        }

        private void OnUIRequest(UIRequestEvent evt)
        {
            if (evt.PanelType != UIPanelType.Result)
            {
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(evt.IsOpen);
            }

            if (evt.IsOpen && resultText != null && !string.IsNullOrEmpty(evt.Message))
            {
                resultText.text = evt.Message;
            }
        }

        private void OnGameFlowChanged(GameFlowStateChangedEvent evt)
        {
            if (resultText == null)
            {
                return;
            }

            if (evt.CurrentState == GameFlowState.Victory)
            {
                resultText.text = string.IsNullOrEmpty(evt.Message) ? defaultVictoryText : evt.Message;
            }
            else if (evt.CurrentState == GameFlowState.Defeat)
            {
                resultText.text = string.IsNullOrEmpty(evt.Message) ? defaultDefeatText : evt.Message;
            }
        }

        public void OnClickRestart()
        {
            GameManager manager = ResolveGameManager();
            if (manager != null)
            {
                manager.UI_RestartCurrentScene();
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
                Debug.LogError("[ResultUIController] Missing ARPGDemo.Core.GameManager in scene.");
            }

            return manager;
        }
    }
}
