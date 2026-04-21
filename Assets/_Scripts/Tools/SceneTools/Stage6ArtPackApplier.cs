// 路径: Assets/_Scripts/Tools/SceneTools/Stage6ArtPackApplier.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ARPGDemo.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Tools.SceneTools
{
    /// <summary>
    /// Stage 6C-2 美术可读性收口工具。
    /// 范围：
    /// 1) 地面 Spawn/Tutorial/Mid/Final（可选 Goal）贴图拼装
    /// 2) FinishZone 传送门拼装
    /// 3) 边界清晰化 + DropZone + FG_Haze + 少量装饰
    /// 4) Enemy_01~Enemy_04 代理美术整理（重点 Enemy_04）
    ///
    /// 限制：
    /// - 不改 collider / hitbox / AI / combat 逻辑
    /// - 不手改 scene YAML 引用
    /// - 仅通过 Editor API 赋值
    /// </summary>
    public static class Stage6ArtPackApplier
    {
        private const string MenuPath = "Tools/ARPG/Apply Stage6 Art Pack";
        private const string ScenePathScene = "Assets/Scenes/SampleScene.scene";
        private const string ScenePathUnity = "Assets/Scenes/SampleScene.unity";

        private const string GroundTilesPath = "Assets/_Art/Generated/Ground/tiles_ruins_white.png";
        private const string FinishSetPath = "Assets/_Art/Generated/FinishZone/finish_portal_set.png";
        private const string PropsSetPath = "Assets/_Art/Generated/Props/props_ruins_boundary_set.png";
        private const float ManualBoundaryLeftX = -15.5f;
        private const float ManualBoundaryRightX = 18f;
        private const float ManualBoundaryLocalY = 0f;
        private static readonly Vector2 ManualBoundaryColliderSize = new Vector2(1f, 8f);

        [MenuItem(MenuPath, false, 2002)]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            StringBuilder log = new StringBuilder(4096);
            int warningCount = 0;

            try
            {
                Scene scene = OpenSampleScene(log, ref warningCount);
                if (!scene.IsValid())
                {
                    Debug.LogError("[Stage6ArtPack] SampleScene is invalid.");
                    return;
                }

                SceneManager.SetActiveScene(scene);

                ConfigureMultipleGridSprite(GroundTilesPath, 256, 256, "ground_tile", log, ref warningCount);
                ConfigureMultipleGridSprite(FinishSetPath, 256, 256, "finish_part", log, ref warningCount);
                ConfigureMultipleGridSprite(PropsSetPath, 256, 256, "props_part", log, ref warningCount);

                List<Sprite> groundSprites = LoadSprites(GroundTilesPath);
                List<Sprite> finishSprites = LoadSprites(FinishSetPath);
                List<Sprite> propsSprites = LoadSprites(PropsSetPath);

                Transform spawnVisual = EnsurePath(scene, "Ground/Ground_Spawn/Visual", log);
                Transform tutorialVisual = EnsurePath(scene, "Ground/Ground_Tutorial/Visual", log);
                Transform midVisual = EnsurePath(scene, "Ground/Ground_Mid/Visual", log);
                Transform finalVisual = EnsurePath(scene, "Ground/Ground_Final/Visual", log);
                Transform goalVisual = FindPath(scene, "Ground/Ground_Goal/Visual");

                ApplyGroundSegmentTileAssembly(spawnVisual, groundSprites, "Spawn", -18, 6, log, ref warningCount);
                ApplyGroundSegmentTileAssembly(tutorialVisual, groundSprites, "Tutorial", -18, 5, log, ref warningCount);
                ApplyGroundSegmentTileAssembly(midVisual, groundSprites, "Mid", -18, 10, log, ref warningCount);
                ApplyGroundSegmentTileAssembly(finalVisual, groundSprites, "Final", -18, 6, log, ref warningCount);
                if (goalVisual != null)
                {
                    ApplyGroundSegmentTileAssembly(goalVisual, groundSprites, "Goal", -18, 4, log, ref warningCount);
                }
                else
                {
                    log.AppendLine("[INFO] Ground_Goal/Visual not found, skip optional goal assembly.");
                }

                ApplyFinishZoneAssembly(scene, finishSprites, log, ref warningCount);
                ApplyBoundaryAssembly(scene, propsSprites, log, ref warningCount);
                ApplyAtmosphereAssembly(scene, groundSprites, propsSprites, log, ref warningCount);
                ApplyEnemyVisualProxies(scene, propsSprites, log, ref warningCount);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[Stage6ArtPack] Apply done.\n" + log + "\nWarnings: " + warningCount);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Stage6ArtPack] Apply failed:\n" + ex);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateApply()
        {
            return !EditorApplication.isPlaying;
        }

        private static Scene OpenSampleScene(StringBuilder log, ref int warnings)
        {
            string path = ResolveSampleScenePath();
            if (!File.Exists(path))
            {
                warnings++;
                log.AppendLine("[WARN] SampleScene not found: " + path);
                return default;
            }

            log.AppendLine("[INFO] Open scene: " + path);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static string ResolveSampleScenePath()
        {
            if (File.Exists(ScenePathScene)) return ScenePathScene;
            if (File.Exists(ScenePathUnity)) return ScenePathUnity;

            string[] guids = AssetDatabase.FindAssets("SampleScene t:Scene");
            if (guids.Length > 0)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (!string.IsNullOrEmpty(p))
                {
                    return p;
                }
            }

            return ScenePathScene;
        }

        private static void ConfigureMultipleGridSprite(
            string path,
            int cellWidth,
            int cellHeight,
            string spriteNamePrefix,
            StringBuilder log,
            ref int warnings)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importer == null || tex == null)
            {
                warnings++;
                log.AppendLine("[WARN] Missing texture/importer: " + path);
                return;
            }

            int cols = tex.width / cellWidth;
            int rows = tex.height / cellHeight;
            if (cols <= 0 || rows <= 0)
            {
                warnings++;
                log.AppendLine("[WARN] Texture too small for slicing: " + path);
                return;
            }

            List<SpriteMetaData> metas = new List<SpriteMetaData>(cols * rows);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int x = col * cellWidth;
                    int y = tex.height - (row + 1) * cellHeight;
                    metas.Add(new SpriteMetaData
                    {
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        rect = new Rect(x, y, cellWidth, cellHeight),
                        name = $"{spriteNamePrefix}_{row:D2}_{col:D2}"
                    });
                }
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                importer.spritePixelsPerUnit = 100f;
                changed = true;
            }

#pragma warning disable 618
            importer.spritesheet = metas.ToArray();
#pragma warning restore 618
            changed = true;

            if (changed)
            {
                importer.SaveAndReimport();
                log.AppendLine("[UPDATE] Multiple slice import: " + path + $" ({cols}x{rows})");
            }
        }

        private static List<Sprite> LoadSprites(string path)
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToList();
        }

        private static void ApplyGroundSegmentTileAssembly(
            Transform visualRoot,
            List<Sprite> groundSprites,
            string sectionName,
            int sortingOrder,
            int pieceCount,
            StringBuilder log,
            ref int warnings)
        {
            if (visualRoot == null)
            {
                warnings++;
                log.AppendLine("[WARN] Missing visual root for section: " + sectionName);
                return;
            }

            if (groundSprites == null || groundSprites.Count == 0)
            {
                warnings++;
                log.AppendLine("[WARN] No sliced sprites for ground tiles.");
                return;
            }

            Transform segmentRoot = visualRoot.parent;
            BoxCollider2D segmentCollider = segmentRoot != null ? segmentRoot.GetComponent<BoxCollider2D>() : null;
            float segmentWidth = segmentCollider != null ? Mathf.Max(1f, segmentCollider.size.x) : 4f;
            float segmentHeight = segmentCollider != null ? Mathf.Max(0.5f, segmentCollider.size.y) : 1f;

            SpriteRenderer oldRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (oldRenderer != null)
            {
                oldRenderer.sprite = null;
                oldRenderer.enabled = false;
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            Sprite edgeLeft = PickSprite(groundSprites, 0);
            Sprite edgeRight = PickSprite(groundSprites, 1);
            Sprite main = PickSprite(groundSprites, 9);
            Sprite broken = PickSprite(groundSprites, 22);
            Sprite variation = PickSprite(groundSprites, 14);

            pieceCount = Mathf.Max(2, pieceCount);
            Sprite[] palette = BuildGroundPalette(edgeLeft, edgeRight, main, broken, variation, pieceCount);
            float pieceWidth = segmentWidth / pieceCount;
            float startX = -segmentWidth * 0.5f + pieceWidth * 0.5f;

            for (int i = 0; i < pieceCount; i++)
            {
                Sprite partSprite = palette[i];
                if (partSprite == null)
                {
                    continue;
                }

                Transform tile = EnsureChild(visualRoot, "Tile_" + i.ToString("00"), log);
                float spriteWidth = Mathf.Max(0.01f, partSprite.rect.width / partSprite.pixelsPerUnit);
                float spriteHeight = Mathf.Max(0.01f, partSprite.rect.height / partSprite.pixelsPerUnit);
                float scaleX = pieceWidth / spriteWidth;
                float scaleY = segmentHeight / spriteHeight;

                tile.localPosition = new Vector3(startX + pieceWidth * i, 0f, 0f);
                tile.localRotation = Quaternion.identity;
                tile.localScale = new Vector3(scaleX, scaleY, 1f);

                AssignSprite(tile, partSprite, sortingOrder, log, "Ground_" + sectionName + "/Tile_" + i.ToString("00"));
            }

            DisableExtraTiles(visualRoot, pieceCount, log);
            log.AppendLine("[INFO] Ground " + sectionName + " tile assembly applied. pieces=" + pieceCount);
        }

        private static Sprite[] BuildGroundPalette(
            Sprite edgeLeft,
            Sprite edgeRight,
            Sprite main,
            Sprite broken,
            Sprite variation,
            int pieceCount)
        {
            Sprite[] palette = new Sprite[Mathf.Max(1, pieceCount)];
            if (pieceCount == 1)
            {
                palette[0] = main ?? edgeLeft ?? variation ?? broken;
                return palette;
            }

            palette[0] = edgeLeft ?? main;
            palette[pieceCount - 1] = edgeRight ?? main ?? edgeLeft;

            for (int i = 1; i < pieceCount - 1; i++)
            {
                if (i % 3 == 0)
                {
                    palette[i] = broken ?? main ?? variation;
                }
                else if (i % 2 == 0)
                {
                    palette[i] = variation ?? main ?? broken;
                }
                else
                {
                    palette[i] = main ?? variation ?? broken;
                }
            }

            return palette;
        }

        private static void DisableExtraTiles(Transform visualRoot, int keepCount, StringBuilder log)
        {
            if (visualRoot == null)
            {
                return;
            }

            for (int i = 0; i < visualRoot.childCount; i++)
            {
                Transform child = visualRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("Tile_"))
                {
                    continue;
                }

                string indexStr = child.name.Substring(5);
                int index;
                if (!int.TryParse(indexStr, out index))
                {
                    continue;
                }

                bool shouldBeActive = index < keepCount;
                if (child.gameObject.activeSelf != shouldBeActive)
                {
                    child.gameObject.SetActive(shouldBeActive);
                    log.AppendLine("[UPDATE] " + child.name + " active=" + shouldBeActive);
                }
            }
        }

        private static void ApplyFinishZoneAssembly(Scene scene, List<Sprite> finishSprites, StringBuilder log, ref int warnings)
        {
            if (finishSprites == null || finishSprites.Count == 0)
            {
                warnings++;
                log.AppendLine("[WARN] No sliced sprites for finish portal.");
                return;
            }

            Transform visual = EnsurePath(scene, "Ground/FinishZone/FinishZone_Visual", log);
            Transform glow = EnsurePath(scene, "Ground/FinishZone/FinishZone_Glow", log);
            Transform label = EnsurePath(scene, "Ground/FinishZone/FinishZone_Label", log);

            Sprite frame = PickSprite(finishSprites, 0);
            Sprite core = PickSprite(finishSprites, 10);
            Sprite basePart = PickSprite(finishSprites, 20);
            Sprite sign = PickSprite(finishSprites, 3);

            SetLocalTRS(visual, Vector3.zero, new Vector3(0.24f, 0.24f, 1f), false);
            SetLocalTRS(glow, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.24f, 1f), false);
            SetLocalTRS(label, new Vector3(0f, 1.1f, 0f), new Vector3(0.2f, 0.2f, 1f), false);

            AssignSprite(visual, frame, 6, log, "FinishZone_Visual(Frame)");
            AssignSprite(glow, core, 7, log, "FinishZone_Glow(Core)");
            AssignSprite(label, sign, 8, log, "FinishZone_Label(Sign)");
            SetRendererAlpha(glow, 0.85f);

            Transform baseNode = EnsurePath(scene, "Ground/FinishZone/FinishZone_Visual/Portal_Base", log);
            SetLocalTRS(baseNode, new Vector3(0f, -0.9f, 0f), new Vector3(0.3f, 0.2f, 1f), false);
            AssignSprite(baseNode, basePart, 5, log, "FinishZone_Visual/Portal_Base");

            log.AppendLine("[INFO] FinishZone assembly applied: frame/core/base/sign.");
        }

        private static void ApplyBoundaryAssembly(Scene scene, List<Sprite> propsSprites, StringBuilder log, ref int warnings)
        {
            if (propsSprites == null || propsSprites.Count == 0)
            {
                warnings++;
                log.AppendLine("[WARN] No sliced sprites for boundary props.");
                return;
            }

            Transform left = EnsurePath(scene, "Ground/Boundary_Left/Visual", log);
            Transform right = EnsurePath(scene, "Ground/Boundary_Right/Visual", log);
            Transform leftRoot = left != null ? left.parent : null;
            Transform rightRoot = right != null ? right.parent : null;

            if (leftRoot != null)
            {
                ApplyManualBoundaryPlacement(leftRoot, true, log);
                EnsureBoundaryCollider(leftRoot, log);
            }

            if (rightRoot != null)
            {
                ApplyManualBoundaryPlacement(rightRoot, false, log);
                EnsureBoundaryCollider(rightRoot, log);
            }

            Sprite leftSprite = PickSprite(propsSprites, 1);
            Sprite rightSprite = PickSprite(propsSprites, 2);
            Sprite markerSprite = PickSprite(propsSprites, 14);

            ApplyBoundaryVisual(left, leftSprite, markerSprite, true, log);
            ApplyBoundaryVisual(right, rightSprite, markerSprite, false, log);
        }

        private static void ApplyBoundaryVisual(
            Transform visualRoot,
            Sprite barrierSprite,
            Sprite markerSprite,
            bool isLeft,
            StringBuilder log)
        {
            if (visualRoot == null)
            {
                return;
            }

            Transform boundaryRoot = visualRoot.parent;
            BoxCollider2D col = boundaryRoot != null ? boundaryRoot.GetComponent<BoxCollider2D>() : null;
            float width = col != null ? Mathf.Max(0.5f, col.size.x) : 1f;
            float height = col != null ? Mathf.Max(1f, col.size.y) : 8f;

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;

            if (barrierSprite != null)
            {
                float sw = Mathf.Max(0.01f, barrierSprite.rect.width / barrierSprite.pixelsPerUnit);
                float sh = Mathf.Max(0.01f, barrierSprite.rect.height / barrierSprite.pixelsPerUnit);
                visualRoot.localScale = new Vector3((width * 1.4f) / sw, height / sh, 1f);
            }
            else
            {
                visualRoot.localScale = new Vector3(1f, 1f, 1f);
            }

            AssignSprite(visualRoot, barrierSprite, -4, log, (isLeft ? "Boundary_Left" : "Boundary_Right") + "/Visual");
            SetRendererColor(visualRoot, new Color(0.84f, 0.84f, 0.9f, 0.95f));

            Transform marker = EnsureChild(visualRoot, "StopMarker", log);
            marker.localPosition = new Vector3(isLeft ? 0.55f : -0.55f, 2.4f, 0f);
            marker.localRotation = Quaternion.identity;
            marker.localScale = new Vector3(0.35f, 0.35f, 1f);
            AssignSprite(marker, markerSprite, -3, log, (isLeft ? "Boundary_Left" : "Boundary_Right") + "/Visual/StopMarker");
            SetRendererColor(marker, new Color(1f, 0.76f, 0.64f, 1f));
        }

        private static void ApplyManualBoundaryPlacement(Transform boundaryRoot, bool isLeft, StringBuilder log)
        {
            if (boundaryRoot == null)
            {
                return;
            }

            boundaryRoot.localPosition = new Vector3(
                isLeft ? ManualBoundaryLeftX : ManualBoundaryRightX,
                ManualBoundaryLocalY,
                0f);
            boundaryRoot.localRotation = Quaternion.identity;
            boundaryRoot.localScale = Vector3.one;
            log.AppendLine("[FIX] " + boundaryRoot.name + " localPosition=" + boundaryRoot.localPosition);
        }

        private static void EnsureBoundaryCollider(Transform boundaryRoot, StringBuilder log)
        {
            if (boundaryRoot == null)
            {
                return;
            }

            BoxCollider2D col = boundaryRoot.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = boundaryRoot.gameObject.AddComponent<BoxCollider2D>();
                log.AppendLine("[ADD] BoxCollider2D -> " + boundaryRoot.name);
            }

            col.enabled = true;
            col.isTrigger = false;
            col.size = ManualBoundaryColliderSize;
            col.offset = Vector2.zero;
        }

        private static void ApplyAtmosphereAssembly(
            Scene scene,
            List<Sprite> groundSprites,
            List<Sprite> propsSprites,
            StringBuilder log,
            ref int warnings)
        {
            Transform dropZoneVisual = EnsurePath(scene, "Ground/DropZone_Marker/DropZone_Visual", log);
            if (dropZoneVisual != null)
            {
                Transform dropRoot = dropZoneVisual.parent;
                BoxCollider2D col = dropRoot != null ? dropRoot.GetComponent<BoxCollider2D>() : null;
                float width = col != null ? Mathf.Max(4f, col.size.x) : 30f;
                float height = col != null ? Mathf.Max(1f, col.size.y) : 2f;

                Sprite dropSprite = PickSprite(propsSprites, 15) ?? PickSprite(groundSprites, 22);
                SetLocalTRS(dropZoneVisual, Vector3.zero, Vector3.one, false);
                ScaleToSize(dropZoneVisual, dropSprite, width, height);
                AssignSprite(dropZoneVisual, dropSprite, -12, log, "DropZone_Marker/DropZone_Visual");
                SetRendererColor(dropZoneVisual, new Color(0.86f, 0.2f, 0.2f, 0.28f));
            }
            else
            {
                warnings++;
                log.AppendLine("[WARN] Missing Ground/DropZone_Marker/DropZone_Visual.");
            }

            Transform haze = EnsurePath(scene, "Backdrop/FG_Haze", log);
            if (haze != null)
            {
                Sprite hazeSprite = PickSprite(propsSprites, 12) ?? PickSprite(groundSprites, 14);
                SetLocalTRS(haze, new Vector3(0f, -2.2f, 0f), Vector3.one, false);
                ScaleToSize(haze, hazeSprite, 36f, 3.2f);
                AssignSprite(haze, hazeSprite, -2, log, "Backdrop/FG_Haze");
                SetRendererColor(haze, new Color(0.16f, 0.2f, 0.28f, 0.38f));
            }
            else
            {
                warnings++;
                log.AppendLine("[WARN] Missing Backdrop/FG_Haze.");
            }

            ApplySegmentDecoration(scene, "Ground/Ground_Mid/Visual", "Deco_Mid_A", PickSprite(propsSprites, 5), new Vector3(-2.2f, 0.8f, 0f), new Vector3(0.42f, 0.42f, 1f), -17, new Color(0.82f, 0.82f, 0.9f, 0.9f), log);
            ApplySegmentDecoration(scene, "Ground/Ground_Mid/Visual", "Deco_Mid_B", PickSprite(propsSprites, 6), new Vector3(2f, 0.75f, 0f), new Vector3(0.36f, 0.36f, 1f), -17, new Color(0.84f, 0.8f, 0.86f, 0.9f), log);
            ApplySegmentDecoration(scene, "Ground/Ground_Final/Visual", "Deco_Final_A", PickSprite(propsSprites, 8), new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.5f, 1f), -16, new Color(1f, 0.74f, 0.68f, 0.92f), log);
        }

        private static void ApplySegmentDecoration(
            Scene scene,
            string segmentVisualPath,
            string decoName,
            Sprite sprite,
            Vector3 localPos,
            Vector3 localScale,
            int sortingOrder,
            Color tint,
            StringBuilder log)
        {
            if (sprite == null)
            {
                return;
            }

            Transform visual = EnsurePath(scene, segmentVisualPath, log);
            if (visual == null)
            {
                return;
            }

            Transform deco = EnsureChild(visual, decoName, log);
            deco.localPosition = localPos;
            deco.localRotation = Quaternion.identity;
            deco.localScale = localScale;
            AssignSprite(deco, sprite, sortingOrder, log, segmentVisualPath + "/" + decoName);
            SetRendererColor(deco, tint);
        }

        private static void ApplyEnemyVisualProxies(Scene scene, List<Sprite> propsSprites, StringBuilder log, ref int warnings)
        {
            if (propsSprites == null || propsSprites.Count == 0)
            {
                warnings++;
                log.AppendLine("[WARN] No sliced sprites for enemy visual proxies.");
                return;
            }

            string[] enemyNames = { "Enemy_01", "Enemy_02", "Enemy_03", "Enemy_04" };
            int[] spriteIndices = { 18, 19, 20, 21 };
            Color[] tints =
            {
                new Color(0.98f, 0.9f, 0.92f, 1f),
                new Color(0.96f, 0.78f, 0.82f, 1f),
                new Color(0.95f, 0.66f, 0.72f, 1f),
                new Color(0.9f, 0.42f, 0.46f, 1f)
            };
            Vector3[] scales =
            {
                new Vector3(0.95f, 1.08f, 1f),
                new Vector3(1.03f, 1.14f, 1f),
                new Vector3(1.12f, 1.22f, 1f),
                new Vector3(1.28f, 1.38f, 1f)
            };

            for (int i = 0; i < enemyNames.Length; i++)
            {
                string enemyName = enemyNames[i];
                Transform enemyView = EnsurePath(scene, enemyName + "/Enemy_View", log);
                if (enemyView == null)
                {
                    warnings++;
                    log.AppendLine("[WARN] Missing " + enemyName + "/Enemy_View");
                    continue;
                }

                enemyView.localPosition = Vector3.zero;
                enemyView.localRotation = Quaternion.identity;

                Transform spritePivot = EnsureChild(enemyView, "SpritePivot", log);
                spritePivot.localPosition = Vector3.zero;
                spritePivot.localRotation = Quaternion.identity;
                spritePivot.localScale = Vector3.one;

                Sprite proxySprite = PickSprite(propsSprites, spriteIndices[i]);
                Transform proxy = EnsureChildUnderParent(enemyView, spritePivot, "VisualProxy", log);
                proxy.localPosition = new Vector3(0f, 0f, 0f);
                proxy.localRotation = Quaternion.identity;
                proxy.localScale = scales[i];

                AssignSprite(proxy, proxySprite, 19, log, enemyName + "/Enemy_View/VisualProxy");
                SetRendererColor(proxy, tints[i]);

                Transform shadow = EnsureChildUnderParent(enemyView, enemyView, "VisualShadow", log);
                shadow.localPosition = new Vector3(0f, -0.55f, 0f);
                shadow.localRotation = Quaternion.identity;
                shadow.localScale = new Vector3(scales[i].x * 0.55f, scales[i].y * 0.22f, 1f);
                AssignSprite(shadow, PickSprite(propsSprites, 0), 18, log, enemyName + "/Enemy_View/VisualShadow");
                SetRendererColor(shadow, new Color(0f, 0f, 0f, 0.35f));

                if (enemyName == "Enemy_04")
                {
                    Transform eliteMark = EnsureChildUnderParent(enemyView, spritePivot, "EliteMark", log);
                    eliteMark.localPosition = new Vector3(0f, 1.18f, 0f);
                    eliteMark.localRotation = Quaternion.identity;
                    eliteMark.localScale = new Vector3(0.38f, 0.38f, 1f);
                    AssignSprite(eliteMark, PickSprite(propsSprites, 7), 21, log, "Enemy_04/Enemy_View/EliteMark");
                    SetRendererColor(eliteMark, new Color(1f, 0.9f, 0.38f, 1f));

                    Transform eliteAura = EnsureChildUnderParent(enemyView, spritePivot, "EliteAura", log);
                    eliteAura.localPosition = new Vector3(0f, 0.05f, 0f);
                    eliteAura.localRotation = Quaternion.identity;
                    eliteAura.localScale = new Vector3(1.55f, 1.7f, 1f);
                    AssignSprite(eliteAura, PickSprite(propsSprites, 13), 17, log, "Enemy_04/Enemy_View/EliteAura");
                    SetRendererColor(eliteAura, new Color(1f, 0.36f, 0.2f, 0.28f));
                }

                NormalizeEnemyMainSpriteCarrier(enemyView, proxy, log);
                ApplySpritePivotCompensation(spritePivot, proxy);
                EnsureEnemyBarAnchor(enemyView.parent, enemyName, log);

                ActorStats stats = enemyView.GetComponentInParent<ActorStats>();
                if (stats != null)
                {
                    log.AppendLine("[INFO] Enemy visual proxy polished -> " + stats.ActorId);
                }
                else
                {
                    log.AppendLine("[INFO] Enemy visual proxy polished -> " + enemyName);
                }
            }
        }

        private static Sprite PickSprite(List<Sprite> sprites, int index)
        {
            if (sprites == null || sprites.Count == 0)
            {
                return null;
            }

            index = Mathf.Clamp(index, 0, sprites.Count - 1);
            return sprites[index];
        }

        private static void ScaleToSize(Transform target, Sprite sprite, float width, float height)
        {
            if (target == null)
            {
                return;
            }

            if (sprite == null)
            {
                target.localScale = new Vector3(width, height, 1f);
                return;
            }

            float sw = Mathf.Max(0.01f, sprite.rect.width / sprite.pixelsPerUnit);
            float sh = Mathf.Max(0.01f, sprite.rect.height / sprite.pixelsPerUnit);
            target.localScale = new Vector3(width / sw, height / sh, 1f);
        }

        private static Transform FindPath(Scene scene, string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            GameObject root = FindRoot(scene, parts[0]);
            if (root == null)
            {
                return null;
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static Transform EnsurePath(Scene scene, string path, StringBuilder log)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            Transform current = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i];
                if (current == null)
                {
                    GameObject root = FindRoot(scene, name);
                    if (root == null)
                    {
                        root = new GameObject(name);
                        SceneManager.MoveGameObjectToScene(root, scene);
                        log.AppendLine("[CREATE] " + name);
                    }
                    else
                    {
                        log.AppendLine("[UPDATE] " + name);
                    }

                    current = root.transform;
                }
                else
                {
                    Transform child = current.Find(name);
                    if (child == null)
                    {
                        GameObject go = new GameObject(name);
                        go.transform.SetParent(current, false);
                        child = go.transform;
                        log.AppendLine("[CREATE] " + current.name + "/" + name);
                    }
                    else
                    {
                        log.AppendLine("[UPDATE] " + current.name + "/" + name);
                    }

                    current = child;
                }
            }

            return current;
        }

        private static Transform EnsureChildUnderParent(Transform searchRoot, Transform desiredParent, string name, StringBuilder log)
        {
            if (searchRoot == null || desiredParent == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform found = FindDescendantByName(searchRoot, name);
            if (found == null)
            {
                return EnsureChild(desiredParent, name, log);
            }

            if (found.parent != desiredParent)
            {
                found.SetParent(desiredParent, false);
                log.AppendLine("[UPDATE] Reparent " + name + " -> " + desiredParent.name);
            }

            return found;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void NormalizeEnemyMainSpriteCarrier(Transform enemyView, Transform visualProxy, StringBuilder log)
        {
            if (enemyView == null || visualProxy == null)
            {
                return;
            }

            SpriteRenderer rootRenderer = enemyView.GetComponent<SpriteRenderer>();
            SpriteRenderer proxyRenderer = visualProxy.GetComponent<SpriteRenderer>();
            if (proxyRenderer == null && rootRenderer != null)
            {
                proxyRenderer = visualProxy.gameObject.AddComponent<SpriteRenderer>();
                CopySpriteRendererSettings(rootRenderer, proxyRenderer);
                log.AppendLine("[ADD] SpriteRenderer -> " + visualProxy.name);
            }

            if (rootRenderer != null && proxyRenderer != null && rootRenderer.enabled)
            {
                rootRenderer.enabled = false;
                log.AppendLine("[UPDATE] Disable duplicate renderer -> " + enemyView.name);
            }
        }

        private static void CopySpriteRendererSettings(SpriteRenderer src, SpriteRenderer dst)
        {
            if (src == null || dst == null)
            {
                return;
            }

            dst.sprite = src.sprite;
            dst.color = src.color;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.drawMode = src.drawMode;
            dst.size = src.size;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
            dst.maskInteraction = src.maskInteraction;
            dst.material = src.sharedMaterial;
            dst.enabled = src.enabled;
        }

        private static void ApplySpritePivotCompensation(Transform spritePivot, Transform visualProxy)
        {
            if (spritePivot == null)
            {
                return;
            }

            float targetY = 0f;
            if (visualProxy != null)
            {
                SpriteRenderer renderer = visualProxy.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    float ppu = Mathf.Max(1f, renderer.sprite.pixelsPerUnit);
                    float pivotY = renderer.sprite.pivot.y / ppu;
                    targetY += pivotY * Mathf.Abs(visualProxy.localScale.y);
                }
            }

            spritePivot.localPosition = new Vector3(0f, Mathf.Clamp(targetY, -0.1f, 1.8f), 0f);
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
        }

        private static void EnsureEnemyBarAnchor(Transform enemyRoot, string enemyName, StringBuilder log)
        {
            if (enemyRoot == null)
            {
                return;
            }

            Transform barAnchor = enemyRoot.Find("BarAnchor");
            if (barAnchor == null)
            {
                Transform legacyHeadAnchor = enemyRoot.Find("HeadAnchor");
                if (legacyHeadAnchor != null)
                {
                    legacyHeadAnchor.name = "BarAnchor";
                    barAnchor = legacyHeadAnchor;
                    log.AppendLine("[UPDATE] Rename " + enemyRoot.name + "/HeadAnchor -> BarAnchor");
                }
                else
                {
                    barAnchor = EnsureChild(enemyRoot, "BarAnchor", log);
                }
            }

            Transform enemyView = enemyRoot.Find("Enemy_View");
            Transform spritePivot = enemyView != null ? enemyView.Find("SpritePivot") : null;
            Transform proxy = enemyView != null ? FindDescendantByName(enemyView, "VisualProxy") : null;

            float spritePivotY = spritePivot != null ? spritePivot.localPosition.y : 0f;
            float proxyScaleY = proxy != null ? Mathf.Abs(proxy.localScale.y) : 1f;
            float localY = Mathf.Max(1.45f, 0.9f + 0.55f + spritePivotY + Mathf.Max(0f, proxyScaleY - 1f) * 0.25f);

            SpriteRenderer proxyRenderer = proxy != null ? proxy.GetComponent<SpriteRenderer>() : null;
            if (proxyRenderer != null && proxyRenderer.sprite != null)
            {
                float ppu = Mathf.Max(1f, proxyRenderer.sprite.pixelsPerUnit);
                float topY = spritePivotY + ((proxyRenderer.sprite.rect.height - proxyRenderer.sprite.pivot.y) / ppu) * proxyScaleY;
                localY = Mathf.Max(localY, topY + 0.2f);
            }

            if (enemyName == "Enemy_04")
            {
                localY += 0.18f;
            }

            localY = Mathf.Clamp(localY, 1.3f, 2.2f);
            barAnchor.localPosition = new Vector3(0f, localY, 0f);
            barAnchor.localRotation = Quaternion.identity;
            barAnchor.localScale = Vector3.one;
            log.AppendLine("[UPDATE] " + enemyRoot.name + "/BarAnchor localPosition=(0," + localY.ToString("F2") + ",0)");
        }

        private static Transform EnsureChild(Transform parent, string name, StringBuilder log)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(name);
            if (child != null)
            {
                log.AppendLine("[UPDATE] " + parent.name + "/" + name);
                return child;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            log.AppendLine("[CREATE] " + parent.name + "/" + name);
            return go.transform;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static void AssignSprite(Transform target, Sprite sprite, int sortingOrder, StringBuilder log, string label)
        {
            if (target == null)
            {
                return;
            }

            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = target.gameObject.AddComponent<SpriteRenderer>();
                log.AppendLine("[ADD] SpriteRenderer -> " + target.name);
            }

            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.enabled = sprite != null;

            string spriteName = sprite != null ? sprite.name : "(null)";
            log.AppendLine("[BIND] " + label + " <= " + spriteName + $" | order={sortingOrder}");
        }

        private static void SetRendererAlpha(Transform target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                return;
            }

            Color c = sr.color;
            c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }

        private static void SetRendererColor(Transform target, Color color)
        {
            if (target == null)
            {
                return;
            }

            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                return;
            }

            sr.color = color;
        }

        private static void SetLocalTRS(Transform tr, Vector3 localPos, Vector3 localScale, bool keepZRotation)
        {
            if (tr == null)
            {
                return;
            }

            tr.localPosition = localPos;
            tr.localScale = localScale;
            if (!keepZRotation)
            {
                tr.localRotation = Quaternion.identity;
            }
        }
    }
}
#endif

