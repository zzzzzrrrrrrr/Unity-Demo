using UnityEngine;

namespace GameMain.GameLogic.Visual
{
    /// <summary>
    /// Display-only sprite flipbook. It only changes SpriteRenderer.sprite.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteFlipbookAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField] [Min(1f)] private float framesPerSecond = 10f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;

        private int frameIndex;
        private float frameTimer;
        private bool playing;

        private void OnEnable()
        {
            EnsureRenderer();
            if (playOnEnable)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!playing || targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            if (frameTimer < frameDuration)
            {
                return;
            }

            frameTimer -= frameDuration;
            frameIndex++;
            if (frameIndex >= frames.Length)
            {
                if (!loop)
                {
                    frameIndex = frames.Length - 1;
                    playing = false;
                }
                else
                {
                    frameIndex = 0;
                }
            }

            ApplyCurrentFrame();
        }

        public void Configure(SpriteRenderer renderer, Sprite[] newFrames, float fps, bool shouldLoop = true)
        {
            if (renderer != null)
            {
                targetRenderer = renderer;
            }

            frames = CompactFrames(newFrames);
            framesPerSecond = Mathf.Max(1f, fps);
            loop = shouldLoop;
            frameIndex = 0;
            frameTimer = 0f;
            playing = playOnEnable && frames.Length > 0;
            ApplyCurrentFrame();
        }

        public void Play()
        {
            if (frames == null || frames.Length == 0)
            {
                playing = false;
                return;
            }

            playing = true;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            EnsureRenderer();
            if (targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            var frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            if (frame != null)
            {
                targetRenderer.sprite = frame;
            }
        }

        private void EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private static Sprite[] CompactFrames(Sprite[] source)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            var count = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    count++;
                }
            }

            if (count == source.Length)
            {
                return source;
            }

            var compact = new Sprite[count];
            var write = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    compact[write++] = source[i];
                }
            }

            return compact;
        }
    }
}
