using Game.Core.Settings;

namespace Game.Client.Controls
{
    public static class ControlBindingDisplay
    {
        public static string ToLabel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "-";
            }

            if (Contains(path, "leftButton"))
            {
                return "마우스 좌클릭";
            }

            if (Contains(path, "rightButton"))
            {
                return "마우스 우클릭";
            }

            if (Contains(path, "middleButton"))
            {
                return "마우스 휠클릭";
            }

            if (Contains(path, "scroll"))
            {
                return "마우스 스크롤";
            }

            if (Contains(path, "leftShift") || path.EndsWith("/shift"))
            {
                return "Shift";
            }

            if (Contains(path, "rightShift"))
            {
                return "Right Shift";
            }

            if (Contains(path, "leftCtrl") || Contains(path, "leftControl"))
            {
                return "Ctrl";
            }

            if (Contains(path, "rightCtrl") || Contains(path, "rightControl"))
            {
                return "Right Ctrl";
            }

            if (Contains(path, "leftAlt"))
            {
                return "Alt";
            }

            if (Contains(path, "space"))
            {
                return "Space";
            }

            if (Contains(path, "escape"))
            {
                return "Esc";
            }

            if (Contains(path, "enter") || Contains(path, "return"))
            {
                return "Enter";
            }

            if (Contains(path, "tab"))
            {
                return "Tab";
            }

            var slash = path.LastIndexOf('/');
            var name = slash >= 0 && slash < path.Length - 1
                ? path.Substring(slash + 1)
                : path;
            if (name.Length == 1)
            {
                return name.ToUpperInvariant();
            }

            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static bool Contains(string path, string token)
        {
            return path.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
