using System;
using System.Collections.Generic;
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
        /// <summary>
        /// 애니메이터마다 지금 재생 중인 상태 이름. 이름이 바뀔 때만 다시 재생한다.
        ///
        /// <para>
        /// 전에는 동작 플래그가 바뀔 때만 봤다. 걷다가 뛰는 것처럼 <b>플래그는 같고 속도만
        /// 바뀌는</b> 변화는 그래서 화면에 나오지 않았다.
        /// </para>
        /// </summary>
        private readonly Dictionary<Animator, string> animationStates = new();

        /// <summary>걷는 것으로 보는 최저 속도(m/s). 실제 플레이의 이동 판정과 같은 값이다.</summary>
        private const float WalkThreshold = 0.35f;

        /// <summary>
        /// 달리는 것으로 보는 속도(m/s).
        ///
        /// <para>
        /// 실제 플레이는 이동 설정(<c>MovementConfigSO</c>)의 걷기·달리기 속도로 가르지만, 재생
        /// 대상은 렌더링 전용 복사본이라 그 설정을 들고 있지 않다. 걷기 3.5 · 달리기 6 사이의
        /// 값으로 두면 둘이 뚜렷이 갈린다.
        /// </para>
        /// </summary>
        private const float RunThreshold = 4.6f;

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
            animationStates.Clear();
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
                var speed = PlanarSpeed(
                    from.PlayerPoses[index],
                    to.PlayerPoses[index],
                    to.RecordedAt - from.RecordedAt,
                    (float)clip.Segment.PlaybackSpeed);
                var animator = playerTargets[index] != null
                    ? playerTargets[index].GetComponentInChildren<Animator>() : null;
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var action = t >= 1f ? to.PlayerActions[index] : from.PlayerActions[index];
                    var state = AnimationStateOf(action, speed);
                    // 걷기 속도는 블렌드 트리를 쓰는 클립을 위해 그대로 넘긴다.
                    animator.SetFloat("Speed", speed);
                    if (cut || !animationStates.TryGetValue(animator, out var previous) || state != previous)
                        animator.Play(state, 0, 0f);
                    animationStates[animator] = state;
                    animator.speed = 1f;
                    animator.Update(sourceDelta);
                    animator.speed = 0f;
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

        /// <summary>
        /// 두 프레임 사이의 수평 이동 속도(m/s). 재생 속도를 곱한 값이라 화면에서 보이는 속도다.
        /// </summary>
        /// <remarks>
        /// 전에는 여기서 <c>PlayerAnimationDriver.ApplyNetworkState</c> 도 불렀다. 재생 대상은
        /// 렌더링 전용 복사본(<c>ReplayVisual</c>)이라 그 컴포넌트가 없어 <b>아무 일도 하지 않는
        /// 호출</b>이었고, 넘기던 <c>carrying: false</c> 와 "항상 앞" 방향이 실제 동작인 것처럼
        /// 읽혔다. 재생 중 애니메이션은 <see cref="AnimationStateOf"/> 하나로 정한다.
        /// </remarks>
        private static float PlanarSpeed(
            Pose from,
            Pose to,
            double recordedDurationSeconds,
            float playbackSpeed)
        {
            if (recordedDurationSeconds <= 0d) return 0f;

            var delta = to.position - from.position;
            delta.y = 0f;
            return delta.magnitude / (float)recordedDurationSeconds * playbackSpeed;
        }

        /// <summary>
        /// 이 동작에 쓸 애니메이터 상태. 이름은 실제 플레이의 <c>PlayerAnimationDriver</c> 와 같은 것을 쓴다.
        ///
        /// <para>
        /// <b>들고 있으면 두 손 클립으로 간다.</b> 들기를 안 보면 팔이 내려간 <c>Idle</c> 이 나와서,
        /// 물건을 들고 있는데 맨손으로 서 있는 것처럼 보인다(2026-09-21 저택 하이라이트에서 확인).
        /// 던지기·내려놓기도 순간 동작이라 들기보다 먼저 본다.
        /// </para>
        ///
        /// <para>
        /// 주먹질은 들고 있어도 <c>Punch</c> 다. <c>Carry_TwoHands_Hit</c> 는 <b>맞은</b> 쪽 클립이고
        /// 이 플래그는 때린 쪽에 붙기 때문이다(<c>MatchSessionCoordinator</c> 의 <c>lastHitAt</c>).
        /// </para>
        /// </summary>
        internal static string AnimationStateOf(HighlightPlayerAction action) =>
            AnimationStateOf(action, 0f);

        /// <summary>
        /// 이 동작과 속도에 쓸 애니메이터 상태. 이름은 실제 플레이와 같은 것을 쓴다.
        /// </summary>
        /// <remarks>
        /// <b>움직이면 걷기·달리기 클립으로 간다</b>(2026-09-21). 전에는 속도를 보지 않아 뛰어가는
        /// 장면도 제자리 <c>Idle</c> 로 나왔다. <b>앞 방향 클립만 쓴다</b> - 뒤·좌·우 클립도 있지만,
        /// 하이라이트는 대개 앞으로 달리는 장면이고 방향까지 나누려면 진행 방향을 아바타가 보는
        /// 쪽으로 옮겨 분류해야 해서 값에 비해 품이 크다. 필요해지면 실제 플레이의
        /// <c>PlayerAnimationDriver.ResolveDirection</c> 을 함께 쓰는 쪽이 맞다.
        /// </remarks>
        internal static string AnimationStateOf(HighlightPlayerAction action, float planarSpeed)
        {
            var carrying = (action & HighlightPlayerAction.Carrying) != 0;
            var prone = (action & HighlightPlayerAction.Prone) != 0;
            var crouching = (action & HighlightPlayerAction.Crouching) != 0;
            var moving = planarSpeed >= WalkThreshold;
            var running = planarSpeed >= RunThreshold;
            if ((action & HighlightPlayerAction.Stunned) != 0) return "Stunned";
            if ((action & HighlightPlayerAction.Throwing) != 0)
                return prone ? "Throw_TwoHands_Prone" : crouching ? "Throw_TwoHands_Crouch" : "Throw_TwoHands";
            if ((action & HighlightPlayerAction.Placing) != 0)
                return prone ? "PutDown_TwoHands_Prone" : crouching ? "PutDown_TwoHands_Crouch" : "PutDown_TwoHands";
            if ((action & HighlightPlayerAction.Punching) != 0) return "Punch";
            if ((action & HighlightPlayerAction.Airborne) != 0) return carrying ? "Carry_TwoHands_Jump" : "Fall";
            if (prone)
                return moving
                    ? (carrying ? "Carry_TwoHands_Crawl_Forward" : "Crawl_Forward")
                    : (carrying ? "Carry_TwoHands_Prone_Idle" : "Prone_Idle");
            if (crouching)
                return moving
                    ? (carrying ? "Carry_TwoHands_Crouch_Walk_Forward" : "Crouch_Walk_Forward")
                    : (carrying ? "Carry_TwoHands_Crouch_Idle" : "Crouch_Idle");
            if (running) return carrying ? "Carry_TwoHands_Run_Forward" : "Run_Forward";
            if (moving) return carrying ? "Carry_TwoHands_Walk_Forward" : "Walk_Forward";
            return carrying ? "Carry_TwoHands" : "Idle";
        }
    }
}
