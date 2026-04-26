using GameMain.GameLogic.Utils;
using GameMain.GameLogic.Projectiles;
using GameMain.GameLogic.Weapons;
using GameMain.Builtin.Sound;
using UnityEngine;

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// 2D top-down player movement and firing input.
    /// Ownership contract: runtime owner of weapon slot truth; WeaponController is the firing execution mirror.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        private struct WeaponSlotRuntime
        {
            public string label;
            public float fireInterval;
            public float projectileSpeed;
            public float projectileDamage;
            public float projectileLifetime;
            public GameObject visualPrefab;
            public Sprite visualSprite;
            public Projectile projectilePrefabOverride;
            public GameObject muzzleFlashPrefab;
            public GameObject hitEffectPrefab;
        }

        public readonly struct ActiveWeaponRuntimeSnapshot
        {
            public ActiveWeaponRuntimeSnapshot(
                int slotIndex,
                string label,
                float fireInterval,
                float projectileSpeed,
                float projectileDamage,
                float projectileLifetime)
            {
                SlotIndex = Mathf.Clamp(slotIndex, 0, 1);
                Label = string.IsNullOrWhiteSpace(label) ? (SlotIndex == 0 ? "Weapon A" : "Weapon B") : label;
                FireInterval = Mathf.Max(0f, fireInterval);
                ProjectileSpeed = Mathf.Max(0f, projectileSpeed);
                ProjectileDamage = Mathf.Max(0f, projectileDamage);
                ProjectileLifetime = Mathf.Max(0f, projectileLifetime);
            }

            public int SlotIndex { get; }

            public string Label { get; }

            public float FireInterval { get; }

            public float ProjectileSpeed { get; }

            public float ProjectileDamage { get; }

            public float ProjectileLifetime { get; }
        }

        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private bool rotateWithAim = true;
        [SerializeField] private float aimRotationOffset = 0f;
        [Header("Weapon Switching")]
        [SerializeField] private bool enableWeaponSwitching = true;
        [SerializeField] private KeyCode weaponSwitchKey = KeyCode.Q;
        [Header("Weapon Presentation")]
        [SerializeField] private Transform weaponVisualRoot;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private SpriteRenderer weaponVisualRenderer;
        [SerializeField] private Transform weaponMuzzlePoint;
        [SerializeField] private Vector3 defaultWeaponPivotLocalPosition = new Vector3(0.36f, -0.04f, 0f);
        [SerializeField] private Vector3 defaultWeaponMuzzleLocalPosition = new Vector3(0.62f, 0f, 0f);
        [SerializeField] private Vector3 weaponVisualLocalScale = Vector3.one;
        [SerializeField] private int weaponVisualSortingOrder = 24;
        [SerializeField] private string autoSlotAWeaponSpriteResourcePath = "Images/Weapon/AssaultRifle";
        [SerializeField] private string autoSlotBWeaponSpriteResourcePath = "Images/Weapon/DesertEagle";
        [SerializeField] private GameObject slotAWeaponVisualPrefab;
        [SerializeField] private GameObject slotBWeaponVisualPrefab;
        [SerializeField] private Sprite slotAWeaponSprite;
        [SerializeField] private Sprite slotBWeaponSprite;
        [SerializeField] private Projectile slotAProjectilePrefabOverride;
        [SerializeField] private Projectile slotBProjectilePrefabOverride;
        [SerializeField] private GameObject slotAMuzzleFlashPrefab;
        [SerializeField] private GameObject slotBMuzzleFlashPrefab;
        [SerializeField] private GameObject slotAHitEffectPrefab;
        [SerializeField] private GameObject slotBHitEffectPrefab;
        [SerializeField] [Min(0.01f)] private float muzzleFlashLifeTime = 0.12f;
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
        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging;

        private Rigidbody2D cachedRigidbody;
        private Camera cachedAimCamera;
        private SpriteRenderer cachedRenderer;
        private PlayerRollSpriteAnimator rollSpriteAnimator;
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
        private WeaponSlotRuntime weaponSlotA;
        private WeaponSlotRuntime weaponSlotB;
        private bool weaponSlotsConfigured;
        private int activeWeaponSlotIndex;
        private GameObject activeWeaponVisualInstance;
        private Projectile baseProjectilePrefab;
        private bool capturedBaseProjectilePrefab;
        private float temporaryFireIntervalMultiplier = 1f;

        public bool IsDodging => isDodging;

        public bool IsDodgeReady => enableDodge && !isDodging && dodgeCooldownRemaining <= 0f;

        public float DodgeCooldownRemaining => Mathf.Max(0f, dodgeCooldownRemaining);

        public float DodgeCooldownDuration => Mathf.Max(0.01f, dodgeCooldown);

        public KeyCode DodgeKey => dodgeKey;

        public KeyCode WeaponSwitchKey => weaponSwitchKey;

        public bool CanStartRoleSkillDodge => enableDodge && !isDodging;

        public string ActiveWeaponLabel => activeWeaponSlotIndex == 0 ? weaponSlotA.label : weaponSlotB.label;

        public int ActiveWeaponSlotIndex => activeWeaponSlotIndex;

        public GameObject ActiveWeaponHitEffectPrefab =>
            activeWeaponSlotIndex == 0 ? weaponSlotA.hitEffectPrefab : weaponSlotB.hitEffectPrefab;

        public ActiveWeaponRuntimeSnapshot GetActiveWeaponRuntimeSnapshot()
        {
            var slot = activeWeaponSlotIndex == 0 ? weaponSlotA : weaponSlotB;
            return new ActiveWeaponRuntimeSnapshot(
                activeWeaponSlotIndex,
                slot.label,
                slot.fireInterval,
                slot.projectileSpeed,
                slot.projectileDamage,
                slot.projectileLifetime);
        }

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.freezeRotation = true;

            TryResolveWeaponController();
            playerHealth = GetComponent<PlayerHealth>();

            cachedAimCamera = aimCamera != null ? aimCamera : Camera.main;
            CacheVisualReference();
            CacheRollSpriteAnimator();
            EnsureWeaponPresentationReady();
            InitializeWeaponSlotsIfNeeded();
            ApplyActiveWeaponSlot(false);
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

            if (enableWeaponSwitching && Input.GetKeyDown(weaponSwitchKey))
            {
                SwitchWeaponSlot();
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

            if (verboseLogging && Time.unscaledTime >= nextFireInputLogTime)
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

            EnsureWeaponSlotSetupForCurrentWeapon();
            weaponController.EnsureRuntimeReferences();
            var fired = weaponController.TryFire(aimDirection, gameObject);
            if (fired)
            {
                SpawnMuzzleFlashForActiveSlot(aimDirection);
            }

            if (verboseLogging && Time.unscaledTime >= nextTryFireLogTime)
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
            capturedBaseProjectilePrefab = false;
            EnsureWeaponPresentationReady();
            EnsureWeaponSlotSetupForCurrentWeapon();
            ApplyActiveWeaponSlot(false);
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

        public bool TryStartRoleSkillDodge(float distanceMultiplier, float durationMultiplier)
        {
            if (!CanStartRoleSkillDodge)
            {
                return false;
            }

            var direction = ResolveDodgeDirection();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            var duration = Mathf.Max(0.05f, dodgeDuration * Mathf.Max(0.1f, durationMultiplier));
            var distance = Mathf.Max(0.1f, dodgeDistance * Mathf.Max(0.1f, distanceMultiplier));
            dodgeDirection = direction.normalized;
            dodgeRemaining = duration;
            dodgeSpeed = distance / duration;
            isDodging = true;
            ApplyDodgeVisual();
            PlayRollSpriteAnimation();
            return true;
        }

        public void ApplyTemporaryFireIntervalMultiplier(float multiplier)
        {
            temporaryFireIntervalMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            EnsureWeaponSlotSetupForCurrentWeapon();
            ApplyActiveWeaponSlot(false, false);
        }

        public void ClearTemporaryFireIntervalMultiplier()
        {
            if (Mathf.Approximately(temporaryFireIntervalMultiplier, 1f))
            {
                return;
            }

            temporaryFireIntervalMultiplier = 1f;
            EnsureWeaponSlotSetupForCurrentWeapon();
            ApplyActiveWeaponSlot(false, false);
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

        public void ConfigureWeaponSlots(
            string slotALabel,
            float slotAFireInterval,
            float slotAProjectileSpeed,
            float slotAProjectileDamage,
            float slotAProjectileLifetime,
            string slotBLabel,
            float slotBFireInterval,
            float slotBProjectileSpeed,
            float slotBProjectileDamage,
            float slotBProjectileLifetime,
            int startSlotIndex = 0)
        {
            weaponSlotA = BuildWeaponSlot(
                slotALabel,
                slotAFireInterval,
                slotAProjectileSpeed,
                slotAProjectileDamage,
                slotAProjectileLifetime,
                slotAWeaponVisualPrefab,
                ResolveConfiguredSlotSprite(slotALabel, 0),
                slotAProjectilePrefabOverride,
                slotAMuzzleFlashPrefab,
                slotAHitEffectPrefab,
                "Weapon A");
            weaponSlotB = BuildWeaponSlot(
                slotBLabel,
                slotBFireInterval,
                slotBProjectileSpeed,
                slotBProjectileDamage,
                slotBProjectileLifetime,
                slotBWeaponVisualPrefab,
                ResolveConfiguredSlotSprite(slotBLabel, 1),
                slotBProjectilePrefabOverride,
                slotBMuzzleFlashPrefab,
                slotBHitEffectPrefab,
                "Weapon B");
            activeWeaponSlotIndex = Mathf.Clamp(startSlotIndex, 0, 1);
            weaponSlotsConfigured = true;
            ApplyActiveWeaponSlot(true);
        }

        public bool TryReplaceActiveWeaponSlot(
            string label,
            float fireInterval,
            float projectileSpeed,
            float projectileDamage,
            float projectileLifetime,
            Sprite visualSprite)
        {
            EnsureWeaponSlotSetupForCurrentWeapon();
            if (!weaponSlotsConfigured)
            {
                return false;
            }

            var slot = activeWeaponSlotIndex == 0 ? weaponSlotA : weaponSlotB;
            slot.label = string.IsNullOrWhiteSpace(label)
                ? (activeWeaponSlotIndex == 0 ? "Weapon A" : "Weapon B")
                : label.Trim();
            slot.fireInterval = Mathf.Max(0.01f, fireInterval);
            slot.projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            slot.projectileDamage = Mathf.Max(0f, projectileDamage);
            slot.projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            slot.visualPrefab = null;
            slot.visualSprite = visualSprite;

            if (activeWeaponSlotIndex == 0)
            {
                weaponSlotA = slot;
            }
            else
            {
                weaponSlotB = slot;
            }

            ApplyActiveWeaponSlot(true);
            return true;
        }

        public void SwitchWeaponSlot()
        {
            EnsureWeaponSlotSetupForCurrentWeapon();
            activeWeaponSlotIndex = activeWeaponSlotIndex == 0 ? 1 : 0;
            ApplyActiveWeaponSlot(true);
            AudioService.PlaySfxById(SoundIds.SfxWeaponSwitch);
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

        private void EnsureWeaponSlotSetupForCurrentWeapon()
        {
            if (weaponController == null)
            {
                TryResolveWeaponController();
            }

            InitializeWeaponSlotsIfNeeded();
        }

        private void InitializeWeaponSlotsIfNeeded()
        {
            if (weaponSlotsConfigured)
            {
                return;
            }

            var baseInterval = weaponController != null ? weaponController.FireInterval : 0.16f;
            var baseSpeed = weaponController != null ? weaponController.ProjectileSpeed : 20f;
            var baseDamage = weaponController != null ? weaponController.ProjectileDamage : 12f;
            var baseLifetime = weaponController != null ? weaponController.ProjectileLifeTime : 3.2f;

            weaponSlotA = BuildWeaponSlot(
                "Weapon A",
                baseInterval,
                baseSpeed,
                baseDamage,
                baseLifetime,
                slotAWeaponVisualPrefab,
                ResolveConfiguredSlotSprite("Weapon A", 0),
                slotAProjectilePrefabOverride,
                slotAMuzzleFlashPrefab,
                slotAHitEffectPrefab,
                "Weapon A");
            weaponSlotB = BuildWeaponSlot(
                "Weapon B",
                baseInterval * 2.1f,
                baseSpeed * 0.78f,
                baseDamage * 1.75f,
                baseLifetime,
                slotBWeaponVisualPrefab,
                ResolveConfiguredSlotSprite("Weapon B", 1),
                slotBProjectilePrefabOverride,
                slotBMuzzleFlashPrefab,
                slotBHitEffectPrefab,
                "Weapon B");
            activeWeaponSlotIndex = Mathf.Clamp(activeWeaponSlotIndex, 0, 1);
            weaponSlotsConfigured = true;
        }

        private void ApplyActiveWeaponSlot(bool logSwitch)
        {
            ApplyActiveWeaponSlot(logSwitch, true);
        }

        private void ApplyActiveWeaponSlot(bool logSwitch, bool resetFireCooldown)
        {
            if (!weaponSlotsConfigured)
            {
                return;
            }

            if (weaponController == null)
            {
                TryResolveWeaponController();
            }

            if (weaponController == null)
            {
                return;
            }

            CaptureBaseProjectilePrefabIfNeeded();
            var slot = activeWeaponSlotIndex == 0 ? weaponSlotA : weaponSlotB;
            weaponController.Configure(ResolveEffectiveFireInterval(slot.fireInterval), slot.projectileSpeed, slot.projectileDamage, slot.projectileLifetime);
            if (slot.projectilePrefabOverride != null)
            {
                weaponController.SetProjectilePrefab(slot.projectilePrefabOverride);
            }
            else if (baseProjectilePrefab != null)
            {
                weaponController.SetProjectilePrefab(baseProjectilePrefab);
            }

            EnsureWeaponPresentationReady();
            ApplyWeaponPresentation(slot);
            if (resetFireCooldown)
            {
                weaponController.ResetFireCooldown();
            }

            if (verboseLogging && logSwitch)
            {
                Debug.Log(
                    "Player weapon switched. slot=" + (activeWeaponSlotIndex + 1) +
                    " label=" + slot.label +
                    " fireInterval=" + ResolveEffectiveFireInterval(slot.fireInterval).ToString("0.###") +
                    " projectileSpeed=" + slot.projectileSpeed.ToString("0.###") +
                    " projectileDamage=" + slot.projectileDamage.ToString("0.###"),
                    this);
            }
        }

        private static WeaponSlotRuntime BuildWeaponSlot(
            string label,
            float fireInterval,
            float projectileSpeed,
            float projectileDamage,
            float projectileLifetime,
            GameObject visualPrefab,
            Sprite visualSprite,
            Projectile projectilePrefabOverride,
            GameObject muzzleFlashPrefab,
            GameObject hitEffectPrefab,
            string fallbackLabel)
        {
            return new WeaponSlotRuntime
            {
                label = string.IsNullOrWhiteSpace(label) ? fallbackLabel : label.Trim(),
                fireInterval = Mathf.Max(0.01f, fireInterval),
                projectileSpeed = Mathf.Max(0.1f, projectileSpeed),
                projectileDamage = Mathf.Max(0f, projectileDamage),
                projectileLifetime = Mathf.Max(0.1f, projectileLifetime),
                visualPrefab = visualPrefab,
                visualSprite = visualSprite,
                projectilePrefabOverride = projectilePrefabOverride,
                muzzleFlashPrefab = muzzleFlashPrefab,
                hitEffectPrefab = hitEffectPrefab,
            };
        }

        private float ResolveEffectiveFireInterval(float baseInterval)
        {
            return Mathf.Max(0.01f, baseInterval * Mathf.Clamp(temporaryFireIntervalMultiplier, 0.05f, 1f));
        }

        private void CaptureBaseProjectilePrefabIfNeeded()
        {
            if (capturedBaseProjectilePrefab)
            {
                return;
            }

            capturedBaseProjectilePrefab = true;
            baseProjectilePrefab = weaponController != null ? weaponController.ProjectilePrefab : null;
        }

        private void EnsureWeaponPresentationReady()
        {
            EnsureWeaponVisualRoot();
            EnsureWeaponPivot();
            EnsureWeaponVisualRenderer();
            EnsureWeaponMuzzlePoint();

            if (weaponController != null && weaponMuzzlePoint != null)
            {
                weaponController.SetProjectileSpawnPoint(weaponMuzzlePoint);
            }
        }

        private void EnsureWeaponVisualRoot()
        {
            if (weaponVisualRoot != null)
            {
                return;
            }

            var existing = transform.Find("WeaponVisualRoot");
            if (existing == null)
            {
                var created = new GameObject("WeaponVisualRoot");
                existing = created.transform;
                existing.SetParent(transform, false);
            }

            weaponVisualRoot = existing;
        }

        private void EnsureWeaponPivot()
        {
            if (weaponPivot != null)
            {
                return;
            }

            if (weaponVisualRoot == null)
            {
                EnsureWeaponVisualRoot();
            }

            if (weaponVisualRoot == null)
            {
                return;
            }

            var existing = weaponVisualRoot.Find("WeaponPivot");
            if (existing == null)
            {
                var created = new GameObject("WeaponPivot");
                existing = created.transform;
                existing.SetParent(weaponVisualRoot, false);
                existing.localPosition = defaultWeaponPivotLocalPosition;
            }

            weaponPivot = existing;
        }

        private void EnsureWeaponVisualRenderer()
        {
            if (weaponVisualRenderer != null)
            {
                return;
            }

            if (weaponPivot == null)
            {
                EnsureWeaponPivot();
            }

            if (weaponPivot == null)
            {
                return;
            }

            var existing = weaponPivot.Find("WeaponSprite");
            if (existing == null)
            {
                var created = new GameObject("WeaponSprite");
                existing = created.transform;
                existing.SetParent(weaponPivot, false);
            }

            weaponVisualRenderer = existing.GetComponent<SpriteRenderer>();
            if (weaponVisualRenderer == null)
            {
                weaponVisualRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void EnsureWeaponMuzzlePoint()
        {
            if (weaponMuzzlePoint != null)
            {
                return;
            }

            if (weaponPivot == null)
            {
                EnsureWeaponPivot();
            }

            if (weaponPivot == null)
            {
                return;
            }

            var existing = weaponPivot.Find("MuzzlePoint");
            if (existing == null)
            {
                var created = new GameObject("MuzzlePoint");
                existing = created.transform;
                existing.SetParent(weaponPivot, false);
                existing.localPosition = defaultWeaponMuzzleLocalPosition;
            }

            weaponMuzzlePoint = existing;
        }

        private Sprite ResolveConfiguredSlotSprite(string label, int slotIndex)
        {
            var configured = slotIndex == 0 ? slotAWeaponSprite : slotBWeaponSprite;
            if (configured != null)
            {
                return configured;
            }

            var directPath = slotIndex == 0 ? autoSlotAWeaponSpriteResourcePath : autoSlotBWeaponSpriteResourcePath;
            if (!string.IsNullOrWhiteSpace(directPath))
            {
                var directLoaded = Resources.Load<Sprite>(directPath.Trim());
                if (directLoaded != null)
                {
                    return directLoaded;
                }
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                var compact = label.Replace(" ", string.Empty);
                var byLabel = Resources.Load<Sprite>("Images/Weapon/" + compact);
                if (byLabel != null)
                {
                    return byLabel;
                }
            }

            return null;
        }

        private void ApplyWeaponPresentation(WeaponSlotRuntime slot)
        {
            EnsureWeaponPresentationReady();

            if (weaponPivot == null)
            {
                return;
            }

            if (activeWeaponVisualInstance != null)
            {
                Destroy(activeWeaponVisualInstance);
                activeWeaponVisualInstance = null;
            }

            if (slot.visualPrefab != null)
            {
                activeWeaponVisualInstance = Instantiate(slot.visualPrefab, weaponPivot);
                activeWeaponVisualInstance.transform.localPosition = Vector3.zero;
                activeWeaponVisualInstance.transform.localRotation = Quaternion.identity;
                activeWeaponVisualInstance.transform.localScale = Vector3.one;
                if (weaponVisualRenderer != null)
                {
                    weaponVisualRenderer.enabled = false;
                }
            }
            else if (weaponVisualRenderer != null)
            {
                weaponVisualRenderer.sprite = slot.visualSprite;
                weaponVisualRenderer.enabled = slot.visualSprite != null;
                weaponVisualRenderer.transform.localPosition = Vector3.zero;
                weaponVisualRenderer.transform.localRotation = Quaternion.identity;
                weaponVisualRenderer.transform.localScale = weaponVisualLocalScale;
                weaponVisualRenderer.sortingOrder = weaponVisualSortingOrder;
                weaponVisualRenderer.color = Color.white;
            }
        }

        private void SpawnMuzzleFlashForActiveSlot(Vector2 direction)
        {
            var slot = activeWeaponSlotIndex == 0 ? weaponSlotA : weaponSlotB;
            if (slot.muzzleFlashPrefab == null)
            {
                return;
            }

            var spawnPoint = weaponMuzzlePoint != null
                ? weaponMuzzlePoint
                : (weaponController != null ? weaponController.ProjectileSpawnPoint : null);
            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector2)transform.right;
            var spawnRotation = Quaternion.FromToRotation(Vector3.right, normalizedDirection);
            var instance = Instantiate(
                slot.muzzleFlashPrefab,
                spawnPosition,
                spawnRotation,
                spawnPoint != null ? spawnPoint : transform);
            Destroy(instance, Mathf.Max(0.01f, muzzleFlashLifeTime));
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
                if (verboseLogging && Time.unscaledTime >= nextDodgeEnergyBlockedLogTime)
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
            PlayRollSpriteAnimation();

            if (verboseLogging && Time.unscaledTime >= nextDodgeInputLogTime)
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

        private void CacheRollSpriteAnimator()
        {
            rollSpriteAnimator = GetComponent<PlayerRollSpriteAnimator>();
        }

        private void PlayRollSpriteAnimation()
        {
            if (rollSpriteAnimator == null)
            {
                CacheRollSpriteAnimator();
            }

            rollSpriteAnimator?.PlayRoll();
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
