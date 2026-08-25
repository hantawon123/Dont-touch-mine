using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Items;
using Game.SOAP.Config;

namespace Game.Server.Match
{
    public sealed class HighlightEventRecorder
    {
        private readonly MatchRulesSO rules;
        private readonly Dictionary<string, ItemRecord> items =
            new(StringComparer.Ordinal);
        private readonly List<double>[] stunnedAtByPlayer;
        private GameEvent? firstDestroyedEvent;
        private GameEvent? lastGameEvent;
        private double searchingStartedAt = -1d;

        public HighlightEventRecorder(
            MatchRulesSO rules,
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            MatchRulesSO.ValidatePlayerCount(assignments.Count);
            stunnedAtByPlayer = CreateStunRecords(assignments.Count);
            foreach (var assignment in assignments)
            {
                if (!items.TryAdd(
                        assignment.Item.ItemId,
                        new ItemRecord(assignment.PlayerIndex)))
                {
                    throw new ArgumentException(
                        $"Duplicate item id: {assignment.Item.ItemId}",
                        nameof(assignments));
                }
            }
        }

        public void StartSearching(double now)
        {
            ValidateTime(now);
            if (searchingStartedAt < 0d)
            {
                searchingStartedAt = now;
            }
        }

        public void RecordItemInteraction(int playerIndex, string itemId, double now)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(now);
            if (!items.TryGetValue(itemId, out var item))
            {
                return;
            }

            item.InteractingPlayers.Add(playerIndex);
            item.InteractionCount++;
            item.LastInteractedAt = now;
            item.InteractedAt.Add(now);
            if (playerIndex != item.OwnerPlayerIndex && !item.FirstOtherPlayerInteractionAt.HasValue)
            {
                item.FirstOtherPlayerInteractionAt = now;
            }

            lastGameEvent = new GameEvent(itemId, now);
        }

        public void RecordItemDestroyed(int destroyerPlayerIndex, string itemId, double now)
        {
            RecordItemInteraction(destroyerPlayerIndex, itemId, now);
            if (!firstDestroyedEvent.HasValue && items.ContainsKey(itemId))
            {
                firstDestroyedEvent = new GameEvent(itemId, now);
            }
        }

        public void RecordPlayerStunned(int playerIndex, double now)
        {
            ValidatePlayerIndex(playerIndex);
            ValidateTime(now);
            stunnedAtByPlayer[playerIndex].Add(now);
            lastGameEvent = new GameEvent(
                playerIndex.ToString(CultureInfo.InvariantCulture),
                now);
        }

        public HighlightCandidate[] CaptureCandidates(double endedAt)
        {
            ValidateTime(endedAt);
            var candidates = new List<HighlightCandidate>();

            if (firstDestroyedEvent.HasValue)
            {
                candidates.Add(CreateEventCandidate(
                    HighlightType.FirstBlood,
                    firstDestroyedEvent.Value,
                    endedAt));
            }

            if (TryGetMostInteractedItem(out var mostInteractedItemId, out var mostInteractedItem))
            {
                candidates.Add(new HighlightCandidate(
                    HighlightType.TteTanMulgun,
                    CreateMontageSegments(mostInteractedItem.InteractedAt, endedAt, 5, 1d),
                    mostInteractedItemId));
            }

            if (lastGameEvent.HasValue &&
                endedAt - lastGameEvent.Value.OccurredAt <= rules.HighlightClipDurationSeconds)
            {
                candidates.Add(CreateEventCandidate(
                    HighlightType.FinalMoment,
                    lastGameEvent.Value,
                    endedAt));
            }

            if (TryGetLongestHiddenItem(endedAt, out var longestHiddenItemId, out var hiddenUntil))
            {
                var hiddenDuration = hiddenUntil - searchingStartedAt;
                candidates.Add(new HighlightCandidate(
                    HighlightType.LongestHidden,
                    new[]
                    {
                        new HighlightSegment(
                            searchingStartedAt,
                            hiddenUntil,
                            Math.Max(1d, hiddenDuration / rules.HighlightClipDurationSeconds))
                    },
                    longestHiddenItemId));
            }

            if (TryGetMostStunnedPlayer(out var mostStunnedPlayerIndex))
            {
                candidates.Add(new HighlightCandidate(
                    HighlightType.MostStunned,
                    CreateMontageSegments(
                        stunnedAtByPlayer[mostStunnedPlayerIndex],
                        endedAt,
                        3,
                        2d),
                    mostStunnedPlayerIndex.ToString(CultureInfo.InvariantCulture)));
            }

            return candidates.ToArray();
        }

        private HighlightCandidate CreateEventCandidate(
            HighlightType type,
            GameEvent gameEvent,
            double matchEndedAt)
        {
            return new HighlightCandidate(
                type,
                Math.Max(0d, gameEvent.OccurredAt - 7d),
                Math.Min(matchEndedAt, gameEvent.OccurredAt + 3d),
                gameEvent.TargetId);
        }

        private bool TryGetMostInteractedItem(out string itemId, out ItemRecord selected)
        {
            itemId = null;
            selected = null;
            foreach (var pair in items)
            {
                var item = pair.Value;
                if (item.InteractingPlayers.Count < 2 ||
                    selected != null && !IsMoreInteracted(item, pair.Key, selected, itemId))
                {
                    continue;
                }

                itemId = pair.Key;
                selected = item;
            }

            return selected != null;
        }

        private static bool IsMoreInteracted(
            ItemRecord candidate,
            string candidateId,
            ItemRecord selected,
            string selectedId)
        {
            if (candidate.InteractingPlayers.Count != selected.InteractingPlayers.Count)
            {
                return candidate.InteractingPlayers.Count > selected.InteractingPlayers.Count;
            }

            if (candidate.InteractionCount != selected.InteractionCount)
            {
                return candidate.InteractionCount > selected.InteractionCount;
            }

            if (candidate.LastInteractedAt != selected.LastInteractedAt)
            {
                return candidate.LastInteractedAt > selected.LastInteractedAt;
            }

            return string.CompareOrdinal(candidateId, selectedId) < 0;
        }

        private bool TryGetLongestHiddenItem(
            double endedAt,
            out string itemId,
            out double hiddenUntil)
        {
            itemId = null;
            hiddenUntil = 0d;
            if (searchingStartedAt < 0d)
            {
                return false;
            }

            foreach (var pair in items)
            {
                var candidateHiddenUntil = pair.Value.FirstOtherPlayerInteractionAt ?? endedAt;
                if (candidateHiddenUntil < searchingStartedAt ||
                    itemId != null && candidateHiddenUntil < hiddenUntil ||
                    itemId != null && candidateHiddenUntil == hiddenUntil &&
                    string.CompareOrdinal(pair.Key, itemId) >= 0)
                {
                    continue;
                }

                itemId = pair.Key;
                hiddenUntil = candidateHiddenUntil;
            }

            return itemId != null;
        }

        private bool TryGetMostStunnedPlayer(out int playerIndex)
        {
            playerIndex = -1;
            for (var index = 0; index < stunnedAtByPlayer.Length; index++)
            {
                var stunCount = stunnedAtByPlayer[index].Count;
                if (stunCount == 0 ||
                    playerIndex >= 0 && stunCount < stunnedAtByPlayer[playerIndex].Count ||
                    playerIndex >= 0 && stunCount == stunnedAtByPlayer[playerIndex].Count &&
                    stunnedAtByPlayer[index][stunCount - 1] <=
                    stunnedAtByPlayer[playerIndex][stunCount - 1])
                {
                    continue;
                }

                playerIndex = index;
            }

            return playerIndex >= 0;
        }

        private HighlightSegment[] CreateMontageSegments(
            IReadOnlyList<double> eventTimes,
            double endedAt,
            int maxSegmentCount,
            double radiusSeconds)
        {
            var segmentCount = Math.Min(eventTimes.Count, maxSegmentCount);
            var segments = new HighlightSegment[segmentCount];
            var totalSourceDuration = 0d;
            for (var index = 0; index < segmentCount; index++)
            {
                var eventIndex = segmentCount == 1
                    ? 0
                    : index * (eventTimes.Count - 1) / (segmentCount - 1);
                var startedAt = Math.Max(searchingStartedAt, eventTimes[eventIndex] - radiusSeconds);
                var segmentEndedAt = Math.Min(endedAt, eventTimes[eventIndex] + radiusSeconds);
                segments[index] = new HighlightSegment(startedAt, segmentEndedAt);
                totalSourceDuration += segmentEndedAt - startedAt;
            }

            var playbackSpeed = Math.Max(1d, totalSourceDuration / rules.HighlightClipDurationSeconds);
            if (playbackSpeed > 1d)
            {
                for (var index = 0; index < segments.Length; index++)
                {
                    segments[index] = new HighlightSegment(
                        segments[index].StartedAt,
                        segments[index].EndedAt,
                        playbackSpeed);
                }
            }

            return segments;
        }

        private static List<double>[] CreateStunRecords(int playerCount)
        {
            var records = new List<double>[playerCount];
            for (var index = 0; index < records.Length; index++)
            {
                records[index] = new List<double>();
            }

            return records;
        }

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= stunnedAtByPlayer.Length)
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

        private sealed class ItemRecord
        {
            public ItemRecord(int ownerPlayerIndex)
            {
                OwnerPlayerIndex = ownerPlayerIndex;
            }

            public int OwnerPlayerIndex { get; }
            public HashSet<int> InteractingPlayers { get; } = new();
            public int InteractionCount { get; set; }
            public double LastInteractedAt { get; set; }
            public double? FirstOtherPlayerInteractionAt { get; set; }
            public List<double> InteractedAt { get; } = new();
        }

        private readonly struct GameEvent
        {
            public GameEvent(string targetId, double occurredAt)
            {
                TargetId = targetId;
                OccurredAt = occurredAt;
            }

            public string TargetId { get; }
            public double OccurredAt { get; }
        }
    }
}
