using GameMain.GameLogic.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Sensor-only next level portal. Flow decides when it is enabled; this only loads the configured scene.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class NextLevelPortalController : MonoBehaviour
    {
        [SerializeField] private bool portalEnabled;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private string nextSceneName = "RunScene_Level2";
        [SerializeField] [Min(0.1f)] private float loadCooldown = 0.6f;
        [Header("Display-Only Prompt")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TextMesh promptText;
        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging;

        private PlayerController currentPlayer;
        private float nextAllowedLoadTime;
        private bool promptVisibilityInitialized;
        private bool promptVisible;

        public bool PortalEnabled => portalEnabled;

        public string NextSceneName => nextSceneName;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
            ApplyVisibleState();
        }

        private void Update()
        {
            if (!portalEnabled || currentPlayer == null || !Input.GetKeyDown(interactKey))
            {
                return;
            }

            TryLoadNextLevel();
        }

        public void Configure(string sceneName, KeyCode key)
        {
            nextSceneName = string.IsNullOrWhiteSpace(sceneName) ? "RunScene_Level2" : sceneName.Trim();
            interactKey = key;
        }

        public void SetPortalEnabled(bool value)
        {
            portalEnabled = value;
            if (!portalEnabled)
            {
                currentPlayer = null;
            }

            ApplyVisibleState();
        }

        public void SetPrompt(GameObject root, TextMesh text)
        {
            promptRoot = root != null ? root : promptRoot;
            promptText = text != null ? text : promptText;
            UpdatePromptVisibility(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryTrackPlayer(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryTrackPlayer(other);
        }

        private void TryTrackPlayer(Collider2D other)
        {
            if (!portalEnabled || other == null)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                currentPlayer = player;
                UpdatePromptVisibility();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && player == currentPlayer)
            {
                currentPlayer = null;
                UpdatePromptVisibility();
            }
        }

        private void TryLoadNextLevel()
        {
            if (Time.unscaledTime < nextAllowedLoadTime)
            {
                return;
            }

            nextAllowedLoadTime = Time.unscaledTime + Mathf.Max(0.1f, loadCooldown);
            if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogWarning("NextLevelPortal cannot load scene because it is not in Build Settings: " + nextSceneName, this);
                return;
            }

            if (verboseLogging)
            {
                Debug.Log("NextLevelPortal loading scene: " + nextSceneName, this);
            }

            SceneManager.LoadScene(nextSceneName);
        }

        private void ApplyVisibleState()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = portalEnabled;
                }
            }

            var colliders = GetComponents<Collider2D>();
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = portalEnabled;
                }
            }

            UpdatePromptVisibility(true);
        }

        private void UpdatePromptVisibility(bool force = false)
        {
            SetPromptVisible(portalEnabled && currentPlayer != null, force);
        }

        private void SetPromptVisible(bool visible, bool force)
        {
            if (!force && promptVisibilityInitialized && promptVisible == visible)
            {
                return;
            }

            promptVisibilityInitialized = true;
            promptVisible = visible;

            var targetRoot = promptRoot != null
                ? promptRoot
                : promptText != null
                    ? promptText.gameObject
                    : null;
            if (targetRoot != null)
            {
                targetRoot.SetActive(visible);
            }

            if (promptText != null)
            {
                var promptObject = promptText.gameObject;
                if (promptObject != targetRoot)
                {
                    promptObject.SetActive(visible);
                }
            }
        }
    }
}
