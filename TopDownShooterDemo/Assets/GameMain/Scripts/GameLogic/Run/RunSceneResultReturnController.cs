using GameMain.Builtin.Procedure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.Run
{
    /// <summary>
    /// Return to CharacterSelectScene after one run finishes and procedure exits Result.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunSceneResultReturnController : MonoBehaviour
    {
        [SerializeField] private ProcedureManager procedureManager;
        [SerializeField] private string characterSelectSceneName = "CharacterSelectScene";

        private bool seenResult;
        private bool loadingScene;

        public void Configure(ProcedureManager manager, string returnSceneName)
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            procedureManager = manager;
            characterSelectSceneName = string.IsNullOrWhiteSpace(returnSceneName)
                ? "CharacterSelectScene"
                : returnSceneName;
            seenResult = false;
            loadingScene = false;

            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }
        }

        private void OnDisable()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            if (current == ProcedureType.Result)
            {
                seenResult = true;
                return;
            }

            if (!seenResult || loadingScene || current != ProcedureType.Menu)
            {
                return;
            }

            loadingScene = true;
            SceneManager.LoadScene(characterSelectSceneName);
        }
    }
}
