using System.Text;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARPGDemo
{
    [DisallowMultipleComponent]
    public sealed class StarterARPGBridge : MonoBehaviour
    {
        [SerializeField] private GameObject cameraTargetOverride = null;

        private bool hasWarnedMissingReferences;

        public ThirdPersonController ThirdPersonController { get; private set; }
        public StarterAssetsInputs Inputs { get; private set; }
        public CharacterController CharacterController { get; private set; }
        public Animator Animator { get; private set; }
        public GameObject CameraTarget { get; private set; }

#if ENABLE_INPUT_SYSTEM
        public PlayerInput PlayerInput { get; private set; }
#else
        public Component PlayerInput { get; private set; }
#endif

        public bool HasRequiredReferences =>
            ThirdPersonController != null &&
            Inputs != null &&
            CharacterController != null &&
            Animator != null &&
            PlayerInput != null &&
            CameraTarget != null;

        private void Awake()
        {
            ResolveReferences();
            WarnMissingReferencesOnce();
        }

        private void Reset()
        {
            ResolveReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif

        public void ResolveReferences()
        {
            ThirdPersonController = GetComponent<ThirdPersonController>();
            Inputs = GetComponent<StarterAssetsInputs>();
            CharacterController = GetComponent<CharacterController>();
            Animator = GetComponentInChildren<Animator>();
#if ENABLE_INPUT_SYSTEM
            PlayerInput = GetComponent<PlayerInput>();
#else
            PlayerInput = GetComponent("PlayerInput") as Component;
#endif
            CameraTarget = cameraTargetOverride != null
                ? cameraTargetOverride
                : ThirdPersonController != null
                    ? ThirdPersonController.CinemachineCameraTarget
                    : null;
        }

        public void WarnMissingReferencesOnce()
        {
            if (hasWarnedMissingReferences || HasRequiredReferences)
            {
                return;
            }

            hasWarnedMissingReferences = true;
            Debug.LogWarning(BuildMissingReferenceMessage(), this);
        }

        private string BuildMissingReferenceMessage()
        {
            var builder = new StringBuilder("StarterARPGBridge is missing references:");

            if (ThirdPersonController == null)
            {
                builder.Append(" ThirdPersonController");
            }

            if (Inputs == null)
            {
                builder.Append(" StarterAssetsInputs");
            }

            if (CharacterController == null)
            {
                builder.Append(" CharacterController");
            }

            if (Animator == null)
            {
                builder.Append(" Animator");
            }

            if (PlayerInput == null)
            {
                builder.Append(" PlayerInput");
            }

            if (CameraTarget == null)
            {
                builder.Append(" CameraTarget");
            }

            builder.Append('.');
            return builder.ToString();
        }
    }
}
