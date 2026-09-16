using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// Everything the 컨트롤 tab lets a key be put on, in the order the rows
    /// are drawn: the microphone, then what the player does, then what the
    /// player does to a thing.
    /// </summary>
    public enum ControlAction
    {
        MicrophoneTalk,
        VoiceToggle,
        MoveForward,
        MoveLeft,
        MoveBackward,
        MoveRight,
        Sprint,
        Jump,
        Crouch,
        Prone,
        ToggleView,
        PrimaryAction,
        Interact,
        PlacementMode,
        RotateLeft,
        RotateRight,
        RaiseObject,
        LowerObject,
        ToggleKeyGuide,
        ToggleSpeaker
    }

    /// <summary>The 컨트롤 tab's sliders, in the order they are drawn.</summary>
    public enum ControlSensitivity
    {
        FirstPersonMouse,
        ThirdPersonMouse,
        ThirdPersonCamera
    }

    /// <summary>The 컨트롤 tab's on-or-off rows, in the order they are drawn.</summary>
    public enum ControlToggle
    {
        FirstPersonInvertX,
        FirstPersonInvertY,
        ThirdPersonInvertX,
        ThirdPersonInvertY
    }

    /// <summary>
    /// What the 컨트롤 tab holds: a key for each action, a number for each
    /// slider, and an answer for each axis that can be reversed.
    /// </summary>
    /// <remarks>
    /// Three kinds of row, so three sets rather than one: the keys and the
    /// reversals are codes and lean on <see cref="OptionValues"/>, and the
    /// sensitivities are numbers.
    /// <para>
    /// Every action has its key to itself, and <see cref="TryRebind"/> is what
    /// keeps that true. The rows that used to be allowed to share are one row
    /// each now — 물건 상호작용 and 공격/던지기/배치 — because each of those was
    /// one key doing one job that the game read three ways.
    /// </para>
    /// </remarks>
    public readonly struct ControlSettings : IEquatable<ControlSettings>
    {
        internal static readonly int SensitivityCount =
            Enum.GetValues(typeof(ControlSensitivity)).Length;

        private readonly OptionValues bindings;
        private readonly OptionValues toggles;
        private readonly int[] sensitivities;

        private ControlSettings(OptionValues bindings, OptionValues toggles, int[] sensitivities)
        {
            this.bindings = bindings;
            this.toggles = toggles;
            this.sensitivities = sensitivities;
        }

        public static ControlSettings Empty => default;

        internal OptionValues Bindings => bindings;

        internal OptionValues Toggles => toggles;

        /// <summary>
        /// The key on an action, as a code <see cref="ControlCatalog.KeyLabel"/>
        /// reads, or empty when the action has no key.
        /// </summary>
        public string Get(ControlAction action) => bindings.Get((int)action);

        /// <summary>
        /// Puts a key on an action without a word about anyone else holding it.
        /// For building a set from nothing; <see cref="TryRebind"/> is what a
        /// player's choice goes through.
        /// </summary>
        public ControlSettings With(ControlAction action, string keyCode) =>
            new ControlSettings(bindings.With((int)action, keyCode), toggles, Copy());

        /// <summary>
        /// Puts a key on an action, unless somebody who may not share already
        /// holds it.
        /// </summary>
        /// <param name="result">
        /// The settings with the key moved. Unchanged when the answer is false.
        /// </param>
        /// <param name="holder">
        /// Who already has the key, when the answer is false.
        /// </param>
        /// <remarks>
        /// A key in use is refused rather than taken. Taking it would leave
        /// whoever held it with nothing, and a row that has quietly gone blank
        /// is how a player loses the ability to jump without knowing why. The
        /// refusal costs them a step — the other action has to be moved first —
        /// but it never loses them a key they did not choose to give up.
        /// </remarks>
        public bool TryRebind(
            ControlAction action, string keyCode, out ControlSettings result, out ControlAction holder)
        {
            holder = default;
            result = this;

            if (string.IsNullOrEmpty(keyCode))
            {
                result = With(action, keyCode);
                return true;
            }

            foreach (ControlAction other in Enum.GetValues(typeof(ControlAction)))
            {
                if (other == action
                    || !string.Equals(Get(other), keyCode, StringComparison.Ordinal))
                {
                    continue;
                }

                holder = other;
                return false;
            }

            result = With(action, keyCode);
            return true;
        }

        public string Get(ControlToggle toggle) => toggles.Get((int)toggle);

        public ControlSettings With(ControlToggle toggle, string code) =>
            new ControlSettings(bindings, toggles.With((int)toggle, code), Copy());

        public bool IsOn(ControlToggle toggle) =>
            string.Equals(Get(toggle), InterfaceCatalog.On, StringComparison.Ordinal);

        /// <inheritdoc cref="SoundSettings.Get(SoundVolume)"/>
        public int Get(ControlSensitivity sensitivity)
        {
            var index = (int)sensitivity;
            if (sensitivities == null || index < 0 || index >= sensitivities.Length)
            {
                return ControlCatalog.Unset;
            }

            return sensitivities[index];
        }

        public ControlSettings With(ControlSensitivity sensitivity, int percent)
        {
            var index = (int)sensitivity;
            if (index < 0 || index >= SensitivityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sensitivity));
            }

            var next = Copy();
            next[index] = percent;
            return new ControlSettings(bindings, toggles, next);
        }

        public bool Equals(ControlSettings other)
        {
            if (bindings != other.bindings || toggles != other.toggles)
            {
                return false;
            }

            for (var index = 0; index < SensitivityCount; index++)
            {
                if (Get((ControlSensitivity)index) != other.Get((ControlSensitivity)index))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is ControlSettings other && Equals(other);

        public override int GetHashCode()
        {
            var hash = (bindings.GetHashCode() * 31) + toggles.GetHashCode();
            for (var index = 0; index < SensitivityCount; index++)
            {
                hash = (hash * 31) + Get((ControlSensitivity)index);
            }

            return hash;
        }

        public static bool operator ==(ControlSettings left, ControlSettings right) => left.Equals(right);

        public static bool operator !=(ControlSettings left, ControlSettings right) => !left.Equals(right);

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                parts.Add($"{action}={Get(action)}");
            }

            return "Controls(" + string.Join(", ", parts) + ")";
        }

        private int[] Copy()
        {
            var copy = new int[SensitivityCount];
            for (var index = 0; index < SensitivityCount; index++)
            {
                copy[index] = Get((ControlSensitivity)index);
            }

            return copy;
        }
    }

    /// <summary>
    /// What the 컨트롤 rows allow: which key each action starts on, how far the
    /// sliders go, and what a reversal can be.
    /// </summary>
    public static class ControlCatalog
    {
        public const int Unset = -1;
        public const int MinSensitivity = 0;
        public const int MaxSensitivity = 100;
        public const int DefaultSensitivity = 50;

        /// <summary>An action nobody has given a key to.</summary>
        public const string Unbound = "";

        /// <summary>Shown for <see cref="Unbound"/>.</summary>
        public const string UnboundLabel = "없음";

        /// <summary>
        /// Keys the game listens to outside the input asset, which no row may
        /// take. Each with the name of what holds it, for the refusal.
        /// </summary>
        /// <remarks>
        /// These are read straight off <c>Keyboard.current</c> rather than
        /// through an action — 숨기기 완료 in the match HUD, the lobby's 1 and 2
        /// shortcuts and Esc in its pause menu, Enter to open the chat — so
        /// nothing here can move them
        /// and the screen must not offer to. A row put on one would fire both,
        /// the row's action and the shortcut, on a single press.
        /// <para>
        /// The number pad is listed beside the digits and beside Enter because
        /// the lobby and the chat read both for the same thing.
        /// </para>
        /// <para>
        /// Esc never reaches this list from a live capture — <c>UnityKeyCapture</c>
        /// answers it as "never mind" — but a save written by a build that let
        /// it through still has to be repaired.
        /// </para>
        /// </remarks>
        private static readonly (string Code, string Holder)[] Reserved =
        {
            ("y", "숨기기 완료"),
            ("1", "캐릭터 단축키"),
            ("numpad1", "캐릭터 단축키"),
            ("2", "참가자 목록 단축키"),
            ("numpad2", "참가자 목록 단축키"),
            ("escape", "환경설정 메뉴"),
            ("enter", "채팅"),
            ("numpadEnter", "채팅")
        };

        /// <summary>
        /// Whether a key belongs to something this screen cannot move, and
        /// what that is.
        /// </summary>
        public static bool IsReserved(string code, out string holder)
        {
            foreach (var entry in Reserved)
            {
                if (string.Equals(entry.Code, code, StringComparison.Ordinal))
                {
                    holder = entry.Holder;
                    return true;
                }
            }

            holder = null;
            return false;
        }

        public const string MouseLeft = "mouseLeft";
        public const string MouseRight = "mouseRight";
        public const string MouseMiddle = "mouseMiddle";
        public const string ScrollUp = "scrollUp";
        public const string ScrollDown = "scrollDown";

        /// <summary>
        /// Nobody shares a key. Two groups used to — 들기·놓기·파괴장치 상호작용 on
        /// F, and 던지기·배치하기·공격하기 on the left button — because each group
        /// was one key that the game read several ways depending on what the
        /// player was holding. They are one row each now, 물건 상호작용 and
        /// 공격/던지기/배치, so the exception has nothing left to cover and the
        /// rule is simply that a key belongs to one action.
        /// </summary>
        /// <summary>Reversing an axis is on or off, like the 인터페이스 toggles.</summary>
        public static OptionChoices Reversals { get; } = new OptionChoices(
            InterfaceCatalog.Off,
            new OptionChoice(InterfaceCatalog.On, "켜기"),
            new OptionChoice(InterfaceCatalog.Off, "끄기"));

        /// <summary>
        /// What each action starts on.
        /// </summary>
        /// <remarks>
        /// The keys <c>InputSystem_Actions</c> binds, which are the design's
        /// own everywhere it says — the same WASD, Q and E to turn a thing, the
        /// wheel to raise it, space, shift, V, C, Z and the two mouse buttons.
        /// <para>
        /// The microphone is the one the design does not agree with: it says T
        /// and the asset says G. G wins, because the asset is what the game
        /// actually listens to. A default that names a key the game does not
        /// use is worse than a default the design did not pick — it reads as a
        /// key that has stopped working.
        /// </para>
        /// <para>
        /// No two start on the same key; see <see cref="ControlSettings"/>.
        /// </para>
        /// </remarks>
        private static readonly (ControlAction Action, string Code)[] Bindings =
        {
            (ControlAction.MicrophoneTalk, "g"),
            (ControlAction.VoiceToggle, "b"),
            (ControlAction.MoveForward, "w"),
            (ControlAction.MoveLeft, "a"),
            (ControlAction.MoveBackward, "s"),
            (ControlAction.MoveRight, "d"),
            (ControlAction.Sprint, "leftShift"),
            (ControlAction.Jump, "space"),
            (ControlAction.Crouch, "c"),
            (ControlAction.Prone, "z"),
            (ControlAction.ToggleView, "v"),
            (ControlAction.PrimaryAction, MouseLeft),
            (ControlAction.Interact, "f"),
            (ControlAction.PlacementMode, MouseRight),
            (ControlAction.RotateLeft, "q"),
            (ControlAction.RotateRight, "e"),
            (ControlAction.RaiseObject, ScrollUp),
            (ControlAction.LowerObject, ScrollDown),
            (ControlAction.ToggleKeyGuide, "l"),
            (ControlAction.ToggleSpeaker, "t")
        };

        /// <summary>
        /// What a player who has never opened the tab gets, and what 초기화
        /// puts back.
        /// </summary>
        public static ControlSettings Defaults
        {
            get
            {
                var settings = ControlSettings.Empty;
                foreach (var binding in Bindings)
                {
                    settings = settings.With(binding.Action, binding.Code);
                }

                foreach (ControlToggle toggle in Enum.GetValues(typeof(ControlToggle)))
                {
                    settings = settings.With(toggle, Reversals.Default.Code);
                }

                foreach (ControlSensitivity sensitivity in Enum.GetValues(typeof(ControlSensitivity)))
                {
                    settings = settings.With(sensitivity, DefaultSensitivity);
                }

                return settings;
            }
        }

        /// <summary>
        /// Brings saved or offered values back within what the game can do:
        /// sliders inside their range and reversals we recognise.
        /// </summary>
        /// <remarks>
        /// Blanks are left alone: an action with no key is a thing a player can
        /// mean, and only the store knows whether a blank was chosen or never
        /// written — see <c>PlayerPrefsControlSettingsStore</c>.
        /// <para>
        /// A key held by two actions is left with the first of them, in the
        /// order the rows are drawn, and the others are emptied.
        /// This screen never makes such a pair — it refuses instead — so this
        /// is repair work for a save written by a build whose rules were
        /// different, or edited by hand, and the game needs the rule to hold
        /// however the save got that way.
        /// </para>
        /// </remarks>
        public static ControlSettings Normalise(ControlSettings settings)
        {
            var result = settings;

            foreach (ControlAction action in Enum.GetValues(typeof(ControlAction)))
            {
                var code = result.Get(action);
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (IsReserved(code, out _))
                {
                    result = result.With(action, Unbound);
                    continue;
                }

                foreach (ControlAction other in Enum.GetValues(typeof(ControlAction)))
                {
                    if (other <= action
                        || !string.Equals(result.Get(other), code, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result = result.With(other, Unbound);
                }
            }

            foreach (ControlSensitivity sensitivity in Enum.GetValues(typeof(ControlSensitivity)))
            {
                var percent = settings.Get(sensitivity);
                result = result.With(
                    sensitivity,
                    percent == Unset ? DefaultSensitivity : Clamp(percent));
            }

            foreach (ControlToggle toggle in Enum.GetValues(typeof(ControlToggle)))
            {
                result = result.With(
                    toggle,
                    Reversals.TryFind(settings.Get(toggle), out var listed)
                        ? listed.Code
                        : Reversals.Default.Code);
            }

            return result;
        }

        public static int Clamp(int percent) =>
            percent < MinSensitivity
                ? MinSensitivity
                : percent > MaxSensitivity
                    ? MaxSensitivity
                    : percent;

        /// <summary>
        /// What a key button shows. The codes are the Input System's own names
        /// wherever there is one, so the layer that listens for a press need
        /// not translate; the words are only for reading.
        /// </summary>
        /// <remarks>
        /// The two wheel directions are told apart by an arrow, where the
        /// design draws both as 스크롤. Once the wheel can be chosen a
        /// direction at a time — roll it and the roll is what gets bound — two
        /// rows reading alike leaves no way to see which way round they went.
        /// </remarks>
        public static string KeyLabel(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return UnboundLabel;
            }

            switch (code)
            {
                case MouseLeft:
                    return "좌클릭";
                case MouseRight:
                    return "우클릭";
                case MouseMiddle:
                    return "휠클릭";
                case ScrollUp:
                    return "스크롤 ↑";
                case ScrollDown:
                    return "스크롤 ↓";
                case "leftShift":
                case "rightShift":
                    return "SHIFT";
                case "leftCtrl":
                case "rightCtrl":
                    return "CTRL";
                case "leftAlt":
                case "rightAlt":
                    return "ALT";
                case "space":
                    return "SPACE";
                case "tab":
                    return "TAB";
                case "enter":
                    return "ENTER";
                case "backspace":
                    return "BACKSPACE";
                case "upArrow":
                    return "↑";
                case "downArrow":
                    return "↓";
                case "leftArrow":
                    return "←";
                case "rightArrow":
                    return "→";
                default:

                    // A letter or a digit, which the Input System names in
                    // lower case and a key button shows in upper.
                    return code.ToUpperInvariant();
            }
        }
    }
}
