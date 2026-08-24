using System;
using System.Collections.Generic;
using Game.Server.Items;
using UnityEngine;

namespace Game.Server.Match
{
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

    public readonly struct HighlightReplayFrame
    {
        public HighlightReplayFrame(
            double recordedAt,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> worldObjects)
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
        }

        public double RecordedAt { get; }
        public IReadOnlyList<Pose> PlayerPoses { get; }
        public IReadOnlyList<WorldObjectState> WorldObjects { get; }
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

        public bool TryRecord(
            double now,
            IReadOnlyList<Pose> playerPoses,
            IReadOnlyList<WorldObjectState> worldObjects)
        {
            if (now < 0d || double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }

            if (lastRecordedAt >= 0d && now < lastRecordedAt)
            {
                throw new ArgumentException("Replay time must not move backwards.", nameof(now));
            }

            if (lastRecordedAt >= 0d && now - lastRecordedAt < sampleIntervalSeconds)
            {
                return false;
            }

            frames.Enqueue(new HighlightReplayFrame(now, playerPoses, worldObjects));
            lastRecordedAt = now;

            var oldestAllowedAt = now - maxDurationSeconds;
            while (frames.Count > 0 && frames.Peek().RecordedAt < oldestAllowedAt)
            {
                frames.Dequeue();
            }

            return true;
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
