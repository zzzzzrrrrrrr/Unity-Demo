using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Combat;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Projectiles;
using UnityEngine;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Lightweight debug panel for procedure and combat quick checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugPanelController : MonoBehaviour
    {
        [SerializeField] private bool showPanel = false;
        [SerializeField] private Rect panelRect = new Rect(12f, 12f, 340f, 430f);
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private const float MinimumPanelWidth = 340f;
        private const float MinimumPanelHeight = 430f;
        private const float PoolReferenceRefreshInterval = 0.75f;

        private PlayerHealth cachedPlayerHealth;
        private BossHealth cachedBossHealth;
        private ProjectilePool[] cachedProjectilePools;
        private float nextPoolReferenceRefreshTime;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showPanel = !showPanel;
            }
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            GUILayout.BeginArea(GetVisiblePanelRect(), GUI.skin.box);
            GUILayout.Label("Boss Rush Debug");

            if (GameEntryBridge.IsReady)
            {
                GUILayout.Label("Procedure: " + GameEntryBridge.Procedure.CurrentProcedureType);
                GUILayout.Label("Last Result: " + GameEntryBridge.Procedure.LastBattleResult);
            }
            else
            {
                GUILayout.Label("Procedure: (GameEntry missing)");
            }

            DrawProcedureButtons();
            DrawHealthButtons();
            DrawPoolStats();

            GUILayout.EndArea();
        }

        private Rect GetVisiblePanelRect()
        {
            var rect = panelRect;
            rect.width = Mathf.Max(rect.width, MinimumPanelWidth);
            rect.height = Mathf.Max(rect.height, MinimumPanelHeight);
            return rect;
        }

        private void DrawProcedureButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Launch);
            }

            if (GUILayout.Button("Menu"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Menu);
            }

            if (GUILayout.Button("Battle"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Battle);
            }

            if (GUILayout.Button("Result"))
            {
                GameEntryBridge.SwitchProcedure(ProcedureType.Result);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawHealthButtons()
        {
            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = Object.FindObjectOfType<PlayerHealth>();
            }

            if (cachedBossHealth == null)
            {
                cachedBossHealth = Object.FindObjectOfType<BossHealth>();
            }

            if (cachedPlayerHealth != null)
            {
                GUILayout.Label(string.Format("Player HP: {0:0}/{1:0}", cachedPlayerHealth.CurrentHealth, cachedPlayerHealth.MaxHealth));
            }

            if (cachedBossHealth != null)
            {
                GUILayout.Label(string.Format("Boss HP: {0:0}/{1:0}", cachedBossHealth.CurrentHealth, cachedBossHealth.MaxHealth));
            }

            if (GUILayout.Button("Reset Health"))
            {
                cachedPlayerHealth?.ResetHealth();
                cachedBossHealth?.ResetHealth();
            }
        }

        private void DrawPoolStats()
        {
            GUILayout.Space(6f);
            GUILayout.Label("对象池统计");

            RefreshPoolReferencesIfNeeded();
            DrawProjectilePoolStats();
            DrawDamageTextPoolStats(DamageTextSpawner.Instance);
            DrawImpactPoolStats(ImpactFlashEffectSpawner.Instance);
        }

        private void RefreshPoolReferencesIfNeeded()
        {
            if (cachedProjectilePools != null && Time.unscaledTime < nextPoolReferenceRefreshTime)
            {
                return;
            }

            cachedProjectilePools = Object.FindObjectsOfType<ProjectilePool>();
            nextPoolReferenceRefreshTime = Time.unscaledTime + PoolReferenceRefreshInterval;
        }

        private void DrawProjectilePoolStats()
        {
            var poolCount = 0;
            var total = 0;
            var active = 0;
            var available = 0;
            var created = 0;
            var requests = 0;
            var reused = 0;
            var returned = 0;
            var misses = 0;
            var expansions = 0;

            if (cachedProjectilePools != null)
            {
                for (var i = 0; i < cachedProjectilePools.Length; i++)
                {
                    var pool = cachedProjectilePools[i];
                    if (pool == null)
                    {
                        continue;
                    }

                    poolCount++;
                    total += pool.TotalCount;
                    active += pool.ActiveCount;
                    available += pool.AvailableCount;
                    created += pool.TotalCreated;
                    requests += pool.TotalGetRequests;
                    reused += pool.TotalReused;
                    returned += pool.TotalReturned;
                    misses += pool.TotalPoolMisses;
                    expansions += pool.TotalExpansionBatches;
                }
            }

            if (poolCount == 0)
            {
                GUILayout.Label("子弹池：未找到活动对象池");
                return;
            }

            DrawPoolStatsLine("子弹池 x" + poolCount, total, active, available, created, requests, reused, returned, misses, expansions);
        }

        private static void DrawDamageTextPoolStats(DamageTextSpawner spawner)
        {
            if (spawner == null)
            {
                GUILayout.Label("飘字池：未找到活动对象池");
                return;
            }

            DrawPoolStatsLine(
                "飘字池",
                spawner.TotalCount,
                spawner.ActiveCount,
                spawner.AvailableCount,
                spawner.TotalCreated,
                spawner.TotalSpawnRequests,
                spawner.TotalReused,
                spawner.TotalReturned,
                spawner.TotalPoolMisses,
                spawner.TotalExpansionBatches);
        }

        private static void DrawImpactPoolStats(ImpactFlashEffectSpawner spawner)
        {
            if (spawner == null)
            {
                GUILayout.Label("命中特效池：未找到活动对象池");
                return;
            }

            DrawPoolStatsLine(
                "命中特效池",
                spawner.TotalCount,
                spawner.ActiveCount,
                spawner.AvailableCount,
                spawner.TotalCreated,
                spawner.TotalSpawnRequests,
                spawner.TotalReused,
                spawner.TotalReturned,
                spawner.TotalPoolMisses,
                spawner.TotalExpansionBatches);
        }

        private static void DrawPoolStatsLine(
            string label,
            int total,
            int active,
            int available,
            int created,
            int requests,
            int reused,
            int returned,
            int misses,
            int expansions)
        {
            GUILayout.Label(string.Format("{0}: 总数 {1} 激活 {2} 空闲 {3}", label, total, active, available));
            GUILayout.Label(string.Format("  请求 {0} 复用 {1} 回收 {2}", requests, reused, returned));
            GUILayout.Label(string.Format("  创建 {0} 扩容 {1} 未命中 {2}", created, expansions, misses));
        }
    }
}
