using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Items;
using Game.SOAP.Config;
using UnityEngine;

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
        private double recordingStartedAt = -1d;

        public void StartRecording(double now)
        {
            ValidateTime(now);
            if (recordingStartedAt < 0d) recordingStartedAt = now;
        }

        public void RecordItemPickup(int playerIndex, string itemId, double now)
        {
            RecordItemInteraction(playerIndex, itemId, now);
            if (!items.TryGetValue(itemId, out var item)) return;
            if (item.LastHolder != playerIndex)
            {
                item.PickedUpAt.Add(now);
                item.Holders.Add(playerIndex);
                item.LastHolder = playerIndex;
            }
        }

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

            item.InteractionCount++;
            item.LastInteractedAt = now;
            if (playerIndex != item.OwnerPlayerIndex && !item.FirstOtherPlayerInteractionAt.HasValue)
            {
                item.FirstOtherPlayerInteractionAt = now;
            }

        }

        public void RecordItemDestroyed(int destroyerPlayerIndex, string itemId, double now)
        {
            RecordItemInteraction(destroyerPlayerIndex, itemId, now);
            if (items.TryGetValue(itemId, out var destroyedItem))
            {
                destroyedItem.Destroyed = true;
                lastGameEvent = new GameEvent(itemId, now);
            }
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

        public HighlightCandidate[] CaptureCandidates(double endedAt, MatchEndReason? endReason = null,
            IReadOnlyList<HighlightReplayFrame> frames = null)
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
                    CreateMontageSegments(mostInteractedItem.PickedUpAt, endedAt, 3, 2d),
                    mostInteractedItemId));
            }

            if (lastGameEvent.HasValue &&
                endedAt - lastGameEvent.Value.OccurredAt <= rules.HighlightClipDurationSeconds &&
                (!firstDestroyedEvent.HasValue ||
                 lastGameEvent.Value.OccurredAt != firstDestroyedEvent.Value.OccurredAt ||
                 lastGameEvent.Value.TargetId != firstDestroyedEvent.Value.TargetId))
            {
                candidates.Add(CreateEventCandidate(
                    HighlightType.FinalMoment,
                    lastGameEvent.Value,
                    endedAt));
            }

            if (TryGetLongestHiddenItem(endedAt, out var longestHiddenItemId, out _))
            {
                var startedAt = recordingStartedAt >= 0d ? recordingStartedAt : searchingStartedAt;
                var hiddenDuration = endedAt - startedAt;
                var hiddenSegments = frames == null
                    ? new[] { new HighlightSegment(startedAt, endedAt,
                        Math.Max(1d, hiddenDuration / rules.HighlightClipDurationSeconds)) }
                    : CreateHiddenSegments(longestHiddenItemId, startedAt, endedAt, frames);
                if (hiddenSegments.Length > 0)
                    candidates.Add(new HighlightCandidate(HighlightType.LongestHidden,
                        hiddenSegments, longestHiddenItemId));
            }

            if (endReason == MatchEndReason.TimeExpired &&
                !candidates.Exists(candidate => candidate.Type == HighlightType.FinalMoment))
            {
                string survivor = null;
                var longest = -1d;
                foreach (var pair in items)
                {
                    if (pair.Value.Destroyed) continue;
                    var hiddenUntil = pair.Value.FirstOtherPlayerInteractionAt ?? endedAt;
                    if (hiddenUntil > longest || hiddenUntil == longest && string.CompareOrdinal(pair.Key, survivor) < 0)
                    {
                        survivor = pair.Key;
                        longest = hiddenUntil;
                    }
                }
                if (survivor != null)
                    candidates.Add(CreateEventCandidate(HighlightType.FinalMoment,
                        new GameEvent(survivor, endedAt), endedAt));
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

        private HighlightSegment[] CreateHiddenSegments(string itemId, double start, double end,
            IReadOnlyList<HighlightReplayFrame> frames)
        {
            var encounters = new List<double>();
            var wasNear = false;
            foreach (var frame in frames)
            {
                if (frame.RecordedAt < searchingStartedAt) continue;
                var near = false;
                foreach (var item in frame.WorldObjects)
                {
                    if (item.ObjectId != itemId) continue;
                    for (var i = 0; i < frame.PlayerPoses.Count; i++)
                        if (i != items[itemId].OwnerPlayerIndex &&
                            Vector3.Distance(frame.PlayerPoses[i].position, item.Pose.position) <= 4f)
                            near = true;
                }
                if (near && !wasNear &&
                    (encounters.Count == 0 || frame.RecordedAt - encounters[encounters.Count - 1] >= 4d))
                    encounters.Add(frame.RecordedAt);
                wasNear = near;
            }
            if (encounters.Count == 0 || end <= start) return Array.Empty<HighlightSegment>();

            var windows = new List<HighlightSegment> { new(start, Math.Min(end, start + 1d)) };
            windows.Add(new HighlightSegment(Math.Max(start, encounters[0] - 1d), Math.Min(end, encounters[0] + 1d)));
            if (encounters.Count > 1)
                windows.Add(new HighlightSegment(Math.Max(start, encounters[encounters.Count - 1] - 1d),
                    Math.Min(end, encounters[encounters.Count - 1] + 1d)));
            windows.Add(new HighlightSegment(Math.Max(start, end - 1d), end));
            var merged = new List<HighlightSegment>();
            foreach (var window in windows)
            {
                if (merged.Count > 0 && window.StartedAt <= merged[merged.Count - 1].EndedAt)
                {
                    var previous = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new HighlightSegment(previous.StartedAt,
                        Math.Max(previous.EndedAt, window.EndedAt));
                }
                else merged.Add(window);
            }
            var normalDuration = 0d;
            foreach (var window in merged) normalDuration += window.PlaybackDurationSeconds;
            var fastSpeed = Math.Max(1d, (end - start - normalDuration) /
                Math.Max(0.1d, rules.HighlightClipDurationSeconds - normalDuration));
            var segments = new List<HighlightSegment>();
            var cursor = start;
            foreach (var window in merged)
            {
                if (window.StartedAt > cursor) segments.Add(new HighlightSegment(cursor, window.StartedAt, fastSpeed));
                segments.Add(window);
                cursor = window.EndedAt;
            }
            return segments.ToArray();
        }

        private HighlightCandidate CreateEventCandidate(
            HighlightType type,
            GameEvent gameEvent,
            double matchEndedAt)
        {
            return new HighlightCandidate(
                type,
                Math.Max(Math.Max(0d, recordingStartedAt >= 0d ? recordingStartedAt : searchingStartedAt), gameEvent.OccurredAt - 7d),
                gameEvent.OccurredAt + 3d,
                gameEvent.TargetId);
        }

        private bool TryGetMostInteractedItem(out string itemId, out ItemRecord selected)
        {
            itemId = null;
            selected = null;
            foreach (var pair in items)
            {
                var item = pair.Value;
                if (item.Holders.Count < 2 ||
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
            if (candidate.Holders.Count != selected.Holders.Count)
            {
                return candidate.Holders.Count > selected.Holders.Count;
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
                var startedAt = Math.Max(Math.Max(0d, recordingStartedAt >= 0d ? recordingStartedAt : searchingStartedAt), eventTimes[eventIndex] - radiusSeconds);
                var segmentEndedAt = Math.Min(endedAt + 3d, eventTimes[eventIndex] + radiusSeconds);
                if (index > 0) startedAt = Math.Max(startedAt, segments[index - 1].EndedAt);
                segmentEndedAt = Math.Max(startedAt, segmentEndedAt);
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
            public bool Destroyed { get; set; }
            public int InteractionCount { get; set; }
            public double LastInteractedAt { get; set; }
            public double? FirstOtherPlayerInteractionAt { get; set; }
            public int LastHolder { get; set; } = -1;
            public HashSet<int> Holders { get; } = new();
            public List<double> PickedUpAt { get; } = new();
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
