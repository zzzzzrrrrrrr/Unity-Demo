using GameMain.GameLogic.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Lobby meta HUD showing local profile data only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetaHudPanel : BasePanel
    {
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text coinText;

        private float nextRefreshTime;

        public static MetaHudPanel Create(Transform parent)
        {
            var panel = MetaUiFactory.CreatePanel(parent, "MetaHudPanel", new Vector2(-690f, 462f), new Vector2(450f, 72f), new Color(0.035f, 0.06f, 0.085f, 0.9f));
            var canvasGroup = MetaUiFactory.GetOrAddComponent<CanvasGroup>(panel);
            var controller = MetaUiFactory.GetOrAddComponent<MetaHudPanel>(panel);
            controller.BindPanelRoot(panel, canvasGroup);
            controller.BuildView(panel.transform);
            controller.Show();
            controller.Refresh();
            return controller;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.5f;
            Refresh();
        }

        public void Refresh()
        {
            var profile = PlayerProfileService.Current;
            if (playerNameText != null)
            {
                playerNameText.text = "玩家 " + profile.PlayerName;
            }

            if (coinText != null)
            {
                coinText.text = "蓝币 " + profile.Coin;
            }
        }

        private void BuildView(Transform root)
        {
            var gem = MetaUiFactory.LoadSprite("Images/Materials/Gem");
            MetaUiFactory.CreateImage(root, "CoinIcon", gem, new Vector2(-172f, -13f), new Vector2(26f, 26f), Color.white);
            playerNameText = MetaUiFactory.CreateText(root, "PlayerNameText", "玩家 Player", new Vector2(8f, 15f), new Vector2(390f, 26f), 19, TextAnchor.MiddleLeft, FontStyle.Bold);
            coinText = MetaUiFactory.CreateText(root, "CoinText", "蓝币 0", new Vector2(36f, -13f), new Vector2(306f, 26f), 19, TextAnchor.MiddleLeft, FontStyle.Normal);
        }
    }
}
