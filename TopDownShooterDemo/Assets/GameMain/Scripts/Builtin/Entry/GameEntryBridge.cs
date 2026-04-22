using GameMain.Builtin.Procedure;

namespace GameMain.Builtin.Entry
{
    /// <summary>
    /// Minimal bridge layer so gameplay code can query or switch procedures
    /// without directly depending on scene object lookups.
    /// </summary>
    public static class GameEntryBridge
    {
        public static bool IsReady
        {
            get
            {
                return GameEntry.Instance != null && GameEntry.Instance.ProcedureManager != null;
            }
        }

        public static ProcedureManager Procedure
        {
            get
            {
                return GameEntry.Instance != null ? GameEntry.Instance.ProcedureManager : null;
            }
        }

        public static void SwitchProcedure(ProcedureType procedureType)
        {
            if (!IsReady)
            {
                return;
            }

            GameEntry.Instance.SwitchProcedure(procedureType);
        }
    }
}
