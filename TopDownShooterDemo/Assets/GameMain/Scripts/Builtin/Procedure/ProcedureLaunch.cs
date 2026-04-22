using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Startup procedure. Keep it short and jump to menu.
    /// </summary>
    public sealed class ProcedureLaunch : ProcedureBase
    {
        [SerializeField] private float launchDuration = 0.5f;

        private float countdown;

        public override ProcedureType ProcedureType => ProcedureType.Launch;

        public override void OnEnter()
        {
            countdown = Mathf.Max(0f, launchDuration);
            Debug.Log("ProcedureLaunch.Enter");
        }

        public override void OnUpdate(float deltaTime)
        {
            countdown -= deltaTime;
            if (countdown <= 0f)
            {
                Manager.ChangeProcedure(ProcedureType.Menu);
            }
        }
    }
}
