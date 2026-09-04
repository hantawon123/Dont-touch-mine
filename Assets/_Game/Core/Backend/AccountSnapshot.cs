using System;

namespace Game.Core.Backend
{
    /// <summary>
    /// The account this machine plays as, as the server last described it.
    /// </summary>
    /// <remarks>
    /// The device identifier that issued the account is not here. It is this
    /// account's credential, it never appears in a server response, and putting
    /// it in a type that presentation can reach is how it ends up in a log.
    /// </remarks>
    public readonly struct AccountSnapshot
    {
        public AccountSnapshot(string userId, string nickname, bool nicknameSet)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("Nickname is required.", nameof(nickname));
            }

            UserId = userId.Trim();
            Nickname = nickname.Trim();
            NicknameSet = nicknameSet;
        }

        /// <summary>
        /// The public identifier every later request identifies this player by,
        /// and the same value Photon uses as its user id.
        /// </summary>
        public string UserId { get; }

        public string Nickname { get; }

        /// <summary>
        /// False while the nickname is the temporary one the server invented.
        /// </summary>
        /// <remarks>
        /// This is what decides whether to open the nickname screen on a first
        /// run. Comparing the name itself against a pattern would break the
        /// moment the server changes how it builds temporary names.
        /// </remarks>
        public bool NicknameSet { get; }
    }
}
