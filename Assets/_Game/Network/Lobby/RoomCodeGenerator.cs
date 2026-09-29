using System;
using System.Text;
using Game.Core.Rooms;

namespace Game.Network.Lobby
{
    /// <summary>
    /// Draws short room codes that players read aloud and type by hand.
    /// </summary>
    /// <remarks>
    /// The shape of a code lives in <see cref="RoomCodeFormat"/>, which the UI
    /// can reach as well; this type only issues them.
    /// </remarks>
    public sealed class RoomCodeGenerator
    {
        public const int CodeLength = RoomCodeFormat.CodeLength;

        private readonly Random _random = new Random();
        private readonly StringBuilder _builder = new StringBuilder(CodeLength);

        public static int AlphabetSize => RoomCodeFormat.AlphabetSize;

        public string Next()
        {
            _builder.Clear();

            for (var i = 0; i < CodeLength; i++)
            {
                _builder.Append(
                    RoomCodeFormat.Alphabet[_random.Next(RoomCodeFormat.Alphabet.Length)]);
            }

            return _builder.ToString();
        }

        /// <inheritdoc cref="RoomCodeFormat.Normalize"/>
        public static string Normalize(string roomCode) => RoomCodeFormat.Normalize(roomCode);

        /// <inheritdoc cref="RoomCodeFormat.IsWellFormed"/>
        public static bool IsWellFormed(string normalizedCode) =>
            RoomCodeFormat.IsWellFormed(normalizedCode);
    }
}
