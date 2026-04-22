using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.CharacterSelect.Editor
{
    [InitializeOnLoad]
    public static class CharacterSelectSceneEditorBootstrap
    {
        private const string SceneName = "CharacterSelectScene";
        private const string BootstrapObjectName = "CharacterSelectSceneBootstrap";

        static CharacterSelectSceneEditorBootstrap()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += TryEnsureForActiveScene;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EnsureSceneLayout(scene, false);
        }

        private static void TryEnsureForActiveScene()
        {
            EnsureSceneLayout(SceneManager.GetActiveScene(), false);
        }

        [MenuItem("Tools/GameMain/Character Select/Repair Active Scene Layout")]
        public static void RepairActiveSceneLayout()
        {
            EnsureSceneLayout(SceneManager.GetActiveScene(), true);
        }

        private static void EnsureSceneLayout(Scene scene, bool logResult)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(scene.name, SceneName, System.StringComparison.Ordinal) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (logResult)
                {
                    Debug.LogWarning(
                        "Repair skipped. Open CharacterSelectScene first, then run this menu command again.");
                }

                return;
            }

            var bootstrap = FindBootstrapInScene(scene);
            if (bootstrap == null)
            {
                var bootstrapObject = new GameObject(BootstrapObjectName);
                bootstrap = bootstrapObject.AddComponent<CharacterSelectSceneBootstrap>();
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            }

            bootstrap.EnsureEditorLayout();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (logResult)
            {
                Debug.Log("CharacterSelect scene layout repaired and saved.");
            }
        }

        private static CharacterSelectSceneBootstrap FindBootstrapInScene(Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootObjects.Length; i++)
            {
                var root = rootObjects[i];
                if (root == null)
                {
                    continue;
                }

                var bootstrap = root.GetComponentInChildren<CharacterSelectSceneBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
        }
    }
}
