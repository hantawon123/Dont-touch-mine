using System;
using Game.Core.Lobby;
using Game.Core.Players;
using Game.SOAP.Config;
using VContainer;

namespace Game.Server.Players
{
    public sealed class PlayerInteractionSystem : IPlayerCombatRules
    {
        private readonly MatchRulesSO rules;
        private readonly int[] hitCounts;
        private readonly int[] remainingDestructionUses;
        private readonly double[] stunnedUntil;
        private readonly double[] invulnerableUntil;

        [Inject]
        public PlayerInteractionSystem(MatchRulesSO rules)
            : this(rules, MatchRulesSO.MaxPlayerCount)
        {
        }

        public PlayerInteractionSystem(MatchRulesSO rules, int playerCount)
            : this(rules, playerCount, rules?.DestructionUsesPerPlayer ?? 0)
        {
        }

        public PlayerInteractionSystem(
            MatchRulesSO rules,
            int playerCount,
            int destructionUsesPerPlayer)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MatchRulesSO.ValidatePlayerCount(playerCount);
            if (destructionUsesPerPlayer < PlaySettingsDraft.MinDestructionLimit ||
                destructionUsesPerPlayer > PlaySettingsDraft.MaxDestructionLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(destructionUsesPerPlayer));
            }

            hitCounts = new int[playerCount];
            remainingDestructionUses = new int[playerCount];
            stunnedUntil = new double[playerCount];
            invulnerableUntil = new double[playerCount];

            for (var playerIndex = 0; playerIndex < remainingDestructionUses.Length; playerIndex++)
            {
                remainingDestructionUses[playerIndex] = destructionUsesPerPlayer;
            }
        }

        public int PlayerCount => hitCounts.Length;

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

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= hitCounts.Length)
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
