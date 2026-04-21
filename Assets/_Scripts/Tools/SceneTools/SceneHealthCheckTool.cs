// 路径: Assets/_Scripts/Tools/SceneTools/SceneHealthCheckTool.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using ARPGDemo.Core;
using ARPGDemo.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Tools.SceneTools
{
    public static class SceneHealthCheckTool
    {
        private const string MenuPath = "Tools/ARPG/Scene Tools/Run Scene Health Check";

        [MenuItem(MenuPath, false, 2201)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SceneHealthCheck] Active scene is invalid.");
                return;
            }

            List<string> errors = new List<string>(32);
            List<string> warnings = new List<string>(64);

            CheckMissingScripts(scene, errors);
            CheckBrokenObjectReferences(scene, warnings);
            CheckCoreRuntimeChain(errors, warnings);
            CheckLayerSetup(warnings);

            if (warnings.Count > 0)
            {
                Debug.LogWarning($"[SceneHealthCheck][{scene.name}] Warnings:\n" + string.Join("\n", warnings));
            }

            if (errors.Count > 0)
            {
                Debug.LogError($"[SceneHealthCheck][{scene.name}] Blocking Issues:\n" + string.Join("\n", errors));
                return;
            }

            if (warnings.Count == 0)
            {
                Debug.Log($"[SceneHealthCheck][{scene.name}] Passed with no warnings.");
            }
            else
            {
                Debug.Log($"[SceneHealthCheck][{scene.name}] Passed with {warnings.Count} warning(s).");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRun()
        {
            return !EditorApplication.isPlaying;
        }

        private static void CheckMissingScripts(Scene scene, List<string> errors)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int missingScriptCount = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    GameObject go = all[j].gameObject;
                    missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                }
            }

            if (missingScriptCount > 0)
            {
                errors.Add($"- Missing script references detected: {missingScriptCount}.");
            }
        }

        private static void CheckBrokenObjectReferences(Scene scene, List<string> warnings)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                MonoBehaviour[] components = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
                for (int j = 0; j < components.Length; j++)
                {
                    MonoBehaviour comp = components[j];
                    if (comp == null)
                    {
                        continue;
                    }

                    SerializedObject so = new SerializedObject(comp);
                    SerializedProperty iter = so.GetIterator();
                    bool enterChildren = true;

                    while (iter.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (iter.propertyPath == "m_Script")
                        {
                            continue;
                        }

                        if (iter.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        if (iter.objectReferenceValue == null && iter.objectReferenceInstanceIDValue != 0)
                        {
                            warnings.Add($"- Broken reference: {comp.GetType().Name}.{iter.displayName} on '{comp.gameObject.name}'.");
                        }
                    }
                }
            }
        }

        private static void CheckCoreRuntimeChain(List<string> errors, List<string> warnings)
        {
            if (Object.FindObjectOfType<GameManager>(true) == null)
            {
                errors.Add("- Missing GameManager.");
            }

            if (Object.FindObjectOfType<UIManager>(true) == null)
            {
                errors.Add("- Missing UIManager.");
            }

            if (Object.FindObjectOfType<PlayerController>(true) == null)
            {
                errors.Add("- Missing PlayerController.");
            }

            if (Object.FindObjectOfType<AttackHitbox2D>(true) == null)
            {
                errors.Add("- Missing AttackHitbox2D.");
            }

            if (Object.FindObjectOfType<EnemyAIController2D>(true) == null)
            {
                warnings.Add("- No EnemyAIController2D found in scene.");
            }

            if (Object.FindObjectOfType<EventSystem>(true) == null)
            {
                warnings.Add("- Missing EventSystem, UI may not receive click input.");
            }

            if (Camera.main == null)
            {
                errors.Add("- Missing MainCamera tag.");
            }
        }

        private static void CheckLayerSetup(List<string> warnings)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            if (playerLayer < 0)
            {
                warnings.Add("- Layer 'Player' not found.");
            }

            if (enemyLayer < 0)
            {
                warnings.Add("- Layer 'Enemy' not found.");
            }

            if (playerLayer >= 0 && enemyLayer >= 0)
            {
                bool ignored = Physics2D.GetIgnoreLayerCollision(playerLayer, enemyLayer);
                if (!ignored)
                {
                    warnings.Add("- Player/Enemy layer collision is not ignored (body collision may push each other).");
                }
            }
        }
    }
}
#endif
