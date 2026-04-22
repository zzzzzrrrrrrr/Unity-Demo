using UnityEngine;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Lightweight movement controller for CharacterSelectScene actor body.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectAvatarController : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float moveSpeed = 5.2f;
        [SerializeField] private bool controllable;

        private Rigidbody2D cachedBody;
        private Vector2 moveInput;

        private void Awake()
        {
            cachedBody = GetComponent<Rigidbody2D>();
            cachedBody.bodyType = RigidbodyType2D.Kinematic;
            cachedBody.gravityScale = 0f;
            cachedBody.freezeRotation = true;
            cachedBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            cachedBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = GetComponent<Collider2D>();
            collider.isTrigger = false;

            ApplyBodyMode();
        }

        private void Update()
        {
            if (!Application.isPlaying || !controllable)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || cachedBody == null)
            {
                return;
            }

            if (!controllable)
            {
                cachedBody.velocity = Vector2.zero;
                return;
            }

            cachedBody.velocity = moveInput * Mathf.Max(0.1f, moveSpeed);
        }

        public bool IsControllable
        {
            get { return controllable; }
        }

        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        public void SetControllable(bool value)
        {
            controllable = value;
            moveInput = Vector2.zero;
            ApplyBodyMode();
        }

        public void ResetTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            if (cachedBody != null)
            {
                cachedBody.velocity = Vector2.zero;
                cachedBody.angularVelocity = 0f;
            }
        }

        private void ApplyBodyMode()
        {
            if (cachedBody == null)
            {
                return;
            }

            cachedBody.bodyType = controllable ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            cachedBody.simulated = true;
            if (!controllable)
            {
                cachedBody.velocity = Vector2.zero;
                cachedBody.angularVelocity = 0f;
            }
        }
    }
}
