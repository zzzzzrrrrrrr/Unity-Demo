namespace GameMain.Builtin.Procedure
{
    public interface IGameProcedure
    {
        ProcedureType ProcedureType { get; }

        void Initialize(ProcedureManager manager);

        void OnEnter();

        void OnUpdate(float deltaTime);

        void OnExit();
    }
}
