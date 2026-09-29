using System;
using Game.Core.Ports;
using Game.Core.Settings;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Math = System.Math;

namespace Game.Bootstrap
{
    /// <summary>
    /// Waits for the next key or mouse button, through the Input System.
    /// </summary>
    /// <remarks>
    /// Listens to every device at once rather than to an action, because the
    /// point is to find out what was pressed rather than what it currently
    /// does. Escape is answered as "never mind" and never bound: it is what
    /// closes the panels on this screen, and an action holding it would fight
    /// them.
    /// </remarks>
    public sealed class UnityKeyCapture : IKeyCapture
    {
        private readonly ScrollCaptureFilter scroll = new ScrollCaptureFilter();
        private IDisposable listener;
        private Action<string> captured;
        private bool watchingScroll;

        public bool IsCapturing => listener != null;

        public void Begin(Action<string> onCaptured)
        {
            Cancel();
            captured = onCaptured;
            listener = InputSystem.onAnyButtonPress.Call(OnPressed);

            // The wheel is not a button, so it never reaches the call above.
            // Watched separately, and only while a capture is running.
            scroll.Reset();
            InputSystem.onAfterUpdate += OnAfterUpdate;
            watchingScroll = true;
        }

        public void Cancel()
        {
            listener?.Dispose();
            listener = null;
            captured = null;

            if (watchingScroll)
            {
                InputSystem.onAfterUpdate -= OnAfterUpdate;
                watchingScroll = false;
            }
        }

        private void OnAfterUpdate()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var code = scroll.Feed(mouse.scroll.ReadValue().y);
            if (code == null)
            {
                return;
            }

            var waiting = captured;
            Cancel();
            waiting?.Invoke(code);
        }

        private void OnPressed(InputControl control)
        {
            var waiting = captured;
            Cancel();
            if (waiting == null)
            {
                return;
            }

            waiting(CodeOf(control));
        }

        /// <summary>
        /// The code to save for a control, or null for a press that means
        /// nothing — Escape, and anything not on a keyboard or mouse.
        /// </summary>
        private static string CodeOf(InputControl control)
        {
            switch (control)
            {
                // The wheel's own button, which is a button; rolling the wheel
                // is handled by ScrollCaptureFilter instead.
                case KeyControl key:
                    return key.keyCode == Key.Escape ? null : control.name;

                case ButtonControl button when button.device is Mouse:
                    switch (control.name)
                    {
                        case "leftButton":
                            return ControlCatalog.MouseLeft;
                        case "rightButton":
                            return ControlCatalog.MouseRight;
                        case "middleButton":
                            return ControlCatalog.MouseMiddle;
                        default:
                            return null;
                    }

                default:
                    return null;
            }
        }
    }
}
