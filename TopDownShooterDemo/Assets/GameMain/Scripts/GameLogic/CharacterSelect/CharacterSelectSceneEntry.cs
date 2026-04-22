using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Auto-installs bootstrap runtime object only in CharacterSelectScene.
    /// </summary>
    public static class CharacterSelectSceneEntry
    {
        private const string SceneName = "CharacterSelectScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExists()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() ||
                !string.Equals(activeScene.name, SceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<CharacterSelectSceneBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("CharacterSelectSceneBootstrap");
            bootstrapObject.AddComponent<CharacterSelectSceneBootstrap>();
        }
    }
}
