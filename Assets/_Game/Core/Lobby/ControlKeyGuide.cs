using System;
using System.Collections.Generic;

namespace Game.Core.Lobby
{
    public readonly struct ControlKeyBinding : IEquatable<ControlKeyBinding>
    {
        public ControlKeyBinding(string action, string keyLabel, string inputActionPath = null)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Action is required.", nameof(action));
            }

            if (string.IsNullOrWhiteSpace(keyLabel))
            {
                throw new ArgumentException("Key label is required.", nameof(keyLabel));
            }

            Action = action.Trim();
            KeyLabel = keyLabel.Trim();
            InputActionPath = string.IsNullOrWhiteSpace(inputActionPath)
                ? string.Empty
                : inputActionPath.Trim();
        }

        public string Action { get; }
        public string KeyLabel { get; }
        public string InputActionPath { get; }

        public bool Equals(ControlKeyBinding other) =>
            string.Equals(Action, other.Action, StringComparison.Ordinal) &&
            string.Equals(KeyLabel, other.KeyLabel, StringComparison.Ordinal) &&
            string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ControlKeyBinding other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Action, KeyLabel, InputActionPath);
    }

    public static class ControlKeyGuide
    {
        private static readonly ControlKeyBinding[] DefaultBindings =
        {
            new("이동", "W A S D", "Player/Move"),
            new("들기 / 놓기", "F", "Player/Interact"),
            new("분쇄기 상호작용", "F", "Player/Interact"),
            new("던지기", "마우스 좌클릭", "Player/Attack"),
            new("배치 모드 ON/OFF", "마우스 우클릭", "UI/RightClick"),
            new("배치 확정", "마우스 좌클릭", "Player/Attack"),
            new("배치 왼쪽 회전", "Q", "Player/Previous"),
            new("배치 오른쪽 회전", "E", "Player/Next"),
            new("배치 위아래", "마우스 스크롤", "UI/ScrollWheel"),
        };

        public static IReadOnlyList<ControlKeyBinding> Bindings => DefaultBindings;
    }
}
