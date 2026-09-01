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
        private double lastSourceTime = -1d;
        private int lastAppliedClip = -1;
        private readonly Dictionary<Animator, HighlightPlayerAction> animationActions = new();

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
        public int CurrentClipIndex => clipIndex;
        public double? SourceTime => lastSourceTime < 0d ? null : lastSourceTime;

        public bool Start(IReadOnlyList<HighlightReplayClip> replayClips)
        {
            clips = replayClips ?? throw new ArgumentNullException(nameof(replayClips));
            clipIndex = 0;
            clipElapsedSeconds = 0d;
            lastSourceTime = -1d;
            lastAppliedClip = -1;
            animationActions.Clear();
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
            var cut = lastAppliedClip != clipIndex || lastSourceTime < 0d;
            var sourceDelta = cut ? 0f : Mathf.Max(0f, (float)(sourceTime - lastSourceTime));

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
                var animator = playerTargets[index] != null
                    ? playerTargets[index].GetComponentInChildren<Animator>() : null;
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var action = t >= 1f ? to.PlayerActions[index] : from.PlayerActions[index];
                    if (cut || !animationActions.TryGetValue(animator, out var previous) || action != previous)
                        animator.Play(AnimationStateOf(action), 0, 0f);
                    animationActions[animator] = action;
                    animator.Update(sourceDelta);
                }
            }

            foreach (var pair in objectTargets)
            {
                var hasFrom = TryFindObject(from.WorldObjects, pair.Key, out var fromObject);
                var hasTo = TryFindObject(to.WorldObjects, pair.Key, out var toObject);
                var visible = t >= 1f ? hasTo : hasFrom;
                var target = pair.Value;
                if (target == null) continue;
                target.gameObject.SetActive(visible);
                if (visible)
                    ApplyPose(target, hasFrom ? fromObject.Pose : toObject.Pose,
                        hasTo ? toObject.Pose : fromObject.Pose, t);
            }
            lastAppliedClip = clipIndex;
            lastSourceTime = sourceTime;
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
            var animator = target.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat("Speed", speed);
            var motor = target.GetComponent<NetworkPlayerMotor>();
            target.GetComponent<PlayerAnimationDriver>()?.ApplyNetworkState(
                speed,
                grounded: true,
                attackSequence: motor != null ? motor.AttackSequence : 0);
        }

        internal static string AnimationStateOf(HighlightPlayerAction action)
        {
            if ((action & HighlightPlayerAction.Stunned) != 0) return "Stunned";
            if ((action & HighlightPlayerAction.Punching) != 0) return "Punch";
            if ((action & HighlightPlayerAction.Airborne) != 0) return "Airborne";
            if ((action & HighlightPlayerAction.Prone) != 0) return "Crawl";
            if ((action & HighlightPlayerAction.Crouching) != 0) return "CrouchMove";
            return "Locomotion";
        }
    }
}
