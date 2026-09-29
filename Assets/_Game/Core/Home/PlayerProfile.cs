using System;

namespace Game.Core.Home
{
    public enum PlayerProfileError
    {
        None,
        NicknameRequired,

        /// <summary>
        /// There was a name, but not one the rule allows.
        /// </summary>
        NicknameNotAllowed
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

        /// <summary>
        /// The account this player signed in as, or empty before sign-in and
        /// when sign-in failed.
        /// </summary>
        /// <remarks>
        /// Not saved with the profile. The account is asked for on every launch,
        /// which is also what keeps this right after the account is deleted or
        /// recreated elsewhere. It rides beside the nickname because it travels
        /// the same road into a room: in the connection token, then onto the
        /// character every peer sees, so the host can say whose actions it is
        /// reporting. Identification, not authentication — the device id never
        /// goes this way.
        /// </remarks>
        public string UserId { get; private set; } = string.Empty;

        /// <summary>
        /// Proves to Photon that this client is <see cref="UserId"/>. Empty when
        /// the server is not running Photon authentication.
        /// </summary>
        /// <remarks>
        /// Lives beside the id because the two are only useful together and
        /// always arrive together. The id on its own would let a suspended
        /// player type someone else's and connect.
        /// </remarks>
        public string PhotonToken { get; private set; } = string.Empty;

        /// <summary>Mirrors the account the server issued. Empty forgets it.</summary>
        public void AdoptUserId(string userId, string photonToken = null)
        {
            var next = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId.Trim();
            var nextToken = string.IsNullOrWhiteSpace(photonToken) ? string.Empty : photonToken.Trim();
            if (string.Equals(UserId, next, StringComparison.Ordinal)
                && string.Equals(PhotonToken, nextToken, StringComparison.Ordinal))
            {
                return;
            }

            UserId = next;
            PhotonToken = nextToken;
            Changed?.Invoke(this);
        }

        /// <summary>
        /// Whether this player has settled on a name. False means the server's
        /// temporary one is still in use and the one change is still available.
        /// </summary>
        /// <remarks>
        /// The server owns this — its account response carries
        /// <c>nicknameSet</c> — so this is the client's copy of that answer, not
        /// its own count. Until the account API is wired it only lasts as long
        /// as the process.
        /// </remarks>
        public bool NicknameSet { get; private set; }

        /// <summary>
        /// Takes the server's word for whether the name has been settled.
        /// </summary>
        public void MarkNicknameSet(bool settled)
        {
            if (NicknameSet == settled)
            {
                return;
            }

            NicknameSet = settled;
            Changed?.Invoke(this);
        }

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

            // The panel refuses a name the rule forbids long before it gets
            // here, and the server refuses it again afterwards. This is the
            // rule holding in the middle, for the callers that are not the
            // panel — the debug overlay is one.
            var trimmed = nickname.Trim();
            if (!NicknamePolicy.IsValid(trimmed))
            {
                error = PlayerProfileError.NicknameNotAllowed;
                return false;
            }

            Nickname = trimmed;

            // The one change is not spent here. Whether it has been is the
            // server's answer — the account response carries nicknameSet — and
            // this is also the setter the screen calls the moment it is asked,
            // before the server has agreed to anything. Spending it here would
            // take the only rename away from a player whose chosen name turned
            // out to be taken. Callers mirror the server's answer with
            // <see cref="MarkNicknameSet"/>.
            error = PlayerProfileError.None;
            Changed?.Invoke(this);
            return true;
        }
    }
}
