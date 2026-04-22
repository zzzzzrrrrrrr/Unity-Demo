using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Player;
using UnityEngine;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Lightweight debug panel for procedure and combat quick checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugPanelController : MonoBehaviour
    {
        [SerializeField] private bool showPanel = false;
        [SerializeField] private Rect panelRect = new Rect(12f, 12f, 260f, 220f);
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private PlayerHealth cachedPlayerHealth;
        private BossHealth cachedBossHealth;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showPanel = !showPanel;
            }
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Boss Rush Debug");

            if (GameEntryBridge.IsReady)
            {
                GUILayout.Label("Procedure: " + GameEntryBridge.Procedure.CurrentProcedureType);
                GUILayout.Label("Last Result: " + GameEntryBridge.Procedure.LastBattleResult);
            }
            else
            {
                GUILayout.Label("Procedure: (GameEntry missing)");
            }

            DrawProcedureButtons();
            DrawHealthButtons();

            GUILayout.EndArea();
        }

        private void DrawProcedureButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Launch);
            }

            if (GUILayout.Button("Menu"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Menu);
            }

            if (GUILayout.Button("Battle"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Battle);
            }

            if (GUILayout.Button("Result"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Result);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawHealthButtons()
        {
            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = Object.FindObjectOfType<PlayerHealth>();
            }

            if (cachedBossHealth == null)
            {
                cachedBossHealth = Object.FindObjectOfType<BossHealth>();
            }

            if (cachedPlayerHealth != null)
            {
                GUILayout.Label(string.Format("Player HP: {0:0}/{1:0}", cachedPlayerHealth.CurrentHealth, cachedPlayerHealth.MaxHealth));
            }

            if (cachedBossHealth != null)
            {
                GUILayout.Label(string.Format("Boss HP: {0:0}/{1:0}", cachedBossHealth.CurrentHealth, cachedBossHealth.MaxHealth));
            }

            if (GUILayout.Button("Reset Health"))
            {
                cachedPlayerHealth?.ResetHealth();
                cachedBossHealth?.ResetHealth();
            }
        }
    }
}
