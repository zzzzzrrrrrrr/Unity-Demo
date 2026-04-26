using System;
using System.Collections;
using System.Collections.Generic;
using GameMain.GameLogic.Run;
using GameMain.GameLogic.World;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameMain.GameLogic.Player
{
    /// <summary>
    /// Minimal sprite flipbook for Ranger roll presentation. It never drives movement, physics, or cooldowns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRollSpriteAnimator : MonoBehaviour
    {
        private const string RangerId = "ranger";
        private const string RangerEnglishName = "Ranger";
        private const string RangerChineseName = "\u6E38\u4FA0";

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] rollFrames;
        [SerializeField] [Min(1f)] private float framesPerSecond = 12f;
        [SerializeField] private bool playOnlyForRanger = true;

#if UNITY_EDITOR
        [SerializeField] private string editorRollSheetPath = "Assets/Sprite/Character/Ranger/1/Roll_Sheet.png";
#endif

        private Sprite defaultSprite;
        private Sprite activeRestoreSprite;
        private Coroutine rollRoutine;

        private void Awake()
        {
            EnsureRenderer();
            AutoLoadEditorRollFramesIfNeeded();
            CaptureDefaultSprite();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            AutoLoadEditorRollFramesIfNeeded();
            if (defaultSprite == null && targetRenderer != null)
            {
                defaultSprite = targetRenderer.sprite;
            }
        }

        private void OnDisable()
        {
            StopRoll(true);
        }

        public void SetTargetRenderer(SpriteRenderer renderer)
        {
            if (renderer != null)
            {
                targetRenderer = renderer;
            }

            CaptureDefaultSprite();
        }

        public void CaptureDefaultSprite()
        {
            EnsureRenderer();
            defaultSprite = targetRenderer != null ? targetRenderer.sprite : null;
        }

        public void PlayRoll()
        {
            if (!isActiveAndEnabled || !CanPlayForCurrentCharacter())
            {
                return;
            }

            EnsureRenderer();
            AutoLoadEditorRollFramesIfNeeded();
            if (targetRenderer == null || rollFrames == null || rollFrames.Length == 0)
            {
                return;
            }

            var restoreSprite = rollRoutine != null && activeRestoreSprite != null
                ? activeRestoreSprite
                : targetRenderer.sprite;
            if (defaultSprite == null && restoreSprite != null)
            {
                defaultSprite = restoreSprite;
            }

            StopRoll(false);
            activeRestoreSprite = restoreSprite;
            rollRoutine = StartCoroutine(PlayRollRoutine());
        }

        private IEnumerator PlayRollRoutine()
        {
            var delay = 1f / Mathf.Max(1f, framesPerSecond);
            for (var i = 0; i < rollFrames.Length; i++)
            {
                var frame = rollFrames[i];
                if (frame != null && targetRenderer != null)
                {
                    targetRenderer.sprite = frame;
                }

                yield return new WaitForSeconds(delay);
            }

            RestoreSprite(activeRestoreSprite);
            activeRestoreSprite = null;
            rollRoutine = null;
        }

        private void StopRoll(bool restoreDefault)
        {
            if (rollRoutine != null)
            {
                StopCoroutine(rollRoutine);
                rollRoutine = null;
            }

            if (restoreDefault)
            {
                RestoreSprite(activeRestoreSprite != null ? activeRestoreSprite : defaultSprite);
                activeRestoreSprite = null;
            }
        }

        private void RestoreSprite(Sprite sprite)
        {
            if (targetRenderer != null && sprite != null)
            {
                targetRenderer.sprite = sprite;
            }
        }

        private void EnsureRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private bool CanPlayForCurrentCharacter()
        {
            if (!playOnlyForRanger)
            {
                return true;
            }

            var runtimeState = GetComponent<RunCharacterRuntimeState>();
            if (runtimeState != null)
            {
                if (IsRangerIdentity(runtimeState.CharacterId, runtimeState.CharacterName))
                {
                    return true;
                }

                var source = runtimeState.SourceCharacterData;
                if (source != null && IsRangerIdentity(source.characterId, source.characterName))
                {
                    return true;
                }
            }

            var selected = RunSessionContext.SelectedCharacterData;
            return selected != null && IsRangerIdentity(selected.characterId, selected.characterName);
        }

        private static bool IsRangerIdentity(string characterId, string characterName)
        {
            return string.Equals(characterId, RangerId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(characterName, RangerEnglishName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(characterName, RangerChineseName, StringComparison.Ordinal);
        }

        private void AutoLoadEditorRollFramesIfNeeded()
        {
#if UNITY_EDITOR
            if (rollFrames != null && rollFrames.Length > 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(editorRollSheetPath))
            {
                return;
            }

            var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(editorRollSheetPath.Trim());
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var frames = new List<Sprite>(assets.Length);
            for (var i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    frames.Add(sprite);
                }
            }

            if (frames.Count == 0)
            {
                return;
            }

            frames.Sort(CompareSpritesBySheetRect);
            rollFrames = frames.ToArray();
#endif
        }

#if UNITY_EDITOR
        private static int CompareSpritesBySheetRect(Sprite left, Sprite right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var yCompare = right.rect.y.CompareTo(left.rect.y);
            return yCompare != 0 ? yCompare : left.rect.x.CompareTo(right.rect.x);
        }
#endif
    }
}
