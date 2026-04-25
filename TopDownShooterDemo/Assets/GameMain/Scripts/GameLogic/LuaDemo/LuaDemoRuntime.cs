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
            return new LuaConfigData(
                ReadString(table, "version"),
                ReadString(table, "title"),
                ReadString(table, "skillDescription"),
                ReadString(table, "weaponDescription"),
                ReadString(table, "hint"));
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
        {
            Version = string.IsNullOrWhiteSpace(version) ? Empty.Version : version;
            Title = string.IsNullOrWhiteSpace(title) ? Empty.Title : title;
            SkillDescription = string.IsNullOrWhiteSpace(skillDescription) ? Empty.SkillDescription : skillDescription;
            WeaponDescription = string.IsNullOrWhiteSpace(weaponDescription) ? Empty.WeaponDescription : weaponDescription;
            Hint = string.IsNullOrWhiteSpace(hint) ? Empty.Hint : hint;
        }

        public string Version { get; }

        public string Title { get; }

        public string SkillDescription { get; }

        public string WeaponDescription { get; }

        public string Hint { get; }
    }
}
