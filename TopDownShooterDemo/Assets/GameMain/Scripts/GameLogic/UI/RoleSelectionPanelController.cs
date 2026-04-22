using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Menu-only role selection and confirmation panel for vertical-slice flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoleSelectionPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image roleDisplayImage;
        [SerializeField] private Text roleNameText;
        [SerializeField] private Text redHealthText;
        [SerializeField] private Text blueArmorText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text skillNameText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Text primaryWeaponText;
        [SerializeField] private Text secondaryWeaponText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Button roleButtonA;
        [SerializeField] private Text roleButtonALabel;
        [SerializeField] private Button roleButtonB;
        [SerializeField] private Text roleButtonBLabel;
        [SerializeField] private Color roleButtonNormalColor = new Color(0.16f, 0.18f, 0.22f, 0.96f);
        [SerializeField] private Color roleButtonSelectedColor = new Color(0.24f, 0.48f, 0.75f, 0.98f);
        [SerializeField] private Color roleButtonLockedColor = new Color(0.2f, 0.6f, 0.28f, 0.98f);
        [SerializeField] private Color warningColor = new Color(1f, 0.42f, 0.34f, 1f);
        [SerializeField] private Color hintColor = new Color(0.84f, 0.92f, 1f, 0.95f);

        private ProcedureManager procedureManager;
        private VerticalSliceFlowController flowController;
        private RoleSelectionProfileData[] profiles;
        private int selectedIndex;
        private bool roleConfirmedForCurrentRun;
        private CanvasGroup panelCanvasGroup;
        private float warningUntilTime;
        private string transientWarningMessage;

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
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void BindView(
            GameObject root,
            Image displayImage,
            Text nameLabel,
            Text redHpLabel,
            Text blueArmorLabel,
            Text energyLabel,
            Text skillNameLabel,
            Text skillDescLabel,
            Text primaryWeaponLabel,
            Text secondaryWeaponLabel,
            Text statusLabel,
            Button confirmBtn,
            Text confirmBtnLabel,
            Button buttonA,
            Text buttonALabel,
            Button buttonB,
            Text buttonBLabel)
        {
            panelRoot = root;
            roleDisplayImage = displayImage;
            roleNameText = nameLabel;
            redHealthText = redHpLabel;
            blueArmorText = blueArmorLabel;
            energyText = energyLabel;
            skillNameText = skillNameLabel;
            skillDescriptionText = skillDescLabel;
            primaryWeaponText = primaryWeaponLabel;
            secondaryWeaponText = secondaryWeaponLabel;
            statusText = statusLabel;
            confirmButton = confirmBtn;
            confirmButtonLabel = confirmBtnLabel;
            roleButtonA = buttonA;
            roleButtonALabel = buttonALabel;
            roleButtonB = buttonB;
            roleButtonBLabel = buttonBLabel;

            EnsureCanvasGroup();
            BindButtons();
        }

        public void Configure(
            ProcedureManager manager,
            VerticalSliceFlowController flow,
            RoleSelectionProfileData[] roleProfiles)
        {
            UnsubscribeEvents();

            procedureManager = manager;
            flowController = flow;
            profiles = roleProfiles;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, GetProfileCount() - 1));

            SubscribeEvents();
            RefreshAll();
        }

        private void ResolveDependencies()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }
        }

        private void SubscribeEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }

            if (flowController != null)
            {
                flowController.RolePortalBlocked -= OnRolePortalBlocked;
                flowController.RolePortalBlocked += OnRolePortalBlocked;
            }
        }

        private void UnsubscribeEvents()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            if (flowController != null)
            {
                flowController.RolePortalBlocked -= OnRolePortalBlocked;
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            if (current == ProcedureType.Menu)
            {
                ResetForMenuEntry();
            }

            RefreshAll();
        }

        private void OnRolePortalBlocked(string message)
        {
            transientWarningMessage = string.IsNullOrWhiteSpace(message)
                ? "Please confirm role before entering portal."
                : message;
            warningUntilTime = Time.unscaledTime + 1.4f;
            UpdateStatusText();
        }

        private void ResetForMenuEntry()
        {
            roleConfirmedForCurrentRun = false;
            selectedIndex = 0;
            RoleSelectionRuntimeState.Clear();
            flowController?.SetRoleConfirmed(false);
        }

        private void RefreshAll()
        {
            var isMenu = procedureManager != null && procedureManager.CurrentProcedureType == ProcedureType.Menu;
            SetVisible(isMenu);
            if (!isMenu)
            {
                return;
            }

            if (!roleConfirmedForCurrentRun && flowController != null && flowController.IsRoleConfirmed)
            {
                roleConfirmedForCurrentRun = true;
            }

            RefreshRoleButtons();
            UpdateSelectedProfileView();
            UpdateConfirmButtonState();
            UpdateStatusText();
        }

        private void Update()
        {
            if (procedureManager == null || procedureManager.CurrentProcedureType != ProcedureType.Menu)
            {
                return;
            }

            if (warningUntilTime > 0f && Time.unscaledTime > warningUntilTime)
            {
                warningUntilTime = 0f;
                transientWarningMessage = string.Empty;
                UpdateStatusText();
            }
        }

        private void OnSelectRoleA()
        {
            SelectRole(0);
        }

        private void OnSelectRoleB()
        {
            SelectRole(1);
        }

        private void SelectRole(int profileIndex)
        {
            if (roleConfirmedForCurrentRun)
            {
                return;
            }

            if (!HasProfile(profileIndex))
            {
                return;
            }

            selectedIndex = profileIndex;
            UpdateSelectedProfileView();
            RefreshRoleButtons();
            UpdateStatusText();
        }

        private void OnConfirmRole()
        {
            if (roleConfirmedForCurrentRun)
            {
                return;
            }

            var profile = GetSelectedProfile();
            if (profile == null)
            {
                transientWarningMessage = "No valid role profile available.";
                warningUntilTime = Time.unscaledTime + 1.2f;
                UpdateStatusText();
                return;
            }

            roleConfirmedForCurrentRun = true;
            RoleSelectionRuntimeState.SetConfirmedProfile(profile);
            flowController?.SetRoleConfirmed(true);
            UpdateConfirmButtonState();
            RefreshRoleButtons();
            UpdateStatusText();
            Debug.Log("RoleSelection confirmed for run. role=" + profile.displayName + " id=" + profile.roleId, this);
        }

        private void RefreshRoleButtons()
        {
            SetupRoleButton(roleButtonA, roleButtonALabel, 0);
            SetupRoleButton(roleButtonB, roleButtonBLabel, 1);
        }

        private void SetupRoleButton(Button button, Text label, int index)
        {
            if (button == null)
            {
                return;
            }

            var hasProfile = HasProfile(index);
            button.gameObject.SetActive(hasProfile);
            button.interactable = hasProfile && !roleConfirmedForCurrentRun;
            if (!hasProfile)
            {
                return;
            }

            var profile = profiles[index];
            if (label != null)
            {
                label.text = profile != null ? profile.displayName : "Unknown";
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                if (roleConfirmedForCurrentRun && selectedIndex == index)
                {
                    image.color = roleButtonLockedColor;
                }
                else if (selectedIndex == index)
                {
                    image.color = roleButtonSelectedColor;
                }
                else
                {
                    image.color = roleButtonNormalColor;
                }
            }
        }

        private void UpdateSelectedProfileView()
        {
            var profile = GetSelectedProfile();
            if (profile == null)
            {
                SetText(roleNameText, "Role: --");
                SetText(redHealthText, "Red HP: --");
                SetText(blueArmorText, "Blue Armor: --");
                SetText(energyText, "Energy: --");
                SetText(skillNameText, "Skill: --");
                SetText(skillDescriptionText, "--");
                SetText(primaryWeaponText, "Primary: --");
                SetText(secondaryWeaponText, "Secondary: --");
                if (roleDisplayImage != null)
                {
                    roleDisplayImage.enabled = false;
                }

                return;
            }

            SetText(roleNameText, "Role: " + profile.displayName);
            SetText(redHealthText, "Red HP: " + profile.redHealth);
            SetText(blueArmorText, "Blue Armor: " + profile.blueArmor);
            SetText(energyText, "Energy: " + profile.energy);
            SetText(skillNameText, "Skill: " + profile.skillName);
            SetText(skillDescriptionText, profile.skillDescription);
            SetText(
                primaryWeaponText,
                "Primary: " + profile.primaryWeaponName + "\n" + profile.primaryWeaponDescription);
            SetText(
                secondaryWeaponText,
                "Secondary: " + profile.secondaryWeaponName + "\n" + profile.secondaryWeaponDescription);

            if (roleDisplayImage != null)
            {
                roleDisplayImage.enabled = true;
                roleDisplayImage.sprite = profile.displaySprite;
                roleDisplayImage.color = profile.displaySprite != null
                    ? Color.white
                    : new Color(0.28f, 0.62f, 0.92f, 0.96f);
            }
        }

        private void UpdateConfirmButtonState()
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = !roleConfirmedForCurrentRun && GetSelectedProfile() != null;
            }

            if (confirmButtonLabel != null)
            {
                confirmButtonLabel.text = roleConfirmedForCurrentRun ? "Confirmed" : "Confirm Role";
            }
        }

        private void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(transientWarningMessage) && Time.unscaledTime <= warningUntilTime)
            {
                statusText.text = transientWarningMessage;
                statusText.color = warningColor;
                return;
            }

            if (roleConfirmedForCurrentRun)
            {
                var profile = GetSelectedProfile();
                statusText.text = "Role locked for this run: " + (profile != null ? profile.displayName : "--") + ". Portal ready.";
                statusText.color = hintColor;
            }
            else
            {
                statusText.text = "Select a role and confirm to unlock portal (Press E at portal).";
                statusText.color = hintColor;
            }
        }

        private int GetProfileCount()
        {
            return profiles != null ? profiles.Length : 0;
        }

        private bool HasProfile(int index)
        {
            return profiles != null && index >= 0 && index < profiles.Length && profiles[index] != null;
        }

        private RoleSelectionProfileData GetSelectedProfile()
        {
            return HasProfile(selectedIndex) ? profiles[selectedIndex] : null;
        }

        private void BindButtons()
        {
            if (roleButtonA != null)
            {
                roleButtonA.onClick.RemoveListener(OnSelectRoleA);
                roleButtonA.onClick.AddListener(OnSelectRoleA);
            }

            if (roleButtonB != null)
            {
                roleButtonB.onClick.RemoveListener(OnSelectRoleB);
                roleButtonB.onClick.AddListener(OnSelectRoleB);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmRole);
                confirmButton.onClick.AddListener(OnConfirmRole);
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

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
