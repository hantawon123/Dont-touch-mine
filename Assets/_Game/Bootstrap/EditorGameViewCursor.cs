#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Bootstrap
{
    // Editor Escape disables native lock/hide independently of Cursor.lockState.
    // Restore the same permission as a Game-view click, only after gameplay asks
    // for capture again. This file is excluded entirely from player builds.
    [InitializeOnLoad]
    internal static class EditorGameViewCursor
    {
        internal static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        internal static readonly MethodInfo AllowCursorLockAndHide = GameViewType?.GetMethod(
            "AllowCursorLockAndHide", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(bool) }, null);
        private static EditorWindow pendingView;

        static EditorGameViewCursor()
        {
            if (AllowCursorLockAndHide == null)
            {
                Debug.LogWarning("[QA-Cursor] Editor cursor bridge is unavailable for this Unity version.");
                return;
            }
            InputSystem.onAfterUpdate += ObserveEscape;
            EditorApplication.update += RestoreAfterEscape;
        }

        private static void ObserveEscape()
        {
            var focused = EditorWindow.focusedWindow;
            if (EditorApplication.isPlaying && !EditorApplication.isPaused &&
                focused != null && GameViewType.IsInstanceOfType(focused) &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                pendingView = focused;
        }

        private static void RestoreAfterEscape()
        {
            if (pendingView == null) return;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorWindow.focusedWindow != pendingView || !Application.isFocused)
            {
                pendingView = null;
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) return;
            // An open menu or text input still owns the mouse. Do not close or
            // bypass it; wait for the existing presenter to request gameplay.
            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible) return;
            AllowCursorLockAndHide.Invoke(pendingView, new object[] { true });
            pendingView = null;
            Debug.Log("[QA-Cursor] Game view native lock/hide restored after Escape.");
        }
    }
}
#endif
