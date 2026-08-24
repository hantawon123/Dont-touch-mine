using System;
using Game.SOAP.Config;

namespace Game.Server.Players
{
    public enum HitResult
    {
        Ignored,
        Registered,
        Stunned
    }

    public sealed class PlayerInteractionSystem
    {
        private readonly MatchRulesSO rules;
        private readonly int[] hitCounts = new int[MatchRulesSO.PlayerCount];
        private readonly int[] remainingDestructionUses = new int[MatchRulesSO.PlayerCount];
        private readonly double[] stunnedUntil = new double[MatchRulesSO.PlayerCount];
        private readonly double[] invulnerableUntil = new double[MatchRulesSO.PlayerCount];

        public PlayerInteractionSystem(MatchRulesSO rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));

            for (var playerIndex = 0; playerIndex < remainingDestructionUses.Length; playerIndex++)
            {
                remainingDestructionUses[playerIndex] = rules.DestructionUsesPerPlayer;
            }
        }

        public int GetHitCount(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return hitCounts[playerIndex];
        }

        public int GetRemainingDestructionUses(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return remainingDestructionUses[playerIndex];
        }

        public bool TryUseDestruction(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            if (remainingDestructionUses[playerIndex] == 0)
            {
                return false;
            }

            remainingDestructionUses[playerIndex]--;
            return true;
        }

        public HitResult RegisterHit(int playerIndex, double now)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(now);

            if (now < stunnedUntil[playerIndex] || now < invulnerableUntil[playerIndex])
            {
                return HitResult.Ignored;
            }

            hitCounts[playerIndex]++;
            if (hitCounts[playerIndex] < rules.HitsRequiredToStun)
            {
                return HitResult.Registered;
            }

            hitCounts[playerIndex] = 0;
            stunnedUntil[playerIndex] = now + rules.StunDurationSeconds;
            invulnerableUntil[playerIndex] =
                stunnedUntil[playerIndex] + rules.InvulnerabilityDurationSeconds;
            return HitResult.Stunned;
        }

        public bool IsStunned(int playerIndex, double now)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(now);
            return now < stunnedUntil[playerIndex];
        }

        public bool IsInvulnerable(int playerIndex, double now)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(now);
            return now >= stunnedUntil[playerIndex] && now < invulnerableUntil[playerIndex];
        }

        private static void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= MatchRulesSO.PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }
        }

        private static void ValidateTime(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }
        }
    }
}
