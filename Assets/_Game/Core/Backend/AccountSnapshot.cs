using System;
using Game.Core.Players;

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
        public AccountSnapshot(
            string userId,
            string nickname,
            bool nicknameSet,
            bool searchable,
            bool appearanceSet = false,
            AvatarAppearance appearance = default,
            string photonToken = null)
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
            Searchable = searchable;
            AppearanceSet = appearanceSet;
            Appearance = appearance;
            PhotonToken = photonToken;
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

        /// <summary>
        /// Whether this account has ever applied an appearance.
        /// </summary>
        /// <remarks>
        /// The server sends <c>appearance</c> as null until someone applies
        /// one, and <see cref="UnityEngine.JsonUtility"/> reads a null object
        /// as an object with empty fields rather than as null. This is the flag
        /// that tells the two apart — the same job <see cref="NicknameSet"/>
        /// does for the invented name.
        /// </remarks>
        public bool AppearanceSet { get; }

        /// <summary>
        /// What this account last applied. Meaningless unless
        /// <see cref="AppearanceSet"/> is true.
        /// </summary>
        /// <remarks>
        /// The part ids are this client's own: the server stores whatever
        /// strings it is given and validates only their shape. An id the
        /// catalogue no longer has is a part to fall back on, not an error —
        /// otherwise every catalogue edit breaks the players who were wearing
        /// what changed.
        /// </remarks>
        public AvatarAppearance Appearance { get; }

        /// <summary>
        /// Whether this player turns up when someone searches nicknames.
        /// </summary>
        /// <remarks>
        /// Only the search. A friend already on the list still shows there, a
        /// pending request still names its sender, and anyone holding this
        /// account's user id can still ask to be friends. Hiding the name
        /// everywhere would break the screens of people who already know each
        /// other.
        /// </remarks>
        public bool Searchable { get; }

        /// <summary>
        /// Proves to Photon that this client is <see cref="UserId"/>. Null when
        /// the server is not running Photon authentication.
        /// </summary>
        /// <remarks>
        /// Not stored on this machine. Every launch reads the account again, so
        /// a token that stopped working (the server rotated its secret) is
        /// replaced without the player doing anything.
        /// </remarks>
        public string PhotonToken { get; }
    }
}
