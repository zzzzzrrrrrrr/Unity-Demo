using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Simple runtime room gate with lock/unlock visual + collider state.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class RoomGateController : MonoBehaviour
    {
        [SerializeField] private bool startsLocked;
        [SerializeField] private Color lockedColor = new Color(0.88f, 0.34f, 0.24f, 0.96f);
        [SerializeField] private Color unlockedColor = new Color(0.32f, 0.9f, 0.62f, 0.2f);

        private BoxCollider2D gateCollider;
        private SpriteRenderer gateRenderer;

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            gateCollider = GetComponent<BoxCollider2D>();
            gateRenderer = GetComponent<SpriteRenderer>();
            SetLocked(startsLocked, true);
        }

        public void Configure(bool locked, Color lockedTint, Color unlockedTint)
        {
            lockedColor = lockedTint;
            unlockedColor = unlockedTint;
            SetLocked(locked, true);
        }

        public void SetLocked(bool locked)
        {
            SetLocked(locked, false);
        }

        private void SetLocked(bool locked, bool immediate)
        {
            IsLocked = locked;

            if (gateCollider == null)
            {
                gateCollider = GetComponent<BoxCollider2D>();
            }

            if (gateRenderer == null)
            {
                gateRenderer = GetComponent<SpriteRenderer>();
            }

            if (gateCollider != null)
            {
                gateCollider.enabled = locked;
                gateCollider.isTrigger = false;
            }

            if (gateRenderer != null)
            {
                gateRenderer.color = locked ? lockedColor : unlockedColor;
            }

            if (!immediate)
            {
                Debug.Log(
                    "RoomGateController state changed. gate=" + name +
                    " locked=" + locked,
                    this);
            }
        }
    }
}
