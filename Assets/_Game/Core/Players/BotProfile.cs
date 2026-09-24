using System;

namespace Game.Core.Players
{
    public enum BotDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>Stable identity and presentation choices for one bot participant.</summary>
    public readonly struct BotProfile
    {
        private const string PlayerIdPrefix = "bot:";

        public BotProfile(
            int number,
            string nickname,
            BotDifficulty difficulty = BotDifficulty.Normal,
            int appearanceSeed = 0)
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
            PlayerId = PlayerIdPrefix + number;
            Nickname = string.IsNullOrWhiteSpace(nickname)
                ? $"Bot {number}"
                : nickname.Trim();
            Difficulty = difficulty;
            AppearanceSeed = appearanceSeed;
        }

        public int Number { get; }
        public string PlayerId { get; }
        public string Nickname { get; }
        public BotDifficulty Difficulty { get; }
        public int AppearanceSeed { get; }

        public static bool IsBotPlayerId(string playerId) =>
            !string.IsNullOrWhiteSpace(playerId) &&
            playerId.StartsWith(PlayerIdPrefix, StringComparison.Ordinal);
    }
}
