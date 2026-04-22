using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Lightweight runtime procedure manager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProcedureManager : MonoBehaviour
    {
        [SerializeField] private List<ProcedureBase> procedures = new List<ProcedureBase>();

        private readonly Dictionary<ProcedureType, ProcedureBase> procedureMap = new Dictionary<ProcedureType, ProcedureBase>();

        public ProcedureType CurrentProcedureType { get; private set; } = ProcedureType.None;
        public BattleResultType PendingBattleResult { get; private set; } = BattleResultType.None;
        public BattleResultType LastBattleResult { get; private set; } = BattleResultType.None;

        public ProcedureBase CurrentProcedure { get; private set; }

        public event Action<ProcedureType, ProcedureType> ProcedureChanged;

        public void Initialize()
        {
            procedureMap.Clear();

            if (procedures.Count == 0)
            {
                procedures.Clear();
                GetComponents(procedures);
            }

            foreach (var procedure in procedures)
            {
                Register(procedure);
            }
        }

        public void Register(ProcedureBase procedure)
        {
            if (procedure == null)
            {
                return;
            }

            var type = procedure.ProcedureType;
            if (type == ProcedureType.None)
            {
                Debug.LogWarning("ProcedureType.None cannot be registered.");
                return;
            }

            if (procedureMap.ContainsKey(type))
            {
                Debug.LogWarningFormat("Procedure '{0}' is already registered.", type);
                return;
            }

            procedure.Initialize(this);
            procedureMap.Add(type, procedure);
        }

        public bool ChangeProcedure(ProcedureType targetType)
        {
            if (!procedureMap.TryGetValue(targetType, out var target))
            {
                Debug.LogWarningFormat("Procedure '{0}' is not registered.", targetType);
                return false;
            }

            if (CurrentProcedure == target)
            {
                return true;
            }

            var previousType = CurrentProcedureType;
            CurrentProcedure?.OnExit();

            CurrentProcedure = target;
            CurrentProcedureType = targetType;
            CurrentProcedure.OnEnter();

            ProcedureChanged?.Invoke(previousType, targetType);
            return true;
        }

        public void SetPendingBattleResult(BattleResultType battleResult)
        {
            PendingBattleResult = battleResult;
        }

        public BattleResultType ConsumePendingBattleResult()
        {
            var result = PendingBattleResult;
            if (result != BattleResultType.None)
            {
                LastBattleResult = result;
            }

            PendingBattleResult = BattleResultType.None;
            return result;
        }

        private void Update()
        {
            CurrentProcedure?.OnUpdate(Time.deltaTime);
        }
    }
}
