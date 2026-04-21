// Path: Assets/_Scripts/Core/UIManager.cs
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Core
{
    /// <summary>
    /// </summary>
    public class UIManager : SingletonMono<UIManager>
    {
        [Header("Boot")]
        [SerializeField] private bool hideAllPanelsOnAwake = true;

        protected override bool DontDestroyEnabled => true;

        protected override void Awake()
        {
            base.Awake();

            if (hideAllPanelsOnAwake)
            {
                CloseAll();
            }
        }

        /// <summary>
        /// </summary>
        public void CloseAll()
        {
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.MainMenu, false, string.Empty));
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.Pause, false, string.Empty));
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.Result, false, string.Empty));
        }

        public void ShowMainMenu(bool show)
        {
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.MainMenu, show, string.Empty));
        }

        public void ShowPause(bool show)
        {
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.Pause, show, string.Empty));
        }

        public void ShowResult(bool show, string message)
        {
            EventCenter.Broadcast(new UIRequestEvent(UIPanelType.Result, show, message));
        }
    }
}

