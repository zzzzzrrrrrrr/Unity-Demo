// Path: Assets/_Scripts/UI/DemoInventoryPanel.cs
// Minimal inventory panel (toggle + fixed slots) for demo showcase.
using System.Collections.Generic;
using ARPGDemo.Game;
using ARPGDemo.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.UI
{
    [DisallowMultipleComponent]
    public class DemoInventoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private List<Text> slotTexts = new List<Text>(8);
        [SerializeField] private KeyCode toggleKey = KeyCode.R;
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private int slotCount = 8;
        [SerializeField] private bool handleHotkeyInSelf = true;

        private DemoInventoryService inventoryService;
        private bool hasAwakened;
        private bool suppressHideOnAwakeOnce;

        private void Awake()
        {
            toggleKey = KeyCode.R;

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            ResolveSlotReferencesIfMissing();

            if (hideOnAwake && !suppressHideOnAwakeOnce)
            {
                panelRoot.SetActive(false);
            }

            hasAwakened = true;
            suppressHideOnAwakeOnce = false;

            TryResolveInventoryService();
            SubscribeInventory();
            RefreshSlots();
        }

        private void ResolveSlotReferencesIfMissing()
        {
            if (slotsRoot == null)
            {
                Transform found = transform.Find("SlotsRoot");
                if (found != null)
                {
                    slotsRoot = found;
                }
            }

            if (slotTexts != null && slotTexts.Count > 0)
            {
                return;
            }

            slotTexts = new List<Text>(8);
            Transform searchRoot = slotsRoot != null ? slotsRoot : transform;
            Text[] all = searchRoot.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Text t = all[i];
                if (t == null)
                {
                    continue;
                }

                if (t.name.StartsWith("Slot_"))
                {
                    slotTexts.Add(t);
                }
            }

            if (slotTexts.Count <= 1)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Text t = all[i];
                    if (t != null && !slotTexts.Contains(t))
                    {
                        slotTexts.Add(t);
                    }
                }
            }
        }

        private void OnEnable()
        {
            SubscribeInventory();
            RefreshSlots();
        }

        private void OnDisable()
        {
            UnsubscribeInventory();
        }

        private void Update()
        {
            // When a global controller handles inventory hotkey, disable local polling
            // to avoid duplicate toggles in the same frame.
            if (!handleHotkeyInSelf)
            {
                return;
            }

            if (InputCompat.IsDown(toggleKey))
            {
                TogglePanel();
            }
        }

        public void ConfigureRuntime(
            GameObject root,
            Transform runtimeSlotsRoot,
            List<Text> runtimeSlotTexts,
            DemoInventoryService service,
            KeyCode runtimeToggleKey,
            int runtimeSlotCount)
        {
            panelRoot = root != null ? root : gameObject;
            slotsRoot = runtimeSlotsRoot;
            slotTexts = runtimeSlotTexts ?? new List<Text>(8);
            inventoryService = service;
            toggleKey = runtimeToggleKey;
            slotCount = Mathf.Max(1, runtimeSlotCount);

            SubscribeInventory();
            RefreshSlots();
        }

        public void SetSelfHotkeyEnabled(bool enabled)
        {
            handleHotkeyInSelf = enabled;
        }

        public void TogglePanel()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelRoot != null)
            {
                bool willOpen = !panelRoot.activeSelf;
                if (willOpen && !hasAwakened)
                {
                    suppressHideOnAwakeOnce = true;
                }

                panelRoot.SetActive(willOpen);
            }

            RefreshSlots();
        }

        public void OpenPanel()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelRoot != null)
            {
                if (!panelRoot.activeSelf && !hasAwakened)
                {
                    suppressHideOnAwakeOnce = true;
                }

                panelRoot.SetActive(true);
            }

            RefreshSlots();
        }

        public void ClosePanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void TryResolveInventoryService()
        {
            if (inventoryService != null)
            {
                return;
            }

            DemoInventoryService[] all = FindObjectsOfType<DemoInventoryService>(true);
            if (all != null && all.Length > 0)
            {
                inventoryService = all[0];
            }
        }

        private void SubscribeInventory()
        {
            TryResolveInventoryService();
            if (inventoryService == null)
            {
                return;
            }

            inventoryService.OnInventoryChanged -= RefreshSlots;
            inventoryService.OnInventoryChanged += RefreshSlots;
        }

        private void UnsubscribeInventory()
        {
            if (inventoryService == null)
            {
                return;
            }

            inventoryService.OnInventoryChanged -= RefreshSlots;
        }

        private void RefreshSlots()
        {
            if (slotTexts == null || slotTexts.Count == 0)
            {
                return;
            }

            IReadOnlyList<DemoInventoryService.InventoryItem> items = inventoryService != null
                ? inventoryService.Items
                : null;

            int safeSlots = Mathf.Max(1, slotCount);
            for (int i = 0; i < slotTexts.Count; i++)
            {
                Text label = slotTexts[i];
                if (label == null)
                {
                    continue;
                }

                if (i >= safeSlots)
                {
                    label.text = string.Empty;
                    continue;
                }

                if (items != null && i < items.Count && items[i] != null)
                {
                    DemoInventoryService.InventoryItem item = items[i];
                    label.text = $"{i + 1}. {item.displayName} x{item.amount}";
                }
                else
                {
                    label.text = $"{i + 1}. (empty)";
                }
            }
        }
    }
}
