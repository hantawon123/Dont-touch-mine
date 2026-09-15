using System;

namespace Game.Core.Settings
{
    /// <summary>
    /// Where in <c>InputSystem_Actions</c> an action's key is written.
    /// </summary>
    /// <param name="ActionName">
    /// The action's name inside the <c>Player</c> map.
    /// </param>
    /// <param name="PartName">
    /// The composite part the key belongs to — <c>up</c>, <c>negative</c> and
    /// so on — or null when the action's key is a binding of its own.
    /// </param>
    public readonly struct ControlBindingTarget
    {
        public ControlBindingTarget(string actionName, string partName = null)
        {
            ActionName = actionName;
            PartName = partName;
        }

        public string ActionName { get; }

        public string PartName { get; }

        public bool IsCompositePart => !string.IsNullOrEmpty(PartName);

        public override string ToString() =>
            IsCompositePart ? $"{ActionName}/{PartName}" : ActionName;
    }

    /// <summary>
    /// What joins the 컨트롤 tab to <c>InputSystem_Actions</c>: which binding
    /// each row writes to, and how a saved key code is spelled as a control
    /// path.
    /// </summary>
    /// <remarks>
    /// Strings on both sides, so this is ordinary C# and can be read without
    /// the Input System. The layer that actually calls
    /// <c>ApplyBindingOverride</c> is <c>InputSystemControlBindingApplier</c>,
    /// which is the only part that needs Unity.
    /// <para>
    /// Every action in the <c>Player</c> map that a player may move is here.
    /// <c>Look</c>, <c>Previous</c> and <c>Next</c> are not, because the
    /// screen has no row for them.
    /// </para>
    /// </remarks>
    public static class ControlBindingMap
    {
        /// <summary>The map every rebindable action lives in.</summary>
        public const string PlayerMap = "Player";

        /// <summary>
        /// The binding group the screen writes to.
        /// </summary>
        /// <remarks>
        /// Several actions carry gamepad, touch, joystick and XR bindings
        /// beside the one a keyboard and mouse use — <c>Attack</c> has four —
        /// and none of those are the player's to move from this screen. Naming
        /// the group is what keeps an override off them.
        /// </remarks>
        public const string KeyboardMouseGroup = "Keyboard&Mouse";

        private const string KeyboardPrefix = "<Keyboard>/";
        private const string MousePrefix = "<Mouse>/";

        private static readonly (ControlAction Action, string Name, string Part)[] Targets =
        {
            (ControlAction.MicrophoneTalk, "PushToTalk", null),

            // Latches the microphone open rather than holding it, so it is a
            // second way to be heard rather than a different thing to bind.
            (ControlAction.VoiceToggle, "VoiceToggle", null),

            // The four directions are parts of one Dpad composite. Each has a
            // letter and an arrow key on it; the letter comes first and is the
            // one this screen shows, so the arrows stay as a second way in that
            // nobody has to know about.
            (ControlAction.MoveForward, "Move", "up"),
            (ControlAction.MoveLeft, "Move", "left"),
            (ControlAction.MoveBackward, "Move", "down"),
            (ControlAction.MoveRight, "Move", "right"),

            (ControlAction.Sprint, "Sprint", null),
            (ControlAction.Jump, "Jump", null),
            (ControlAction.Crouch, "Crouch", null),
            (ControlAction.Prone, "Prone", null),
            (ControlAction.ToggleView, "ToggleView", null),

            // One action, because the game reads one click three ways: a swing
            // with empty hands, a throw with something in them, a placement
            // while 배치모드 is on. See ControlSettings.
            (ControlAction.PrimaryAction, "Attack", null),

            (ControlAction.Interact, "Interact", null),
            (ControlAction.PlacementMode, "PlacementMode", null),
            (ControlAction.RotateLeft, "RotateObject", "negative"),
            (ControlAction.RotateRight, "RotateObject", "positive"),
            (ControlAction.RaiseObject, "AdjustHeight", "positive"),
            (ControlAction.LowerObject, "AdjustHeight", "negative"),
            (ControlAction.ToggleKeyGuide, "ToggleKeyGuide", null),
            (ControlAction.ToggleSpeaker, "VoiceListen", null)
        };

        /// <summary>
        /// Where <paramref name="action"/>'s key is written, or false for an
        /// action the input asset has nowhere to put.
        /// </summary>
        public static bool TryTarget(ControlAction action, out ControlBindingTarget target)
        {
            foreach (var entry in Targets)
            {
                if (entry.Action != action)
                {
                    continue;
                }

                target = new ControlBindingTarget(entry.Name, entry.Part);
                return true;
            }

            target = default;
            return false;
        }

        /// <summary>
        /// A saved key code as a control path, or the empty string for an
        /// action nobody has given a key to — which is what disables a binding.
        /// </summary>
        /// <remarks>
        /// The exact inverse of <c>UnityKeyCapture.CodeOf</c>, which is what
        /// wrote the code in the first place: keyboard codes are the Input
        /// System's own control names and only need the device in front, and
        /// the mouse's five have names of our own.
        /// </remarks>
        public static string PathOf(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return string.Empty;
            }

            switch (code)
            {
                case ControlCatalog.MouseLeft:
                    return MousePrefix + "leftButton";
                case ControlCatalog.MouseRight:
                    return MousePrefix + "rightButton";
                case ControlCatalog.MouseMiddle:
                    return MousePrefix + "middleButton";
                case ControlCatalog.ScrollUp:
                    return MousePrefix + "scroll/up";
                case ControlCatalog.ScrollDown:
                    return MousePrefix + "scroll/down";
                default:
                    return KeyboardPrefix + code;
            }
        }

        /// <summary>
        /// The code a path was made from. For proving the two directions agree;
        /// nothing at runtime reads a path back.
        /// </summary>
        public static bool TryCodeOf(string path, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(path))
            {
                code = ControlCatalog.Unbound;
                return true;
            }

            switch (path)
            {
                case MousePrefix + "leftButton":
                    code = ControlCatalog.MouseLeft;
                    return true;
                case MousePrefix + "rightButton":
                    code = ControlCatalog.MouseRight;
                    return true;
                case MousePrefix + "middleButton":
                    code = ControlCatalog.MouseMiddle;
                    return true;
                case MousePrefix + "scroll/up":
                    code = ControlCatalog.ScrollUp;
                    return true;
                case MousePrefix + "scroll/down":
                    code = ControlCatalog.ScrollDown;
                    return true;
            }

            if (path.StartsWith(KeyboardPrefix, StringComparison.Ordinal))
            {
                code = path.Substring(KeyboardPrefix.Length);
                return true;
            }

            return false;
        }
    }
}
