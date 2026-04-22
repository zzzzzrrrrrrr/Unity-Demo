using GameMain.GameLogic.Tools;
using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Menu procedure. Waiting for input or external request to enter battle.
    /// </summary>
    public sealed class ProcedureMenu : ProcedureBase
    {
        [SerializeField] private bool enableAutoStartFromBattleConfig = true;
        [SerializeField] private float autoStartBattleDelay = 0.2f;
        [SerializeField] private bool enableKeyboardStartShortcut = false;
        [SerializeField] private bool requirePortalTokenForManualStart = true;

        private static bool autoStartConsumedThisSession;
        private static bool pendingPortalStartToken;
        private static string pendingPortalStartSource;

        private bool pendingAutoStartBattle;
        private float autoStartCountdown;
        private bool autoStartResolutionOpen;

        public override ProcedureType ProcedureType => ProcedureType.Menu;

        public override void OnEnter()
        {
            Debug.Log("ProcedureMenu.Enter");
            pendingAutoStartBattle = false;
            autoStartCountdown = 0f;
            autoStartResolutionOpen = !autoStartConsumedThisSession && enableAutoStartFromBattleConfig;
            TryScheduleAutoStart("OnEnter");
        }

        public override void OnUpdate(float deltaTime)
        {
            if (!pendingAutoStartBattle && autoStartResolutionOpen)
            {
                TryScheduleAutoStart("OnUpdate");
            }

            if (pendingAutoStartBattle)
            {
                autoStartCountdown -= Mathf.Max(0f, deltaTime);
                if (autoStartCountdown <= 0f)
                {
                    pendingAutoStartBattle = false;
                    StartBattleInternal(true, true, "auto");
                    return;
                }
            }

            if (enableKeyboardStartShortcut && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                StartBattle();
            }
        }

        public void StartBattle()
        {
            StartBattleInternal(false, false, "manual");
        }

        public void StartBattleFromPortal(string source)
        {
            pendingPortalStartToken = true;
            pendingPortalStartSource = string.IsNullOrWhiteSpace(source) ? "portal" : source;
            StartBattleInternal(false, true, pendingPortalStartSource);
        }

        private void StartBattleInternal(bool triggeredByAuto, bool isAutoOrPortalAuthorized, string sourceLabel)
        {
            if (!triggeredByAuto && requirePortalTokenForManualStart)
            {
                var authorized = isAutoOrPortalAuthorized || ConsumePortalStartToken();
                if (!authorized)
                {
                    Debug.Log("ProcedureMenu.StartBattle blocked: waiting for portal-triggered start token.");
                    return;
                }
            }

            autoStartConsumedThisSession = true;
            autoStartResolutionOpen = false;
            pendingPortalStartToken = false;
            pendingPortalStartSource = string.Empty;
            Debug.Log("ProcedureMenu.StartBattle source=" + (triggeredByAuto ? "auto" : sourceLabel));
            Manager.SetPendingBattleResult(BattleResultType.None);
            Manager.ChangeProcedure(ProcedureType.Battle);
        }

        private static bool ShouldAutoStartBattle()
        {
            var hooks = RuntimeSceneHooks.Active;
            return hooks != null && hooks.BattleConfig != null && hooks.BattleConfig.autoEnterBattleOnPlay;
        }

        private void TryScheduleAutoStart(string source)
        {
            if (pendingAutoStartBattle)
            {
                return;
            }

            if (autoStartConsumedThisSession)
            {
                autoStartResolutionOpen = false;
                Debug.Log("ProcedureMenu auto-start skipped (" + source + "): already consumed this play session.");
                return;
            }

            if (!enableAutoStartFromBattleConfig)
            {
                autoStartResolutionOpen = false;
                Debug.Log("ProcedureMenu auto-start skipped (" + source + "): disabled by config.");
                return;
            }

            if (!ShouldAutoStartBattle())
            {
                return;
            }

            pendingAutoStartBattle = true;
            autoStartCountdown = Mathf.Max(0f, autoStartBattleDelay);
            autoStartResolutionOpen = false;
            Debug.Log("ProcedureMenu auto-start battle scheduled (" + source + "). delay=" + autoStartCountdown.ToString("0.##") + "s");
        }

        private static bool ConsumePortalStartToken()
        {
            if (!pendingPortalStartToken)
            {
                return false;
            }

            pendingPortalStartToken = false;
            pendingPortalStartSource = string.Empty;
            return true;
        }
    }
}
