using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Shared base type for demo procedures.
    /// </summary>
    public abstract class ProcedureBase : MonoBehaviour, IGameProcedure
    {
        protected ProcedureManager Manager { get; private set; }

        public abstract ProcedureType ProcedureType { get; }

        public virtual void Initialize(ProcedureManager manager)
        {
            Manager = manager;
        }

        public virtual void OnEnter()
        {
        }

        public virtual void OnUpdate(float deltaTime)
        {
        }

        public virtual void OnExit()
        {
        }
    }
}
