using System;

namespace Game.Core.Home
{
    public enum PlayerProfileError
    {
        None,
        NicknameRequired,
        InvalidLevel
    }

    public sealed class PlayerProfile
    {
        public PlayerProfile(string nickname, int level)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            Nickname = nickname.Trim();
            Level = level;
        }

        public string Nickname { get; private set; }
        public int Level { get; private set; }

        public event Action<PlayerProfile> Changed;

        public bool TryChangeNickname(
            string nickname,
            out PlayerProfileError error)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                error = PlayerProfileError.NicknameRequired;
                return false;
            }

            Nickname = nickname.Trim();
            error = PlayerProfileError.None;
            Changed?.Invoke(this);
            return true;
        }

        public bool TryUpdateLevel(int level, out PlayerProfileError error)
        {
            if (level < 1)
            {
                error = PlayerProfileError.InvalidLevel;
                return false;
            }

            Level = level;
            error = PlayerProfileError.None;
            Changed?.Invoke(this);
            return true;
        }
    }
}
