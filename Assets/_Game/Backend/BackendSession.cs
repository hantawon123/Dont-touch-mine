using System;

namespace Game.Backend
{
    /// <summary>
    /// Who this machine is to the backend.
    /// </summary>
    /// <remarks>
    /// Holds three values because they are not the same kind of thing. The
    /// user id identifies and is public; the device id authenticates and is not;
    /// the account token proves the user id is ours and rides with it. Keeping
    /// them in one place with different visibility is what stops the private
    /// ones from travelling where the public one is expected.
    /// </remarks>
    public sealed class BackendSession
    {
        public BackendSession(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device id is required.", nameof(deviceId));
            }

            DeviceId = deviceId.Trim();
        }

        /// <summary>
        /// This account's credential.
        /// </summary>
        /// <remarks>
        /// Internal on purpose. Anyone who has it can be this player, it is never
        /// in a server response, and the two ways it leaks are a log line and a
        /// query string. Only <see cref="BackendClient"/> reads it, and only to
        /// put it in a header.
        /// </remarks>
        internal string DeviceId { get; }

        /// <summary>
        /// The public identifier issued by the server, or null before signing in.
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// The server's signature over <see cref="UserId"/>, or null when the
        /// server issued none.
        /// </summary>
        /// <remarks>
        /// Sent beside the user id on every identified call, because the user
        /// id alone is public and anyone who knows another player's could
        /// otherwise act as them. A server without a signing secret issues no
        /// token and checks none, so null here is a working state, not an error.
        /// Internal for the same reason as <see cref="DeviceId"/>: it belongs
        /// in a header and nowhere else.
        /// </remarks>
        internal string AccountToken { get; private set; }

        public bool SignedIn => !string.IsNullOrEmpty(UserId);

        /// <remarks>
        /// Not persisted. Issuing an account is idempotent for a given device, so
        /// asking the server on each launch is both simpler than a cache and
        /// correct after the account is renamed or deleted elsewhere.
        /// </remarks>
        public void Adopt(string userId, string accountToken = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            UserId = userId.Trim();
            AccountToken = string.IsNullOrWhiteSpace(accountToken) ? null : accountToken.Trim();
        }

        /// <summary>Forgets the account, after it is deleted.</summary>
        public void Clear()
        {
            UserId = null;
            AccountToken = null;
        }
    }
}
