// 路径: Assets/_Scripts/Tools/SceneTools/ArtSlotBatchReplaceTool.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Tools.SceneTools
{
    public static class ArtSlotBatchReplaceTool
    {
        private const string MenuApply = "Tools/ARPG/Scene Tools/Art Slots/Apply Selected Sprites";
        private const string MenuCreate = "Tools/ARPG/Scene Tools/Art Slots/Create Slots From Selected Renderers";

        [MenuItem(MenuApply, false, 2301)]
        public static void ApplySelectedSpritesToSlots()
        {
            Sprite[] selectedSprites = Selection.GetFiltered<Sprite>(SelectionMode.Assets);
            if (selectedSprites == null || selectedSprites.Length == 0)
            {
                Debug.LogWarning("[ArtSlotTool] Select sprites in Project window first.");
                return;
            }

            Dictionary<string, Sprite> spriteMap = new Dictionary<string, Sprite>(selectedSprites.Length);
            for (int i = 0; i < selectedSprites.Length; i++)
            {
                Sprite sprite = selectedSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                spriteMap[sprite.name] = sprite;
            }

            Scene scene = SceneManager.GetActiveScene();
            ArtSlot2D[] slots = Object.FindObjectsOfType<ArtSlot2D>(true);

            int applied = 0;
            int missingSprite = 0;
            int missingRenderer = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                ArtSlot2D slot = slots[i];
                if (slot == null || slot.gameObject.scene != scene)
                {
                    continue;
                }

                string key = slot.ResolveKey();
                if (string.IsNullOrEmpty(key))
                {
                    missingSprite++;
                    continue;
                }

                if (!spriteMap.TryGetValue(key, out Sprite sprite) || sprite == null)
                {
                    missingSprite++;
                    continue;
                }

                Undo.RecordObject(slot, "Apply Art Slot Sprite");
                if (slot.TargetRenderer != null)
                {
                    Undo.RecordObject(slot.TargetRenderer, "Apply Art Slot Sprite");
                }

                if (slot.TryApplySprite(sprite))
                {
                    applied++;
                    continue;
                }

                missingRenderer++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[ArtSlotTool] Apply complete. Applied={applied}, MissingSprite={missingSprite}, MissingRenderer={missingRenderer}.");
        }

        [MenuItem(MenuApply, true)]
        private static bool ValidateApplySelectedSpritesToSlots()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem(MenuCreate, false, 2302)]
        public static void CreateSlotsFromSelectedRenderers()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[ArtSlotTool] Select GameObjects with SpriteRenderer first.");
                return;
            }

            int created = 0;
            int skipped = 0;

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject go = selectedObjects[i];
                if (go == null)
                {
                    continue;
                }

                SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    skipped++;
                    continue;
                }

                ArtSlot2D slot = go.GetComponent<ArtSlot2D>();
                if (slot == null)
                {
                    Undo.AddComponent<ArtSlot2D>(go);
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            Debug.Log($"[ArtSlotTool] Create complete. Created={created}, Skipped={skipped}.");
        }

        [MenuItem(MenuCreate, true)]
        private static bool ValidateCreateSlotsFromSelectedRenderers()
        {
            return !EditorApplication.isPlaying;
        }
    }
}
#endif
