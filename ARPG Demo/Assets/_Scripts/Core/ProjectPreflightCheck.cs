// Path: Assets/_Scripts/Core/ProjectPreflightCheck.cs
// Function: Runtime preflight validator for the minimal playable ARPG loop.
using System.Collections.Generic;
using System.Reflection;
using ARPGDemo.Game;
using ARPGDemo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ARPGDemo.Core
{
    public static class ProjectPreflightCheck
    {
        private const string MainMenuSceneName = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            ValidateCurrentScene();
        }

        private static void ValidateCurrentScene()
        {
            List<string> errors = new List<string>(16);
            List<string> warnings = new List<string>(16);
            string sceneName = SceneManager.GetActiveScene().name;

            if (string.Equals(sceneName, MainMenuSceneName, System.StringComparison.Ordinal))
            {
                Debug.Log("[Preflight][" + sceneName + "] Skip gameplay preflight on menu scene.");
                return;
            }

            ValidateManagers(errors, warnings);
            ValidatePlayerChain(errors, warnings);
            ValidateEnemyChain(errors, warnings);
            ValidateUIChain(warnings);
            ValidateCamera(errors);

            if (warnings.Count > 0)
            {
                Debug.LogWarning("[Preflight][" + sceneName + "]\n" + string.Join("\n", warnings));
            }

            if (errors.Count > 0)
            {
                Debug.LogError("[Preflight][" + sceneName + "] Blocking Issues:\n" + string.Join("\n", errors));
                return;
            }

            Debug.Log("[Preflight][" + sceneName + "] Passed.");
        }

        private static void ValidateManagers(List<string> errors, List<string> warnings)
        {
            int mainGameManagerCount = CountActive(Object.FindObjectsOfType<GameManager>(true));
            if (mainGameManagerCount == 0)
            {
                errors.Add("- Missing active ARPGDemo.Core.GameManager.");
            }
            else if (mainGameManagerCount > 1)
            {
                errors.Add("- Multiple active ARPGDemo.Core.GameManager found (" + mainGameManagerCount + ").");
            }

            int mainUiManagerCount = CountActive(Object.FindObjectsOfType<UIManager>(true));
            if (mainUiManagerCount == 0)
            {
                errors.Add("- Missing active ARPGDemo.Core.UIManager.");
            }
            else if (mainUiManagerCount > 1)
            {
                errors.Add("- Multiple active ARPGDemo.Core.UIManager found (" + mainUiManagerCount + ").");
            }

            int legacyGameManagerCount = CountActiveByTypeName("ARPGDemo.Core.Managers.GameManager");
            if (legacyGameManagerCount > 0)
            {
                warnings.Add("- Legacy GameManager is active. Disable/remove ARPGDemo.Core.Managers.GameManager.");
            }

            int legacyUiManagerCount = CountActiveByTypeName("ARPGDemo.Core.Managers.UIManager");
            if (legacyUiManagerCount > 0)
            {
                warnings.Add("- Legacy UIManager is active. Disable/remove ARPGDemo.Core.Managers.UIManager.");
            }
        }

        private static void ValidatePlayerChain(List<string> errors, List<string> warnings)
        {
            PlayerController[] players = Object.FindObjectsOfType<PlayerController>(true);
            int activePlayerCount = CountActive(players);
            if (activePlayerCount == 0)
            {
                errors.Add("- Missing active PlayerController.");
                return;
            }

            if (activePlayerCount > 1)
            {
                warnings.Add("- Multiple active PlayerController found (" + activePlayerCount + ").");
            }

            for (int i = 0; i < players.Length; i++)
            {
                PlayerController player = players[i];
                if (!IsActive(player))
                {
                    continue;
                }

                ValidatePlayerObject(player, errors, warnings);
            }

            int legacyPlayerControllerCount = CountActiveByTypeName("ARPGDemo.Game.PlayerController2D");
            if (legacyPlayerControllerCount > 0)
            {
                warnings.Add("- Legacy PlayerController2D is active. Keep only PlayerController.");
            }

            int legacySkillSystemCount = CountActiveByTypeName("ARPGDemo.Game.PlayerSkillSystem");
            if (legacySkillSystemCount > 0)
            {
                warnings.Add("- Legacy PlayerSkillSystem is active. Keep combo logic in PlayerController only.");
            }
        }

        private static void ValidatePlayerObject(PlayerController player, List<string> errors, List<string> warnings)
        {
            GameObject go = player.gameObject;
            string prefix = "- [Player:" + go.name + "] ";

            ActorStats stats = go.GetComponent<ActorStats>();
            if (stats == null)
            {
                errors.Add(prefix + "Missing ActorStats.");
            }
            else if (stats.Team != ActorTeam.Player)
            {
                warnings.Add(prefix + "ActorStats.Team should be Player.");
            }

            if (go.GetComponent<Rigidbody2D>() == null)
            {
                errors.Add(prefix + "Missing Rigidbody2D.");
            }

            if (go.GetComponent<Collider2D>() == null)
            {
                warnings.Add(prefix + "Missing Collider2D.");
            }

            if (go.GetComponentInChildren<AttackHitbox2D>(true) == null)
            {
                errors.Add(prefix + "Missing AttackHitbox2D in hierarchy.");
            }

            if (go.GetComponentInChildren<Animator>(true) == null)
            {
                warnings.Add(prefix + "Missing Animator. Attack hitbox fallback will be used.");
            }

            if (GetPrivateFieldValue(player, "groundCheck") == null)
            {
                warnings.Add(prefix + "PlayerController.groundCheck is not assigned.");
            }
        }

        private static void ValidateEnemyChain(List<string> errors, List<string> warnings)
        {
            EnemyAIController2D[] enemies = Object.FindObjectsOfType<EnemyAIController2D>(true);
            int activeEnemyCount = CountActive(enemies);
            if (activeEnemyCount == 0)
            {
                warnings.Add("- No active EnemyAIController2D found. Victory/Defeat battle loop cannot be validated.");
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAIController2D enemy = enemies[i];
                if (!IsActive(enemy))
                {
                    continue;
                }

                ValidateEnemyObject(enemy, errors, warnings);
            }

            int legacyEnemyFsmCount = CountActiveByTypeName("ARPGDemo.Game.Enemy.EnemyFSM");
            if (legacyEnemyFsmCount > 0)
            {
                warnings.Add("- Legacy EnemyFSM is active. Keep only EnemyAIController2D.");
            }
        }

        private static void ValidateEnemyObject(EnemyAIController2D enemy, List<string> errors, List<string> warnings)
        {
            GameObject go = enemy.gameObject;
            string prefix = "- [Enemy:" + go.name + "] ";

            ActorStats stats = go.GetComponent<ActorStats>();
            if (stats == null)
            {
                errors.Add(prefix + "Missing ActorStats.");
            }
            else if (stats.Team != ActorTeam.Enemy)
            {
                warnings.Add(prefix + "ActorStats.Team should be Enemy.");
            }

            if (go.GetComponent<Rigidbody2D>() == null)
            {
                errors.Add(prefix + "Missing Rigidbody2D.");
            }

            if (go.GetComponent<Collider2D>() == null)
            {
                warnings.Add(prefix + "Missing Collider2D.");
            }

            if (go.GetComponentInChildren<AttackHitbox2D>(true) == null)
            {
                errors.Add(prefix + "Missing AttackHitbox2D in hierarchy.");
            }
        }

        private static void ValidateUIChain(List<string> warnings)
        {
            PlayerHUDController[] hudControllers = Object.FindObjectsOfType<PlayerHUDController>(true);
            if (CountActive(hudControllers) == 0)
            {
                if (!HasAutoHudSetupEnabled())
                {
                    warnings.Add("- Missing active PlayerHUDController.");
                }
            }
            else
            {
                for (int i = 0; i < hudControllers.Length; i++)
                {
                    if (IsActive(hudControllers[i]))
                    {
                        ValidatePlayerHudReferences(hudControllers[i], warnings);
                    }
                }
            }

            EnemyWorldBarController[] enemyBars = Object.FindObjectsOfType<EnemyWorldBarController>(true);
            if (CountActive(enemyBars) == 0)
            {
                warnings.Add("- Missing active EnemyWorldBarController.");
            }
            else
            {
                for (int i = 0; i < enemyBars.Length; i++)
                {
                    if (IsActive(enemyBars[i]))
                    {
                        ValidateEnemyBarReferences(enemyBars[i], warnings);
                    }
                }
            }

            if (CountActive(Object.FindObjectsOfType<MainMenuUIController>(true)) == 0)
            {
                warnings.Add("- Missing active MainMenuUIController.");
            }

            if (CountActive(Object.FindObjectsOfType<PauseUIController>(true)) == 0)
            {
                warnings.Add("- Missing active PauseUIController.");
            }

            if (CountActive(Object.FindObjectsOfType<ResultUIController>(true)) == 0)
            {
                warnings.Add("- Missing active ResultUIController.");
            }

            bool hasMenuButtons =
                CountActive(Object.FindObjectsOfType<MainMenuUIController>(true)) > 0 ||
                CountActive(Object.FindObjectsOfType<PauseUIController>(true)) > 0 ||
                CountActive(Object.FindObjectsOfType<ResultUIController>(true)) > 0;
            if (hasMenuButtons && Object.FindObjectOfType<EventSystem>() == null)
            {
                warnings.Add("- Missing EventSystem. UI buttons will not receive input.");
            }
        }

        private static void ValidatePlayerHudReferences(PlayerHUDController hud, List<string> warnings)
        {
            object hpSlider = GetPrivateFieldValue(hud, "hpSlider");
            object hpSmoothSlider = GetPrivateFieldValue(hud, "hpSmoothSlider");
            object mpSlider = GetPrivateFieldValue(hud, "mpSlider");
            object mpSmoothSlider = GetPrivateFieldValue(hud, "mpSmoothSlider");

            if (hpSlider == null && hpSmoothSlider == null)
            {
                warnings.Add("- [PlayerHUDController:" + hud.gameObject.name + "] HP slider references are empty.");
            }

            if (mpSlider == null && mpSmoothSlider == null)
            {
                warnings.Add("- [PlayerHUDController:" + hud.gameObject.name + "] MP slider references are empty.");
            }
        }

        private static void ValidateEnemyBarReferences(EnemyWorldBarController bar, List<string> warnings)
        {
            object hpSlider = GetPrivateFieldValue(bar, "hpSlider");
            object hpSmoothSlider = GetPrivateFieldValue(bar, "hpSmoothSlider");
            object followTarget = GetPrivateFieldValue(bar, "followTarget");

            if (hpSlider == null && hpSmoothSlider == null)
            {
                warnings.Add("- [EnemyWorldBarController:" + bar.gameObject.name + "] HP slider references are empty.");
            }

            if (followTarget == null)
            {
                warnings.Add("- [EnemyWorldBarController:" + bar.gameObject.name + "] Missing UIFollowTarget2D reference.");
            }
        }

        private static void ValidateCamera(List<string> errors)
        {
            if (Camera.main == null)
            {
                errors.Add("- Missing MainCamera (tag: MainCamera).");
            }
        }

        private static bool IsActive(Behaviour behaviour)
        {
            return behaviour != null && behaviour.isActiveAndEnabled;
        }

        private static int CountActive<T>(T[] components) where T : Behaviour
        {
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (IsActive(components[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveByTypeName(string fullTypeName)
        {
            System.Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            {
                return 0;
            }

            Object[] components = Object.FindObjectsOfType(type, true);
            int count = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is Behaviour behaviour && IsActive(behaviour))
                {
                    count++;
                }
            }

            return count;
        }

        private static System.Type ResolveTypeByName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return null;
            }

            System.Type direct = System.Type.GetType(fullTypeName, false);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                System.Type candidate = assembly.GetType(fullTypeName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static object GetPrivateFieldValue(object target, string fieldName)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target);
        }

        private static bool HasAutoHudSetupEnabled()
        {
            GameManager gameManager = Object.FindObjectOfType<GameManager>(true);
            if (gameManager == null)
            {
                return false;
            }

            object value = GetPrivateFieldValue(gameManager, "autoEnsureBattleHud");
            return value is bool b && b;
        }
    }
}
