using System;

namespace Game.Core.Home
{
    public enum PlayerProfileError
    {
        None,
        NicknameRequired
    }

    /// <summary>
    /// The name this player goes by, held in one place for the whole
    /// application.
    /// </summary>
    /// <remarks>
    /// The server owns this value: it is set from the account at sign-in and
    /// written back when the player renames themselves. Nothing is saved on this
    /// machine, because issuing an account is idempotent for a given device and
    /// returns the same name on every launch.
    /// <para>
    /// There is no level here. There was one, shown beside an experience bar,
    /// and neither the server nor the game had any notion of what raised it.
    /// </para>
    /// </remarks>
    public sealed class PlayerProfile
    {
        public PlayerProfile(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            Nickname = nickname.Trim();
        }

        public string Nickname { get; private set; }

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
    }
}
