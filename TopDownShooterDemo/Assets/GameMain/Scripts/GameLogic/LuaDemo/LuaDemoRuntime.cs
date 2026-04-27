using System;
using System.IO;
using System.Text;
using UnityEngine;
using XLua;

namespace GameMain.GameLogic.LuaDemo
{
    /// <summary>
    /// Minimal xLua runtime for display-only config demos. Lua does not receive gameplay objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuaDemoRuntime : MonoBehaviour
    {
        [SerializeField] private string luaConfigResourcePath = "Lua/LuaConfigDemo.lua";
        [SerializeField] private string hotUpdateRelativePath = "HotUpdate/Lua/LuaConfigDemo.lua.txt";

        private LuaEnv luaEnv;

        public LuaConfigData CurrentConfig { get; private set; } = LuaConfigData.Empty;

        public LuaConfigSource CurrentSource { get; private set; } = LuaConfigSource.Missing;

        public string SourceLabel => CurrentSource.ToString();

        private void Awake()
        {
            EnsureLuaEnv();
        }

        private void OnDestroy()
        {
            if (luaEnv != null)
            {
                luaEnv.Dispose();
                luaEnv = null;
            }
        }

        public bool Reload()
        {
            EnsureLuaEnv();

            var persistentPath = GetPersistentLuaPath();
            if (TryLoadExternalLua(persistentPath, LuaConfigSource.PersistentDataPath, out var persistentConfig))
            {
                CurrentConfig = persistentConfig;
                CurrentSource = LuaConfigSource.PersistentDataPath;
                return true;
            }

            if (File.Exists(persistentPath))
            {
                return TryLoadResourcesFallback(LuaConfigSource.ErrorFallback);
            }

            var streamingPath = GetStreamingAssetsLuaPath();
            if (TryLoadExternalLua(streamingPath, LuaConfigSource.StreamingAssets, out var streamingConfig))
            {
                CurrentConfig = streamingConfig;
                CurrentSource = LuaConfigSource.StreamingAssets;
                return true;
            }

            if (File.Exists(streamingPath))
            {
                return TryLoadResourcesFallback(LuaConfigSource.ErrorFallback);
            }

            return TryLoadResourcesFallback(LuaConfigSource.ResourcesFallback);
        }

        private bool TryLoadExternalLua(string path, LuaConfigSource source, out LuaConfigData config)
        {
            config = LuaConfigData.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var luaText = File.ReadAllText(path, Encoding.UTF8);
                config = ExecuteLuaConfig(NormalizeLuaText(luaText), source + ":" + path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LuaDemoRuntime failed to load external Lua config from " + path + ": " + exception.Message, this);
                return false;
            }
        }

        private bool TryLoadResourcesFallback(LuaConfigSource source)
        {
            var asset = Resources.Load<TextAsset>(luaConfigResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("LuaDemoRuntime could not load Lua config from Resources/" + luaConfigResourcePath, this);
                CurrentConfig = LuaConfigData.Empty;
                CurrentSource = source == LuaConfigSource.ErrorFallback ? LuaConfigSource.ErrorFallback : LuaConfigSource.Missing;
                return false;
            }

            try
            {
                CurrentConfig = ExecuteLuaConfig(NormalizeLuaText(asset.text), "Resources/" + luaConfigResourcePath);
                CurrentSource = source;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LuaDemoRuntime failed to execute Resources Lua config: " + exception.Message, this);
                CurrentConfig = LuaConfigData.Empty;
                CurrentSource = source == LuaConfigSource.ErrorFallback ? LuaConfigSource.ErrorFallback : LuaConfigSource.Missing;
                return false;
            }
        }

        private LuaConfigData ExecuteLuaConfig(string luaText, string chunkName)
        {
            var results = luaEnv.DoString(luaText, chunkName);
            var table = results != null && results.Length > 0 ? results[0] as LuaTable : null;
            if (table == null)
            {
                throw new InvalidOperationException("Lua config must return a table.");
            }

            try
            {
                return ReadConfig(table);
            }
            finally
            {
                table.Dispose();
            }
        }

        private static string NormalizeLuaText(string luaText)
        {
            if (!string.IsNullOrEmpty(luaText) && luaText[0] == '\uFEFF')
            {
                return luaText.Substring(1);
            }

            return luaText;
        }

        private string GetPersistentLuaPath()
        {
            return Path.Combine(Application.persistentDataPath, hotUpdateRelativePath);
        }

        private string GetStreamingAssetsLuaPath()
        {
            return Path.Combine(Application.streamingAssetsPath, hotUpdateRelativePath);
        }

        private void EnsureLuaEnv()
        {
            if (luaEnv == null)
            {
                luaEnv = new LuaEnv();
            }
        }

        private LuaConfigData ReadConfig(LuaTable table)
        {
            var selectedRoleId = ReadOptionalString(table, "selectedRoleId", "Ranger");
            if (TryReadFunctionConfig(table, selectedRoleId, out var functionConfig))
            {
                return functionConfig;
            }

            return ReadConfigTable(table, selectedRoleId);
        }

        private bool TryReadFunctionConfig(LuaTable rootTable, string selectedRoleId, out LuaConfigData config)
        {
            config = LuaConfigData.Empty;
            var configFunction = rootTable.Get<LuaFunction>("getCharacterConfig");
            if (configFunction == null)
            {
                return false;
            }

            LuaTable resultTable = null;
            try
            {
                var results = configFunction.Call(selectedRoleId);
                resultTable = results != null && results.Length > 0 ? results[0] as LuaTable : null;
                if (resultTable == null)
                {
                    Debug.LogWarning("LuaDemoRuntime getCharacterConfig did not return a table. Falling back to root table.", this);
                    return false;
                }

                config = ReadConfigTable(resultTable, selectedRoleId);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LuaDemoRuntime getCharacterConfig failed: " + exception.Message, this);
                return false;
            }
            finally
            {
                resultTable?.Dispose();
                configFunction.Dispose();
            }
        }

        private LuaConfigData ReadConfigTable(LuaTable table, string fallbackRoleId)
        {
            return new LuaConfigData(
                ReadString(table, "version"),
                ReadString(table, "title"),
                ReadString(table, "skillDescription"),
                ReadString(table, "weaponDescription"),
                ReadString(table, "hint"),
                ReadOptionalString(table, "roleId", fallbackRoleId),
                ReadOptionalInt(table, "redHealth", 0),
                ReadOptionalInt(table, "blueArmor", 0),
                ReadOptionalInt(table, "energy", 0));
        }

        private string ReadString(LuaTable table, string fieldName)
        {
            var value = table.Get<string>(fieldName);
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogWarning("LuaDemoRuntime config field is missing or empty: " + fieldName, this);
                return string.Empty;
            }

            return value;
        }

        private string ReadOptionalString(LuaTable table, string fieldName, string fallback)
        {
            var value = table.Get<string>(fieldName);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private int ReadOptionalInt(LuaTable table, string fieldName, int fallback)
        {
            var value = table.Get<int>(fieldName);
            return value <= 0 ? fallback : value;
        }
    }

    public enum LuaConfigSource
    {
        PersistentDataPath,
        StreamingAssets,
        ResourcesFallback,
        Missing,
        ErrorFallback,
    }

    public readonly struct LuaConfigData
    {
        public static readonly LuaConfigData Empty = new LuaConfigData(
            "Lua Config Missing",
            "Lua 配置演示",
            "未读取到 Lua 技能说明。",
            "未读取到 Lua 武器说明。",
            "Lua 配置缺失或执行失败。");

        public LuaConfigData(
            string version,
            string title,
            string skillDescription,
            string weaponDescription,
            string hint)
            : this(version, title, skillDescription, weaponDescription, hint, string.Empty, 0, 0, 0)
        {
        }

        public LuaConfigData(
            string version,
            string title,
            string skillDescription,
            string weaponDescription,
            string hint,
            string roleId,
            int redHealth,
            int blueArmor,
            int energy)
        {
            Version = string.IsNullOrWhiteSpace(version) ? Empty.Version : version;
            Title = string.IsNullOrWhiteSpace(title) ? Empty.Title : title;
            SkillDescription = string.IsNullOrWhiteSpace(skillDescription) ? Empty.SkillDescription : skillDescription;
            WeaponDescription = string.IsNullOrWhiteSpace(weaponDescription) ? Empty.WeaponDescription : weaponDescription;
            Hint = string.IsNullOrWhiteSpace(hint) ? Empty.Hint : hint;
            RoleId = string.IsNullOrWhiteSpace(roleId) ? "N/A" : roleId;
            RedHealth = Mathf.Max(0, redHealth);
            BlueArmor = Mathf.Max(0, blueArmor);
            Energy = Mathf.Max(0, energy);
        }

        public string Version { get; }

        public string Title { get; }

        public string SkillDescription { get; }

        public string WeaponDescription { get; }

        public string Hint { get; }

        public string RoleId { get; }

        public int RedHealth { get; }

        public int BlueArmor { get; }

        public int Energy { get; }

        public string RoleSummary => RoleId + " HP " + RedHealth + " / Armor " + BlueArmor + " / Energy " + Energy;
    }
}
