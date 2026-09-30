using System;
using System.Collections.Generic;
using Game.Server.Items;
using UnityEngine;

namespace Game.Server.Match
{
    [Flags]
    public enum HighlightPlayerAction : ushort
    {
        None = 0,
        Punching = 1 << 0,
        Stunned = 1 << 1,
        Airborne = 1 << 2,
        Crouching = 1 << 3,
        Prone = 1 << 4,
        Carrying = 1 << 5,
        Throwing = 1 << 6,
        Placing = 1 << 7,

        /// <summary>맞은 쪽. <see cref="Punching"/> 은 때린 쪽에 붙는다.</summary>
        Hit = 1 << 8,

        /// <summary>기절에 막 들어가 쓰러지는 구간. 그 뒤는 <see cref="Stunned"/> 만 남는다.</summary>
        StunEntry = 1 << 9,

        /// <summary>감정 표현 재생 중. 어떤 표현인지는 <see cref="EmoteIdMask"/> 세 비트에 담는다.</summary>
        Emoting = 1 << 10,

        /// <summary>감정 표현 카탈로그 ID(0–7)를 담는 자리.</summary>
        EmoteIdMask = 7 << 11,

        /// <summary>기절이 풀려 일어나는 구간.</summary>
        StunRecovery = 1 << 14,
    }

    /// <summary>감정 표현을 동작 값 한 칸에 함께 담고 꺼낸다.</summary>
    public static class HighlightPlayerActions
    {
        private const int EmoteIdShift = 11;
        private const int MaxEmoteId = 7;

        public static HighlightPlayerAction WithEmote(
            this HighlightPlayerAction action,
            int emoteId)
        {
            if (emoteId < 0 || emoteId > MaxEmoteId)
            {
                return action.WithoutEmote();
            }

            return (action & ~HighlightPlayerAction.EmoteIdMask) |
                   HighlightPlayerAction.Emoting |
                   (HighlightPlayerAction)(emoteId << EmoteIdShift);
        }

        public static HighlightPlayerAction WithoutEmote(this HighlightPlayerAction action) =>
            action & ~(HighlightPlayerAction.Emoting | HighlightPlayerAction.EmoteIdMask);

        public static bool TryGetEmote(this HighlightPlayerAction action, out int emoteId)
        {
            emoteId = (int)(action & HighlightPlayerAction.EmoteIdMask) >> EmoteIdShift;
            return (action & HighlightPlayerAction.Emoting) != 0;
        }
    }

    public interface IHighlightReplayActionSource
    {
        IReadOnlyList<HighlightPlayerAction> PlayerReplayActions { get; }
    }

    public readonly struct HighlightReplayClip
    {
        public HighlightReplayClip(
            HighlightSegment segment,
            IReadOnlyList<HighlightReplayFrame> frames)
        {
            Segment = segment;
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
        }

        public HighlightSegment Segment { get; }
        public IReadOnlyList<HighlightReplayFrame> Frames { get; }
    }

    public sealed class HighlightReplayData
    {
        public HighlightReplayData(
            HighlightCandidate candidate,
            IReadOnlyList<HighlightReplayClip> clips, string title = null, string summary = null)
        {
            if (candidate.Segments == null)
            {
                throw new ArgumentException("A valid highlight is required.", nameof(candidate));
            }

            if (clips == null)
            {
                throw new ArgumentNullException(nameof(clips));
            }

            if (clips.Count != candidate.Segments.Count)
            {
                throw new ArgumentException(
                    "Replay clips must match the highlight segments.",
                    nameof(clips));
            }

            var copiedClips = new HighlightReplayClip[clips.Count];
            for (var index = 0; index < clips.Count; index++)
            {
                var expected = candidate.Segments[index];
                var actual = clips[index].Segment;
                if (actual.StartedAt != expected.StartedAt ||
                    actual.EndedAt != expected.EndedAt ||
                    actual.PlaybackSpeed != expected.PlaybackSpeed)
                {
                    throw new ArgumentException(
                        "Replay clip order must match the highlight segments.",
                        nameof(clips));
                }

                copiedClips[index] = clips[index];
            }

            if(title != null && (!Game.Core.Ports.HighlightDirectorReply.ValidText(title,24) ||
                !Game.Core.Ports.HighlightDirectorReply.ValidText(summary,70))) throw new ArgumentException("Invalid highlight caption.");
            Title=title; Summary=summary;
            Candidate = candidate;
            Clips = Array.AsReadOnly(copiedClips);
        }

        public string Title { get; }
        public string Summary { get; }
        public HighlightCandidate Candidate { get; }
        public IReadOnlyList<HighlightReplayClip> Clips { get; }
    }

    public readonly struct HighlightReplayFrame
    {
        public HighlightReplayFrame(
            double recordedAt,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> worldObjects,
            IReadOnlyList<HighlightPlayerAction> playerActions = null)
        {
            if (recordedAt < 0d || double.IsNaN(recordedAt) || double.IsInfinity(recordedAt))
            {
                throw new ArgumentOutOfRangeException(nameof(recordedAt));
            }

            if (playerPoses == null)
            {
                throw new ArgumentNullException(nameof(playerPoses));
            }

            if (worldObjects == null)
            {
                throw new ArgumentNullException(nameof(worldObjects));
            }

            var copiedPlayerPoses = new Pose[playerPoses.Count];
            for (var index = 0; index < playerPoses.Count; index++)
            {
                copiedPlayerPoses[index] = playerPoses[index];
            }

            var copiedWorldObjects = new WorldObjectState[worldObjects.Count];
            for (var index = 0; index < worldObjects.Count; index++)
            {
                copiedWorldObjects[index] = worldObjects[index];
            }

            RecordedAt = recordedAt;
            PlayerPoses = Array.AsReadOnly(copiedPlayerPoses);
            WorldObjects = Array.AsReadOnly(copiedWorldObjects);
            if (playerActions != null && playerActions.Count != playerPoses.Count)
                throw new ArgumentException("Actions must match player poses.", nameof(playerActions));
            var actions = new HighlightPlayerAction[playerPoses.Count];
            for (var i = 0; i < actions.Length; i++)
            {
                actions[i] = playerActions == null ? HighlightPlayerAction.None : playerActions[i];
            }
            PlayerActions = Array.AsReadOnly(actions);
        }

        public double RecordedAt { get; }
        public IReadOnlyList<Pose> PlayerPoses { get; }
        public IReadOnlyList<WorldObjectState> WorldObjects { get; }
        public IReadOnlyList<HighlightPlayerAction> PlayerActions { get; }
    }

    public sealed class HighlightReplayBuffer
    {
        private readonly double sampleIntervalSeconds;
        private readonly double maxDurationSeconds;
        private readonly Queue<HighlightReplayFrame> frames = new();
        private double lastRecordedAt = -1d;

        public HighlightReplayBuffer(double sampleIntervalSeconds, double maxDurationSeconds)
        {
            if (sampleIntervalSeconds <= 0d ||
                double.IsNaN(sampleIntervalSeconds) ||
                double.IsInfinity(sampleIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIntervalSeconds));
            }

            if (maxDurationSeconds <= 0d ||
                double.IsNaN(maxDurationSeconds) ||
                double.IsInfinity(maxDurationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDurationSeconds));
            }

            this.sampleIntervalSeconds = sampleIntervalSeconds;
            this.maxDurationSeconds = maxDurationSeconds;
        }

        public int Count => frames.Count;

        public bool IsSampleDue(double now)
        {
            if (now < 0d || double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }

            if (lastRecordedAt >= 0d && now < lastRecordedAt)
            {
                throw new ArgumentException("Replay time must not move backwards.", nameof(now));
            }

            return lastRecordedAt < 0d || now - lastRecordedAt >= sampleIntervalSeconds;
        }

        public bool TryRecord(
            double now,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> worldObjects,
            IReadOnlyList<HighlightPlayerAction> playerActions = null)
        {
            if (!IsSampleDue(now)) return false;

            frames.Enqueue(new HighlightReplayFrame(now, playerPoses, worldObjects, playerActions));
            lastRecordedAt = now;

            var oldestAllowedAt = now - maxDurationSeconds;
            while (frames.Count > 0 && frames.Peek().RecordedAt < oldestAllowedAt)
            {
                frames.Dequeue();
            }

            return true;
        }

        // Include neighboring samples only when contiguous, so a cut does not
        // borrow a distant stale state or start with an already-destroyed item.
        public bool HasFramesWithBoundary(double startedAt,double endedAt)
        {
            foreach(var frame in frames)
            {
                if(frame.RecordedAt>endedAt+sampleIntervalSeconds*2d) return false;
                if(frame.RecordedAt>=startedAt-sampleIntervalSeconds*2d) return true;
            }
            return false;
        }

        public HighlightReplayFrame[] CaptureWithBoundary(double startedAt, double endedAt)
        {
            var result = new List<HighlightReplayFrame>(Capture(startedAt, endedAt));
            HighlightReplayFrame? previous = null;
            foreach (var frame in frames)
            {
                if (frame.RecordedAt < startedAt) previous = frame;
                if (frame.RecordedAt > endedAt)
                {
                    if (frame.RecordedAt - endedAt <= sampleIntervalSeconds * 2d) result.Add(frame);
                    break;
                }
            }
            if (previous.HasValue && startedAt - previous.Value.RecordedAt <= sampleIntervalSeconds * 2d)
                result.Insert(0, previous.Value);
            return result.ToArray();
        }

        public HighlightReplayFrame[] Capture(double startedAt, double endedAt)
        {
            if (startedAt < 0d || endedAt < startedAt)
            {
                throw new ArgumentOutOfRangeException(nameof(startedAt));
            }

            var captured = new List<HighlightReplayFrame>();
            foreach (var frame in frames)
            {
                if (frame.RecordedAt >= startedAt && frame.RecordedAt <= endedAt)
                {
                    captured.Add(frame);
                }
            }

            return captured.ToArray();
        }
    }
}
