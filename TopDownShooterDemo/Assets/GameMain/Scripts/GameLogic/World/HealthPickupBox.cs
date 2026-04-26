using GameMain.GameLogic.Player;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Level pickup that restores HP through PlayerHealth's public API.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class HealthPickupBox : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] [Min(0f)] private float restoreAmount = 40f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private bool consumeWhenUsed = true;

        private PlayerHealth nearbyPlayer;
        private Collider2D triggerCollider;
        private bool consumed;

        private void Awake()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;
        }

        private void Update()
        {
            if (consumed || nearbyPlayer == null || !Input.GetKeyDown(interactKey))
            {
                return;
            }

            TryUse(nearbyPlayer);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerHealth>() : null;
            if (player != null)
            {
                nearbyPlayer = player;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerHealth>() : null;
            if (player != null && player == nearbyPlayer)
            {
                nearbyPlayer = null;
            }
        }

        public void Configure(SpriteRenderer renderer, float amount, KeyCode key, bool consumeOnUse)
        {
            bodyRenderer = renderer != null ? renderer : bodyRenderer;
            restoreAmount = Mathf.Max(0f, amount);
            interactKey = key;
            consumeWhenUsed = consumeOnUse;
            consumed = false;

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
            }
        }

        private void TryUse(PlayerHealth player)
        {
            if (player == null || player.IsDead)
            {
                return;
            }

            if (!player.RestoreHealth(restoreAmount))
            {
                return;
            }

            if (consumeWhenUsed)
            {
                consumed = true;
                if (triggerCollider != null)
                {
                    triggerCollider.enabled = false;
                }

                if (bodyRenderer != null)
                {
                    bodyRenderer.enabled = false;
                }
            }
        }
    }
}
