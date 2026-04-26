using GameMain.Builtin.Procedure;
using GameMain.GameLogic.Meta;
using GameMain.GameLogic.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// One-run revive offer. It spends profile coins and calls a narrow PlayerHealth revive API.
    /// It does not own damage, death, or combat result truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RevivePanelController : MonoBehaviour
    {
        private const int ReviveCost = 200;
        private const float ReviveHealthRatio = 0.5f;
        private const float ReviveEnergyRatio = 0.5f;
        private const float ReviveProtectionSeconds = 1.2f;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text coinText;
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button giveUpButton;

        private PlayerHealth playerHealth;
        private ProcedureBattle battleProcedure;
        private CanvasGroup canvasGroup;
        private bool offerVisible;
        private bool reviveUsed;
        private bool reviveDeclined;
        private float previousTimeScale = 1f;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }

            BindEvents();
            Hide();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        public void BindView(GameObject root, Text title, Text detail, Text coin, Button revive, Button giveUp)
        {
            panelRoot = root != null ? root : gameObject;
            titleText = title;
            detailText = detail;
            coinText = coin;
            reviveButton = revive;
            giveUpButton = giveUp;
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }

            BindEvents();
            Hide();
        }

        public void Configure(ProcedureBattle battle, PlayerHealth player)
        {
            battleProcedure = battle;
            playerHealth = player;
        }

        public void ResetForBattle()
        {
            offerVisible = false;
            reviveUsed = false;
            reviveDeclined = false;
            Hide();
        }

        public bool TryShow(PlayerHealth player, ProcedureBattle battle)
        {
            if (reviveUsed || reviveDeclined)
            {
                return false;
            }

            if (offerVisible)
            {
                return true;
            }

            playerHealth = player;
            battleProcedure = battle;
            if (playerHealth == null || battleProcedure == null)
            {
                return false;
            }

            offerVisible = true;
            previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            RefreshView();
            Show();
            return true;
        }

        private void BindEvents()
        {
            if (reviveButton != null)
            {
                reviveButton.onClick.RemoveListener(OnReviveClicked);
                reviveButton.onClick.AddListener(OnReviveClicked);
            }

            if (giveUpButton != null)
            {
                giveUpButton.onClick.RemoveListener(OnGiveUpClicked);
                giveUpButton.onClick.AddListener(OnGiveUpClicked);
            }
        }

        private void UnbindEvents()
        {
            if (reviveButton != null)
            {
                reviveButton.onClick.RemoveListener(OnReviveClicked);
            }

            if (giveUpButton != null)
            {
                giveUpButton.onClick.RemoveListener(OnGiveUpClicked);
            }
        }

        private void OnReviveClicked()
        {
            if (!offerVisible || playerHealth == null)
            {
                return;
            }

            if (!PlayerProfileService.SpendCoins(ReviveCost))
            {
                RefreshView();
                return;
            }

            reviveUsed = true;
            offerVisible = false;
            RestoreTimeScale();
            Hide();
            playerHealth.Revive(ReviveHealthRatio, 0f, ReviveEnergyRatio, ReviveProtectionSeconds);
        }

        private void OnGiveUpClicked()
        {
            if (!offerVisible)
            {
                return;
            }

            reviveDeclined = true;
            offerVisible = false;
            RestoreTimeScale();
            Hide();
            battleProcedure?.ResolvePlayerDeathAfterReviveDeclined();
        }

        private void RefreshView()
        {
            var coins = PlayerProfileService.GetCoins();
            var canRevive = coins >= ReviveCost;

            SetText(titleText, "复活");
            SetText(detailText, canRevive
                ? "消耗蓝币继续本局。每局仅可复活一次，复活后恢复 50% HP 与 50% 能量。"
                : "蓝币不足，无法复活。本局可以选择放弃并进入结算。");
            SetText(coinText, string.Format("蓝币：{0} / 花费：{1}", coins, ReviveCost));
            if (reviveButton != null)
            {
                reviveButton.interactable = canRevive;
            }
        }

        private void Show()
        {
            transform.SetAsLastSibling();
            if (panelRoot != null)
            {
                panelRoot.transform.SetAsLastSibling();
                panelRoot.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }

        private void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = Mathf.Approximately(previousTimeScale, 0f) ? 1f : previousTimeScale;
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
