using System;
using Game.Core.Home;

namespace Game.Core.Settings
{
    /// <summary>
    /// The name this player is known by outside their own screen: their own,
    /// or the <see cref="Settings.Pseudonym"/> 스트리머 모드 gives them.
    /// </summary>
    /// <remarks>
    /// One answer, because two places need it and they must agree. The room
    /// browser writes a host's name into the session's properties as the room
    /// is made, and the room itself sends a name to the other players once
    /// everybody is in. A pseudonym worked out separately in each would show
    /// the room list one name and the room another, which reads as two people.
    /// <para>
    /// The pseudonym is made the first time it is wanted and held until
    /// <see cref="ForgetPseudonym"/>, which is what a visit ending looks like.
    /// So the room can talk to somebody by the name it saw a moment ago, and
    /// the next visit is a different name.
    /// </para>
    /// </remarks>
    public sealed class PublishedPlayerName
    {
        private readonly InterfaceSettingsSystem settings;
        private readonly PlayerProfile profile;
        private string pseudonym;

        public PublishedPlayerName(InterfaceSettingsSystem settings, PlayerProfile profile)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>Whether this player is going by a made-up name.</summary>
        public bool IsPseudonymous => settings.Current.IsOn(InterfaceOption.StreamerMode);

        /// <summary>
        /// What to show anybody else: the pseudonym while 스트리머 모드 is on,
        /// and the player's own name otherwise.
        /// </summary>
        public string Current => IsPseudonymous ? Pseudonym : profile.Nickname;

        /// <summary>
        /// This visit's made-up name, whether or not it is being used. Asked
        /// for directly by the layer that sends it to the other players, which
        /// says separately that it is a pseudonym.
        /// </summary>
        public string Pseudonym
        {
            get
            {
                if (string.IsNullOrEmpty(pseudonym))
                {
                    // The account id goes in beside a fresh seed so that two
                    // players arriving at the same moment are unlikely to be
                    // given the same name. The fresh half is what makes the
                    // name change between visits; the account id alone would
                    // give the same person the same name for ever, which is a
                    // name they could be followed by.
                    var seed = Guid.NewGuid().GetHashCode();
                    var userId = profile.UserId;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        seed ^= userId.GetHashCode();
                    }

                    pseudonym = Settings.Pseudonym.From(seed);
                }

                return pseudonym;
            }
        }

        /// <summary>
        /// Drops this visit's name, so the next one is somebody else. Called
        /// when the player is no longer in a room.
        /// </summary>
        public void ForgetPseudonym() => pseudonym = null;
    }
}
