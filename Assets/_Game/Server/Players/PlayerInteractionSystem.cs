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
        private readonly int hitsRequiredToStun;
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
            int destructionUsesPerPlayer,
            int? hitsRequiredToStun = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MatchRulesSO.ValidatePlayerCount(playerCount);
            if (destructionUsesPerPlayer != PlaySettingsDraft.UnlimitedDestructionLimit &&
                (destructionUsesPerPlayer < PlaySettingsDraft.MinDestructionLimit ||
                 destructionUsesPerPlayer > PlaySettingsDraft.MaxDestructionLimit))
            {
                throw new ArgumentOutOfRangeException(nameof(destructionUsesPerPlayer));
            }

            this.hitsRequiredToStun = hitsRequiredToStun ?? rules.HitsRequiredToStun;
            if (this.hitsRequiredToStun < MatchRuleSettings.MinStunHitCount ||
                this.hitsRequiredToStun > MatchRuleSettings.MaxStunHitCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hitsRequiredToStun));
            }

            hitCounts = new int[playerCount];
            remainingDestructionUses = new int[playerCount];
            stunnedUntil = new double[playerCount];
            invulnerableUntil = new double[playerCount];

            for (var playerIndex = 0; playerIndex < remainingDestructionUses.Length; playerIndex++)
            {
                remainingDestructionUses[playerIndex] = destructionUsesPerPlayer ==
                    PlaySettingsDraft.UnlimitedDestructionLimit
                    ? PlaySettingsDraft.UnlimitedDestructionUses
                    : destructionUsesPerPlayer;
            }
        }

        public int PlayerCount => hitCounts.Length;

        public double GetStunEndsAt(int playerIndex) => stunnedUntil[playerIndex];
        public double GetInvulnerableEndsAt(int playerIndex) => invulnerableUntil[playerIndex];

        internal void Restore(int playerIndex, int hits, int uses, double stunEnd, double invulnerableEnd)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(stunEnd);
            ValidateTime(invulnerableEnd);
            if (hits < 0 || hits >= hitsRequiredToStun || uses < 0 || invulnerableEnd < stunEnd)
                throw new ArgumentOutOfRangeException(nameof(hits));
            hitCounts[playerIndex] = hits;
            remainingDestructionUses[playerIndex] = uses;
            stunnedUntil[playerIndex] = stunEnd;
            invulnerableUntil[playerIndex] = invulnerableEnd;
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

            if (remainingDestructionUses[playerIndex] ==
                PlaySettingsDraft.UnlimitedDestructionUses)
            {
                return true;
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
            if (hitCounts[playerIndex] < hitsRequiredToStun)
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
