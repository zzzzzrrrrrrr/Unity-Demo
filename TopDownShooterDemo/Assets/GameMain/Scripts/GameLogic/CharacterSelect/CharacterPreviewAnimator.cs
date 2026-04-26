using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Display-only flipbook for CharacterSelectScene role previews.
    /// It only changes SpriteRenderer.sprite and never drives movement or combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPreviewAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private CharacterSelectAvatarController movementSource;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] [Min(1f)] private float framesPerSecond = 8f;
        [SerializeField] private bool restoreDefaultOnDisable = true;

        private Sprite defaultSprite;
        private Sprite[] activeFrames;
        private int frameIndex;
        private float frameTimer;
        private float dodgeSuppressUntil;

        private void Awake()
        {
            EnsureRenderer();
            CaptureDefaultSprite();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            if (defaultSprite == null && targetRenderer != null)
            {
                defaultSprite = targetRenderer.sprite;
            }
        }

        private void OnDisable()
        {
            if (restoreDefaultOnDisable && targetRenderer != null && defaultSprite != null)
            {
                targetRenderer.sprite = defaultSprite;
            }
        }

        private void Update()
        {
            EnsureRenderer();
            if (targetRenderer == null)
            {
                return;
            }

            if (movementSource != null && movementSource.IsDodging)
            {
                dodgeSuppressUntil = Time.time + 0.45f;
                return;
            }

            if (Time.time < dodgeSuppressUntil)
            {
                return;
            }

            var frames = SelectFrames();
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            if (frames != activeFrames)
            {
                activeFrames = frames;
                frameIndex = 0;
                frameTimer = 0f;
                ApplyFrame();
            }

            frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (frameTimer < frameDuration)
            {
                return;
            }

            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % activeFrames.Length;
            ApplyFrame();
        }

        public void Configure(
            SpriteRenderer renderer,
            CharacterSelectAvatarController source,
            Sprite[] idle,
            Sprite[] walk,
            float fps,
            bool trustConfiguredFrames = false)
        {
            if (renderer != null)
            {
                targetRenderer = renderer;
            }

            movementSource = source;
            framesPerSecond = Mathf.Max(1f, fps);
            activeFrames = null;
            frameIndex = 0;
            frameTimer = 0f;
            CaptureDefaultSprite();
            idleFrames = FilterFramesForDefaultSprite(idle, trustConfiguredFrames);
            walkFrames = FilterFramesForDefaultSprite(walk, trustConfiguredFrames);
            RestoreDefaultSprite();
        }

        public void CaptureDefaultSprite()
        {
            EnsureRenderer();
            defaultSprite = targetRenderer != null ? targetRenderer.sprite : null;
        }

        private Sprite[] SelectFrames()
        {
            if (movementSource != null && movementSource.IsControllable && movementSource.IsMoving && walkFrames != null && walkFrames.Length > 0)
            {
                return walkFrames;
            }

            if (idleFrames != null && idleFrames.Length > 0)
            {
                return idleFrames;
            }

            return walkFrames;
        }

        private void ApplyFrame()
        {
            if (activeFrames == null || activeFrames.Length == 0 || targetRenderer == null)
            {
                return;
            }

            var frame = activeFrames[Mathf.Clamp(frameIndex, 0, activeFrames.Length - 1)];
            if (frame != null)
            {
                targetRenderer.sprite = frame;
                targetRenderer.color = Color.white;
            }
        }

        private Sprite[] FilterFramesForDefaultSprite(Sprite[] frames, bool trustConfiguredFrames)
        {
            if (frames == null || frames.Length == 0)
            {
                return Array.Empty<Sprite>();
            }

            if (trustConfiguredFrames)
            {
                return CompactFrames(frames);
            }

            if (defaultSprite == null)
            {
                return CompactFrames(frames);
            }

#if UNITY_EDITOR
            var defaultPath = AssetDatabase.GetAssetPath(defaultSprite);
            if (string.IsNullOrWhiteSpace(defaultPath))
            {
                return Array.Empty<Sprite>();
            }

            for (var i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                var framePath = AssetDatabase.GetAssetPath(frame);
                if (!string.Equals(defaultPath, framePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Array.Empty<Sprite>();
                }
            }
#endif

            return CompactFrames(frames);
        }

        private static Sprite[] CompactFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return Array.Empty<Sprite>();
            }

            var count = 0;
            for (var i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    count++;
                }
            }

            if (count == frames.Length)
            {
                return frames;
            }

            var compact = new Sprite[count];
            var write = 0;
            for (var i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    compact[write++] = frames[i];
                }
            }

            return compact;
        }

        private void RestoreDefaultSprite()
        {
            if (targetRenderer != null && defaultSprite != null)
            {
                targetRenderer.sprite = defaultSprite;
            }
        }

        private void EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }
}
