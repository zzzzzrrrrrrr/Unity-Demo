using GameMain.GameLogic.CharacterSelect;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.UI;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Display-only lobby NPC interaction. It opens DialogPanel and never changes gameplay data.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class NpcDialogueTrigger : MonoBehaviour
    {
        private const string DefaultLobbyStatus = "左键选择角色 -> 确认选择 -> WASD 控制已确认角色 -> 靠近 NPC 按 E 对话 -> 在传送门处按 E 进入战斗。";

        [SerializeField] private string npcName = "NPC";
        [SerializeField] private string[] dialogueLines;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private DialogPanel dialogPanel;
        [SerializeField] private GameObject promptObject;

        private bool playerNearby;
        private CharacterInfoPanelController infoPanel;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (!playerNearby || !Input.GetKeyDown(interactKey))
            {
                return;
            }

            if (dialogPanel != null)
            {
                dialogPanel.ShowDialog(npcName, dialogueLines);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayerLikeInteractor(other))
            {
                return;
            }

            playerNearby = true;
            SetPromptVisible(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayerLikeInteractor(other))
            {
                return;
            }

            playerNearby = false;
            SetPromptVisible(false);
        }

        public void Configure(string displayName, string[] lines, DialogPanel panel, GameObject prompt, KeyCode key)
        {
            npcName = string.IsNullOrWhiteSpace(displayName) ? "NPC" : displayName.Trim();
            dialogueLines = lines ?? new string[0];
            dialogPanel = panel;
            promptObject = prompt;
            interactKey = key;
            SetPromptVisible(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptObject != null)
            {
                promptObject.SetActive(false);
            }

            if (infoPanel == null)
            {
                infoPanel = FindObjectOfType<CharacterInfoPanelController>();
            }

            if (infoPanel != null)
            {
                infoPanel.SetStatus(visible ? "按 E 对话：" + npcName : DefaultLobbyStatus);
            }
        }

        private static bool IsPlayerLikeInteractor(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            return other.GetComponentInParent<CharacterSelectAvatarController>() != null ||
                   other.GetComponentInParent<PlayerHealth>() != null;
        }
    }
}
