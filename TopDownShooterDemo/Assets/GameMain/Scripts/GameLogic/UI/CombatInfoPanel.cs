using GameMain.GameLogic.LuaDemo;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.UI
{
    /// <summary>
    /// Display-only combat info panel driven by Lua config text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatInfoPanel : BasePanel
    {
        [SerializeField] private LuaDemoRuntime luaRuntime;
        [SerializeField] private Text titleText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Text weaponDescriptionText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text sourceText;
        [SerializeField] private Button reloadButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private KeyCode reloadKey = KeyCode.L;

        protected override void Awake()
        {
            base.Awake();
            ResolveRuntime();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveRuntime();
            BindButtons();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void Update()
        {
            if (Input.GetKeyDown(reloadKey))
            {
                ReloadLua();
            }
        }

        public void BindView(
            Text titleLabel,
            Text skillLabel,
            Text weaponLabel,
            Text hintLabel,
            Text sourceLabel,
            Button reloadBtn,
            Button closeBtn)
        {
            titleText = titleLabel;
            skillDescriptionText = skillLabel;
            weaponDescriptionText = weaponLabel;
            hintText = hintLabel;
            sourceText = sourceLabel;
            reloadButton = reloadBtn;
            closeButton = closeBtn;
            BindButtons();
        }

        public void Configure(LuaDemoRuntime runtime)
        {
            luaRuntime = runtime;
            Refresh();
        }

        public void ReloadLua()
        {
            ResolveRuntime();
            if (luaRuntime != null)
            {
                luaRuntime.Reload();
            }

            Refresh();
        }

        private void Refresh()
        {
            var config = luaRuntime != null ? luaRuntime.CurrentConfig : LuaConfigData.Empty;
            SetText(titleText, config.Title);
            SetText(skillDescriptionText, "技能说明：" + config.SkillDescription);
            SetText(weaponDescriptionText, "武器说明：" + config.WeaponDescription);
            SetText(hintText, config.Hint);
            SetText(sourceText, "Lua Source: " + (luaRuntime != null ? luaRuntime.SourceLabel : LuaConfigSource.Missing.ToString()));
        }

        private void ResolveRuntime()
        {
            if (luaRuntime == null)
            {
                luaRuntime = GetComponent<LuaDemoRuntime>();
            }
        }

        private void BindButtons()
        {
            if (reloadButton != null)
            {
                reloadButton.onClick.RemoveListener(ReloadLua);
                reloadButton.onClick.AddListener(ReloadLua);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void UnbindButtons()
        {
            if (reloadButton != null)
            {
                reloadButton.onClick.RemoveListener(ReloadLua);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
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
