using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core.Lobby;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Session
{
    internal static class HighlightReplaySerializer
    {
        private const int Magic = 0x4852504C;
        private const byte Version = 2;
        private const int MaxPayloadBytes = 8 * 1024 * 1024;
        private const int MaxHighlightCount = 3;
        private const int MaxSegmentsPerHighlight = 8;
        private const int MaxFramesPerClip = 1024;
        private const int MaxIdLength = 64;
        private const int MaxWorldObjectsPerFrame = 64;

        public static byte[] Serialize(IReadOnlyList<HighlightReplayData> replay)
        {
            if (replay == null ||
                replay.Count > MaxHighlightCount)
            {
                throw new ArgumentException(
                    "At most three highlights can be transferred.",
                    nameof(replay));
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((byte)replay.Count);

            foreach (var highlight in replay)
            {
                WriteHighlight(writer, highlight);
            }

            writer.Flush();
            if (stream.Length > MaxPayloadBytes)
            {
                throw new ArgumentException(
                    "Highlight replay payload exceeds 8 MB.",
                    nameof(replay));
            }

            return stream.ToArray();
        }

        public static bool TryDeserialize(
            ReadOnlySpan<byte> data,
            out HighlightReplayData[] replay)
        {
            replay = Array.Empty<HighlightReplayData>();
            if (data.Length == 0 || data.Length > MaxPayloadBytes)
            {
                return false;
            }

            try
            {
                using var stream = new MemoryStream(data.ToArray(), writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                if (reader.ReadInt32() != Magic || reader.ReadByte() != Version)
                {
                    return false;
                }

                var highlightCount = reader.ReadByte();
                if (highlightCount > MaxHighlightCount)
                {
                    return false;
                }

                var decoded = new HighlightReplayData[highlightCount];
                for (var index = 0; index < decoded.Length; index++)
                {
                    decoded[index] = ReadHighlight(reader);
                }

                if (stream.Position != stream.Length)
                {
                    return false;
                }

                replay = decoded;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is ArgumentException ||
                exception is OverflowException)
            {
                return false;
            }
        }

        private static void WriteHighlight(
            BinaryWriter writer,
            HighlightReplayData replay)
        {
            if (replay == null ||
                replay.Candidate.Segments.Count > MaxSegmentsPerHighlight ||
                replay.Clips.Count != replay.Candidate.Segments.Count)
            {
                throw new ArgumentException("Highlight replay is invalid.", nameof(replay));
            }

            writer.Write((byte)replay.Candidate.Type);
            WriteId(writer, replay.Candidate.TargetId);
            writer.Write((byte)replay.Clips.Count);
            foreach (var clip in replay.Clips)
            {
                WriteSegment(writer, clip.Segment);
                var frameCount = Math.Min(clip.Frames.Count, MaxFramesPerClip);
                writer.Write(frameCount);
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var sourceIndex = frameCount <= 1
                        ? 0
                        : frameIndex * (clip.Frames.Count - 1) / (frameCount - 1);
                    WriteFrame(writer, clip.Frames[sourceIndex]);
                }
            }
        }

        private static HighlightReplayData ReadHighlight(BinaryReader reader)
        {
            var type = (HighlightType)reader.ReadByte();
            var targetId = ReadId(reader);
            var segmentCount = reader.ReadByte();
            if (segmentCount == 0 || segmentCount > MaxSegmentsPerHighlight)
            {
                throw new InvalidDataException("Highlight segment count is invalid.");
            }

            var segments = new HighlightSegment[segmentCount];
            var clips = new HighlightReplayClip[segmentCount];
            for (var index = 0; index < segmentCount; index++)
            {
                var segment = ReadSegment(reader);
                var frameCount = ReadCount(reader, MaxFramesPerClip);
                var frames = new HighlightReplayFrame[frameCount];
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    frames[frameIndex] = ReadFrame(reader);
                }

                segments[index] = segment;
                clips[index] = new HighlightReplayClip(segment, frames);
            }

            return new HighlightReplayData(
                new HighlightCandidate(type, segments, targetId),
                clips);
        }

        private static void WriteSegment(BinaryWriter writer, HighlightSegment segment)
        {
            writer.Write(segment.StartedAt);
            writer.Write(segment.EndedAt);
            writer.Write(segment.PlaybackSpeed);
        }

        private static HighlightSegment ReadSegment(BinaryReader reader) =>
            new HighlightSegment(
                reader.ReadDouble(),
                reader.ReadDouble(),
                reader.ReadDouble());

        private static void WriteFrame(BinaryWriter writer, HighlightReplayFrame frame)
        {
            if (frame.PlayerPoses.Count > RoomSettings.MaxPlayerCount ||
                frame.WorldObjects.Count > MaxWorldObjectsPerFrame)
            {
                throw new ArgumentException("Highlight frame capacity was exceeded.");
            }

            writer.Write(frame.RecordedAt);
            writer.Write((byte)frame.PlayerPoses.Count);
            foreach (var pose in frame.PlayerPoses)
            {
                WritePose(writer, pose);
            }
            foreach (var action in frame.PlayerActions) writer.Write(action);

            writer.Write((byte)frame.WorldObjects.Count);
            foreach (var worldObject in frame.WorldObjects)
            {
                WriteId(writer, worldObject.ObjectId);
                WritePose(writer, worldObject.Pose);
            }
        }

        private static HighlightReplayFrame ReadFrame(BinaryReader reader)
        {
            var recordedAt = reader.ReadDouble();
            var playerCount = reader.ReadByte();
            if (playerCount > RoomSettings.MaxPlayerCount)
            {
                throw new InvalidDataException("Highlight player count is invalid.");
            }

            var playerPoses = new Pose[playerCount];
            for (var index = 0; index < playerPoses.Length; index++)
            {
                playerPoses[index] = ReadPose(reader);
            }
            var actions = new byte[playerCount];
            for (var index = 0; index < actions.Length; index++) actions[index] = reader.ReadByte();

            var objectCount = reader.ReadByte();
            if (objectCount > MaxWorldObjectsPerFrame)
            {
                throw new InvalidDataException("Highlight object count is invalid.");
            }

            var worldObjects = new WorldObjectState[objectCount];
            for (var index = 0; index < worldObjects.Length; index++)
            {
                worldObjects[index] = new WorldObjectState(
                    ReadId(reader),
                    ReadPose(reader));
            }

            return new HighlightReplayFrame(recordedAt, playerPoses, worldObjects, actions);
        }

        private static void WritePose(BinaryWriter writer, Pose pose)
        {
            if (!IsFinite(pose))
            {
                throw new ArgumentException("Highlight pose must be finite.");
            }

            writer.Write(pose.position.x);
            writer.Write(pose.position.y);
            writer.Write(pose.position.z);
            writer.Write(pose.rotation.x);
            writer.Write(pose.rotation.y);
            writer.Write(pose.rotation.z);
            writer.Write(pose.rotation.w);
        }

        private static Pose ReadPose(BinaryReader reader)
        {
            var pose = new Pose(
                new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                new Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()));
            if (!IsFinite(pose))
            {
                throw new InvalidDataException("Highlight pose is invalid.");
            }

            return pose;
        }

        private static void WriteId(BinaryWriter writer, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdLength)
            {
                throw new ArgumentException("Highlight id is invalid.", nameof(value));
            }

            writer.Write(value);
        }

        private static string ReadId(BinaryReader reader)
        {
            var value = reader.ReadString();
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdLength)
            {
                throw new InvalidDataException("Highlight id is invalid.");
            }

            return value;
        }

        private static int ReadCount(BinaryReader reader, int max)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw new InvalidDataException("Highlight collection size is invalid.");
            }

            return count;
        }

        private static bool IsFinite(Pose pose) =>
            float.IsFinite(pose.position.x) &&
            float.IsFinite(pose.position.y) &&
            float.IsFinite(pose.position.z) &&
            float.IsFinite(pose.rotation.x) &&
            float.IsFinite(pose.rotation.y) &&
            float.IsFinite(pose.rotation.z) &&
            float.IsFinite(pose.rotation.w);
    }
}
