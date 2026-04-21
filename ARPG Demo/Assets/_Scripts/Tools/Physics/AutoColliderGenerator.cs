// Path: Assets/_Scripts/Tools/Physics/AutoColliderGenerator.cs

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ARPGDemo.Tools.Physics
{
    /// <summary>
    public static class AutoColliderGenerator
    {
        private const string MenuRoot = "Tools/ARPG Tools/Physics/";

        [MenuItem(MenuRoot + "Generate BoxCollider2D For Selection", false, 2001)]
        public static void GenerateBoxColliderForSelection()
        {
            GenerateForSelection(usePolygon: false, replaceExisting: false);
        }

        [MenuItem(MenuRoot + "Generate PolygonCollider2D For Selection", false, 2002)]
        public static void GeneratePolygonColliderForSelection()
        {
            GenerateForSelection(usePolygon: true, replaceExisting: false);
        }

        [MenuItem(MenuRoot + "Regenerate Collider2D (Replace Existing)", false, 2003)]
        public static void RegenerateColliderForSelection()
        {
            GenerateForSelection(usePolygon: true, replaceExisting: true);
        }

        [MenuItem(MenuRoot + "Generate BoxCollider2D For Selection", true)]
        [MenuItem(MenuRoot + "Generate PolygonCollider2D For Selection", true)]
        [MenuItem(MenuRoot + "Regenerate Collider2D (Replace Existing)", true)]
        private static bool ValidateGenerate()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static void GenerateForSelection(bool usePolygon, bool replaceExisting)
        {
            GameObject[] roots = Selection.gameObjects;
            int created = 0;
            int skipped = 0;

            try
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    SpriteRenderer[] renderers = roots[i].GetComponentsInChildren<SpriteRenderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        SpriteRenderer sr = renderers[r];
                        if (sr == null || sr.sprite == null)
                        {
                            continue;
                        }

                        Collider2D existing = sr.GetComponent<Collider2D>();
                        if (existing != null && !replaceExisting)
                        {
                            skipped++;
                            continue;
                        }

                        Undo.RecordObject(sr.gameObject, "Generate Collider2D");
                        if (existing != null && replaceExisting)
                        {
                            Object.DestroyImmediate(existing);
                        }

                        if (usePolygon)
                        {
                            sr.gameObject.AddComponent<PolygonCollider2D>();
                        }
                        else
                        {
                            sr.gameObject.AddComponent<BoxCollider2D>();
                        }

                        EditorUtility.SetDirty(sr.gameObject);
                        created++;
                    }
                }

                string mode = usePolygon ? "PolygonCollider2D" : "BoxCollider2D";
                EditorUtility.DisplayDialog("AutoColliderGenerator", $"Done.\nMode: {mode}\nCreated: {created}\nSkipped: {skipped}", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutoColliderGenerator] Generate failed: {ex}");
                EditorUtility.DisplayDialog("AutoColliderGenerator", "Generate failed. Check Console.", "OK");
            }
        }
    }
}
#endif
