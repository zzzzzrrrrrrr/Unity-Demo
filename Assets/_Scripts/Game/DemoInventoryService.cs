// Path: Assets/_Scripts/Game/DemoInventoryService.cs
// Minimal reusable inventory data service for demo interactions.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    public class DemoInventoryService : MonoBehaviour
    {
        [Serializable]
        public class InventoryItem
        {
            public string itemId;
            public string displayName;
            public int amount;
        }

        [SerializeField] private List<InventoryItem> items = new List<InventoryItem>(8);
        [SerializeField] private int maxSlots = 8;

        public event Action OnInventoryChanged;

        public IReadOnlyList<InventoryItem> Items => items;
        public int MaxSlots => Mathf.Max(1, maxSlots);

        public bool AddItem(string itemId, string displayName, int amount)
        {
            int safeAmount = Mathf.Max(1, amount);
            string safeId = string.IsNullOrEmpty(itemId) ? "item_unknown" : itemId;
            string safeName = string.IsNullOrEmpty(displayName) ? safeId : displayName;

            for (int i = 0; i < items.Count; i++)
            {
                InventoryItem item = items[i];
                if (item != null && item.itemId == safeId)
                {
                    item.amount += safeAmount;
                    NotifyChanged();
                    return true;
                }
            }

            if (items.Count >= MaxSlots)
            {
                return false;
            }

            items.Add(new InventoryItem
            {
                itemId = safeId,
                displayName = safeName,
                amount = safeAmount
            });
            NotifyChanged();
            return true;
        }

        public void ClearAll()
        {
            items.Clear();
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnInventoryChanged?.Invoke();
        }
    }
}
