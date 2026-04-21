// Path: Assets/_Scripts/Tools/InputCompat.cs
// Input abstraction helper for both Input System and Legacy Input Manager.
using System.Collections.Generic;
using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARPGDemo.Tools
{
    public static class InputCompat
    {
        private static readonly object BindingLock = new object();
        private static readonly Dictionary<string, KeyCode> ActionBindings = new Dictionary<string, KeyCode>(8);
        private static float mouseSensitivity = 1f;

        public static float GetHorizontal(string legacyAxisName, KeyCode leftKey, KeyCode rightKey)
        {
            float value = 0f;

            if (IsPressed(leftKey))
            {
                value -= 1f;
            }

            if (IsPressed(rightKey))
            {
                value += 1f;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Mathf.Abs(value) < 0.01f && !string.IsNullOrEmpty(legacyAxisName))
            {
                value = Input.GetAxisRaw(legacyAxisName);
            }
#endif

            return Mathf.Clamp(value, -1f, 1f);
        }

        public static bool GetButtonDown(KeyCode primaryKey, KeyCode secondaryKey, string legacyButtonName)
        {
            bool pressed = IsDown(primaryKey) || IsDown(secondaryKey);

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!pressed && !string.IsNullOrEmpty(legacyButtonName))
            {
                pressed = Input.GetButtonDown(legacyButtonName);
            }
#endif

            return pressed;
        }

        public static bool GetButton(KeyCode primaryKey, KeyCode secondaryKey, string legacyButtonName)
        {
            bool pressed = IsPressed(primaryKey) || IsPressed(secondaryKey);

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!pressed && !string.IsNullOrEmpty(legacyButtonName))
            {
                pressed = Input.GetButton(legacyButtonName);
            }
#endif

            return pressed;
        }

        public static bool IsDown(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                KeyControl key = GetKeyControl(keyboard, keyCode);
                if (key != null && key.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        public static bool IsPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                KeyControl key = GetKeyControl(keyboard, keyCode);
                if (key != null && key.isPressed)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#else
            return false;
#endif
        }

        public static void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Max(0.01f, sensitivity);
        }

        public static float GetMouseSensitivity()
        {
            return mouseSensitivity;
        }

        public static float GetMouseX()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue().x * mouseSensitivity * 0.01f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Mouse X") * mouseSensitivity;
#else
            return 0f;
#endif
        }

        public static float GetMouseY()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue().y * mouseSensitivity * 0.01f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
#else
            return 0f;
#endif
        }

        public static void SetActionBinding(string actionName, KeyCode key)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return;
            }

            lock (BindingLock)
            {
                ActionBindings[actionName] = key;
            }
        }

        public static bool TryGetActionBinding(string actionName, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(actionName))
            {
                return false;
            }

            lock (BindingLock)
            {
                return ActionBindings.TryGetValue(actionName, out key);
            }
        }

        public static KeyCode GetActionBindingOrDefault(string actionName, KeyCode fallback)
        {
            return TryGetActionBinding(actionName, out KeyCode key) ? key : fallback;
        }

        public static bool IsActionDown(string actionName, KeyCode fallback)
        {
            KeyCode key = GetActionBindingOrDefault(actionName, fallback);
            return IsDown(key);
        }

        public static bool IsActionPressed(string actionName, KeyCode fallback)
        {
            KeyCode key = GetActionBindingOrDefault(actionName, fallback);
            return IsPressed(key);
        }

        public static bool TryParseKeyCode(string raw, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            return Enum.TryParse(raw, true, out key);
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl GetKeyControl(Keyboard keyboard, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.K: return keyboard.kKey;
                case KeyCode.B: return keyboard.bKey;
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.U: return keyboard.uKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.F5: return keyboard.f5Key;
                case KeyCode.F9: return keyboard.f9Key;
                default: return null;
            }
        }
#endif
    }
}

