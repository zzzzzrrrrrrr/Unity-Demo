using System;
using GameMain.Builtin.Sound;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using GameMain.GameLogic.UI;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.Builtin.Procedure
{
    /// <summary>
    /// Main combat procedure for boss rush.
    /// </summary>
    public sealed class ProcedureBattle : ProcedureBase
    {
        [SerializeField] private bool autoResetHealthOnEnter = true;
        [SerializeField] private BattleConfigData battleConfig;
        [SerializeField] private bool allowPause = true;
        [SerializeField] private KeyCode pauseToggleKey = KeyCode.Escape;
        [SerializeField] private bool deferWinResultOnBossDied;

        private PlayerHealth playerHealth;
        private BossHealth bossHealth;
        private bool playerSubscribed;
        private bool bossSubscribed;
        private bool battleEnded;
        private bool isPaused;
        private bool hasTimeLimit;
        private float battleTimeLimit;
        private float battleTimeRemaining;
        private RevivePanelController revivePanel;

        public bool HasTimeLimit => hasTimeLimit;

        public float BattleTimeLimit => battleTimeLimit;

        public float RemainingBattleTime => battleTimeRemaining;

        public bool IsPaused => isPaused;

        public event Action<float, float, bool> BattleTimeUpdated;
        public event Action<bool> PauseStateChanged;
        public event Action BattleRestarted;

        public override ProcedureType ProcedureType => ProcedureType.Battle;

        public override void OnEnter()
        {
            Debug.Log("ProcedureBattle.Enter");
            SetPausedInternal(false, true);
            battleEnded = false;
            Manager.SetPendingBattleResult(BattleResultType.None);
            ResolveBattleReferences();
            ResolveBattleConfig();
            BindBattleSignals();
            ApplyBossPreset();
            if (autoResetHealthOnEnter)
            {
                ResetCombatState();
            }

            ResetBattlePositions();
            ResetBossBrainState();
            RuntimeSceneHooks.Active?.ResetTransientEffects();
            ResetRevivePanelState();
            NotifyBattleTimeUpdated();
            LogBattleChainSummary();
        }

        public override void OnUpdate(float deltaTime)
        {
            if (allowPause && Input.GetKeyDown(pauseToggleKey))
            {
                TogglePause();
                return;
            }

            if (isPaused)
            {
                return;
            }

            if (battleEnded)
            {
                return;
            }

            RefreshBattleParticipantsFromRuntimeHooks();

            if (playerHealth != null && playerHealth.IsDead)
            {
                OnPlayerDied();
                return;
            }

            if (bossHealth != null && bossHealth.IsDead)
            {
                OnBossDied();
                return;
            }

            if (hasTimeLimit)
            {
                battleTimeRemaining = Mathf.Max(0f, battleTimeRemaining - Mathf.Max(0f, deltaTime));
                NotifyBattleTimeUpdated();
                if (battleTimeRemaining <= 0f)
                {
                    EndBattle(BattleResultType.Abort);
                }
            }
        }

        public override void OnExit()
        {
            SetPausedInternal(false, true);
            UnbindBattleSignals();
        }

        public void SetBattleParticipants(PlayerHealth player, BossHealth boss)
        {
            var changed = playerHealth != player || bossHealth != boss;
            if (!changed)
            {
                return;
            }

            var wasSubscribed = playerSubscribed || bossSubscribed;
            if (wasSubscribed)
            {
                UnbindBattleSignals();
            }

            playerHealth = player;
            bossHealth = boss;

            if (wasSubscribed)
            {
                BindBattleSignals();
            }

            ConfigureRevivePanelReference();
        }

        public void SetBattleConfig(BattleConfigData config)
        {
            battleConfig = config;
            ResolveBattleConfig();
            NotifyBattleTimeUpdated();
        }

        public void SetDeferWinResultOnBossDied(bool defer)
        {
            deferWinResultOnBossDied = defer;
        }

        public void TogglePause()
        {
            if (!allowPause || battleEnded)
            {
                return;
            }

            SetPausedInternal(!isPaused, false);
        }

        public void SetPaused(bool paused)
        {
            if (!allowPause && paused)
            {
                return;
            }

            if (battleEnded && paused)
            {
                return;
            }

            SetPausedInternal(paused, false);
        }

        public void RestartBattle()
        {
            if (!IsCurrentProcedureActive())
            {
                return;
            }

            SetPausedInternal(false, true);
            battleEnded = false;
            Manager.SetPendingBattleResult(BattleResultType.None);
            ResolveBattleReferences();
            ResolveBattleConfig();
            ResetCombatState();
            ResetBattlePositions();
            ResetBossBrainState();
            RuntimeSceneHooks.Active?.ResetTransientEffects();
            ResetRevivePanelState();
            NotifyBattleTimeUpdated();
            BattleRestarted?.Invoke();
        }

        public void BackToMenu()
        {
            SetPausedInternal(false, true);
            Manager.SetPendingBattleResult(BattleResultType.None);
            Manager.ChangeProcedure(ProcedureType.Menu);
        }

        private void BindBattleSignals()
        {
            ResolveBattleReferences();

            if (playerHealth == null)
            {
                playerHealth = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
            }

            if (bossHealth == null)
            {
                bossHealth = UnityEngine.Object.FindObjectOfType<BossHealth>();
            }

            if (playerHealth != null && !playerSubscribed)
            {
                playerHealth.OnDied += OnPlayerDied;
                playerSubscribed = true;
            }

            if (bossHealth != null && !bossSubscribed)
            {
                bossHealth.OnDied += OnBossDied;
                bossSubscribed = true;
            }
        }

        private void ResolveBattleReferences()
        {
            if (playerHealth != null && bossHealth != null && battleConfig != null)
            {
                return;
            }

            var hooks = RuntimeSceneHooks.Active;
            if (hooks != null)
            {
                if (playerHealth == null)
                {
                    playerHealth = hooks.PlayerHealth;
                }

                if (bossHealth == null)
                {
                    bossHealth = hooks.BossHealth;
                }

                if (battleConfig == null)
                {
                    battleConfig = hooks.BattleConfig;
                }
            }
        }

        private void ResolveBattleConfig()
        {
            if (battleConfig == null && RuntimeSceneHooks.Active != null)
            {
                battleConfig = RuntimeSceneHooks.Active.BattleConfig;
            }

            battleTimeLimit = battleConfig != null ? battleConfig.battleTimeLimit : -1f;
            hasTimeLimit = battleTimeLimit > 0f;
            battleTimeRemaining = hasTimeLimit ? battleTimeLimit : -1f;
        }

        private void ApplyBossPreset()
        {
            var presetController = RuntimeSceneHooks.Active != null ? RuntimeSceneHooks.Active.BossPresetController : null;
            presetController?.ApplyCurrentPreset();
        }

        private void UnbindBattleSignals()
        {
            if (playerHealth != null && playerSubscribed)
            {
                playerHealth.OnDied -= OnPlayerDied;
            }

            if (bossHealth != null && bossSubscribed)
            {
                bossHealth.OnDied -= OnBossDied;
            }

            playerHealth = null;
            bossHealth = null;
            playerSubscribed = false;
            bossSubscribed = false;
        }

        private void ResetCombatState()
        {
            playerHealth?.ResetHealth();
            bossHealth?.ResetHealth();

            var playerWeapon = playerHealth != null ? playerHealth.GetComponent<WeaponController>() : null;
            var bossWeapon = bossHealth != null ? bossHealth.GetComponent<WeaponController>() : null;

            playerWeapon?.ResetFireCooldown();
            bossWeapon?.ResetFireCooldown();
        }

        private void ResetBattlePositions()
        {
            if (battleConfig == null)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.transform.position = battleConfig.playerSpawnPosition;
            }

            if (bossHealth != null)
            {
                bossHealth.transform.position = battleConfig.bossSpawnPosition;
            }
        }

        private void ResetBossBrainState()
        {
            if (bossHealth == null)
            {
                return;
            }

            var brain = bossHealth.GetComponent<BossBrain>();
            if (brain != null)
            {
                if (playerHealth != null)
                {
                    brain.SetTargetPlayer(playerHealth);
                }

                brain.ResetForBattle();
            }
        }

        private void LogBattleChainSummary()
        {
            var playerWeapon = playerHealth != null ? playerHealth.GetComponent<WeaponController>() : null;
            var bossWeapon = bossHealth != null ? bossHealth.GetComponent<WeaponController>() : null;
            var bossBrain = bossHealth != null ? bossHealth.GetComponent<BossBrain>() : null;
            Debug.Log(
                "[ProcedureBattle] EnterSummary " +
                "player=" + (playerHealth != null ? playerHealth.name : "null") +
                " boss=" + (bossHealth != null ? bossHealth.name : "null") +
                " playerWeapon=" + (playerWeapon != null ? playerWeapon.BuildRuntimeDebugSummary() : "null") +
                " bossWeapon=" + (bossWeapon != null ? bossWeapon.BuildRuntimeDebugSummary() : "null") +
                " bossBrain=" + (bossBrain != null ? "ready" : "null"));
        }

        private void OnPlayerDied()
        {
            if (battleEnded)
            {
                return;
            }

            RefreshBattleParticipantsFromRuntimeHooks();
            if (playerHealth == null || !playerHealth.IsDead)
            {
                return;
            }

            if (TryOfferRevive())
            {
                return;
            }

            AudioService.PlaySfxById(SoundIds.SfxPlayerDied);
            EndBattle(BattleResultType.Lose);
        }

        public void ResolvePlayerDeathAfterReviveDeclined()
        {
            if (battleEnded)
            {
                return;
            }

            AudioService.PlaySfxById(SoundIds.SfxPlayerDied);
            EndBattle(BattleResultType.Lose);
        }

        private void OnBossDied()
        {
            if (battleEnded)
            {
                return;
            }

            AudioService.PlaySfxById(SoundIds.SfxBossDied);
            if (deferWinResultOnBossDied)
            {
                battleEnded = true;
                Manager.SetPendingBattleResult(BattleResultType.None);
                Debug.Log("ProcedureBattle deferred Win Result after boss death; external flow may continue.", this);
                return;
            }

            EndBattle(BattleResultType.Win);
        }

        private void EndBattle(BattleResultType result)
        {
            if (battleEnded)
            {
                return;
            }

            SetPausedInternal(false, true);
            battleEnded = true;
            Manager.SetPendingBattleResult(result);
            Manager.ChangeProcedure(ProcedureType.Result);
        }

        private bool TryOfferRevive()
        {
            if (revivePanel == null)
            {
                revivePanel = UnityEngine.Object.FindObjectOfType<RevivePanelController>(true);
            }

            return revivePanel != null && revivePanel.TryShow(playerHealth, this);
        }

        private void ResetRevivePanelState()
        {
            ConfigureRevivePanelReference();
            revivePanel?.ResetForBattle();
        }

        private void ConfigureRevivePanelReference()
        {
            if (revivePanel == null)
            {
                revivePanel = UnityEngine.Object.FindObjectOfType<RevivePanelController>(true);
            }

            revivePanel?.Configure(this, playerHealth);
        }

        private void RefreshBattleParticipantsFromRuntimeHooks()
        {
            var hooks = RuntimeSceneHooks.Active;
            if (hooks == null)
            {
                return;
            }

            var hookPlayer = hooks.PlayerHealth;
            var hookBoss = hooks.BossHealth;
            var resolvedPlayer = hookPlayer != null ? hookPlayer : playerHealth;
            var resolvedBoss = hookBoss != null ? hookBoss : bossHealth;
            if (resolvedPlayer == playerHealth && resolvedBoss == bossHealth)
            {
                return;
            }

            SetBattleParticipants(resolvedPlayer, resolvedBoss);
        }

        private void NotifyBattleTimeUpdated()
        {
            BattleTimeUpdated?.Invoke(battleTimeRemaining, battleTimeLimit, hasTimeLimit);
        }

        private bool IsCurrentProcedureActive()
        {
            return Manager != null && Manager.CurrentProcedureType == ProcedureType.Battle;
        }

        private void SetPausedInternal(bool paused, bool force)
        {
            if (!force && isPaused == paused)
            {
                return;
            }

            isPaused = paused;
            Time.timeScale = isPaused ? 0f : 1f;
            PauseStateChanged?.Invoke(isPaused);
        }
    }
}
