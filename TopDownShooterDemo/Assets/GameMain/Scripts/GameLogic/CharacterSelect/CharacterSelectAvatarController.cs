using System;
using System.Collections;
using GameMain.GameLogic.Data;
using GameMain.GameLogic.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Lightweight movement and display-only preview controller for the confirmed CharacterSelectScene actor.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class CharacterSelectAvatarController : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float moveSpeed = 5.2f;
        [SerializeField] [Min(0.1f)] private float dodgeDistance = 3f;
        [SerializeField] [Min(0.05f)] private float dodgeDuration = 0.15f;
        [SerializeField] [Min(0.1f)] private float dodgeCooldown = 0.65f;
        [SerializeField] private KeyCode dodgeKey = KeyCode.Space;
        [SerializeField] private KeyCode weaponSwitchKey = KeyCode.Q;
        [SerializeField] private bool controllable;
        [SerializeField] private PlayerRollSpriteAnimator rollPreviewAnimator;

        [Header("Lobby Loadout Preview")]
        [SerializeField] private Transform weaponVisualRoot;
        [SerializeField] private SpriteRenderer weaponVisualRenderer;
        [SerializeField] private Vector3 weaponVisualLocalPosition = new Vector3(0.66f, 0.06f, 0f);
        [SerializeField] private Vector3 weaponVisualLocalScale = new Vector3(0.92f, 0.92f, 1f);
        [SerializeField] private int weaponVisualSortingOrder = 26;
        [SerializeField] [Min(0.05f)] private float lobbyProjectileLifetime = 0.55f;
        [SerializeField] [Min(0.05f)] private float fallbackFireInterval = 0.22f;
        [SerializeField] [Min(1f)] private float fallbackProjectileSpeed = 12f;

        [Header("Lobby Skill Preview")]
        [SerializeField] private SpriteRenderer skillPreviewRenderer;
        [SerializeField] private int skillPreviewSortingOrder = 25;

        private Rigidbody2D cachedBody;
        private SpriteRenderer bodyRenderer;
        private Vector2 moveInput;
        private Vector2 lastMoveDirection = Vector2.right;
        private Vector2 currentAimDirection = Vector2.right;
        private Vector2 dodgeDirection;
        private bool isDodging;
        private float dodgeTimeRemaining;
        private float dodgeCooldownRemaining;
        private float dodgeSpeed;
        private CharacterData lobbyCharacterData;
        private CharacterInfoPanelController infoPanel;
        private readonly string[] lobbyWeaponLabels = new string[2];
        private readonly Sprite[] lobbyWeaponSprites = new Sprite[2];
        private int activeLobbyWeaponSlot;
        private float fireCooldownRemaining;
        private float skillCooldownRemaining;
        private float skillPreviewRemaining;
        private string activePreviewSkillId = string.Empty;
        private Color baseBodyColor = Color.white;
        private bool hasBaseBodyColor;
        private static Sprite runtimeWhiteSprite;

        public bool IsControllable => controllable;

        public bool IsMoving => moveInput.sqrMagnitude > 0.001f;

        public bool IsDodging => isDodging;

        private void Awake()
        {
            cachedBody = GetComponent<Rigidbody2D>();
            bodyRenderer = GetComponent<SpriteRenderer>();
            rollPreviewAnimator = GetComponent<PlayerRollSpriteAnimator>();
            CaptureBaseBodyColor();

            cachedBody.bodyType = RigidbodyType2D.Kinematic;
            cachedBody.gravityScale = 0f;
            cachedBody.freezeRotation = true;
            cachedBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            cachedBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = GetComponent<Collider2D>();
            collider.isTrigger = false;

            ApplyBodyMode();
            EnsureWeaponPreview();
            EnsureSkillPreview();
        }

        private void Update()
        {
            if (!Application.isPlaying || !controllable)
            {
                moveInput = Vector2.zero;
                TickDodgeCooldown(Time.deltaTime);
                return;
            }

            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            if (moveInput.sqrMagnitude > 0.001f)
            {
                lastMoveDirection = moveInput;
            }

            currentAimDirection = GetAimDirectionFromMouse();
            if (currentAimDirection.sqrMagnitude <= 0.001f)
            {
                currentAimDirection = lastMoveDirection.sqrMagnitude > 0.001f ? lastMoveDirection : Vector2.right;
            }

            currentAimDirection.Normalize();
            ApplyAimVisual(currentAimDirection);

            TickDodgeCooldown(Time.deltaTime);
            TickFireCooldown(Time.deltaTime);
            TickSkillPreview(Time.deltaTime);

            if (Input.GetKeyDown(weaponSwitchKey))
            {
                SwitchLobbyWeapon();
            }

            if (Input.GetMouseButton(0))
            {
                TryFireLobbyPreview();
            }

            if (lobbyCharacterData != null && Input.GetKeyDown(lobbyCharacterData.activeSkillKey))
            {
                TryStartLobbySkillPreview();
            }

            if (Input.GetKeyDown(dodgeKey))
            {
                TryStartDodge();
            }
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
                CancelDodge();
                return;
            }

            if (isDodging)
            {
                var speed = dodgeSpeed > 0f
                    ? dodgeSpeed
                    : Mathf.Max(0.1f, dodgeDistance) / Mathf.Max(0.05f, dodgeDuration);
                cachedBody.velocity = dodgeDirection * speed;
                dodgeTimeRemaining -= Time.fixedDeltaTime;
                if (dodgeTimeRemaining <= 0f)
                {
                    isDodging = false;
                    dodgeSpeed = 0f;
                }

                return;
            }

            cachedBody.velocity = moveInput * Mathf.Max(0.1f, moveSpeed);
        }

        public void SetMoveSpeed(float value)
        {
            moveSpeed = Mathf.Max(0.1f, value);
        }

        public void ConfigureDodge(float distance, float duration, float cooldown, KeyCode key)
        {
            dodgeDistance = Mathf.Max(0.1f, distance);
            dodgeDuration = Mathf.Max(0.05f, duration);
            dodgeCooldown = Mathf.Max(0.1f, cooldown);
            dodgeKey = key;
        }

        public void SetRollPreviewAnimator(PlayerRollSpriteAnimator animator)
        {
            rollPreviewAnimator = animator;
        }

        public void ConfigureLobbyLoadout(CharacterData data, Sprite weapon1Sprite, Sprite weapon2Sprite)
        {
            lobbyCharacterData = data;
            lobbyWeaponLabels[0] = data != null ? data.initialWeapon1 : string.Empty;
            lobbyWeaponLabels[1] = data != null ? data.initialWeapon2 : string.Empty;
            lobbyWeaponSprites[0] = weapon1Sprite;
            lobbyWeaponSprites[1] = weapon2Sprite;
            activeLobbyWeaponSlot = 0;
            fireCooldownRemaining = 0f;
            skillCooldownRemaining = 0f;
            activePreviewSkillId = string.Empty;
            skillPreviewRemaining = 0f;
            EnsureWeaponPreview();
            ApplyLobbyWeaponVisual();
            SetSkillPreviewVisible(false);
        }

        public void SetControllable(bool value)
        {
            controllable = value;
            moveInput = Vector2.zero;
            CancelDodge();
            ApplyBodyMode();
            ApplyLobbyWeaponVisual();
            if (!controllable)
            {
                fireCooldownRemaining = 0f;
                skillPreviewRemaining = 0f;
                activePreviewSkillId = string.Empty;
                SetSkillPreviewVisible(false);
                RestoreBodyColor();
            }
            else
            {
                SetLobbyStatus(BuildConfirmedStatus());
            }
        }

        public void ResetTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            if (cachedBody != null)
            {
                cachedBody.velocity = Vector2.zero;
                cachedBody.angularVelocity = 0f;
            }

            CancelDodge();
        }

        private void TryStartDodge()
        {
            if (isDodging || dodgeCooldownRemaining > 0f)
            {
                return;
            }

            dodgeDirection = moveInput.sqrMagnitude > 0.001f ? moveInput : lastMoveDirection;
            if (dodgeDirection.sqrMagnitude <= 0.001f)
            {
                dodgeDirection = Vector2.right;
            }

            dodgeDirection.Normalize();
            isDodging = true;
            dodgeTimeRemaining = Mathf.Max(0.05f, dodgeDuration);
            dodgeCooldownRemaining = Mathf.Max(0.1f, dodgeCooldown);
            dodgeSpeed = Mathf.Max(0.1f, dodgeDistance) / Mathf.Max(0.05f, dodgeDuration);

            if (rollPreviewAnimator == null)
            {
                rollPreviewAnimator = GetComponent<PlayerRollSpriteAnimator>();
            }

            rollPreviewAnimator?.PlayRoll();
        }

        private void TryFireLobbyPreview()
        {
            if (fireCooldownRemaining > 0f || IsPointerOverUi())
            {
                return;
            }

            var direction = currentAimDirection;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = lastMoveDirection.sqrMagnitude > 0.001f ? lastMoveDirection : Vector2.right;
            }

            direction.Normalize();
            fireCooldownRemaining = GetActiveLobbyFireInterval();
            SpawnLobbyProjectile(direction);
        }

        private void TryStartLobbySkillPreview()
        {
            if (lobbyCharacterData == null || skillCooldownRemaining > 0f)
            {
                return;
            }

            var skillId = string.IsNullOrWhiteSpace(lobbyCharacterData.activeSkillId)
                ? string.Empty
                : lobbyCharacterData.activeSkillId.Trim();
            if (string.IsNullOrEmpty(skillId) || string.Equals(skillId, "None", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(skillId, "TacticalDodge", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryStartSkillDodgePreview())
                {
                    return;
                }

                SetLobbyStatus("\u6280\u80fd\u9884\u89c8\uff1a\u6218\u672f\u7ffb\u6eda\u3002\u9f20\u6807\u5de6\u952e\u5c04\u51fb\u9884\u89c8\uff0c\u6309 Q \u5207\u6362\u521d\u59cb\u6b66\u5668\uff0c\u5728\u4f20\u9001\u95e8\u5904\u6309 E \u51fa\u53d1\u3002");
            }
            else
            {
                activePreviewSkillId = skillId;
                skillPreviewRemaining = Mathf.Clamp(lobbyCharacterData.activeSkillDuration, 0.75f, 1.8f);
                SetSkillPreviewVisible(true);
                ApplySkillPreviewFrame(1f);
                SetLobbyStatus("\u6280\u80fd\u9884\u89c8\uff1a" + lobbyCharacterData.skillName + "\u3002\u9f20\u6807\u5de6\u952e\u5c04\u51fb\u9884\u89c8\uff0c\u6309 Q \u5207\u6362\u521d\u59cb\u6b66\u5668\uff0c\u5728\u4f20\u9001\u95e8\u5904\u6309 E \u51fa\u53d1\u3002");
            }

            skillCooldownRemaining = Mathf.Clamp(lobbyCharacterData.activeSkillCooldown, 0.5f, 2.5f);
        }

        private bool TryStartSkillDodgePreview()
        {
            if (isDodging)
            {
                return false;
            }

            var direction = moveInput.sqrMagnitude > 0.001f ? moveInput : lastMoveDirection;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            var durationMultiplier = Mathf.Max(0.1f, lobbyCharacterData.activeSkillDuration > 0f ? lobbyCharacterData.activeSkillDuration : 1f);
            var distanceMultiplier = Mathf.Max(1f, lobbyCharacterData.activeSkillPower);
            var duration = Mathf.Max(0.05f, dodgeDuration * durationMultiplier);
            var distance = Mathf.Max(0.1f, dodgeDistance * distanceMultiplier);
            dodgeDirection = direction.normalized;
            isDodging = true;
            dodgeTimeRemaining = duration;
            dodgeCooldownRemaining = Mathf.Max(0.1f, dodgeCooldown * 0.55f);
            dodgeSpeed = distance / duration;

            if (rollPreviewAnimator == null)
            {
                rollPreviewAnimator = GetComponent<PlayerRollSpriteAnimator>();
            }

            rollPreviewAnimator?.PlayRoll();
            activePreviewSkillId = "TacticalDodge";
            skillPreviewRemaining = dodgeTimeRemaining;
            SetSkillPreviewVisible(false);
            return distanceMultiplier > 0f;
        }

        private void TickDodgeCooldown(float deltaTime)
        {
            if (dodgeCooldownRemaining > 0f)
            {
                dodgeCooldownRemaining = Mathf.Max(0f, dodgeCooldownRemaining - Mathf.Max(0f, deltaTime));
            }
        }

        private void TickFireCooldown(float deltaTime)
        {
            if (fireCooldownRemaining > 0f)
            {
                fireCooldownRemaining = Mathf.Max(0f, fireCooldownRemaining - Mathf.Max(0f, deltaTime));
            }
        }

        private void TickSkillPreview(float deltaTime)
        {
            var safeDelta = Mathf.Max(0f, deltaTime);
            if (skillCooldownRemaining > 0f)
            {
                skillCooldownRemaining = Mathf.Max(0f, skillCooldownRemaining - safeDelta);
            }

            if (skillPreviewRemaining <= 0f)
            {
                return;
            }

            skillPreviewRemaining = Mathf.Max(0f, skillPreviewRemaining - safeDelta);
            var normalized = Mathf.Clamp01(skillPreviewRemaining / 1.8f);
            ApplySkillPreviewFrame(normalized);
            if (skillPreviewRemaining <= 0f)
            {
                activePreviewSkillId = string.Empty;
                SetSkillPreviewVisible(false);
                RestoreBodyColor();
            }
        }

        private void CancelDodge()
        {
            isDodging = false;
            dodgeTimeRemaining = 0f;
            dodgeSpeed = 0f;
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

        private void SwitchLobbyWeapon()
        {
            if (!HasSecondLobbyWeapon())
            {
                return;
            }

            activeLobbyWeaponSlot = activeLobbyWeaponSlot == 0 ? 1 : 0;
            fireCooldownRemaining = 0f;
            ApplyLobbyWeaponVisual();
            SetLobbyStatus("\u5f53\u524d\u521d\u59cb\u6b66\u5668\uff1a" + GetActiveLobbyWeaponLabel() + "\u3002\u9f20\u6807\u5de6\u952e\u5c04\u51fb\u9884\u89c8\uff0c\u6309 Q \u5207\u6362\uff0c\u6309 F \u9884\u89c8\u6280\u80fd\u3002");
        }

        private bool HasSecondLobbyWeapon()
        {
            return !string.IsNullOrWhiteSpace(lobbyWeaponLabels[1]) || lobbyWeaponSprites[1] != null;
        }

        private void EnsureWeaponPreview()
        {
            if (weaponVisualRoot == null)
            {
                var existing = transform.Find("LobbyWeaponPreview");
                if (existing == null)
                {
                    existing = new GameObject("LobbyWeaponPreview").transform;
                    existing.SetParent(transform, false);
                }

                weaponVisualRoot = existing;
            }

            weaponVisualRoot.localPosition = weaponVisualLocalPosition;
            weaponVisualRoot.localRotation = Quaternion.identity;
            weaponVisualRoot.localScale = Vector3.one;

            if (weaponVisualRenderer == null)
            {
                weaponVisualRenderer = weaponVisualRoot.GetComponent<SpriteRenderer>();
                if (weaponVisualRenderer == null)
                {
                    weaponVisualRenderer = weaponVisualRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }
        }

        private void ApplyLobbyWeaponVisual()
        {
            EnsureWeaponPreview();
            if (weaponVisualRenderer == null)
            {
                return;
            }

            var sprite = lobbyWeaponSprites[Mathf.Clamp(activeLobbyWeaponSlot, 0, 1)];
            weaponVisualRenderer.sprite = sprite;
            weaponVisualRenderer.enabled = controllable && sprite != null;
            weaponVisualRenderer.color = Color.white;
            weaponVisualRenderer.sortingOrder = weaponVisualSortingOrder;
            weaponVisualRenderer.transform.localScale = weaponVisualLocalScale;
            ApplyAimVisual(currentAimDirection.sqrMagnitude > 0.001f ? currentAimDirection : Vector2.right);
        }

        private string GetActiveLobbyWeaponLabel()
        {
            var label = lobbyWeaponLabels[Mathf.Clamp(activeLobbyWeaponSlot, 0, 1)];
            return string.IsNullOrWhiteSpace(label) ? "\u672a\u914d\u7f6e" : label;
        }

        private string BuildConfirmedStatus()
        {
            var roleName = lobbyCharacterData != null && !string.IsNullOrWhiteSpace(lobbyCharacterData.characterName)
                ? lobbyCharacterData.characterName
                : "\u5f53\u524d\u89d2\u8272";
            return "\u5df2\u786e\u8ba4\uff1a" + roleName + "\u3002\u5f53\u524d\u521d\u59cb\u6b66\u5668\uff1a" + GetActiveLobbyWeaponLabel() + "\u3002WASD\u79fb\u52a8 / \u9f20\u6807\u5c04\u51fb / Q\u5207\u67aa / F\u6280\u80fd / Space\u95ea\u907f / \u4f20\u9001\u95e8\u6309E\u8fdb\u5165\u6218\u6597\u3002";
        }

        private Vector2 GetAimDirectionFromMouse()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return lastMoveDirection;
            }

            var mouse = Input.mousePosition;
            var world = camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -camera.transform.position.z));
            return (Vector2)(world - transform.position);
        }

        private void ApplyAimVisual(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.flipX = direction.x < -0.001f;
            }

            EnsureWeaponPreview();
            if (weaponVisualRoot != null)
            {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                weaponVisualRoot.localPosition = new Vector3(direction.x * 0.52f, 0.06f + direction.y * 0.28f, 0f);
                weaponVisualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (weaponVisualRenderer != null)
            {
                weaponVisualRenderer.flipY = direction.x < -0.001f;
            }
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void SpawnLobbyProjectile(Vector2 direction)
        {
            var projectile = new GameObject("LobbyProjectilePreview");
            projectile.transform.position = GetLobbyMuzzleWorldPosition(direction);
            projectile.transform.rotation = Quaternion.identity;
            projectile.transform.localScale = Vector3.one;

            var renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRuntimeWhiteSprite();
            renderer.color = GetActiveLobbyProjectileColor();
            renderer.sortingOrder = weaponVisualSortingOrder + 1;
            renderer.transform.localScale = GetActiveLobbyProjectileScale();

            StartCoroutine(MoveLobbyProjectile(projectile.transform, direction, GetActiveLobbyProjectileSpeed(), lobbyProjectileLifetime));
        }

        private Vector3 GetLobbyMuzzleWorldPosition(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            var origin = weaponVisualRoot != null ? weaponVisualRoot.position : transform.position;
            return origin + (Vector3)(direction * 0.58f);
        }

        private IEnumerator MoveLobbyProjectile(Transform projectile, Vector2 direction, float speed, float lifetime)
        {
            var remaining = Mathf.Max(0.05f, lifetime);
            while (projectile != null && remaining > 0f)
            {
                var delta = Time.deltaTime;
                projectile.position += (Vector3)(direction * Mathf.Max(1f, speed) * delta);
                remaining -= delta;
                yield return null;
            }

            if (projectile != null)
            {
                Destroy(projectile.gameObject);
            }
        }

        private float GetActiveLobbyFireInterval()
        {
            if (lobbyCharacterData == null)
            {
                return fallbackFireInterval;
            }

            var value = activeLobbyWeaponSlot == 0
                ? lobbyCharacterData.weapon1FireInterval
                : lobbyCharacterData.weapon2FireInterval;
            return Mathf.Max(0.05f, value > 0f ? value : fallbackFireInterval);
        }

        private float GetActiveLobbyProjectileSpeed()
        {
            if (lobbyCharacterData == null)
            {
                return fallbackProjectileSpeed;
            }

            var value = activeLobbyWeaponSlot == 0
                ? lobbyCharacterData.weapon1ProjectileSpeed
                : lobbyCharacterData.weapon2ProjectileSpeed;
            return Mathf.Max(1f, value > 0f ? value * 0.55f : fallbackProjectileSpeed);
        }

        private Vector3 GetActiveLobbyProjectileScale()
        {
            return activeLobbyWeaponSlot == 0 ? new Vector3(0.18f, 0.18f, 1f) : new Vector3(0.28f, 0.28f, 1f);
        }

        private Color GetActiveLobbyProjectileColor()
        {
            return activeLobbyWeaponSlot == 0
                ? new Color(0.42f, 0.95f, 1f, 0.92f)
                : new Color(1f, 0.72f, 0.25f, 0.92f);
        }

        private void SetLobbyStatus(string message)
        {
            if (infoPanel == null)
            {
                infoPanel = FindObjectOfType<CharacterInfoPanelController>();
            }

            infoPanel?.SetStatus(message);
        }

        private void EnsureSkillPreview()
        {
            if (skillPreviewRenderer != null)
            {
                return;
            }

            var existing = transform.Find("LobbySkillPreview");
            if (existing == null)
            {
                existing = new GameObject("LobbySkillPreview").transform;
                existing.SetParent(transform, false);
            }

            skillPreviewRenderer = existing.GetComponent<SpriteRenderer>();
            if (skillPreviewRenderer == null)
            {
                skillPreviewRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
            }

            skillPreviewRenderer.sprite = GetRuntimeWhiteSprite();
            skillPreviewRenderer.sortingOrder = skillPreviewSortingOrder;
            SetSkillPreviewVisible(false);
        }

        private void ApplySkillPreviewFrame(float normalized)
        {
            EnsureSkillPreview();
            if (skillPreviewRenderer == null)
            {
                return;
            }

            var t = Mathf.Clamp01(normalized);
            if (string.Equals(activePreviewSkillId, "Bulwark", StringComparison.OrdinalIgnoreCase))
            {
                skillPreviewRenderer.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                skillPreviewRenderer.transform.localScale = new Vector3(2.35f + (1f - t) * 0.25f, 2.35f + (1f - t) * 0.25f, 1f);
                skillPreviewRenderer.color = new Color(0.34f, 0.88f, 1f, 0.28f + 0.3f * t);
                TintBody(new Color(0.76f, 1f, 0.92f, 1f));
                return;
            }

            if (string.Equals(activePreviewSkillId, "Overclock", StringComparison.OrdinalIgnoreCase))
            {
                skillPreviewRenderer.transform.localPosition = new Vector3(0.58f, 0.14f, 0f);
                skillPreviewRenderer.transform.localScale = new Vector3(1.65f + Mathf.PingPong(Time.time * 9f, 0.35f), 0.28f, 1f);
                skillPreviewRenderer.color = new Color(1f, 0.72f, 0.22f, 0.36f + 0.36f * t);
                TintBody(new Color(1f, 0.9f, 0.62f, 1f));
                return;
            }

            skillPreviewRenderer.transform.localPosition = Vector3.zero;
            skillPreviewRenderer.transform.localScale = Vector3.one;
            skillPreviewRenderer.color = Color.clear;
        }

        private void SetSkillPreviewVisible(bool visible)
        {
            EnsureSkillPreview();
            if (skillPreviewRenderer != null)
            {
                skillPreviewRenderer.enabled = visible;
            }
        }

        private void CaptureBaseBodyColor()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }

            if (bodyRenderer != null)
            {
                baseBodyColor = bodyRenderer.color;
                hasBaseBodyColor = true;
            }
        }

        private void TintBody(Color color)
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = color;
            }
        }

        private void RestoreBodyColor()
        {
            if (bodyRenderer != null && hasBaseBodyColor)
            {
                bodyRenderer.color = baseBodyColor;
            }
        }

        private static Sprite GetRuntimeWhiteSprite()
        {
            if (runtimeWhiteSprite != null)
            {
                return runtimeWhiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "LobbyPreview_WhitePixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);
            runtimeWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            runtimeWhiteSprite.name = "LobbyPreview_WhitePixelSprite";
            return runtimeWhiteSprite;
        }
    }
}
