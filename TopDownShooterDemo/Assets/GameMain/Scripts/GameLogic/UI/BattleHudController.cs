using GameMain.Builtin.Entry;
using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Boss;
using GameMain.GameLogic.Player;
using GameMain.GameLogic.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Formal runtime HUD for health and battle timer.
    /// Ownership contract: display-only; reads runtime truth from gameplay owners and does not own combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHudController : MonoBehaviour
    {
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Text procedureText;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private Text bossHealthText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text dodgeCooldownText;
        [SerializeField] private Text bossDangerText;
        [SerializeField] private Text hpValueText;
        [SerializeField] private Text armorValueText;
        [SerializeField] private Text energyValueText;
        [SerializeField] private Image playerHealthFillImage;
        [SerializeField] private Image bossHealthFillImage;
        [SerializeField] private Image hpBarFillImage;
        [SerializeField] private Image armorBarFillImage;
        [SerializeField] private Image energyBarFillImage;
        [SerializeField] private Image dodgeCooldownFillImage;
        [SerializeField] private Image dodgeIconImage;
        [SerializeField] private Color healthGoodColor = new Color(0.28f, 0.86f, 0.45f, 1f);
        [SerializeField] private Color healthLowColor = new Color(0.93f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color dodgeReadyColor = new Color(0.3f, 0.9f, 1f, 1f);
        [SerializeField] private Color dodgeCooldownColor = new Color(0.3f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color dodgeActiveColor = new Color(1f, 0.8f, 0.35f, 1f);
        [SerializeField] private Color bossDangerIdleColor = new Color(1f, 0.96f, 0.84f, 0.74f);
        [SerializeField] private Color bossDangerActiveColor = new Color(1f, 0.28f, 0.2f, 1f);
        [SerializeField] private bool showProcedureName = true;
        [SerializeField] private bool visibleOnlyInBattle = true;

        private ProcedureManager procedureManager;
        private ProcedureBattle activeBattleProcedure;
        private PlayerHealth playerHealth;
        private BossHealth bossHealth;
        private PlayerController playerController;
        private BossBrain bossBrain;
        private CanvasGroup selfCanvasGroup;
        private bool loggedMissingViewRefs;

        private void Awake()
        {
            if (hudRoot == null)
            {
                hudRoot = gameObject;
            }

            if (hudRoot == gameObject)
            {
                selfCanvasGroup = GetComponent<CanvasGroup>();
                if (selfCanvasGroup == null)
                {
                    selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void OnEnable()
        {
            TryAutoResolve();
            ValidateViewReferences();
            SubscribeAll();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        public void BindView(
            GameObject root,
            Text procedureLabel,
            Text playerLabel,
            Text bossLabel,
            Text timerLabel,
            Image playerFill = null,
            Image bossFill = null,
            Text dodgeLabel = null,
            Image dodgeFill = null,
            Image dodgeIcon = null,
            Text bossDangerLabel = null)
        {
            hudRoot = root;
            procedureText = procedureLabel;
            playerHealthText = playerLabel;
            bossHealthText = bossLabel;
            timerText = timerLabel;
            playerHealthFillImage = playerFill;
            bossHealthFillImage = bossFill;
            dodgeCooldownText = dodgeLabel;
            dodgeCooldownFillImage = dodgeFill;
            dodgeIconImage = dodgeIcon;
            bossDangerText = bossDangerLabel;
        }

        public void BindResourceBars(Image hpFill, Image armorFill, Image energyFill, Text hpValue = null, Text armorValue = null, Text energyValue = null)
        {
            hpBarFillImage = hpFill;
            armorBarFillImage = armorFill;
            energyBarFillImage = energyFill;
            hpValueText = hpValue;
            armorValueText = armorValue;
            energyValueText = energyValue;
        }

        public void Configure(ProcedureManager manager, PlayerHealth player, BossHealth boss)
        {
            UnsubscribeAll();
            procedureManager = manager;
            playerHealth = player;
            bossHealth = boss;
            playerController = playerHealth != null ? playerHealth.GetComponent<PlayerController>() : null;
            bossBrain = bossHealth != null ? bossHealth.GetComponent<BossBrain>() : null;
            SubscribeAll();
            RefreshAll();
        }

        private void Update()
        {
            RefreshDynamicReadouts();
        }

        private void TryAutoResolve()
        {
            if (procedureManager == null && GameEntryBridge.IsReady)
            {
                procedureManager = GameEntryBridge.Procedure;
            }

            if (playerHealth == null && RuntimeSceneHooks.Active != null)
            {
                playerHealth = RuntimeSceneHooks.Active.PlayerHealth;
            }

            if (bossHealth == null && RuntimeSceneHooks.Active != null)
            {
                bossHealth = RuntimeSceneHooks.Active.BossHealth;
            }

            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (bossBrain == null && bossHealth != null)
            {
                bossBrain = bossHealth.GetComponent<BossBrain>();
            }
        }

        private void ValidateViewReferences()
        {
            if (loggedMissingViewRefs)
            {
                return;
            }

            if (playerHealthText == null || bossHealthText == null || timerText == null ||
                playerHealthFillImage == null || bossHealthFillImage == null)
            {
                loggedMissingViewRefs = true;
                Debug.LogWarning(
                    "BattleHudController is missing text or fill image references. HUD display may be incomplete.",
                    this);
            }
        }

        private void SubscribeAll()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
                procedureManager.ProcedureChanged += OnProcedureChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= OnPlayerHealthChanged;
                playerHealth.HealthChanged += OnPlayerHealthChanged;
                playerHealth.ArmorChanged -= OnPlayerArmorChanged;
                playerHealth.ArmorChanged += OnPlayerArmorChanged;
                playerHealth.EnergyChanged -= OnPlayerEnergyChanged;
                playerHealth.EnergyChanged += OnPlayerEnergyChanged;
                playerHealth.OnDied -= OnPlayerDied;
                playerHealth.OnDied += OnPlayerDied;
            }

            if (bossHealth != null)
            {
                bossHealth.HealthChanged -= OnBossHealthChanged;
                bossHealth.HealthChanged += OnBossHealthChanged;
                bossHealth.OnDied -= OnBossDied;
                bossHealth.OnDied += OnBossDied;
            }

            BindActiveBattleProcedure();
        }

        private void UnsubscribeAll()
        {
            if (procedureManager != null)
            {
                procedureManager.ProcedureChanged -= OnProcedureChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= OnPlayerHealthChanged;
                playerHealth.ArmorChanged -= OnPlayerArmorChanged;
                playerHealth.EnergyChanged -= OnPlayerEnergyChanged;
                playerHealth.OnDied -= OnPlayerDied;
            }

            if (bossHealth != null)
            {
                bossHealth.HealthChanged -= OnBossHealthChanged;
                bossHealth.OnDied -= OnBossDied;
            }

            UnbindBattleProcedure();
        }

        private void BindActiveBattleProcedure()
        {
            UnbindBattleProcedure();
            activeBattleProcedure = procedureManager != null ? procedureManager.CurrentProcedure as ProcedureBattle : null;
            if (activeBattleProcedure != null)
            {
                activeBattleProcedure.BattleTimeUpdated -= OnBattleTimeUpdated;
                activeBattleProcedure.BattleTimeUpdated += OnBattleTimeUpdated;
                UpdateTimerText(activeBattleProcedure.RemainingBattleTime, activeBattleProcedure.BattleTimeLimit, activeBattleProcedure.HasTimeLimit);
            }
            else
            {
                SetText(timerText, "Time: --");
            }
        }

        private void UnbindBattleProcedure()
        {
            if (activeBattleProcedure != null)
            {
                activeBattleProcedure.BattleTimeUpdated -= OnBattleTimeUpdated;
                activeBattleProcedure = null;
            }
        }

        private void RefreshAll()
        {
            var currentProcedure = procedureManager != null ? procedureManager.CurrentProcedureType : ProcedureType.None;

            if (showProcedureName)
            {
                var presetName = BossPresetController.Instance != null ? BossPresetController.Instance.CurrentPresetName : "--";
                SetText(procedureText, "Procedure: " + currentProcedure + " | Boss: " + presetName);
            }
            else
            {
                SetText(procedureText, string.Empty);
            }

            if (visibleOnlyInBattle && hudRoot != null)
            {
                ApplyVisibility(currentProcedure == ProcedureType.Battle);
            }

            if (playerHealth != null)
            {
                OnPlayerHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                OnPlayerArmorChanged(playerHealth.CurrentArmor, playerHealth.MaxArmor);
                OnPlayerEnergyChanged(playerHealth.CurrentEnergy, playerHealth.MaxEnergy);
            }

            if (bossHealth != null)
            {
                OnBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }

            RefreshDynamicReadouts();
        }

        private void OnProcedureChanged(ProcedureType previous, ProcedureType current)
        {
            BindActiveBattleProcedure();
            RefreshAll();
        }

        private void OnPlayerHealthChanged(float current, float max)
        {
            SetText(playerHealthText, BuildPlayerHealthLabel(current, max));
            SetHealthFill(playerHealthFillImage, current, max);
            SetResourceFill(hpBarFillImage, current, max);
            SetResourceText(hpValueText, current, max);
        }

        private void OnPlayerArmorChanged(float current, float max)
        {
            SetResourceFill(armorBarFillImage, current, max);
            SetResourceText(armorValueText, current, max);
        }

        private void OnPlayerEnergyChanged(float current, float max)
        {
            SetResourceFill(energyBarFillImage, current, max);
            SetResourceText(energyValueText, current, max);
        }

        private void OnBossHealthChanged(float current, float max)
        {
            SetText(bossHealthText, string.Format("Boss HP: {0:0}/{1:0}", current, max));
            SetHealthFill(bossHealthFillImage, current, max);
        }

        private void OnBattleTimeUpdated(float remaining, float limit, bool hasLimit)
        {
            UpdateTimerText(remaining, limit, hasLimit);
        }

        private void UpdateTimerText(float remaining, float limit, bool hasLimit)
        {
            if (!hasLimit)
            {
                SetText(timerText, "Time: --");
                return;
            }

            SetText(timerText, string.Format("Time: {0:0.0}s / {1:0.0}s", Mathf.Max(0f, remaining), limit));
        }

        private void OnPlayerDied()
        {
            if (playerHealth != null)
            {
                OnPlayerHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                OnPlayerArmorChanged(playerHealth.CurrentArmor, playerHealth.MaxArmor);
                OnPlayerEnergyChanged(playerHealth.CurrentEnergy, playerHealth.MaxEnergy);
            }
        }

        private void OnBossDied()
        {
            if (bossHealth != null)
            {
                OnBossHealthChanged(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }
        }

        private void RefreshDynamicReadouts()
        {
            UpdatePlayerWeaponReadout();
            UpdateDodgeReadout();
            UpdateBossDangerReadout();
        }

        private void UpdatePlayerWeaponReadout()
        {
            if (playerHealth == null || playerHealthText == null)
            {
                return;
            }

            SetText(playerHealthText, BuildPlayerHealthLabel(playerHealth.CurrentHealth, playerHealth.MaxHealth));
        }

        private void UpdateDodgeReadout()
        {
            if (dodgeCooldownText == null && dodgeCooldownFillImage == null && dodgeIconImage == null)
            {
                return;
            }

            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (playerController == null)
            {
                SetText(dodgeCooldownText, "Dodge: --");
                if (dodgeCooldownFillImage != null)
                {
                    dodgeCooldownFillImage.fillAmount = 0f;
                }

                if (dodgeIconImage != null)
                {
                    dodgeIconImage.color = dodgeCooldownColor;
                }

                return;
            }

            var cooldownDuration = Mathf.Max(0.01f, playerController.DodgeCooldownDuration);
            var cooldownRemaining = Mathf.Max(0f, playerController.DodgeCooldownRemaining);
            var readyRatio = 1f - Mathf.Clamp01(cooldownRemaining / cooldownDuration);

            if (playerController.IsDodging)
            {
                SetText(dodgeCooldownText, "Dodge: ACTIVE");
                if (dodgeCooldownFillImage != null)
                {
                    dodgeCooldownFillImage.fillAmount = 1f;
                    dodgeCooldownFillImage.color = dodgeActiveColor;
                }

                if (dodgeIconImage != null)
                {
                    dodgeIconImage.color = dodgeActiveColor;
                }

                return;
            }

            if (playerController.IsDodgeReady)
            {
                SetText(dodgeCooldownText, "Dodge (" + playerController.DodgeKey + "): READY");
                if (dodgeCooldownFillImage != null)
                {
                    dodgeCooldownFillImage.fillAmount = 1f;
                    dodgeCooldownFillImage.color = dodgeReadyColor;
                }

                if (dodgeIconImage != null)
                {
                    dodgeIconImage.color = dodgeReadyColor;
                }

                return;
            }

            SetText(dodgeCooldownText, string.Format("Dodge ({0}): {1:0.0}s", playerController.DodgeKey, cooldownRemaining));
            if (dodgeCooldownFillImage != null)
            {
                dodgeCooldownFillImage.fillAmount = readyRatio;
                dodgeCooldownFillImage.color = dodgeCooldownColor;
            }

            if (dodgeIconImage != null)
            {
                dodgeIconImage.color = dodgeCooldownColor;
            }
        }

        private void UpdateBossDangerReadout()
        {
            if (bossDangerText == null)
            {
                return;
            }

            if (bossBrain == null && bossHealth != null)
            {
                bossBrain = bossHealth.GetComponent<BossBrain>();
            }

            if (bossBrain == null)
            {
                SetText(bossDangerText, string.Empty);
                return;
            }

            var warning = bossBrain.CurrentHudDangerText;
            var isActiveWarning = !string.IsNullOrEmpty(warning);
            SetText(bossDangerText, warning);
            bossDangerText.color = isActiveWarning ? bossDangerActiveColor : bossDangerIdleColor;
        }

        private string BuildPlayerHealthLabel(float current, float max)
        {
            var baseLabel = string.Format("Player HP: {0:0}/{1:0}", current, max);
            if (playerController == null && playerHealth != null)
            {
                playerController = playerHealth.GetComponent<PlayerController>();
            }

            if (playerController == null)
            {
                return baseLabel;
            }

            var weaponSnapshot = playerController.GetActiveWeaponRuntimeSnapshot();
            var weaponLabel = weaponSnapshot.Label;

            return baseLabel + " | Weapon: " + weaponLabel + " [" + playerController.WeaponSwitchKey + "]";
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private void SetHealthFill(Image fillImage, float current, float max)
        {
            if (fillImage == null)
            {
                return;
            }

            var ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(healthLowColor, healthGoodColor, ratio);
        }

        private static void SetResourceFill(Image fillImage, float current, float max)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private static void SetResourceText(Text label, float current, float max)
        {
            if (label != null)
            {
                label.text = string.Format("{0:0}/{1:0}", Mathf.Max(0f, current), Mathf.Max(0f, max));
            }
        }

        private void ApplyVisibility(bool visible)
        {
            if (hudRoot == null)
            {
                return;
            }

            if (hudRoot != gameObject)
            {
                hudRoot.SetActive(visible);
                return;
            }

            if (selfCanvasGroup == null)
            {
                selfCanvasGroup = GetComponent<CanvasGroup>();
                if (selfCanvasGroup == null)
                {
                    selfCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            selfCanvasGroup.alpha = visible ? 1f : 0f;
            selfCanvasGroup.blocksRaycasts = visible;
            selfCanvasGroup.interactable = visible;
        }
    }
}
