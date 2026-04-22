using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Menu-side panel for choosing boss preset before battle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuPresetPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text presetNameText;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button frenzyButton;
        [SerializeField] private Button startBattleButton;
        [SerializeField] private bool allowDirectStartButton = false;
        [SerializeField] private Color selectedButtonColor = new Color(0.27f, 0.62f, 0.31f, 0.98f);
        [SerializeField] private Color normalButtonColor = new Color(0.2f, 0.2f, 0.24f, 0.95f);

        private ProcedureManager procedureManager;
        private BossPresetController bossPresetController;
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
            ResolveDependencies();
            SubscribeEvents();
            RefreshView();
            ValidateReferences();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void BindView(GameObject root, Text presetLabel, Button normalBtn, Button frenzyBtn, Button startBtn)
        {
            panelRoot = root;
            presetNameText = presetLabel;
            normalButton = normalBtn;
            frenzyButton = frenzyBtn;
            startBattleButton = startBtn;
            EnsureCanvasGroup();
            BindButtons();
        }

        public void Configure(ProcedureManager manager, BossPresetController presetController)
        {
            UnsubscribeEvents();
            procedureManager = manager;
            bossPresetController = presetController;
            SubscribeEvents();
            RefreshView();
        }

        private void ResolveDependencies()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (bossPresetController == null)
            {
                bossPresetController = BossPresetController.Instance;
            }
        }

        private void SubscribeEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }

            if (bossPresetController != null)
            {
                bossPresetController.PresetChanged -= OnPresetChanged;
                bossPresetController.PresetChanged += OnPresetChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            if (bossPresetController != null)
            {
                bossPresetController.PresetChanged -= OnPresetChanged;
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            RefreshView();
        }

        private void OnPresetChanged(BossPresetController.BossPresetType presetType, string presetName)
        {
            UpdatePresetText(presetName);
            UpdateButtonHighlight(presetType);
        }

        private void RefreshView()
        {
            var isInMenu = procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Menu;
            SetVisible(isInMenu);

            if (bossPresetController != null)
            {
                UpdatePresetText(bossPresetController.CurrentPresetName);
                UpdateButtonHighlight(bossPresetController.CurrentPreset);
            }
            else
            {
                UpdatePresetText("--");
            }

            if (startBattleButton != null)
            {
                startBattleButton.interactable = isInMenu && allowDirectStartButton;
            }
        }

        private void OnNormalClicked()
        {
            bossPresetController?.SelectNormalPreset();
        }

        private void OnFrenzyClicked()
        {
            bossPresetController?.SelectFrenzyPreset();
        }

        private void OnStartBattleClicked()
        {
            if (procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Menu)
            {
                return;
            }

            var menuProcedure = procedureManager.CurrentProcedure as ProcedureMenu;
            if (menuProcedure != null)
            {
                menuProcedure.StartBattle();
            }
            else
            {
                procedureManager.ChangeProcedure(ProcedureType.Battle);
            }
        }

        private void BindButtons()
        {
            if (normalButton != null)
            {
                normalButton.onClick.RemoveListener(OnNormalClicked);
                normalButton.onClick.AddListener(OnNormalClicked);
            }

            if (frenzyButton != null)
            {
                frenzyButton.onClick.RemoveListener(OnFrenzyClicked);
                frenzyButton.onClick.AddListener(OnFrenzyClicked);
            }

            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveListener(OnStartBattleClicked);
                startBattleButton.onClick.AddListener(OnStartBattleClicked);
            }
        }

        private void UpdatePresetText(string presetName)
        {
            if (presetNameText != null)
            {
                presetNameText.text = "Boss Preset: " + presetName;
            }
        }

        private void UpdateButtonHighlight(BossPresetController.BossPresetType currentPreset)
        {
            SetButtonColor(normalButton, currentPreset == BossPresetController.BossPresetType.Normal ? selectedButtonColor : normalButtonColor);
            SetButtonColor(frenzyButton, currentPreset == BossPresetController.BossPresetType.Frenzy ? selectedButtonColor : normalButtonColor);
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private void SetVisible(bool visible)
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

            if (presetNameText == null || normalButton == null || frenzyButton == null || startBattleButton == null)
            {
                loggedMissingRefs = true;
                Debug.LogWarning("MenuPresetPanelController is missing one or more UI references.", this);
            }
        }
    }
}
