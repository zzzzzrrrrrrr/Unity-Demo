using GameMain.GameLogic.Data;
using GameMain.GameLogic.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Handles confirm action and reserves entrance to run scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectConfirmController : MonoBehaviour
    {
        [SerializeField] private CharacterSelectionController selectionController;
        [SerializeField] private CharacterInfoPanelController infoPanelController;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmButtonLabel;
        [SerializeField] private Button enterRunSceneButton;
        [SerializeField] private CharacterSelectPortalController worldPortal;
        [SerializeField] private string runSceneName = "RunScene";
        [SerializeField] private bool isSelectionConfirmed;

        private CharacterData confirmedCharacter;

        public void Bind(
            CharacterSelectionController selection,
            CharacterInfoPanelController panel,
            Button confirm,
            Text confirmLabel,
            Button enterRunButton,
            CharacterSelectPortalController portalController = null)
        {
            selectionController = selection;
            infoPanelController = panel;
            confirmButton = confirm;
            confirmButtonLabel = confirmLabel;
            enterRunSceneButton = enterRunButton;
            worldPortal = portalController;
            BindButtons();
            RebindSelectionEvents();
            ApplyActorControlState();
            RefreshInteractable();
        }

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            RebindSelectionEvents();
            ApplyActorControlState();
            RefreshInteractable();
        }

        private void OnDisable()
        {
            if (selectionController != null)
            {
                selectionController.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void RebindSelectionEvents()
        {
            if (selectionController == null)
            {
                return;
            }

            selectionController.SelectionChanged -= OnSelectionChanged;
            selectionController.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(CharacterData selectedData)
        {
            if (isSelectionConfirmed)
            {
                isSelectionConfirmed = false;
                confirmedCharacter = null;
                RunSessionContext.Clear();
                selectionController?.SetSelectionLocked(false);
                worldPortal?.SetPortalEnabled(false);
            }

            if (infoPanelController != null)
            {
                infoPanelController.ShowCharacter(selectedData);
                infoPanelController.SetStatus(
                    selectedData != null
                        ? "Character selected. Click Confirm Selection."
                        : "Left click a character to inspect.");
            }

            ApplyActorControlState();
            RefreshInteractable();
        }

        private void OnConfirmSelectionClicked()
        {
            if (selectionController == null)
            {
                return;
            }

            var selectedData = selectionController.SelectedCharacterData;
            if (selectedData == null)
            {
                infoPanelController?.SetStatus("No character selected.");
                return;
            }

            isSelectionConfirmed = true;
            confirmedCharacter = selectedData;
            var selectedSprite = ResolveSelectedActorSprite();
            RunSessionContext.SetSelectedCharacter(selectedData, selectedSprite);
            selectionController.SetSelectionLocked(true);
            worldPortal?.SetPortalEnabled(true);
            infoPanelController?.SetStatus("Confirmed: " + selectedData.characterName + ". Control this character with WASD, then press E at portal.");
            ApplyActorControlState();
            RefreshInteractable();
        }

        private void OnEnterRunSceneClicked()
        {
            if (!isSelectionConfirmed || confirmedCharacter == null || !RunSessionContext.HasSelectedCharacter)
            {
                infoPanelController?.SetStatus("Please confirm selection before entering RunScene.");
                RefreshInteractable();
                return;
            }

            if (string.IsNullOrWhiteSpace(runSceneName))
            {
                infoPanelController?.SetStatus("Run scene name is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(runSceneName))
            {
                infoPanelController?.SetStatus("RunScene entry reserved. Scene not in build list yet.");
                Debug.Log("CharacterSelect: run scene entry reserved, scene not configured yet: " + runSceneName, this);
                return;
            }

            SceneManager.LoadScene(runSceneName);
        }

        private void BindButtons()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmSelectionClicked);
                confirmButton.onClick.AddListener(OnConfirmSelectionClicked);
            }

            if (enterRunSceneButton != null)
            {
                enterRunSceneButton.onClick.RemoveListener(OnEnterRunSceneClicked);
                enterRunSceneButton.onClick.AddListener(OnEnterRunSceneClicked);
            }
        }

        private void RefreshInteractable()
        {
            if (confirmButton != null)
            {
                confirmButton.interactable =
                    selectionController != null &&
                    selectionController.SelectedCharacterData != null &&
                    !isSelectionConfirmed;
            }

            if (confirmButtonLabel != null && isSelectionConfirmed)
            {
                confirmButtonLabel.text = "Confirmed";
            }
            else if (confirmButtonLabel != null)
            {
                confirmButtonLabel.text = "Confirm Selection";
            }

            if (enterRunSceneButton != null)
            {
                enterRunSceneButton.interactable =
                    isSelectionConfirmed &&
                    confirmedCharacter != null &&
                    RunSessionContext.HasSelectedCharacter;
            }

            if (worldPortal != null)
            {
                worldPortal.SetPortalEnabled(
                    isSelectionConfirmed &&
                    confirmedCharacter != null &&
                    RunSessionContext.HasSelectedCharacter);
            }
        }

        private void ApplyActorControlState()
        {
            if (selectionController == null)
            {
                return;
            }

            var selectedTarget = selectionController.SelectedTarget;
            var targets = selectionController.Targets;
            if (targets == null)
            {
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var isConfirmedActor = isSelectionConfirmed && target == selectedTarget;
                var shouldDim = isSelectionConfirmed && target != selectedTarget;
                target.SetControlState(isConfirmedActor, shouldDim);

                var actorController = target.GetComponent<CharacterSelectAvatarController>();
                if (actorController != null)
                {
                    actorController.SetControllable(isConfirmedActor);
                }
            }
        }

        private Sprite ResolveSelectedActorSprite()
        {
            var selectedTarget = selectionController != null ? selectionController.SelectedTarget : null;
            if (selectedTarget == null)
            {
                return null;
            }

            var sprite = selectedTarget.BodySprite;
            if (sprite != null)
            {
                return sprite;
            }

            var renderer = selectedTarget.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer.sprite : null;
        }
    }
}
