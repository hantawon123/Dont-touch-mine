using System;
using Game.Core.Settings;
using UnityEngine.InputSystem;

namespace Game.Client.Emotes
{
    public enum EmoteId
    {
        HipHop,
        Chicken,
        Wave,
        Insult,
        Taunt,
        Spin
    }

    public readonly struct EmoteDefinition
    {
        public EmoteDefinition(EmoteId id, string labelKey, string stateName, bool loop, float durationSeconds)
        {
            Id = id;
            LabelKey = labelKey;
            StateName = stateName;
            Loop = loop;
            DurationSeconds = durationSeconds;
        }

        public EmoteId Id { get; }

        /// <summary><see cref="UiText.Emote"/> key; <see cref="Label"/> reads it in the applied language.</summary>
        public string LabelKey { get; }

        public string Label => UiLocale.Applied(LabelKey);
        public string StateName { get; }
        public bool Loop { get; }
        public float DurationSeconds { get; }
    }

    public static class EmoteCatalog
    {
        public const int Count = 6;
        public const string StatePrefix = "Emote_";

        public static readonly EmoteDefinition[] All =
        {
            new EmoteDefinition(EmoteId.Wave, UiText.Emote.Wave, "Emote_Wave", false, 61f / 30f),
            new EmoteDefinition(EmoteId.Taunt, UiText.Emote.Taunt, "Emote_Taunt", false, 76f / 30f),
            new EmoteDefinition(EmoteId.Insult, UiText.Emote.Insult, "Emote_Insult", false, 81f / 30f),
            new EmoteDefinition(EmoteId.Chicken, UiText.Emote.Chicken, "Emote_Chicken", true, 144f / 30f),
            new EmoteDefinition(EmoteId.HipHop, UiText.Emote.HipHop, "Emote_HipHop", true, 520f / 30f),
            new EmoteDefinition(EmoteId.Spin, UiText.Emote.Spin, "Emote_Spin", true, 266f / 30f)
        };

        public static EmoteDefinition Of(EmoteId id)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }

            return All[0];
        }

        public static bool IsEmoteState(string state) =>
            !string.IsNullOrEmpty(state) &&
            state.StartsWith(StatePrefix, StringComparison.Ordinal);

        /// <summary>
        /// 1회성 표현(인사·도발·모욕) 상태인가. 이들은 걸으면 끊긴다.
        /// 춤은 걸어도 이어진다(전신 클립이라 캡슐이 미끄러지는 건 감수).
        /// </summary>
        public static bool IsOneShotEmoteState(string state)
        {
            if (!IsEmoteState(state))
            {
                return false;
            }

            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].StateName == state)
                {
                    return !All[i].Loop;
                }
            }

            return true;
        }
    }

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
