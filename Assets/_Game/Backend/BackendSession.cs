using System;

namespace Game.Backend
{
    /// <summary>
    /// Who this machine is to the backend.
    /// </summary>
    /// <remarks>
    /// Holds both identifiers because they are not the same kind of thing. The
    /// user id identifies and is public; the device id authenticates and is not.
    /// Keeping them in one place with different visibility is what stops the
    /// second from travelling where the first is expected.
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

        public bool SignedIn => !string.IsNullOrEmpty(UserId);

        /// <remarks>
        /// Not persisted. Issuing an account is idempotent for a given device, so
        /// asking the server on each launch is both simpler than a cache and
        /// correct after the account is renamed or deleted elsewhere.
        /// </remarks>
        public void Adopt(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            UserId = userId.Trim();
        }

        /// <summary>Forgets the account, after it is deleted.</summary>
        public void Clear()
        {
            UserId = null;
        }
    }
}
