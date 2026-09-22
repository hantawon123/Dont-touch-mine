using Game.Core.Emotes;
using UnityEngine.InputSystem;

namespace Game.Client.Emotes
{
    /// <summary>
    /// 휠이 열린 동안의 선택: 1–6(넘패드 포함)이 조각, 그 외 키는 취소.
    /// </summary>
    public static class EmoteWheelSelection
    {
        public const float SliceDegrees = 360f / EmoteCatalog.Count;

        public static bool TrySliceFromKeyboard(Keyboard keyboard, out int slice)
        {
            slice = 0;
            if (keyboard == null)
            {
                return false;
            }

            return TryDigit(keyboard, out slice);
        }

        public static bool TrySliceFromDigit(int digit, out int slice)
        {
            if (digit < 1 || digit > EmoteCatalog.Count)
            {
                slice = 0;
                return false;
            }

            slice = digit - 1;
            return true;
        }

        public static bool IsSliceKey(Key key) =>
            key is Key.Digit1 or Key.Digit2 or Key.Digit3 or Key.Digit4 or Key.Digit5 or Key.Digit6
                or Key.Numpad1 or Key.Numpad2 or Key.Numpad3 or Key.Numpad4 or Key.Numpad5 or Key.Numpad6;

        public static bool WasCancelKeyPressed(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return false;
            }

            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame)
                {
                    continue;
                }

                if (IsSliceKey(control.keyCode))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool TryDigit(Keyboard keyboard, out int slice)
        {
            if (Pressed(keyboard.digit1Key) || Pressed(keyboard.numpad1Key))
            {
                return TrySliceFromDigit(1, out slice);
            }

            if (Pressed(keyboard.digit2Key) || Pressed(keyboard.numpad2Key))
            {
                return TrySliceFromDigit(2, out slice);
            }

            if (Pressed(keyboard.digit3Key) || Pressed(keyboard.numpad3Key))
            {
                return TrySliceFromDigit(3, out slice);
            }

            if (Pressed(keyboard.digit4Key) || Pressed(keyboard.numpad4Key))
            {
                return TrySliceFromDigit(4, out slice);
            }

            if (Pressed(keyboard.digit5Key) || Pressed(keyboard.numpad5Key))
            {
                return TrySliceFromDigit(5, out slice);
            }

            if (Pressed(keyboard.digit6Key) || Pressed(keyboard.numpad6Key))
            {
                return TrySliceFromDigit(6, out slice);
            }

            slice = 0;
            return false;
        }

        private static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl button) =>
            button != null && button.wasPressedThisFrame;
    }
}
