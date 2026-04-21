// Path: Assets/_Scripts/Tools/SpriteImportSettings.cs

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARPGDemo.Tools
{
    /// <summary>
    /// Batch tool to apply consistent sprite import settings for selected folders.
    /// </summary>
    public static class SpriteImportSettings
    {
        private const string MenuPath = "Tools/ARPG Tools/Sprite/Apply 2D ARPG Import Settings";

        private const float DefaultPixelsPerUnit = 100f;
        private const string DefaultPackingTag = "ARPG_2D";
        private const int MaxTextureSize = 2048;
        private const int CompressionQuality = 50;

        [MenuItem(MenuPath, false, 1101)]
        public static void ApplySettingsToSelectedFolder()
        {
            try
            {
                string folderPath = ResolveSelectedFolderPath();
                if (string.IsNullOrEmpty(folderPath))
                {
                    EditorUtility.DisplayDialog(
                        "Sprite Import Settings",
                        "Please select a Sprite folder in Project window.",
                        "OK");
                    return;
                }

                string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                if (textureGuids == null || textureGuids.Length == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Sprite Import Settings",
                        $"No texture assets found in selected folder:\n{folderPath}",
                        "OK");
                    return;
                }

                int successCount = 0;
                int failedCount = 0;

                for (int i = 0; i < textureGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    float progress = (i + 1f) / textureGuids.Length;
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "Sprite Import Settings",
                        $"Processing: {Path.GetFileName(assetPath)}",
                        progress);

                    if (cancel)
                    {
                        Debug.LogWarning("[SpriteImportSettings] Batch process canceled by user.");
                        break;
                    }

                    try
                    {
                        if (ApplySingleTextureSettings(assetPath))
                        {
                            successCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception innerEx)
                    {
                        failedCount++;
                        Debug.LogError($"[SpriteImportSettings] Failed to process '{assetPath}': {innerEx}");
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Sprite Import Settings",
                    $"Batch sprite import settings applied.\nSuccess: {successCount}\nFailed: {failedCount}",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpriteImportSettings] Execution failed: {ex}");
                EditorUtility.DisplayDialog(
                    "Sprite Import Settings Error",
                    "Batch import settings failed. Check Console for details.",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateApplySettingsToSelectedFolder()
        {
            return !string.IsNullOrEmpty(ResolveSelectedFolderPath());
        }

        /// <summary>
        /// Applies sprite import settings to a single texture path.
        /// </summary>
        private static bool ApplySingleTextureSettings(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = DefaultPixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;

            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            importer.compressionQuality = CompressionQuality;

            importer.spritePackingTag = DefaultPackingTag;
#pragma warning restore CS0618

            ApplyPlatformSettings(importer, "DefaultTexturePlatform");
            ApplyPlatformSettings(importer, "Standalone");
            ApplyPlatformSettings(importer, "Android");
            ApplyPlatformSettings(importer, "iPhone");

            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Applies platform-specific texture settings.
        /// </summary>
        private static void ApplyPlatformSettings(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings
            {
                name = platformName
            };

            settings.overridden = true;
            settings.maxTextureSize = MaxTextureSize;
            settings.format = TextureImporterFormat.Automatic;
            settings.compressionQuality = CompressionQuality;

            importer.SetPlatformTextureSettings(settings);
        }

        /// <summary>
        /// Resolves selected folder path from Project window selection.
        /// </summary>
        private static string ResolveSelectedFolderPath()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            string dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
            {
                return string.Empty;
            }

            return dir.Replace("\\", "/");
        }
    }
}
#endif
