using GameMain.GameLogic.Player;
using UnityEngine;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Interaction-only weapon pickup payload. PlayerController remains the weapon slot truth owner.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [DisallowMultipleComponent]
    public sealed class WeaponPickupStation : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer frameRenderer;
        [SerializeField] private SpriteRenderer weaponRenderer;
        [SerializeField] private Sprite weaponSprite;
        [Header("Interaction")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [Header("Weapon Payload")]
        [SerializeField] private string weaponLabel = "Pickup Weapon";
        [SerializeField] [Min(0.01f)] private float fireInterval = 0.16f;
        [SerializeField] [Min(0.1f)] private float projectileSpeed = 20f;
        [SerializeField] [Min(0f)] private float projectileDamage = 12f;
        [SerializeField] [Min(0.1f)] private float projectileLifetime = 3.2f;
        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging;

        private PlayerController nearbyPlayer;
        private bool loggedMissingSprite;
        private bool loggedPlayerEntered;

        private void Awake()
        {
            ResolveRenderers();
            EnsureTriggerCollider();
            ApplyVisual();
        }

        private void OnValidate()
        {
            ResolveRenderers();
            ApplyVisual();
        }

        private void Update()
        {
            if (nearbyPlayer == null || !Input.GetKeyDown(interactKey))
            {
                return;
            }

            TryApplyPickup(nearbyPlayer);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player != null)
            {
                nearbyPlayer = player;
                if (verboseLogging && !loggedPlayerEntered)
                {
                    loggedPlayerEntered = true;
                    Debug.Log(
                        "WeaponPickupStation player entered. station=" + name +
                        " weapon=" + weaponLabel +
                        " key=" + interactKey,
                        this);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player != null && player == nearbyPlayer)
            {
                nearbyPlayer = null;
            }
        }

        public void Configure(
            SpriteRenderer newFrameRenderer,
            SpriteRenderer newWeaponRenderer,
            Sprite newWeaponSprite,
            KeyCode newInteractKey,
            string newWeaponLabel,
            float newFireInterval,
            float newProjectileSpeed,
            float newProjectileDamage,
            float newProjectileLifetime)
        {
            frameRenderer = newFrameRenderer;
            weaponRenderer = newWeaponRenderer;
            weaponSprite = newWeaponSprite != null ? newWeaponSprite : (frameRenderer != null ? frameRenderer.sprite : null);
            interactKey = newInteractKey;
            weaponLabel = string.IsNullOrWhiteSpace(newWeaponLabel) ? "Pickup Weapon" : newWeaponLabel.Trim();
            fireInterval = Mathf.Max(0.01f, newFireInterval);
            projectileSpeed = Mathf.Max(0.1f, newProjectileSpeed);
            projectileDamage = Mathf.Max(0f, newProjectileDamage);
            projectileLifetime = Mathf.Max(0.1f, newProjectileLifetime);
            EnsureTriggerCollider();
            ApplyVisual();
        }

        private void TryApplyPickup(PlayerController player)
        {
            if (weaponSprite == null)
            {
                if (!loggedMissingSprite)
                {
                    loggedMissingSprite = true;
                    Debug.LogWarning("WeaponPickupStation ignored pickup because weaponSprite is missing.", this);
                }

                return;
            }

            var before = player.GetActiveWeaponRuntimeSnapshot();
            if (verboseLogging)
            {
                Debug.Log(
                    "WeaponPickupStation pickup input. station=" + name +
                    " old=" + before.Label +
                    " new=" + weaponLabel,
                    this);
            }

            var replaced = player.TryReplaceActiveWeaponSlot(
                weaponLabel,
                fireInterval,
                projectileSpeed,
                projectileDamage,
                projectileLifetime,
                weaponSprite);
            if (verboseLogging && replaced)
            {
                Debug.Log(
                    "WeaponPickupStation replaced active weapon. station=" + name +
                    " slot=" + before.SlotIndex +
                    " old=" + before.Label +
                    " new=" + weaponLabel,
                    this);
            }
        }

        private void ResolveRenderers()
        {
            if (frameRenderer == null)
            {
                frameRenderer = GetComponent<SpriteRenderer>();
            }

            if (weaponRenderer == null)
            {
                var icon = transform.Find("WeaponIcon");
                if (icon != null)
                {
                    weaponRenderer = icon.GetComponent<SpriteRenderer>();
                }
            }
        }

        private void EnsureTriggerCollider()
        {
            var trigger = GetComponent<CircleCollider2D>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<CircleCollider2D>();
            }

            trigger.isTrigger = true;
            trigger.enabled = true;
            trigger.radius = 0.95f;
            trigger.offset = Vector2.zero;
        }

        private void ApplyVisual()
        {
            if (weaponRenderer == null)
            {
                return;
            }

            weaponRenderer.sprite = weaponSprite;
            weaponRenderer.enabled = weaponSprite != null;
            weaponRenderer.color = Color.white;
        }
    }
}
