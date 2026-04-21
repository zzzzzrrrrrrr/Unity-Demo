// 路径: Assets/_Scripts/Audio/UIButtonSfxHook.cs
// 功能: 按钮点击音效钩子，自动转发到 DemoSfxService。
using UnityEngine;
using UnityEngine.UI;

namespace ARPGDemo.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonSfxHook : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            DemoSfxService.TryPlayUiClick();
        }
    }
}
