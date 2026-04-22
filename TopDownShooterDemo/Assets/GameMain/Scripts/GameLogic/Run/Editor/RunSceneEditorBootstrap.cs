using GameMain.GameLogic.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.Run.Editor
{
    [InitializeOnLoad]
    public static class RunSceneEditorBootstrap
    {
        private const string RunSceneName = "RunScene";

        static RunSceneEditorBootstrap()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += TryEnsureForActiveScene;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EnsureRunSceneLayout(scene);
        }

        private static void TryEnsureForActiveScene()
        {
            EnsureRunSceneLayout(SceneManager.GetActiveScene());
        }

        private static void EnsureRunSceneLayout(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(scene.name, RunSceneName, System.StringComparison.Ordinal))
            {
                return;
            }

            BossRushRuntimeSceneBuilder.BootstrapCurrentSceneInEditor();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
