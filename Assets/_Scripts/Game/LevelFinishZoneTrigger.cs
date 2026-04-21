// Path: Assets/_Scripts/Game/LevelFinishZoneTrigger.cs
using ARPGDemo.Core;
using ARPGDemo.Tools;
using UnityEngine;

namespace ARPGDemo.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class LevelFinishZoneTrigger : MonoBehaviour
    {
        [SerializeField] private string playerActorId = "Player";
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private bool logWhenBlocked = true;
        [SerializeField] private bool logWhenReady = true;
        [SerializeField] private float logCooldownSeconds = 1f;
        [SerializeField] private bool showWorldPrompt = true;
        [SerializeField] private Vector3 promptLocalOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private float promptCharacterSize = 0.14f;
        [SerializeField] private Color blockedPromptColor = new Color(1f, 0.66f, 0.66f, 1f);
        [SerializeField] private Color readyPromptColor = new Color(0.75f, 1f, 0.82f, 1f);

        private ActorStats residentPlayer;
        private float nextLogTime;
        private TextMesh promptTextMesh;
        private Collider2D triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }

            EnsurePrompt();
            SetPromptVisible(false, string.Empty, blockedPromptColor);
        }

        private void Update()
        {
            if (residentPlayer != null && !IsResidentPlayerStillInsideTrigger())
            {
                ClearResidentState();
            }

            if (residentPlayer == null)
            {
                SetPromptVisible(false, string.Empty, blockedPromptColor);
                return;
            }

            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return;
            }

            bool enemiesCleared = manager.AreAllMajorEnemiesDefeated();
            if (!enemiesCleared)
            {
                SetPromptVisible(true, "尚未清空敌人", blockedPromptColor);
                if (logWhenBlocked)
                {
                    TryLog("[FinishZone] 尚未清空敌人。");
                }

                return;
            }

            SetPromptVisible(true, "按 E 离开", readyPromptColor);
            if (logWhenReady)
            {
                TryLog("[FinishZone] 按 E 离开。");
            }

            if (InputCompat.IsDown(interactKey))
            {
                manager.TryCompleteLevelAtFinishZone();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            ActorStats playerStats = ResolvePlayerStats(other);
            if (playerStats == null)
            {
                return;
            }

            residentPlayer = playerStats;
            nextLogTime = 0f;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || residentPlayer == null)
            {
                return;
            }

            ActorStats playerStats = ResolvePlayerStats(other);
            if (playerStats == null || playerStats != residentPlayer)
            {
                return;
            }

            ClearResidentState();
        }

        private void OnDisable()
        {
            ClearResidentState();
        }

        private ActorStats ResolvePlayerStats(Collider2D other)
        {
            ActorStats playerStats = other.GetComponentInParent<ActorStats>();
            if (playerStats == null || playerStats.Team != ActorTeam.Player)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(playerActorId) && playerStats.ActorId != playerActorId)
            {
                return null;
            }

            return playerStats;
        }

        private bool IsResidentPlayerStillInsideTrigger()
        {
            if (residentPlayer == null || !residentPlayer.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (triggerCollider == null || !triggerCollider.enabled || !triggerCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            Collider2D[] playerColliders = residentPlayer.GetComponentsInChildren<Collider2D>(false);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (triggerCollider.IsTouching(playerCollider))
                {
                    return true;
                }

                if (triggerCollider.bounds.Intersects(playerCollider.bounds))
                {
                    return true;
                }
            }

            return triggerCollider.OverlapPoint(residentPlayer.transform.position);
        }

        private void ClearResidentState()
        {
            residentPlayer = null;
            nextLogTime = 0f;
            SetPromptVisible(false, string.Empty, blockedPromptColor);
        }

        private void TryLog(string message)
        {
            if (Time.unscaledTime < nextLogTime)
            {
                return;
            }

            nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logCooldownSeconds);
            Debug.Log(message, this);
        }

        private void EnsurePrompt()
        {
            if (!showWorldPrompt)
            {
                return;
            }

            Transform prompt = transform.Find("FinishPrompt");
            if (prompt == null)
            {
                GameObject go = new GameObject("FinishPrompt");
                go.transform.SetParent(transform, false);
                prompt = go.transform;
            }

            prompt.localPosition = promptLocalOffset;
            prompt.localRotation = Quaternion.identity;
            prompt.localScale = Vector3.one;

            promptTextMesh = prompt.GetComponent<TextMesh>();
            if (promptTextMesh == null)
            {
                promptTextMesh = prompt.gameObject.AddComponent<TextMesh>();
            }

            promptTextMesh.anchor = TextAnchor.MiddleCenter;
            promptTextMesh.alignment = TextAlignment.Center;
            promptTextMesh.characterSize = promptCharacterSize;
            promptTextMesh.fontSize = 64;
        }

        private void SetPromptVisible(bool visible, string text, Color color)
        {
            if (!showWorldPrompt)
            {
                return;
            }

            EnsurePrompt();
            if (promptTextMesh == null)
            {
                return;
            }

            promptTextMesh.text = visible ? text : string.Empty;
            promptTextMesh.color = color;
        }
    }
}
