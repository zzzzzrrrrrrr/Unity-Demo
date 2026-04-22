using GameMain.Builtin.Procedure;
using UnityEngine;

namespace GameMain.Builtin.Entry
{
    /// <summary>
    /// Runtime entry point for the lightweight demo skeleton.
    /// It bridges scene objects to procedure flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameEntry : MonoBehaviour
    {
        [SerializeField] private ProcedureManager procedureManager;
        [SerializeField] private bool autoStartLaunch = true;
        [SerializeField] private bool persistAcrossScenes = false;

        public static GameEntry Instance { get; private set; }

        public ProcedureManager ProcedureManager => procedureManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (procedureManager == null)
            {
                procedureManager = GetComponent<ProcedureManager>();
            }

            if (procedureManager == null)
            {
                procedureManager = gameObject.AddComponent<ProcedureManager>();
            }

            procedureManager.Initialize();
        }

        private void Start()
        {
            if (autoStartLaunch)
            {
                SwitchProcedure(ProcedureType.Launch);
            }
        }

        public void SwitchProcedure(ProcedureType procedureType)
        {
            if (procedureManager == null)
            {
                Debug.LogWarning("ProcedureManager is missing.");
                return;
            }

            procedureManager.ChangeProcedure(procedureType);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
