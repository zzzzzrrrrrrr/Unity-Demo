using UnityEngine;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Display-only proximity hint for lobby role stands and decorative points.
    /// Hints are routed to the UGUI status text so Chinese never renders through TextMesh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectAmbientHint : MonoBehaviour
    {
        private const string DefaultLobbyStatus = "左键选择角色 -> 确认选择 -> WASD 控制已确认角色 -> 靠近 NPC 按 E 对话 -> 在传送门处按 E 进入战斗。";

        [SerializeField] private string hintText = "靠近查看。";
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] [Min(0.1f)] private float revealDistance = 2.4f;

        private Transform promptTransform;
        private CharacterInfoPanelController infoPanel;
        private bool isVisible;

        private void Awake()
        {
            DisableLegacyPrompt();
            SetVisible(false);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                SetVisible(false);
                return;
            }

            var actor = FindControllableActor();
            if (actor == null)
            {
                SetVisible(false);
                return;
            }

            var distance = Vector2.Distance(actor.position, transform.position);
            SetVisible(distance <= revealDistance);
        }

        public void Configure(string text, Vector3 offset, float distance)
        {
            hintText = string.IsNullOrWhiteSpace(text) ? hintText : text;
            localOffset = offset;
            revealDistance = Mathf.Max(0.1f, distance);
            DisableLegacyPrompt();
        }

        private void DisableLegacyPrompt()
        {
            var prompt = transform.Find("AmbientHint");
            promptTransform = prompt;
            if (promptTransform == null)
            {
                return;
            }

            promptTransform.localPosition = localOffset;

            var textMesh = promptTransform.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = string.Empty;
            }

            var textRenderer = promptTransform.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.enabled = false;
            }

            promptTransform.gameObject.SetActive(false);
        }

        private static Transform FindControllableActor()
        {
            var actors = FindObjectsOfType<CharacterSelectAvatarController>();
            for (var i = 0; i < actors.Length; i++)
            {
                if (actors[i] != null && actors[i].IsControllable)
                {
                    return actors[i].transform;
                }
            }

            return null;
        }

        private void SetVisible(bool visible)
        {
            if (isVisible == visible)
            {
                return;
            }

            isVisible = visible;
            if (promptTransform != null)
            {
                promptTransform.gameObject.SetActive(false);
            }

            if (infoPanel == null)
            {
                infoPanel = FindObjectOfType<CharacterInfoPanelController>();
            }

            if (infoPanel != null)
            {
                infoPanel.SetStatus(visible ? hintText : DefaultLobbyStatus);
            }
        }
    }
}
