using System;
using System.Text;

namespace Game.Server.Lobby
{
    /// <summary>
    /// Draws short room codes that players read aloud and type by hand.
    /// </summary>
    public sealed class RoomCodeGenerator
    {
        /// <summary>
        /// Digits plus letters, minus the four that get misread: I and L look
        /// like 1, O looks like 0, and U is heard as You. Dropping the letters
        /// rather than the digits keeps every digit usable.
        /// </summary>
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public const int CodeLength = 6;

        private readonly Random _random = new Random();
        private readonly StringBuilder _builder = new StringBuilder(CodeLength);

        /// <summary>32^6, roughly 1.07 billion combinations.</summary>
        public static int AlphabetSize => Alphabet.Length;

        public string Next()
        {
            _builder.Clear();

            for (var i = 0; i < CodeLength; i++)
            {
                _builder.Append(Alphabet[_random.Next(Alphabet.Length)]);
            }

            return _builder.ToString();
        }

        /// <summary>
        /// Puts a typed code into the form codes are issued in, so case and
        /// stray spaces do not turn a correct code into "room not found".
        /// </summary>
        public static string Normalize(string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode)
                ? string.Empty
                : roomCode.Trim().ToUpperInvariant();
        }
    }
}
