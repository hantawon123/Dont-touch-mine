namespace Game.Core.Settings
{
    public enum ControlSettingsError
    {
        None,
        UnknownAction,
        InvalidPath,
        DuplicatePath
    }

    public enum ControlAction
    {
        MoveForward,
        MoveBack,
        MoveLeft,
        MoveRight,
        Pickup,
        Drop,
        Throw,
        Place,
        InteractDevice,
        RotateYawLeft,
        RotateYawRight,
        RotatePitch,
        Jump,
        Sprint,
        ToggleView,
        Crouch,
        Prone,
        Attack
    }

    public readonly struct ControlBindingRow
    {
        public ControlBindingRow(ControlAction action, string label)
        {
            Action = action;
            Label = label;
        }

        public ControlAction Action { get; }

        public string Label { get; }
    }
}
