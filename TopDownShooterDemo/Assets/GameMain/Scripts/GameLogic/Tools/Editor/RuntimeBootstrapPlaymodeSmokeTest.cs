using System;
using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Projectiles;
using GameMain.GameLogic.UI;
using GameMain.GameLogic.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameMain.GameLogic.Tools.Editor
{
    /// <summary>
    /// Batch-friendly smoke test for runtime bootstrap and combat main loop.
    /// </summary>
    public static class RuntimeBootstrapPlaymodeSmokeTest
    {
        private const string RunScenePath = "Assets/Scenes/RunScene.scene";

        private static bool running;
        private static bool pendingExit;
        private static bool battleRequested;
        private static bool battleObserved;
        private static bool playerFireAttempted;
        private static bool playerFireSucceeded;
        private static bool bossMovementObserved;
        private static bool bossFireObserved;
        private static bool fanShotObserved;
        private static bool uiPanelsObserved;
        private static int maxActiveProjectileCount;
        private static Vector3 bossStartPosition;
        private static bool bossStartCaptured;
        private static double playModeStartTime;
        private static double battleObservedTime;

        [MenuItem("Tools/GameMain/Diagnostics/Run Runtime Bootstrap Playmode Smoke Test")]
        public static void RunFromMenu()
        {
            RunBatch();
        }

        public static void RunBatch()
        {
            if (running)
            {
                Debug.LogWarning("[RuntimeSmoke] Smoke test is already running.");
                return;
            }

            ResetState();
            running = true;

            if (!System.IO.File.Exists(RunScenePath))
            {
                Debug.LogError("[RuntimeSmoke] Scene not found: " + RunScenePath);
                CleanupAndExit(1);
                return;
            }

            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.isPlaying = true;
            Debug.Log("[RuntimeSmoke] Entering play mode...");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!running)
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playModeStartTime = EditorApplication.timeSinceStartup;
                Debug.Log("[RuntimeSmoke] Play mode entered.");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && pendingExit)
            {
                PrintSummaryAndExit();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!running || !EditorApplication.isPlaying)
            {
                return;
            }

            var elapsed = EditorApplication.timeSinceStartup - playModeStartTime;
            ObserveUiPanels();
            RequestBattleIfReady(elapsed);
            ObserveBossMovement();
            ObserveProjectilesBeforeAndAfterPlayerShot();
            TryPlayerFire(elapsed);

            if (maxActiveProjectileCount >= 3)
            {
                fanShotObserved = true;
            }

            if (elapsed >= 16.0)
            {
                EndPlayModeAndExit();
            }
        }

        private static void ObserveUiPanels()
        {
            if (uiPanelsObserved)
            {
                return;
            }

            uiPanelsObserved =
                UnityEngine.Object.FindObjectOfType<BattleHudController>() != null &&
                UnityEngine.Object.FindObjectOfType<ResultPanelController>() != null &&
                UnityEngine.Object.FindObjectOfType<BattlePausePanelController>() != null &&
                UnityEngine.Object.FindObjectOfType<MenuPresetPanelController>() != null;
        }

        private static void RequestBattleIfReady(double elapsed)
        {
            if (!GameEntryBridge.IsReady)
            {
                return;
            }

            var current = GameEntryBridge.Procedure.CurrentProcedureType;
            if (!battleRequested && elapsed >= 1.5 && current != ProcedureType.Battle)
            {
                battleRequested = true;
                GameEntryBridge.SwitchProcedure(ProcedureType.Battle);
                Debug.Log("[RuntimeSmoke] Requested ProcedureType.Battle.");
            }

            if (current == ProcedureType.Battle && !battleObserved)
            {
                battleObserved = true;
                battleObservedTime = EditorApplication.timeSinceStartup;
                CaptureBossStartPosition();
                Debug.Log("[RuntimeSmoke] Battle procedure observed.");
            }
        }

        private static void CaptureBossStartPosition()
        {
            if (bossStartCaptured)
            {
                return;
            }

            var boss = UnityEngine.Object.FindObjectOfType<BossHealth>();
            if (boss == null)
            {
                return;
            }

            bossStartCaptured = true;
            bossStartPosition = boss.transform.position;
        }

        private static void ObserveBossMovement()
        {
            if (!battleObserved)
            {
                return;
            }

            var boss = UnityEngine.Object.FindObjectOfType<BossHealth>();
            if (boss == null)
            {
                return;
            }

            if (!bossStartCaptured)
            {
                bossStartCaptured = true;
                bossStartPosition = boss.transform.position;
                return;
            }

            if (Vector3.Distance(bossStartPosition, boss.transform.position) >= 0.08f)
            {
                bossMovementObserved = true;
            }
        }

        private static void ObserveProjectilesBeforeAndAfterPlayerShot()
        {
            var projectiles = UnityEngine.Object.FindObjectsOfType<Projectile>(true);
            var activeCount = 0;
            for (var i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i] != null && projectiles[i].gameObject.activeInHierarchy)
                {
                    activeCount++;
                }
            }

            if (activeCount > maxActiveProjectileCount)
            {
                maxActiveProjectileCount = activeCount;
            }

            if (!playerFireAttempted && battleObserved && activeCount > 0)
            {
                bossFireObserved = true;
            }
        }

        private static void TryPlayerFire(double elapsed)
        {
            if (!battleObserved || playerFireAttempted)
            {
                return;
            }

            if (elapsed < 3.5)
            {
                return;
            }

            playerFireAttempted = true;
            var player = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
            if (player == null)
            {
                Debug.LogWarning("[RuntimeSmoke] PlayerHealth not found when trying to fire.");
                return;
            }

            var weapon = player.GetComponent<WeaponController>();
            if (weapon == null)
            {
                Debug.LogWarning("[RuntimeSmoke] Player WeaponController not found.");
                return;
            }

            playerFireSucceeded = weapon.TryFire(Vector2.right, player.gameObject);
            Debug.Log("[RuntimeSmoke] Player fire attempt result: " + playerFireSucceeded);
        }

        private static void EndPlayModeAndExit()
        {
            if (!running || pendingExit)
            {
                return;
            }

            pendingExit = true;
            EditorApplication.isPlaying = false;
        }

        private static void PrintSummaryAndExit()
        {
            Debug.Log("[RuntimeSmoke] ===== Summary =====");
            Debug.Log("[RuntimeSmoke] battleObserved=" + battleObserved);
            Debug.Log("[RuntimeSmoke] uiPanelsObserved=" + uiPanelsObserved);
            Debug.Log("[RuntimeSmoke] playerFireAttempted=" + playerFireAttempted);
            Debug.Log("[RuntimeSmoke] playerFireSucceeded=" + playerFireSucceeded);
            Debug.Log("[RuntimeSmoke] bossMovementObserved=" + bossMovementObserved);
            Debug.Log("[RuntimeSmoke] bossFireObserved(before player shot)=" + bossFireObserved);
            Debug.Log("[RuntimeSmoke] fanShotObserved(maxActiveProjectileCount>=3)=" + fanShotObserved);
            Debug.Log("[RuntimeSmoke] maxActiveProjectileCount=" + maxActiveProjectileCount);
            Debug.Log("[RuntimeSmoke] battleObserveDelaySec=" +
                      (battleObserved ? (battleObservedTime - playModeStartTime).ToString("F2") : "N/A"));

            CleanupAndExit(0);
        }

        private static void CleanupAndExit(int exitCode)
        {
            running = false;
            pendingExit = false;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.Exit(exitCode);
        }

        private static void ResetState()
        {
            running = false;
            pendingExit = false;
            battleRequested = false;
            battleObserved = false;
            playerFireAttempted = false;
            playerFireSucceeded = false;
            bossMovementObserved = false;
            bossFireObserved = false;
            fanShotObserved = false;
            uiPanelsObserved = false;
            maxActiveProjectileCount = 0;
            bossStartPosition = Vector3.zero;
            bossStartCaptured = false;
            playModeStartTime = 0d;
            battleObservedTime = 0d;
        }
    }
}
