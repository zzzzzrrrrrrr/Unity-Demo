using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// World portal in CharacterSelectScene. Enabled only after confirmation.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectPortalController : MonoBehaviour
    {
        [SerializeField] private string runSceneName = "RunScene";
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private bool portalEnabled;
        [SerializeField] private SpriteRenderer portalRenderer;
        [SerializeField] private Color disabledColor = new Color(0.4f, 0.52f, 0.66f, 0.5f);
        [SerializeField] private Color enabledColor = new Color(0.3f, 0.94f, 0.9f, 0.95f);

        private CharacterSelectAvatarController insideAvatar;

        public bool PortalEnabled => portalEnabled;

        public void Configure(string targetSceneName, KeyCode key)
        {
            runSceneName = string.IsNullOrWhiteSpace(targetSceneName) ? "RunScene" : targetSceneName;
            interactKey = key;
        }

        public void SetPortalEnabled(bool value)
        {
            portalEnabled = value;
            RefreshVisual();
        }

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            if (portalRenderer == null)
            {
                portalRenderer = GetComponent<SpriteRenderer>();
            }

            RefreshVisual();
        }

        private void Update()
        {
            if (!Application.isPlaying || !portalEnabled || insideAvatar == null)
            {
                return;
            }

            if (!insideAvatar.IsControllable)
            {
                insideAvatar = null;
                return;
            }

            if (!Input.GetKeyDown(interactKey))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(runSceneName))
            {
                Debug.LogWarning("CharacterSelect portal target scene not in build settings: " + runSceneName, this);
                return;
            }

            SceneManager.LoadScene(runSceneName);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            var avatar = other.GetComponentInParent<CharacterSelectAvatarController>();
            if (avatar != null && avatar.IsControllable)
            {
                insideAvatar = avatar;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || insideAvatar == null)
            {
                return;
            }

            var avatar = other.GetComponentInParent<CharacterSelectAvatarController>();
            if (avatar == insideAvatar)
            {
                insideAvatar = null;
            }
        }

        private void RefreshVisual()
        {
            if (portalRenderer != null)
            {
                portalRenderer.color = portalEnabled ? enabledColor : disabledColor;
            }
        }
    }
}
