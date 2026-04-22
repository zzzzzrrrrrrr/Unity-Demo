namespace GameMain.Builtin.Event
{
    /// <summary>
    /// Reserved event id slots for future event bus integration.
    /// </summary>
    public static class EventIds
    {
        public const int ProcedureEnter = 10001;
        public const int ProcedureExit = 10002;
        public const int BattleStart = 11001;
        public const int BattleFinish = 11002;
        public const int PlayerDied = 12001;
        public const int BossDefeated = 12002;
    }
}
