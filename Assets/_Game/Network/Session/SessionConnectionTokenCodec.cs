using System;
using System.Text;

namespace Game.Network.Session
{
    /// <summary>
    /// Packs what a joiner hands the host into one connection token: the
    /// password it presents, the name it asks to be shown as, and the backend
    /// account it signed in as.
    /// </summary>
    /// <remarks>
    /// Every field is length-prefixed or last, never separated by a character:
    /// a password may contain anything, so any separator could occur inside one
    /// and split it in the wrong place.
    /// <para>
    /// Two layouts are readable. Version 1 carried password and nickname only;
    /// version 2 adds the account id. Reading both matters during a rollout —
    /// a host on the new build must still admit a joiner on the old one, and
    /// the only thing it loses is the account id, which reads as empty.
    /// </para>
    /// <pre>
    /// v1: [1][pwLen:2][password][nickname...]
    /// v2: [2][pwLen:2][password][nickLen:2][nickname][userId...]
    /// </pre>
    /// </remarks>
    internal static class SessionConnectionTokenCodec
    {
        private const byte VersionWithoutUserId = 1;
        private const byte Version = 2;
        private const int HeaderSize = 3;
        private const int LengthSize = 2;

        /// <summary>Kept for callers that have no account to present.</summary>
        public static byte[] Encode(string password, string nickname) =>
            Encode(password, nickname, null);

        public static byte[] Encode(string password, string nickname, string userId)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
            var nicknameBytes = Encoding.UTF8.GetBytes(nickname ?? string.Empty);
            var userIdBytes = Encoding.UTF8.GetBytes(userId ?? string.Empty);

            if (passwordBytes.Length == 0 && nicknameBytes.Length == 0 && userIdBytes.Length == 0)
            {
                return null;
            }

            if (passwordBytes.Length > ushort.MaxValue || nicknameBytes.Length > ushort.MaxValue)
            {
                return null;
            }

            var token = new byte[HeaderSize + passwordBytes.Length + LengthSize + nicknameBytes.Length + userIdBytes.Length];
            var offset = 0;

            token[offset++] = Version;
            token[offset++] = (byte)(passwordBytes.Length >> 8);
            token[offset++] = (byte)(passwordBytes.Length & 0xFF);
            passwordBytes.CopyTo(token, offset);
            offset += passwordBytes.Length;

            token[offset++] = (byte)(nicknameBytes.Length >> 8);
            token[offset++] = (byte)(nicknameBytes.Length & 0xFF);
            nicknameBytes.CopyTo(token, offset);
            offset += nicknameBytes.Length;

            userIdBytes.CopyTo(token, offset);
            return token;
        }

        /// <summary>Kept for callers that only need the two original fields.</summary>
        public static void Decode(byte[] token, out string password, out string nickname) =>
            Decode(token, out password, out nickname, out _);

        /// <summary>
        /// Reads either layout. A token that is malformed, or of a version this
        /// build does not know, reads as three empty strings rather than
        /// throwing: the host decides what to do with an empty password.
        /// </summary>
        public static void Decode(
            byte[] token,
            out string password,
            out string nickname,
            out string userId)
        {
            password = string.Empty;
            nickname = string.Empty;
            userId = string.Empty;

            if (token == null || token.Length < HeaderSize)
            {
                return;
            }

            var version = token[0];
            if (version != VersionWithoutUserId && version != Version)
            {
                return;
            }

            var passwordLength = (token[1] << 8) | token[2];
            var offset = HeaderSize;
            if (offset + passwordLength > token.Length)
            {
                return;
            }

            password = Encoding.UTF8.GetString(token, offset, passwordLength);
            offset += passwordLength;

            if (version == VersionWithoutUserId)
            {
                // Everything after the password is the name. There is no account.
                nickname = Encoding.UTF8.GetString(token, offset, token.Length - offset);
                return;
            }

            if (offset + LengthSize > token.Length)
            {
                return;
            }

            var nicknameLength = (token[offset] << 8) | token[offset + 1];
            offset += LengthSize;
            if (offset + nicknameLength > token.Length)
            {
                return;
            }

            nickname = Encoding.UTF8.GetString(token, offset, nicknameLength);
            offset += nicknameLength;

            userId = Encoding.UTF8.GetString(token, offset, token.Length - offset);
        }

        public static bool MatchesPassword(string presented, string expected) =>
            !string.IsNullOrEmpty(expected) &&
            string.Equals(presented, expected, StringComparison.Ordinal);
    }
}
