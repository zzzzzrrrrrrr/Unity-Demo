using UnityEngine;

namespace GameMain.GameLogic.Meta
{
    /// <summary>
    /// Lightweight local profile facade. It is presentation data only and never drives combat truth.
    /// </summary>
    public static class PlayerProfileService
    {
        public const string PlayerNameKey = "playerName";
        public const string CoinKey = "coin";
        public const string SelectedRoleIdKey = "selectedRoleId";

        private const string DefaultPlayerName = "Player";
        private const int DefaultCoins = 5393;

        private static PlayerProfile cachedProfile;
        private static bool loaded;

        public static PlayerProfile Current
        {
            get { return loaded ? cachedProfile : Load(); }
        }

        public static PlayerProfile Load()
        {
            cachedProfile = new PlayerProfile
            {
                PlayerName = PlayerPrefs.GetString(PlayerNameKey, DefaultPlayerName),
                Coin = PlayerPrefs.GetInt(CoinKey, DefaultCoins),
                SelectedRoleId = PlayerPrefs.GetString(SelectedRoleIdKey, string.Empty),
            };
            loaded = true;
            return cachedProfile;
        }

        public static void Save()
        {
            if (!loaded)
            {
                Load();
            }

            PlayerPrefs.SetString(PlayerNameKey, string.IsNullOrWhiteSpace(cachedProfile.PlayerName) ? DefaultPlayerName : cachedProfile.PlayerName);
            PlayerPrefs.SetInt(CoinKey, Mathf.Max(0, cachedProfile.Coin));
            PlayerPrefs.SetString(SelectedRoleIdKey, cachedProfile.SelectedRoleId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void SetPlayerName(string playerName)
        {
            if (!loaded)
            {
                Load();
            }

            cachedProfile.PlayerName = string.IsNullOrWhiteSpace(playerName) ? DefaultPlayerName : playerName.Trim();
            Save();
        }

        public static void SetSelectedRoleId(string roleId)
        {
            if (!loaded)
            {
                Load();
            }

            cachedProfile.SelectedRoleId = roleId ?? string.Empty;
            Save();
        }

        public static int AddCoins(int amount)
        {
            if (!loaded)
            {
                Load();
            }

            cachedProfile.Coin = Mathf.Max(0, cachedProfile.Coin + Mathf.Max(0, amount));
            Save();
            return cachedProfile.Coin;
        }

        public static bool SpendCoins(int amount)
        {
            if (!loaded)
            {
                Load();
            }

            var cost = Mathf.Max(0, amount);
            if (cachedProfile.Coin < cost)
            {
                return false;
            }

            cachedProfile.Coin -= cost;
            Save();
            return true;
        }

        public static int GetCoins()
        {
            return Current.Coin;
        }
    }

    public struct PlayerProfile
    {
        public string PlayerName;
        public int Coin;
        public string SelectedRoleId;
    }
}
