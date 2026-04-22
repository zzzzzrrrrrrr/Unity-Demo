using System;
using System.Collections.Generic;
using GameMain.GameLogic.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameMain.GameLogic.CharacterSelect
{
    /// <summary>
    /// Handles left-click world selection for character targets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask selectableLayers = ~0;
        [SerializeField] private bool selectionLocked;

        private readonly List<CharacterSelectTarget> targets = new List<CharacterSelectTarget>();
        private CharacterSelectTarget selectedTarget;

        public event Action<CharacterData> SelectionChanged;

        public CharacterData SelectedCharacterData
        {
            get { return selectedTarget != null ? selectedTarget.CharacterData : null; }
        }

        public CharacterSelectTarget SelectedTarget
        {
            get { return selectedTarget; }
        }

        public IReadOnlyList<CharacterSelectTarget> Targets
        {
            get { return targets; }
        }

        public void SetWorldCamera(Camera targetCamera)
        {
            worldCamera = targetCamera;
        }

        public void SetSelectionLocked(bool locked)
        {
            selectionLocked = locked;
        }

        public void RegisterTarget(CharacterSelectTarget target)
        {
            if (target == null || targets.Contains(target))
            {
                return;
            }

            targets.Add(target);
        }

        public void ClearTargets()
        {
            targets.Clear();
            selectedTarget = null;
            selectionLocked = false;
            SelectionChanged?.Invoke(null);
        }

        public void SelectFirstAvailable()
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null && targets[i].CharacterData != null)
                {
                    SelectTarget(targets[i]);
                    return;
                }
            }

            SelectTarget(null);
        }

        public void SelectTarget(CharacterSelectTarget target)
        {
            if (target != null && !targets.Contains(target))
            {
                return;
            }

            if (selectionLocked && target != selectedTarget)
            {
                return;
            }

            if (selectedTarget == target)
            {
                return;
            }

            if (selectedTarget != null)
            {
                selectedTarget.SetSelected(false);
            }

            selectedTarget = target;

            if (selectedTarget != null)
            {
                selectedTarget.SetSelected(true);
            }

            SelectionChanged?.Invoke(SelectedCharacterData);
        }

        private void Update()
        {
            if (selectionLocked)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TrySelectFromMouseClick();
        }

        private void TrySelectFromMouseClick()
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            var mousePosition = Input.mousePosition;
            var worldPosition = cam.ScreenToWorldPoint(mousePosition);
            var point2D = new Vector2(worldPosition.x, worldPosition.y);
            var hitCollider = Physics2D.OverlapPoint(point2D, selectableLayers);
            if (hitCollider == null)
            {
                return;
            }

            var target = hitCollider.GetComponent<CharacterSelectTarget>();
            if (target == null)
            {
                target = hitCollider.GetComponentInParent<CharacterSelectTarget>();
            }

            if (target == null || target.CharacterData == null)
            {
                return;
            }

            SelectTarget(target);
        }
    }
}
