// Path: Assets/_Scripts/Game/DemoChestInteractable.cs
// Minimal chest interaction: press Q to open, grant reward, show popup.
using ARPGDemo.Core;
using ARPGDemo.Tools;
using ARPGDemo.UI;
using UnityEngine;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class DemoChestInteractable : MonoBehaviour
    {
        [Header("Reward")]
        [SerializeField] private string itemId = "potion_small";
        [SerializeField] private string itemDisplayName = "Small Potion";
        [SerializeField] private int itemAmount = 1;
        [SerializeField] private bool healOnOpen = false;
        [SerializeField] private int healAmount = 15;

        [Header("Interaction")]
        [SerializeField] private KeyCode interactKey = KeyCode.Q;
        [SerializeField] private string playerActorId = "Player";
        [SerializeField] private bool canOpenOnlyOnce = true;
        [SerializeField] private bool opened = false;

        [Header("Prompt")]
        [SerializeField] private bool showPrompt = true;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float promptCharacterSize = 0.12f;
        [SerializeField] private string closedPrompt = "按 Q 开启";
        [SerializeField] private string openedPrompt = "已开启";

        [Header("Visual")]
        [SerializeField] private SpriteRenderer chestRenderer;
        [SerializeField] private Color closedColor = new Color(0.8f, 0.58f, 0.18f, 1f);
        [SerializeField] private Color openedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        private ActorStats residentPlayer;
        private TextMesh promptText;
        private Collider2D triggerCollider;

        private void Awake()
        {
            // Force demo interaction to Q and keep prompt text consistent with finish-zone style.
            interactKey = KeyCode.Q;
            closedPrompt = "按 Q 开启";
            openedPrompt = "已开启";

            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            if (chestRenderer == null)
            {
                chestRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            EnsurePrompt();
            RefreshVisualState();
            RefreshPrompt();
        }

        private void Update()
        {
            RefreshPrompt();

            if (residentPlayer == null)
            {
                return;
            }

            if (opened && canOpenOnlyOnce)
            {
                return;
            }

            if (!InputCompat.IsDown(interactKey))
            {
                return;
            }

            OpenChest();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ActorStats stats = ResolvePlayerStats(other);
            if (stats != null)
            {
                residentPlayer = stats;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (residentPlayer == null)
            {
                return;
            }

            ActorStats stats = ResolvePlayerStats(other);
            if (stats == residentPlayer)
            {
                residentPlayer = null;
            }
        }

        private ActorStats ResolvePlayerStats(Collider2D other)
        {
            if (other == null)
            {
                return null;
            }

            ActorStats stats = other.GetComponentInParent<ActorStats>();
            if (stats == null || stats.Team != ActorTeam.Player)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(playerActorId) && stats.ActorId != playerActorId)
            {
                return null;
            }

            return stats;
        }

        private void OpenChest()
        {
            // Single source of truth for chest open flow: mark opened, grant reward, then show popup.
            opened = true;
            RefreshVisualState();
            RefreshPrompt();

            DemoInventoryService inventory = null;
            DemoInventoryService[] allInventory = FindObjectsOfType<DemoInventoryService>(true);
            if (allInventory != null && allInventory.Length > 0)
            {
                inventory = allInventory[0];
            }
            if (inventory != null)
            {
                inventory.AddItem(itemId, itemDisplayName, itemAmount);
            }

            if (healOnOpen && residentPlayer != null && !residentPlayer.IsDead)
            {
                residentPlayer.RecoverHealth(Mathf.Max(1, healAmount));
            }

            DemoRewardPopupPanel popup = DemoRewardPopupPanel.Instance;
            if (popup != null)
            {
                string message = $"{itemDisplayName} x{Mathf.Max(1, itemAmount)}";
                if (healOnOpen)
                {
                    message += $"\\nHeal +{Mathf.Max(1, healAmount)}";
                }

                popup.Show("Reward", message);
            }

            EventCenter.Broadcast(new ChestOpenedEvent(
                itemId,
                itemDisplayName,
                Mathf.Max(1, itemAmount),
                transform.position));
        }

        private void EnsurePrompt()
        {
            if (!showPrompt)
            {
                return;
            }

            Transform prompt = transform.Find("ChestPrompt");
            if (prompt == null)
            {
                GameObject go = new GameObject("ChestPrompt");
                go.transform.SetParent(transform, false);
                prompt = go.transform;
            }

            prompt.localPosition = promptOffset;
            prompt.localRotation = Quaternion.identity;
            prompt.localScale = Vector3.one;

            promptText = prompt.GetComponent<TextMesh>();
            if (promptText == null)
            {
                promptText = prompt.gameObject.AddComponent<TextMesh>();
            }

            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.characterSize = promptCharacterSize;
            promptText.fontSize = 64;
        }

        private void RefreshPrompt()
        {
            if (!showPrompt || promptText == null)
            {
                return;
            }

            bool visible = residentPlayer != null || (opened && canOpenOnlyOnce);
            if (!visible)
            {
                promptText.text = string.Empty;
                return;
            }

            if (opened && canOpenOnlyOnce)
            {
                promptText.text = openedPrompt;
                promptText.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                return;
            }

            promptText.text = closedPrompt;
            promptText.color = new Color(0.95f, 0.95f, 0.78f, 1f);
        }

        private void RefreshVisualState()
        {
            if (chestRenderer == null)
            {
                return;
            }

            chestRenderer.color = opened ? openedColor : closedColor;
        }
    }
}

