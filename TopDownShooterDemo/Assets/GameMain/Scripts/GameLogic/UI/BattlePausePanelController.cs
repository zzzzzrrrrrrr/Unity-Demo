using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// In-battle pause panel with continue/restart/back actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePausePanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToMenuButton;

        private ProcedureManager procedureManager;
        private ProcedureBattle activeBattleProcedure;
        private CanvasGroup panelCanvasGroup;
        private bool loggedMissingRefs;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            EnsureCanvasGroup();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveProcedureManager();
            SubscribeProcedureEvents();
            BindActiveBattleProcedure();
            RefreshVisibility();
            ValidateReferences();
        }

        private void OnDisable()
        {
            UnsubscribeProcedureEvents();
            UnbindActiveBattleProcedure();
        }

        public void BindView(GameObject root, Button continueBtn, Button restartBtn, Button menuBtn)
        {
            panelRoot = root;
            continueButton = continueBtn;
            restartButton = restartBtn;
            backToMenuButton = menuBtn;
            EnsureCanvasGroup();
            BindButtons();
        }

        public void Configure(ProcedureManager manager)
        {
            if (procedureManager == manager)
            {
                return;
            }

            UnsubscribeProcedureEvents();
            procedureManager = manager;
            SubscribeProcedureEvents();
            BindActiveBattleProcedure();
            RefreshVisibility();
        }

        private void ResolveProcedureManager()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }
        }

        private void SubscribeProcedureEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }
        }

        private void UnsubscribeProcedureEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }
        }

        private void BindActiveBattleProcedure()
        {
            UnbindActiveBattleProcedure();
            if (procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Battle)
            {
                SetPanelVisible(false);
                return;
            }

            activeBattleProcedure = procedureManager.CurrentProcedure as ProcedureBattle;
            if (activeBattleProcedure != null)
            {
                activeBattleProcedure.PauseStateChanged -= OnPauseStateChanged;
                activeBattleProcedure.PauseStateChanged += OnPauseStateChanged;
            }

            RefreshVisibility();
        }

        private void UnbindActiveBattleProcedure()
        {
            if (activeBattleProcedure != null)
            {
                activeBattleProcedure.PauseStateChanged -= OnPauseStateChanged;
                activeBattleProcedure = null;
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            BindActiveBattleProcedure();
        }

        private void OnPauseStateChanged(bool paused)
        {
            SetPanelVisible(paused);
        }

        private void RefreshVisibility()
        {
            var show = activeBattleProcedure != null
                && procedureManager != null
                && procedureManager.CurrentProcedureType == ProcedureType.Battle
                && activeBattleProcedure.IsPaused;
            SetPanelVisible(show);
        }

        private void OnContinueClicked()
        {
            activeBattleProcedure?.SetPaused(false);
        }

        private void OnRestartClicked()
        {
            activeBattleProcedure?.RestartBattle();
        }

        private void OnBackToMenuClicked()
        {
            activeBattleProcedure?.BackToMenu();
        }

        private void BindButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.RemoveListener(OnBackToMenuClicked);
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot == null)
            {
                return;
            }

            EnsureCanvasGroup();
            if (panelRoot != gameObject)
            {
                panelRoot.SetActive(visible);
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
                panelCanvasGroup.blocksRaycasts = visible;
                panelCanvasGroup.interactable = visible;
            }

            if (continueButton != null)
            {
                continueButton.interactable = visible;
            }

            if (restartButton != null)
            {
                restartButton.interactable = visible;
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.interactable = visible;
            }
        }

        private void EnsureCanvasGroup()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
        }

        private void ValidateReferences()
        {
            if (loggedMissingRefs)
            {
                return;
            }

            if (continueButton == null || restartButton == null || backToMenuButton == null)
            {
                loggedMissingRefs = true;
                Debug.LogWarning("BattlePausePanelController is missing one or more button references.", this);
            }
        }
    }
}
