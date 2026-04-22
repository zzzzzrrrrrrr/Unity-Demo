using System;
using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Post-battle procedure. Press Enter or Space to return menu.
    /// </summary>
    public sealed class ProcedureResult : ProcedureBase
    {
        [SerializeField] private float autoBackToMenuDelay = -1f;

        private float countdown;

        public BattleResultType CurrentResult { get; private set; } = BattleResultType.None;

        /// <summary>
        /// Static entry for future ui panel pull-mode usage.
        /// </summary>
        public static BattleResultType LastRecordedResult { get; private set; } = BattleResultType.None;

        public event Action<BattleResultType> ResultEntered;

        public override ProcedureType ProcedureType => ProcedureType.Result;

        public override void OnEnter()
        {
            countdown = autoBackToMenuDelay;
            CurrentResult = Manager.ConsumePendingBattleResult();
            if (CurrentResult == BattleResultType.None)
            {
                CurrentResult = Manager.LastBattleResult;
            }

            LastRecordedResult = CurrentResult;
            ResultEntered?.Invoke(CurrentResult);
            Debug.LogFormat("ProcedureResult.Enter ({0})", CurrentResult);
        }

        public override void OnUpdate(float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                Manager.ChangeProcedure(ProcedureType.Menu);
                return;
            }

            if (countdown > 0f)
            {
                countdown -= deltaTime;
                if (countdown <= 0f)
                {
                    Manager.ChangeProcedure(ProcedureType.Menu);
                }
            }
        }
    }
}
