using UnityEngine;
using UnityEngine.UI;

namespace GameMain.GameLogic.LuaDemo
{
    /// <summary>
    /// Display-only Lua config panel. It never writes gameplay state or calls combat systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuaConfigDemoPanel : MonoBehaviour
    {
        [SerializeField] private LuaDemoRuntime runtime;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text versionText;
        [SerializeField] private Text skillDescriptionText;
        [SerializeField] private Text weaponDescriptionText;
        [SerializeField] private Text hintText;
        [SerializeField] private Button reloadButton;
        [SerializeField] private KeyCode reloadKey = KeyCode.L;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (runtime == null)
            {
                runtime = GetComponent<LuaDemoRuntime>();
            }

            BindButton();
        }

        private void OnEnable()
        {
            BindButton();
            ReloadAndRefresh();
        }

        private void OnDisable()
        {
            if (reloadButton != null)
            {
                reloadButton.onClick.RemoveListener(ReloadAndRefresh);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(reloadKey))
            {
                ReloadAndRefresh();
            }
        }

        public void BindView(
            GameObject root,
            Text titleLabel,
            Text versionLabel,
            Text skillLabel,
            Text weaponLabel,
            Text hintLabel,
            Button reloadBtn)
        {
            panelRoot = root;
            titleText = titleLabel;
            versionText = versionLabel;
            skillDescriptionText = skillLabel;
            weaponDescriptionText = weaponLabel;
            hintText = hintLabel;
            reloadButton = reloadBtn;
            BindButton();
        }

        public void Configure(LuaDemoRuntime newRuntime)
        {
            runtime = newRuntime;
            Refresh(runtime != null ? runtime.CurrentConfig : LuaConfigData.Empty);
        }

        public void ReloadAndRefresh()
        {
            if (runtime == null)
            {
                Refresh(LuaConfigData.Empty);
                return;
            }

            runtime.Reload();
            Refresh(runtime.CurrentConfig);
        }

        private void Refresh(LuaConfigData config)
        {
            SetText(titleText, config.Title);
            SetText(versionText, config.Version + "\nLua Source: " + (runtime != null ? runtime.SourceLabel : LuaConfigSource.Missing.ToString()));
            EnsureSourceLineHeight();
            SetText(skillDescriptionText, "技能说明：" + config.SkillDescription);
            SetText(weaponDescriptionText, "武器说明：" + config.WeaponDescription);
            SetText(hintText, config.Hint + "\nLua 表配置：" + config.RoleSummary);
        }

        private void BindButton()
        {
            if (reloadButton == null)
            {
                return;
            }

            reloadButton.onClick.RemoveListener(ReloadAndRefresh);
            reloadButton.onClick.AddListener(ReloadAndRefresh);
        }

        private void EnsureSourceLineHeight()
        {
            if (versionText == null)
            {
                return;
            }

            var size = versionText.rectTransform.sizeDelta;
            if (size.y < 42f)
            {
                size.y = 42f;
                versionText.rectTransform.sizeDelta = size;
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
