using System;

namespace Game.Core.Flow
{
    public enum ClientVersionVerdict
    {
        /// <summary>This build is the one on the download page.</summary>
        Current,

        /// <summary>A different build is on the download page.</summary>
        Outdated,

        /// <summary>
        /// Nothing to compare: an editor run, a build without a revision, or no
        /// answer from the release host.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Whether this build is the one players are meant to be on
    /// (S15P21D205-1109).
    /// </summary>
    /// <remarks>
    /// A release build carries its full commit SHA as <c>Application.version</c>
    /// (ClientBuild), and the Photon AppVersion is built from it. A client on any
    /// other SHA sits in a matchmaking partition of its own and can neither see
    /// nor join the released rooms - so "different" is all this needs to know.
    /// Which of the two is newer does not matter to a player who cannot play.
    /// </remarks>
    public static class ClientVersionCheck
    {
        private const int RevisionLength = 40;

        public static ClientVersionVerdict Compare(string local, string released)
        {
            // Unknown on either side rather than Outdated. The editor reports
            // 0.1.0 and a failed request reports nothing; blocking either would
            // put the update notice in front of someone who cannot update.
            if (!IsRevision(local) || !IsRevision(released))
            {
                return ClientVersionVerdict.Unknown;
            }

            return string.Equals(local, released, StringComparison.OrdinalIgnoreCase)
                ? ClientVersionVerdict.Current
                : ClientVersionVerdict.Outdated;
        }

        /// <summary>True for a full 40-character commit SHA.</summary>
        public static bool IsRevision(string value)
        {
            if (value == null || value.Length != RevisionLength)
            {
                return false;
            }

            foreach (var c in value)
            {
                var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
