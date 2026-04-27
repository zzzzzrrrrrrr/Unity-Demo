using System.IO;
using GameMain.GameLogic.ResourceLoading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.Tools.Editor
{
    /// <summary>
    /// Editor-only builder for the isolated AssetBundle loading demo.
    /// </summary>
    public static class AssetBundleDemoBuilder
    {
        private const string DemoRootFolder = "Assets/GameMain/AssetBundleDemo";
        private const string DemoPrefabFolder = DemoRootFolder + "/Prefabs";
        private const string DemoPrefabPath = DemoPrefabFolder + "/AssetBundleDemo_CharacterPlaceholder.prefab";
        private const string StreamingAssetsFolder = "Assets/StreamingAssets";
        private const string BundleOutputFolder = StreamingAssetsFolder + "/AssetBundleDemo";
        private const string BundleName = "client_demo_assets";

        [MenuItem("Tools/GameMain/AssetBundle Demo/Build Demo Bundle")]
        public static void BuildDemoBundle()
        {
            EnsureFolders();
            EnsureDemoPrefab();
            AssignBundleName();

            var manifest = BuildPipeline.BuildAssetBundles(
                BundleOutputFolder,
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget);

            AssetDatabase.Refresh();
            if (manifest == null)
            {
                Debug.LogError("AssetBundle demo build failed.");
                return;
            }

            Debug.Log("AssetBundle demo built: " + Path.Combine(BundleOutputFolder, BundleName).Replace("\\", "/"));
        }

        [MenuItem("Tools/GameMain/AssetBundle Demo/Create Runtime Demo In Active Scene")]
        public static void CreateRuntimeDemoInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("Open a scene before creating the AssetBundle demo runner.");
                return;
            }

            var runner = GameObject.Find("AssetBundleLoadDemo");
            if (runner == null)
            {
                runner = new GameObject("AssetBundleLoadDemo");
                Undo.RegisterCreatedObjectUndo(runner, "Create AssetBundleLoadDemo");
            }

            if (runner.GetComponent<AssetBundleLoadDemoController>() == null)
            {
                Undo.AddComponent<AssetBundleLoadDemoController>(runner);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = runner;
            Debug.Log("AssetBundleLoadDemo runner is ready. Press Play and use its IMGUI panel.");
        }

        private static void EnsureDemoPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(DemoPrefabPath);
                EnsurePlaceholderComponents(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, DemoPrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
                AssetDatabase.SaveAssets();
                return;
            }

            var template = new GameObject("AssetBundleDemo_CharacterPlaceholder");
            EnsurePlaceholderComponents(template);
            PrefabUtility.SaveAsPrefabAsset(template, DemoPrefabPath);
            Object.DestroyImmediate(template);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsurePlaceholderComponents(GameObject target)
        {
            if (target.GetComponent<SpriteRenderer>() == null)
            {
                target.AddComponent<SpriteRenderer>();
            }

            if (target.GetComponent<AssetBundleDemoPlaceholderVisual>() == null)
            {
                target.AddComponent<AssetBundleDemoPlaceholderVisual>();
            }

            target.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
        }

        private static void AssignBundleName()
        {
            var importer = AssetImporter.GetAtPath(DemoPrefabPath);
            if (importer == null)
            {
                Debug.LogError("AssetBundle demo prefab importer missing: " + DemoPrefabPath);
                return;
            }

            importer.assetBundleName = BundleName;
            importer.SaveAndReimport();
        }

        private static void EnsureFolders()
        {
            EnsureFolder(DemoRootFolder);
            EnsureFolder(DemoPrefabFolder);
            EnsureFolder(StreamingAssetsFolder);
            EnsureFolder(BundleOutputFolder);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
