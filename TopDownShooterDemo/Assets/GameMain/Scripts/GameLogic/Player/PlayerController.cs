using GameMain.GameLogic.Utils;
using GameMain.GameLogic.Weapons;
using UnityEngine;

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// 2D top-down player movement and firing input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private bool rotateWithAim = true;
        [SerializeField] private float aimRotationOffset = 0f;
        [Header("Active Skill - Dodge")]
        [SerializeField] private bool enableDodge = true;
        [SerializeField] private KeyCode dodgeKey = KeyCode.Space;
        [SerializeField] [Min(0.1f)] private float dodgeDistance = 2.8f;
        [SerializeField] [Min(0.05f)] private float dodgeDuration = 0.18f;
        [SerializeField] [Min(0.1f)] private float dodgeCooldown = 1.6f;
        [SerializeField] private bool dodgeInvulnerable = true;
        [SerializeField] [Range(0f, 0.95f)] private float dodgeDamageReduction = 0.75f;
        [SerializeField] [Range(0.2f, 1f)] private float dodgeVisualAlpha = 0.48f;
        [SerializeField] [Min(0.15f)] private float dodgeInputLogInterval = 0.7f;
        [SerializeField] [Min(0f)] private float dodgeEnergyCost = 20f;
        [SerializeField] [Min(0.1f)] private float dodgeFailedEnergyLogInterval = 0.7f;

        private Rigidbody2D cachedRigidbody;
        private Camera cachedAimCamera;
        private SpriteRenderer cachedRenderer;
        private PlayerHealth playerHealth;
        private Vector2 moveInput;
        private Vector2 aimDirection = Vector2.right;
        private float nextFireInputLogTime;
        private float nextTryFireLogTime;
        private float nextMissingWeaponLogTime;
        private bool isDodging;
        private Vector2 dodgeDirection;
        private float dodgeRemaining;
        private float dodgeCooldownRemaining;
        private float dodgeSpeed;
        private float nextDodgeInputLogTime;
        private float nextDodgeEnergyBlockedLogTime;
        private Color baseRendererColor = Color.white;
        private bool hasBaseRendererColor;

        public bool IsDodging => isDodging;

        public bool IsDodgeReady => enableDodge && !isDodging && dodgeCooldownRemaining <= 0f;

        public float DodgeCooldownRemaining => Mathf.Max(0f, dodgeCooldownRemaining);

        public float DodgeCooldownDuration => Mathf.Max(0.01f, dodgeCooldown);

        public KeyCode DodgeKey => dodgeKey;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;

            TryResolveWeaponController();
            playerHealth = GetComponent<PlayerHealth>();

            cachedAimCamera = aimCamera != null ? aimCamera : Camera.main;
            CacheVisualReference();
        }

        private void Update()
        {
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            aimDirection = GetAimDirection();
            TickDodgeTimers(Time.deltaTime);

            if (rotateWithAim)
            {
                RotateToDirection(aimDirection);
            }

            if (enableDodge && Input.GetKeyDown(dodgeKey))
            {
                TryStartDodge();
            }

            var firePressed = Input.GetButton("Fire1") || Input.GetMouseButton(0);
            if (!firePressed)
            {
                return;
            }

            if (Time.unscaledTime >= nextFireInputLogTime)
            {
                nextFireInputLogTime = Time.unscaledTime + 0.85f;
                Debug.Log("Player fire input detected. source=Fire1|Mouse0", this);
            }

            if (weaponController == null)
            {
                TryResolveWeaponController();
                if (weaponController == null && Time.unscaledTime >= nextMissingWeaponLogTime)
                {
                    nextMissingWeaponLogTime = Time.unscaledTime + 1.2f;
                    Debug.LogWarning("PlayerController cannot fire because WeaponController is missing.", this);
                }

                return;
            }

            weaponController.EnsureRuntimeReferences();
            var fired = weaponController.TryFire(aimDirection, gameObject);
            if (Time.unscaledTime >= nextTryFireLogTime)
            {
                nextTryFireLogTime = Time.unscaledTime + 0.85f;
                Debug.Log(
                    "PlayerController TryFire called. result=" + fired + " " + weaponController.BuildRuntimeDebugSummary(),
                    this);
            }
        }

        private void FixedUpdate()
        {
            if (isDodging)
            {
                cachedRigidbody.velocity = dodgeDirection * dodgeSpeed;
                return;
            }

            cachedRigidbody.velocity = moveInput * moveSpeed;
        }

        private void OnDisable()
        {
            RestoreDodgeVisual();
            isDodging = false;
        }

        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        public void SetAimRotationOffset(float value)
        {
            aimRotationOffset = value;
        }

        public void SetWeaponController(WeaponController controller)
        {
            weaponController = controller;
        }

        public void SetAimCamera(Camera camera)
        {
            aimCamera = camera;
            cachedAimCamera = aimCamera != null ? aimCamera : Camera.main;
        }

        public void ConfigureDodge(
            KeyCode key,
            float distance,
            float duration,
            float cooldown,
            bool invulnerable,
            float damageReduction)
        {
            dodgeKey = key;
            dodgeDistance = Mathf.Max(0.1f, distance);
            dodgeDuration = Mathf.Max(0.05f, duration);
            dodgeCooldown = Mathf.Max(0.1f, cooldown);
            dodgeInvulnerable = invulnerable;
            dodgeDamageReduction = Mathf.Clamp(damageReduction, 0f, 0.95f);
        }

        public float GetIncomingDamageMultiplier()
        {
            if (!isDodging)
            {
                return 1f;
            }

            if (dodgeInvulnerable)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - dodgeDamageReduction);
        }

        private Vector2 GetAimDirection()
        {
            if (cachedAimCamera == null)
            {
                cachedAimCamera = aimCamera != null ? aimCamera : Camera.main;
            }

            if (cachedAimCamera == null)
            {
                return Vector2.right;
            }

            var screenPoint = Input.mousePosition;
            var worldPoint = cachedAimCamera.ScreenToWorldPoint(screenPoint);
            return Physics2DUtility.SafeDirection(transform.position, worldPoint, Vector2.right);
        }

        private void RotateToDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + aimRotationOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void TryResolveWeaponController()
        {
            if (weaponController == null)
            {
                weaponController = GetComponent<WeaponController>();
            }
        }

        private void TickDodgeTimers(float deltaTime)
        {
            if (dodgeCooldownRemaining > 0f)
            {
                dodgeCooldownRemaining = Mathf.Max(0f, dodgeCooldownRemaining - Mathf.Max(0f, deltaTime));
            }

            if (!isDodging)
            {
                return;
            }

            dodgeRemaining -= Mathf.Max(0f, deltaTime);
            if (dodgeRemaining <= 0f)
            {
                EndDodge();
            }
        }

        private void TryStartDodge()
        {
            if (!enableDodge || isDodging || dodgeCooldownRemaining > 0f)
            {
                return;
            }

            if (playerHealth != null && dodgeEnergyCost > 0f && !playerHealth.TryConsumeEnergy(dodgeEnergyCost))
            {
                if (Time.unscaledTime >= nextDodgeEnergyBlockedLogTime)
                {
                    nextDodgeEnergyBlockedLogTime = Time.unscaledTime + Mathf.Max(0.1f, dodgeFailedEnergyLogInterval);
                    Debug.Log("Player dodge blocked: not enough energy.", this);
                }

                return;
            }

            var direction = ResolveDodgeDirection();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            dodgeDirection = direction.normalized;
            dodgeRemaining = Mathf.Max(0.05f, dodgeDuration);
            dodgeCooldownRemaining = Mathf.Max(0.1f, dodgeCooldown);
            dodgeSpeed = Mathf.Max(0.1f, dodgeDistance) / Mathf.Max(0.05f, dodgeDuration);
            isDodging = true;
            ApplyDodgeVisual();

            if (Time.unscaledTime >= nextDodgeInputLogTime)
            {
                nextDodgeInputLogTime = Time.unscaledTime + Mathf.Max(0.15f, dodgeInputLogInterval);
                Debug.Log(
                    "Player dodge activated. key=" + dodgeKey +
                    " duration=" + dodgeDuration.ToString("0.##") +
                    " cooldown=" + dodgeCooldown.ToString("0.##") +
                    " invulnerable=" + dodgeInvulnerable,
                    this);
            }
        }

        private void EndDodge()
        {
            isDodging = false;
            dodgeRemaining = 0f;
            RestoreDodgeVisual();
        }

        private Vector2 ResolveDodgeDirection()
        {
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                return moveInput;
            }

            if (cachedRigidbody != null && cachedRigidbody.velocity.sqrMagnitude > 0.001f)
            {
                return cachedRigidbody.velocity.normalized;
            }

            if (aimDirection.sqrMagnitude > 0.001f)
            {
                return aimDirection.normalized;
            }

            return transform.right;
        }

        private void CacheVisualReference()
        {
            cachedRenderer = GetComponentInChildren<SpriteRenderer>();
            if (cachedRenderer == null)
            {
                return;
            }

            baseRendererColor = cachedRenderer.color;
            hasBaseRendererColor = true;
        }

        private void ApplyDodgeVisual()
        {
            if (cachedRenderer == null)
            {
                CacheVisualReference();
            }

            if (cachedRenderer == null)
            {
                return;
            }

            if (!hasBaseRendererColor)
            {
                baseRendererColor = cachedRenderer.color;
                hasBaseRendererColor = true;
            }

            var color = baseRendererColor;
            color.a = Mathf.Clamp(dodgeVisualAlpha, 0.2f, 1f);
            cachedRenderer.color = color;
        }

        private void RestoreDodgeVisual()
        {
            if (cachedRenderer == null || !hasBaseRendererColor)
            {
                return;
            }

            cachedRenderer.color = baseRendererColor;
        }
    }
}
