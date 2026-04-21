// Path: Assets/_Scripts/Tools/Physics/LayerCollisionAutoSetup.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ARPGDemo.Tools.Physics
{
    /// <summary>
    public static class LayerCollisionAutoSetup
    {
        private const string MenuPath = "Tools/ARPG Tools/Physics/Setup Layer Collision Matrix";

        [MenuItem(MenuPath, false, 2010)]
        public static void SetupLayerCollision()
        {
            try
            {
                int player = EnsureLayer("Player");
                int enemy = EnsureLayer("Enemy");
                int playerAttack = EnsureLayer("PlayerAttack");
                int enemyAttack = EnsureLayer("EnemyAttack");
                int ground = EnsureLayer("Ground");

                SetIgnore(enemy, ground, false);


                SetIgnore(playerAttack, enemy, false);
                SetIgnore(enemyAttack, enemy, true);
                SetIgnore(enemyAttack, player, false);

                SetIgnore(playerAttack, playerAttack, true);
                SetIgnore(enemyAttack, enemyAttack, true);

                SetIgnore(playerAttack, ground, true);
                SetIgnore(enemyAttack, ground, true);

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("LayerCollisionAutoSetup", "Layer and 2D collision matrix setup completed.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LayerCollisionAutoSetup] Setup failed: {ex}");
                EditorUtility.DisplayDialog("LayerCollisionAutoSetup", "Setup failed. Check Console.", "OK");
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject so = new SerializedObject(tagManager);
            SerializedProperty layers = so.FindProperty("layers");

            for (int i = 8; i <= 31; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (sp != null && string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    so.ApplyModifiedProperties();
                    return i;
                }
            }

            throw new System.Exception($"No available user layer slot for '{layerName}'.");
        }

        private static void SetIgnore(int a, int b, bool ignore)
        {
            if (a < 0 || b < 0)
            {
                return;
            }

            Physics2D.IgnoreLayerCollision(a, b, ignore);
        }
    }
}
#endif
