using System;

namespace Game.Core.Settings
{
    public sealed class ControlSettingsState
    {
        public static readonly ControlBindingRow[] Rows =
        {
            new ControlBindingRow(ControlAction.MoveForward, "이동 (앞)"),
            new ControlBindingRow(ControlAction.MoveBack, "이동 (뒤)"),
            new ControlBindingRow(ControlAction.MoveLeft, "이동 (왼쪽)"),
            new ControlBindingRow(ControlAction.MoveRight, "이동 (오른쪽)"),
            new ControlBindingRow(ControlAction.Pickup, "들기"),
            new ControlBindingRow(ControlAction.Drop, "놓기"),
            new ControlBindingRow(ControlAction.Throw, "던지기"),
            new ControlBindingRow(ControlAction.Place, "배치"),
            new ControlBindingRow(ControlAction.InteractDevice, "파괴 장치 상호작용"),
            new ControlBindingRow(ControlAction.RotateYawLeft, "가로축 회전 (왼쪽)"),
            new ControlBindingRow(ControlAction.RotateYawRight, "가로축 회전 (오른쪽)"),
            new ControlBindingRow(ControlAction.RotatePitch, "세로축 회전"),
            new ControlBindingRow(ControlAction.Jump, "점프"),
            new ControlBindingRow(ControlAction.Sprint, "달리기"),
            new ControlBindingRow(ControlAction.ToggleView, "시점 변경"),
            new ControlBindingRow(ControlAction.Crouch, "앉기"),
            new ControlBindingRow(ControlAction.Prone, "엎드리기"),
            new ControlBindingRow(ControlAction.Attack, "공격하기")
        };

        private readonly string[] paths;

        public ControlSettingsState()
        {
            var count = Enum.GetValues(typeof(ControlAction)).Length;
            paths = new string[count];
            for (var index = 0; index < count; index++)
            {
                paths[index] = GetDefaultPath((ControlAction)index);
            }
        }

        public ControlSettingsState(ControlSettingsState source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            paths = (string[])source.paths.Clone();
        }

        public string GetPath(ControlAction action)
        {
            if (!Enum.IsDefined(typeof(ControlAction), action))
            {
                return string.Empty;
            }

            return paths[(int)action];
        }

        public bool TrySetPath(ControlAction action, string path, out ControlSettingsError error)
        {
            if (!Enum.IsDefined(typeof(ControlAction), action))
            {
                error = ControlSettingsError.UnknownAction;
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                error = ControlSettingsError.InvalidPath;
                return false;
            }

            if (TryFindConflict(action, path, out _))
            {
                error = ControlSettingsError.DuplicatePath;
                return false;
            }

            paths[(int)action] = path.Trim();
            error = ControlSettingsError.None;
            return true;
        }

        public bool TryFindConflict(ControlAction action, string path, out ControlAction occupiedBy)
        {
            occupiedBy = default;
            if (!Enum.IsDefined(typeof(ControlAction), action) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var group = ShareGroup(action);
            var incoming = NormalizeBinding(path);
            var values = (ControlAction[])Enum.GetValues(typeof(ControlAction));
            for (var index = 0; index < values.Length; index++)
            {
                var other = values[index];
                if (other == action || ShareGroup(other) == group)
                {
                    continue;
                }

                if (string.Equals(NormalizeBinding(GetPath(other)), incoming, StringComparison.Ordinal))
                {
                    occupiedBy = other;
                    return true;
                }
            }

            return false;
        }

        public static string RowLabel(ControlAction action)
        {
            for (var index = 0; index < Rows.Length; index++)
            {
                if (Rows[index].Action == action)
                {
                    return Rows[index].Label;
                }
            }

            return string.Empty;
        }

        public static string GetDefaultPath(ControlAction action)
        {
            switch (action)
            {
                case ControlAction.MoveForward:
                    return "<Keyboard>/w";
                case ControlAction.MoveBack:
                    return "<Keyboard>/s";
                case ControlAction.MoveLeft:
                    return "<Keyboard>/a";
                case ControlAction.MoveRight:
                    return "<Keyboard>/d";
                case ControlAction.Place:
                    return "<Mouse>/rightButton";
                case ControlAction.RotateYawLeft:
                    return "<Keyboard>/q";
                case ControlAction.RotateYawRight:
                    return "<Keyboard>/e";
                case ControlAction.RotatePitch:
                    return "<Mouse>/scroll/y";
                case ControlAction.Jump:
                    return "<Keyboard>/space";
                case ControlAction.Sprint:
                    return "<Keyboard>/leftShift";
                case ControlAction.ToggleView:
                    return "<Keyboard>/v";
                case ControlAction.Crouch:
                    return "<Keyboard>/c";
                case ControlAction.Prone:
                    return "<Keyboard>/z";
                case ControlAction.Throw:
                case ControlAction.Attack:
                    return "<Mouse>/leftButton";
                default:
                    return "<Keyboard>/f";
            }
        }

        public static ControlAction ShareGroup(ControlAction action)
        {
            switch (action)
            {
                case ControlAction.Pickup:
                case ControlAction.Drop:
                case ControlAction.InteractDevice:
                    return ControlAction.Pickup;
                case ControlAction.Throw:
                case ControlAction.Attack:
                    return ControlAction.Throw;
                default:
                    return action;
            }
        }

        private static string NormalizeBinding(string path)
        {
            return path.Trim().ToLowerInvariant();
        }
    }
}
