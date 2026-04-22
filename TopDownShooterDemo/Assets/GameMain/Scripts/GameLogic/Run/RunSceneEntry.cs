using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.Run
{
    /// <summary>
    /// Installs run bootstrap whenever RunScene is loaded.
    /// </summary>
    public static class RunSceneEntry
    {
        private const string RunSceneName = "RunScene";
        private static bool sceneCallbacksRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            sceneCallbacksRegistered = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallbacks()
        {
            if (sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneCallbacksRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunBootstrapForActiveScene()
        {
            EnsureRunBootstrap(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRunBootstrap(scene);
        }

        private static void EnsureRunBootstrap(Scene scene)
        {
            if (!scene.IsValid() ||
                !string.Equals(scene.name, RunSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<RunSceneSessionBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("RunSceneSessionBootstrap");
            bootstrapObject.AddComponent<RunSceneSessionBootstrap>();
        }
    }
}
