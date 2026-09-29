using System;
using System.Collections.Generic;

namespace Game.Core.Match
{
    /// <summary>
    /// 엔딩 장면에서 누가 어느 자리에 서는지 정한다.
    /// 승자(탈출)는 철창 앞 자리, 패자(체포)는 철창 안 자리를 플레이어 번호 순으로 받는다.
    /// </summary>
    public readonly struct EndingStagePlacement
    {
        public EndingStagePlacement(string playerId, int playerIndex, bool escaped, int slot)
        {
            PlayerId = playerId;
            PlayerIndex = playerIndex;
            Escaped = escaped;
            Slot = slot;
        }

        public string PlayerId { get; }
        public int PlayerIndex { get; }
        /// <summary>true면 탈출(승자) 자리, false면 체포(패자) 자리.</summary>
        public bool Escaped { get; }
        /// <summary>해당 그룹 안에서의 자리 번호(0부터).</summary>
        public int Slot { get; }
    }

    public static class EndingStageLayout
    {
        /// <summary>
        /// 참가자를 승자 명단으로 나눠 자리를 배정한다. 자리가 부족하면 마지막 자리를 재사용한다.
        /// </summary>
        public static IReadOnlyList<EndingStagePlacement> Assign(
            IReadOnlyList<MatchParticipant> participants,
            IReadOnlyList<int> winnerPlayerIndices,
            int escapeSlots,
            int arrestSlots)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (escapeSlots <= 0) throw new ArgumentOutOfRangeException(nameof(escapeSlots));
            if (arrestSlots <= 0) throw new ArgumentOutOfRangeException(nameof(arrestSlots));

            var winners = new HashSet<int>();
            if (winnerPlayerIndices != null)
            {
                foreach (var index in winnerPlayerIndices) winners.Add(index);
            }

            var ordered = new List<MatchParticipant>(participants);
            ordered.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

            var result = new List<EndingStagePlacement>(ordered.Count);
            var escapeUsed = 0;
            var arrestUsed = 0;
            foreach (var participant in ordered)
            {
                var escaped = winners.Contains(participant.PlayerIndex);
                var slot = escaped
                    ? Math.Min(escapeUsed++, escapeSlots - 1)
                    : Math.Min(arrestUsed++, arrestSlots - 1);
                result.Add(new EndingStagePlacement(participant.PlayerId, participant.PlayerIndex, escaped, slot));
            }

            return result;
        }
    }
}
