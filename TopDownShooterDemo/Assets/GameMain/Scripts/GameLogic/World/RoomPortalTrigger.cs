using System;
using GameMain.GameLogic.Player;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Minimal room portal trigger with optional interact key gating.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class RoomPortalTrigger : MonoBehaviour
    {
        [SerializeField] private string portalId = "Portal";
        [SerializeField] private bool portalEnabled = true;
        [SerializeField] private bool requireInteractKey = true;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] [Min(0.05f)] private float triggerCooldown = 0.45f;
        [SerializeField] [Min(0.1f)] private float inputLogInterval = 0.7f;

        private float nextAllowedTriggerTime;
        private float nextInputLogTime;

        public string PortalId => portalId;

        public bool PortalEnabled => portalEnabled;

        public event Action<RoomPortalTrigger, PlayerHealth> PortalTriggered;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        public void Configure(string id, KeyCode key, bool requireKey)
        {
            portalId = string.IsNullOrWhiteSpace(id) ? "Portal" : id;
            interactKey = key;
            requireInteractKey = requireKey;
        }

        public void SetPortalEnabled(bool value)
        {
            portalEnabled = value;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!portalEnabled || other == null || Time.unscaledTime < nextAllowedTriggerTime)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
            {
                return;
            }

            if (requireInteractKey && !Input.GetKeyDown(interactKey))
            {
                return;
            }

            nextAllowedTriggerTime = Time.unscaledTime + Mathf.Max(0.05f, triggerCooldown);
            if (Time.unscaledTime >= nextInputLogTime)
            {
                nextInputLogTime = Time.unscaledTime + Mathf.Max(0.1f, inputLogInterval);
                Debug.Log(
                    "RoomPortalTrigger activated. id=" + portalId +
                    " key=" + interactKey +
                    " requireKey=" + requireInteractKey,
                    this);
            }

            PortalTriggered?.Invoke(this, player);
        }
    }
}
