using System;

namespace Game.Core.Players
{
    /// <summary>
    /// Stable identity for a server-controlled NPC that joins only the searching
    /// world. It is deliberately not a room or match participant identity.
    /// </summary>
    public readonly struct MatchNpcBotProfile
    {
        private const string IdPrefix = "npc:";

        public MatchNpcBotProfile(
            int number,
            string displayName,
            BotDifficulty difficulty = BotDifficulty.Normal)
        {
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            }

            Number = number;
            NpcId = IdPrefix + number;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? $"NPC Bot {number}"
                : displayName.Trim();
            Difficulty = difficulty;
        }

        public int Number { get; }
        public string NpcId { get; }
        public string DisplayName { get; }
        public BotDifficulty Difficulty { get; }

        public static bool IsNpcId(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith(IdPrefix, StringComparison.Ordinal);
    }
}
