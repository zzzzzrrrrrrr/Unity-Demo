using System.Collections.Generic;
using UnityEngine;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Small UGUI panel registry for demo panels. It does not own gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        public const string CombatInfoPanelKey = "CombatInfoPanel";

        [SerializeField] private KeyCode combatInfoToggleKey = KeyCode.I;

        private readonly Dictionary<string, BasePanel> panels = new Dictionary<string, BasePanel>();

        private void Update()
        {
            if (Input.GetKeyDown(combatInfoToggleKey))
            {
                TogglePanel(CombatInfoPanelKey);
            }
        }

        public void RegisterPanel(string key, BasePanel panel)
        {
            if (string.IsNullOrWhiteSpace(key) || panel == null)
            {
                return;
            }

            panels[key] = panel;
        }

        public void ShowPanel(string key)
        {
            if (TryGetPanel(key, out var panel))
            {
                panel.Show();
            }
        }

        public void HidePanel(string key)
        {
            if (TryGetPanel(key, out var panel))
            {
                panel.Hide();
            }
        }

        public void TogglePanel(string key)
        {
            if (TryGetPanel(key, out var panel))
            {
                panel.Toggle();
            }
        }

        private bool TryGetPanel(string key, out BasePanel panel)
        {
            panel = null;
            return !string.IsNullOrWhiteSpace(key) && panels.TryGetValue(key, out panel) && panel != null;
        }
    }
}
