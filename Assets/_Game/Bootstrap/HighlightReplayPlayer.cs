using System;
using System.Collections.Generic;
using Game.Client.Players;
using Game.Network.Players;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed class HighlightReplayPlayer
    {
        private readonly IReadOnlyList<Transform> playerTargets;
        private readonly Dictionary<string, Transform> objectTargets;
        private IReadOnlyList<HighlightReplayClip> clips = Array.Empty<HighlightReplayClip>();
        private int clipIndex;
        private double clipElapsedSeconds;

        public HighlightReplayPlayer(
            IReadOnlyList<Transform> playerTargets,
            IReadOnlyList<SceneWorldObjectReference> objectTargets)
        {
            this.playerTargets = playerTargets ??
                throw new ArgumentNullException(nameof(playerTargets));
            if (objectTargets == null)
            {
                throw new ArgumentNullException(nameof(objectTargets));
            }

            this.objectTargets = new Dictionary<string, Transform>(
                objectTargets.Count,
                StringComparer.Ordinal);
            foreach (var reference in objectTargets)
            {
                if (reference == null ||
                    reference.Target == null ||
                    string.IsNullOrWhiteSpace(reference.ObjectId) ||
                    !this.objectTargets.TryAdd(reference.ObjectId.Trim(), reference.Target))
                {
                    throw new ArgumentException(
                        "Replay object targets must have unique ids and Transforms.",
                        nameof(objectTargets));
                }
            }
        }

        public bool IsPlaying { get; private set; }

        public bool Start(IReadOnlyList<HighlightReplayClip> replayClips)
        {
            clips = replayClips ?? throw new ArgumentNullException(nameof(replayClips));
            clipIndex = 0;
            clipElapsedSeconds = 0d;
            IsPlaying = MoveToPlayableClip();
            if (IsPlaying)
            {
                ApplyCurrentFrame();
            }

            return IsPlaying;
        }

        public bool Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (!IsPlaying)
            {
                return false;
            }

            clipElapsedSeconds += deltaSeconds;
            while (IsPlaying && clipElapsedSeconds >= CurrentPlaybackDurationSeconds())
            {
                var completedDuration = CurrentPlaybackDurationSeconds();
                var remainingSeconds = clipElapsedSeconds - completedDuration;
                clipElapsedSeconds = completedDuration;
                ApplyCurrentFrame();
                clipElapsedSeconds = remainingSeconds;
                clipIndex++;
                IsPlaying = MoveToPlayableClip();
            }

            if (!IsPlaying)
            {
                return false;
            }

            ApplyCurrentFrame();
            return true;
        }

        private bool MoveToPlayableClip()
        {
            while (clipIndex < clips.Count && clips[clipIndex].Frames.Count == 0)
            {
                clipIndex++;
            }

            return clipIndex < clips.Count;
        }

        private double CurrentPlaybackDurationSeconds()
        {
            return clips[clipIndex].Segment.PlaybackDurationSeconds;
        }

        private void ApplyCurrentFrame()
        {
            var clip = clips[clipIndex];
            var sourceTime = clip.Segment.StartedAt +
                             (clipElapsedSeconds * clip.Segment.PlaybackSpeed);
            FindFrames(clip.Frames, sourceTime, out var from, out var to, out var t);

            var playerCount = Math.Min(
                playerTargets.Count,
                Math.Min(from.PlayerPoses.Count, to.PlayerPoses.Count));
            for (var index = 0; index < playerCount; index++)
            {
                ApplyPose(playerTargets[index], from.PlayerPoses[index], to.PlayerPoses[index], t);
                ApplyLocomotion(
                    playerTargets[index],
                    from.PlayerPoses[index],
                    to.PlayerPoses[index],
                    to.RecordedAt - from.RecordedAt,
                    (float)clip.Segment.PlaybackSpeed);
            }

            foreach (var fromObject in from.WorldObjects)
            {
                if (!objectTargets.TryGetValue(fromObject.ObjectId, out var target))
                {
                    continue;
                }

                var toPose = TryFindObject(to.WorldObjects, fromObject.ObjectId, out var toObject)
                    ? toObject.Pose
                    : fromObject.Pose;
                ApplyPose(target, fromObject.Pose, toPose, t);
            }
        }

        private static void FindFrames(
            IReadOnlyList<HighlightReplayFrame> frames,
            double sourceTime,
            out HighlightReplayFrame from,
            out HighlightReplayFrame to,
            out float t)
        {
            from = frames[0];
            to = frames[frames.Count - 1];
            for (var index = 1; index < frames.Count; index++)
            {
                if (frames[index].RecordedAt < sourceTime)
                {
                    from = frames[index];
                    continue;
                }

                to = frames[index];
                break;
            }

            var duration = to.RecordedAt - from.RecordedAt;
            t = duration <= 0d
                ? 0f
                : Mathf.Clamp01((float)((sourceTime - from.RecordedAt) / duration));
        }

        private static bool TryFindObject(
            IReadOnlyList<WorldObjectState> states,
            string objectId,
            out WorldObjectState found)
        {
            foreach (var state in states)
            {
                if (string.Equals(state.ObjectId, objectId, StringComparison.Ordinal))
                {
                    found = state;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private static void ApplyPose(Transform target, Pose from, Pose to, float t)
        {
            if (target == null)
            {
                return;
            }

            target.SetPositionAndRotation(
                Vector3.Lerp(from.position, to.position, t),
                Quaternion.Slerp(from.rotation, to.rotation, t));
        }

        private static void ApplyLocomotion(
            Transform target,
            Pose from,
            Pose to,
            double recordedDurationSeconds,
            float playbackSpeed)
        {
            if (target == null || recordedDurationSeconds <= 0d)
            {
                return;
            }

            var delta = to.position - from.position;
            delta.y = 0f;
            var speed = delta.magnitude /
                        (float)recordedDurationSeconds * playbackSpeed;
            var motor = target.GetComponent<NetworkPlayerMotor>();
            target.GetComponent<PlayerAnimationDriver>()?.ApplyNetworkState(
                speed,
                grounded: true,
                attackSequence: motor != null ? motor.AttackSequence : 0);
        }
    }
}
