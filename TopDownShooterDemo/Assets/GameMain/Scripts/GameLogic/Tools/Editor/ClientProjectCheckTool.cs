using System.Collections.Generic;
using System.IO;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Projectiles;
using UnityEditor;
using UnityEngine;

namespace GameMain.GameLogic.Tools.Editor
{
    /// <summary>
    /// One-click editor report for client-demo scene/data/build integrity.
    /// </summary>
    public static class ClientProjectCheckTool
    {
        private static readonly string[] RequiredBuildScenes =
        {
            "Assets/Scenes/MainMenuScene.scene",
            "Assets/Scenes/CharacterSelectScene.scene",
            "Assets/Scenes/RunScene.scene",
            "Assets/Scenes/RunScene_Level2.scene",
        };

        [MenuItem("Tools/GameMain/Client Project Check/Run Full Check")]
        public static void RunFullCheck()
        {
            var report = new CheckReport();
            CheckSceneAssets(report);
            CheckCharacterConfigs(report);
            CheckObjectPoolPrefabs(report);
            CheckBuildSettings(report);
            report.Flush();
        }

        private static void CheckSceneAssets(CheckReport report)
        {
            report.Info("Scene reference check");
            for (var i = 0; i < RequiredBuildScenes.Length; i++)
            {
                var scenePath = RequiredBuildScenes[i];
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset == null)
                {
                    report.Error("Missing required scene asset: " + scenePath);
                    continue;
                }

                var sceneText = File.ReadAllText(scenePath);
                if (sceneText.Contains("m_Script: {fileID: 0}"))
                {
                    report.Warning("Scene has missing script references: " + scenePath);
                }

                report.Info("OK scene asset: " + scenePath);
            }

            CheckRunScenePoolNames(report);
        }

        private static void CheckRunScenePoolNames(CheckReport report)
        {
            const string runScenePath = "Assets/Scenes/RunScene.scene";
            if (!File.Exists(runScenePath))
            {
                return;
            }

            var runSceneText = File.ReadAllText(runScenePath);
            CheckSceneNameToken(report, runSceneText, "ProjectilePool", runScenePath);
            CheckSceneNameToken(report, runSceneText, "DamageTextSpawner", runScenePath);
            CheckSceneNameToken(report, runSceneText, "ImpactFlashEffectSpawner", runScenePath);
        }

        private static void CheckSceneNameToken(CheckReport report, string sceneText, string token, string scenePath)
        {
            if (sceneText.Contains(token))
            {
                report.Info("OK scene object token: " + token);
                return;
            }

            report.Warning("Scene may be missing object token '" + token + "': " + scenePath);
        }

        private static void CheckCharacterConfigs(CheckReport report)
        {
            report.Info("CharacterData check");
            var guids = AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/Resources/CharacterSelect" });
            if (guids.Length == 0)
            {
                report.Error("No CharacterData assets found under Assets/Resources/CharacterSelect.");
                return;
            }

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (data == null)
                {
                    report.Error("CharacterData load failed: " + path);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(data.characterId) || string.IsNullOrWhiteSpace(data.characterName))
                {
                    report.Warning("CharacterData identity incomplete: " + path);
                }

                if (data.redHealth <= 0 || data.energy < 0 || data.blueArmor < 0)
                {
                    report.Warning("CharacterData stats invalid: " + data.characterName + " at " + path);
                }

                if (string.IsNullOrWhiteSpace(data.initialWeapon1) || string.IsNullOrWhiteSpace(data.initialWeapon2))
                {
                    report.Warning("CharacterData initial weapons incomplete: " + data.characterName + " at " + path);
                }

                report.Info("OK character config: " + data.characterName);
            }
        }

        private static void CheckObjectPoolPrefabs(CheckReport report)
        {
            report.Info("Object pool prefab check");
            var projectilePrefabCount = 0;
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                if (prefab.GetComponentInChildren<Projectile>(true) != null)
                {
                    projectilePrefabCount++;
                    report.Info("OK projectile prefab: " + path);
                }
            }

            if (projectilePrefabCount == 0)
            {
                report.Warning("No projectile prefab with Projectile component found.");
            }

            CheckSpawnerTypePresent<DamageTextSpawner>(report, "DamageTextSpawner");
            CheckSpawnerTypePresent<ImpactFlashEffectSpawner>(report, "ImpactFlashEffectSpawner");
        }

        private static void CheckSpawnerTypePresent<T>(CheckReport report, string typeName) where T : Component
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren<T>(true) != null)
                {
                    report.Info("OK pooled spawner prefab component: " + typeName + " at " + path);
                    return;
                }
            }

            report.Info(typeName + " prefab component not found; RunScene scene object fallback is acceptable for current slice.");
        }

        private static void CheckBuildSettings(CheckReport report)
        {
            report.Info("Build Settings check");
            var enabledScenes = new HashSet<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    enabledScenes.Add(scenes[i].path);
                }
            }

            for (var i = 0; i < RequiredBuildScenes.Length; i++)
            {
                var scenePath = RequiredBuildScenes[i];
                if (enabledScenes.Contains(scenePath))
                {
                    report.Info("OK Build Settings scene: " + scenePath);
                }
                else
                {
                    report.Warning("Build Settings missing or disabled scene: " + scenePath);
                }
            }
        }

        private sealed class CheckReport
        {
            private readonly List<string> lines = new List<string>();
            private int warningCount;
            private int errorCount;

            public void Info(string message)
            {
                lines.Add("[OK] " + message);
            }

            public void Warning(string message)
            {
                warningCount++;
                lines.Add("[WARN] " + message);
            }

            public void Error(string message)
            {
                errorCount++;
                lines.Add("[ERROR] " + message);
            }

            public void Flush()
            {
                var summary = "Client Project Check complete. warnings=" + warningCount + " errors=" + errorCount;
                var body = string.Join("\n", lines);
                if (errorCount > 0)
                {
                    Debug.LogError(summary + "\n" + body);
                    return;
                }

                if (warningCount > 0)
                {
                    Debug.LogWarning(summary + "\n" + body);
                    return;
                }

                Debug.Log(summary + "\n" + body);
            }
        }
    }
}
