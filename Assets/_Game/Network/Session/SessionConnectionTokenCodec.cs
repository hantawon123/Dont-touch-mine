using System;
using System.Text;

namespace Game.Network.Session
{
    /// <summary>
    /// Encodes the password and nickname presented before a peer joins a session.
    /// The wire format is versioned and length-prefixed so passwords may contain
    /// any character without being split by a separator.
    /// </summary>
    internal static class SessionConnectionTokenCodec
    {
        private const byte Version = 1;
        private const int HeaderSize = 3;

        public static byte[] Encode(string password, string nickname)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
            var nicknameBytes = Encoding.UTF8.GetBytes(nickname ?? string.Empty);

            if (passwordBytes.Length == 0 && nicknameBytes.Length == 0)
            {
                return null;
            }

            if (passwordBytes.Length > ushort.MaxValue)
            {
                return null;
            }

            var token = new byte[HeaderSize + passwordBytes.Length + nicknameBytes.Length];
            token[0] = Version;
            token[1] = (byte)(passwordBytes.Length >> 8);
            token[2] = (byte)(passwordBytes.Length & 0xFF);

            passwordBytes.CopyTo(token, HeaderSize);
            nicknameBytes.CopyTo(token, HeaderSize + passwordBytes.Length);

            return token;
        }

        /// <summary>
        /// Invalid peer input is decoded as empty values instead of throwing.
        /// </summary>
        public static void Decode(
            byte[] token,
            out string password,
            out string nickname)
        {
            password = string.Empty;
            nickname = string.Empty;

            if (token == null || token.Length < HeaderSize || token[0] != Version)
            {
                return;
            }

            var passwordLength = (token[1] << 8) | token[2];
            if (HeaderSize + passwordLength > token.Length)
            {
                return;
            }

            password = Encoding.UTF8.GetString(token, HeaderSize, passwordLength);

            var nicknameStart = HeaderSize + passwordLength;
            nickname = Encoding.UTF8.GetString(
                token,
                nicknameStart,
                token.Length - nicknameStart);
        }

        public static bool MatchesPassword(string presented, string expected) =>
            !string.IsNullOrEmpty(expected) &&
            string.Equals(presented, expected, StringComparison.Ordinal);
    }
}
