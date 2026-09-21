using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed partial class HighlightCameraDirector
    {
        private const int MaxCctvSwitches = 2;
        private const float CctvLookAheadSeconds = 0.75f;
        private const double FinalCctvSwitchStartRatio = 0.6d;
        private const double CctvPlanSampleSeconds = 0.5d;
        private const double MinimumPlannedShotSeconds = 1.25d;
        private const float PlannedCutCost = 18f;
        private const float MinimumCctvDistance = 5f;
        private const float MaximumCctvDistance = 18f;
        private const float MaximumCctvDistanceBonus = 3f;
        private const float TopDownPenaltyStartAngle = 60f;
        private const float TopDownPenaltyPerDegree = 0.2f;
        private readonly IReadOnlyList<HighlightCctvCamera> cctvCameras;
        private readonly IReadOnlyList<HighlightReplayClip> replayClips;
        private HighlightCctvCamera activeCctv;
        private float cctvHold, cctvCheck, cctvSampleElapsed;
        private int cctvSwitchCount;
        private Vector3 previousCctvFocus;
        private bool hasPreviousCctvFocus;
        private Transform cctvTarget;
        private readonly List<Bounds> cctvOccluders = new();
        private CctvPlanShot[] cctvPlan = Array.Empty<CctvPlanShot>();
        private int cctvPlanIndex = -1;
        public string CctvLocation => activeCctv == null ? "" : activeCctv.LocationName;
        public Vector3? CctvPosition => activeCctv == null ? null : activeCctv.transform.position;

        private void CaptureCctvOccluders()
        {
            if (cctvCameras.Count == 0 || cctvCameras[0] == null || collisionLayerMask == 0) return;
            var subjects = new HashSet<Renderer>();
            foreach (var renderers in replayRenderers.Values) subjects.UnionWith(renderers);
            foreach (var root in cctvCameras[0].gameObject.scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (subjects.Contains(renderer) || !renderer.enabled || renderer.forceRenderingOff ||
                    renderer.bounds.size.y < 0.5f ||
                    (collisionLayerMask & (1 << renderer.gameObject.layer)) == 0 ||
                    renderer.GetComponentInParent<CarryableItem>() != null ||
                    renderer.GetComponentInParent<Animator>() != null ||
                    renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue > 2500) continue;
                // Static furniture can have no collider, and gameplay item colliders are disabled during replay.
                // Bounds are conservative: use authored visibility volumes if hollow props need finer coverage.
                cctvOccluders.Add(renderer.bounds);
            }
        }

        private bool IsCctvOccluded(Vector3 origin, Vector3 focus, Transform subject)
        {
            if (collisionLayerMask == 0) return false;
            // Judge what is rendered, not hidden live-avatar or invisible gameplay colliders.
            var ray = new Ray(origin, (focus - origin).normalized);
            var distance = Vector3.Distance(origin, focus);
            foreach (var bounds in cctvOccluders)
                if (!bounds.Contains(origin) && bounds.IntersectRay(ray, out var entry) &&
                    entry > 0.05f && entry < distance - 0.1f) return true;
            // Replay actors and objects move, so evaluate their current rendered bounds on each check.
            foreach (var pair in replayRenderers)
            {
                if (pair.Key == null || pair.Key == subject ||
                    !pair.Key.gameObject.activeInHierarchy) continue;
                foreach (var renderer in pair.Value)
                    if (renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                        renderer.bounds.IntersectRay(ray, out var entry) &&
                        entry > 0.05f && entry < distance - 0.1f) return true;
            }
            return false;
        }

        private void ResetCctv()
        {
            activeCctv = null;
            cctvHold = cctvCheck = cctvSampleElapsed = 0f;
            cctvSwitchCount = 0;
            hasPreviousCctvFocus = false;
            cctvPlan = Array.Empty<CctvPlanShot>();
            cctvPlanIndex = -1;
        }

        private void AdvanceCctv(float delta)
        {
            cctvHold += delta;
            cctvCheck -= delta;
            cctvSampleElapsed += delta;
        }

        private void ResetCctvPrediction()
        {
            cctvSampleElapsed = 0f;
            hasPreviousCctvFocus = false;
        }

        private void SetCctv(HighlightCctvCamera camera)
        {
            if (activeCctv != null) cctvSwitchCount++;
            activeCctv = camera;
            cctvHold = 0f;
            if (replayCameraRig != null) replayCameraRig.SetFieldOfView(camera.FieldOfView);
            else if (cameraTransform.TryGetComponent<Camera>(out var output)) output.fieldOfView = camera.FieldOfView;
        }

        private void ApplyCctvPose()
        {
            if (currentTarget == null) return;
            if (cctvPlan.Length > 0)
            {
                ApplyActiveCctvPose();
                return;
            }

            // A destroyed replay item is inactive; keep the actor in view for the consequence.
            cctvTarget = currentTarget.gameObject.activeInHierarchy
                ? currentTarget : ResolvePlayer(currentHighlight.ActorPlayerIndex) ?? currentTarget;
            var subject = cctvTarget.position + Vector3.up * 0.5f;
            var focus = subject;
            if (supportingPlayer != null && Vector3.Distance(subject, supportingPlayer.position) < 8f)
                focus = (subject + supportingPlayer.position + Vector3.up) * 0.5f;
            if (activeCctv == null || cctvCheck <= 0f)
            {
                cctvCheck = 0.25f;
                var velocity = hasPreviousCctvFocus && cctvSampleElapsed > 0f
                    ? (focus - previousCctvFocus) / cctvSampleElapsed
                    : Vector3.zero;
                var predictedFocus = focus + velocity * CctvLookAheadSeconds;
                previousCctvFocus = focus;
                hasPreviousCctvFocus = true;
                cctvSampleElapsed = 0f;
                HighlightCctvCamera best = null;
                var bestScore = float.NegativeInfinity;
                foreach (var camera in cctvCameras)
                {
                    if (camera == null) continue;
                    var score = CctvScore(camera, focus);
                    if (velocity.sqrMagnitude > 0.01f &&
                        !CanCctvSeePoint(camera, cctvTarget, predictedFocus)) score -= 600f;
                    if (score > bestScore) { best = camera; bestScore = score; }
                }
                if (best != null && best != activeCctv)
                {
                    if (activeCctv == null) SetCctv(best);
                    else if (CanSwitchCctv())
                    {
                        var currentVisible = CanCctvSeeSubjects(activeCctv);
                        var currentWillStayVisible = velocity.sqrMagnitude <= 0.01f ||
                            CanCctvSeePoint(activeCctv, cctvTarget, predictedFocus);
                        var bestVisible = CanCctvSeeSubjects(best) &&
                            (velocity.sqrMagnitude <= 0.01f ||
                             CanCctvSeePoint(best, cctvTarget, predictedFocus));
                        // Cut immediately when the shot is blocked. A stable movement trend may
                        // trigger the same hard cut shortly before the subject leaves the view.
                        if (bestVisible && (!currentVisible ||
                            cctvHold >= 0.5f && !currentWillStayVisible)) SetCctv(best);
                    }
                }
            }

            ApplyActiveCctvPose();
        }

        private void ApplyActiveCctvPose()
        {
            if (activeCctv == null) return;
            // A CCTV keeps its authored position, direction and lens throughout the shot.
            var position = activeCctv.transform.position;
            var rotation = activeCctv.transform.rotation;
            if (replayCameraRig != null) replayCameraRig.SetPose(position, rotation, 1f, true);
            else cameraTransform.SetPositionAndRotation(position, rotation);
        }

        private readonly struct CctvPlanShot
        {
            public CctvPlanShot(double startedAt, HighlightCctvCamera camera)
            {
                StartedAt = startedAt;
                Camera = camera;
            }

            public double StartedAt { get; }
            public HighlightCctvCamera Camera { get; }
        }

        private readonly struct CctvPlanSample
        {
            public CctvPlanSample(
                double time,
                HighlightReplayFrame frame,
                Vector3 target,
                bool targetIsPlayer,
                int targetPlayerIndex,
                string targetObjectId,
                Vector3? support,
                int supportPlayerIndex)
            {
                Time = time;
                Frame = frame;
                Target = target;
                TargetIsPlayer = targetIsPlayer;
                TargetPlayerIndex = targetPlayerIndex;
                TargetObjectId = targetObjectId;
                Support = support;
                SupportPlayerIndex = supportPlayerIndex;
            }

            public double Time { get; }
            public HighlightReplayFrame Frame { get; }
            public Vector3 Target { get; }
            public bool TargetIsPlayer { get; }
            public int TargetPlayerIndex { get; }
            public string TargetObjectId { get; }
            public Vector3? Support { get; }
            public int SupportPlayerIndex { get; }
        }

        private void BuildCctvPlan(HighlightCandidate highlight)
        {
            if (cctvCameras.Count == 0 || replayClips.Count == 0) return;
            var cameras = new List<HighlightCctvCamera>(cctvCameras.Count);
            foreach (var camera in cctvCameras)
                if (camera != null) cameras.Add(camera);
            if (cameras.Count == 0) return;

            var samples = CaptureCctvPlanSamples(highlight);
            if (samples.Count == 0) return;
            var scores = new float[cameras.Count, samples.Count];
            for (var cameraIndex = 0; cameraIndex < cameras.Count; cameraIndex++)
            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                scores[cameraIndex, sampleIndex] = PlannedCctvScore(cameras[cameraIndex], samples[sampleIndex]);

            cctvPlan = OptimizeCctvPlan(cameras, samples, scores, highlight.PlaybackDurationSeconds);
        }

        private List<CctvPlanSample> CaptureCctvPlanSamples(HighlightCandidate highlight)
        {
            var result = new List<CctvPlanSample>();
            var playbackOffset = 0d;
            var lastSampleTime = double.NegativeInfinity;
            foreach (var clip in replayClips)
            {
                for (var frameIndex = 0; frameIndex < clip.Frames.Count; frameIndex++)
                {
                    var frame = clip.Frames[frameIndex];
                    var time = playbackOffset + Math.Clamp(
                        (frame.RecordedAt - clip.Segment.StartedAt) / clip.Segment.PlaybackSpeed,
                        0d,
                        clip.Segment.PlaybackDurationSeconds);
                    var lastFrame = frameIndex == clip.Frames.Count - 1;
                    if (!lastFrame && time - lastSampleTime < CctvPlanSampleSeconds) continue;
                    if (!TryResolvePlannedTarget(highlight, frame, out var target, out var targetIsPlayer,
                            out var targetPlayerIndex, out var targetObjectId)) continue;

                    var supportIndex = highlight.ActorPlayerIndex;
                    Vector3? support = null;
                    if (supportIndex >= 0 && supportIndex < frame.PlayerPoses.Count &&
                        supportIndex != targetPlayerIndex)
                        support = frame.PlayerPoses[supportIndex].position;
                    result.Add(new CctvPlanSample(time, frame, target, targetIsPlayer,
                        targetPlayerIndex, targetObjectId, support, supportIndex));
                    lastSampleTime = time;
                }
                playbackOffset += clip.Segment.PlaybackDurationSeconds;
            }
            return result;
        }

        private static bool TryResolvePlannedTarget(
            HighlightCandidate highlight,
            HighlightReplayFrame frame,
            out Vector3 target,
            out bool targetIsPlayer,
            out int targetPlayerIndex,
            out string targetObjectId)
        {
            target = default;
            targetIsPlayer = false;
            targetPlayerIndex = -1;
            targetObjectId = null;
            if (int.TryParse(highlight.TargetId, out var playerIndex) &&
                playerIndex >= 0 && playerIndex < frame.PlayerPoses.Count)
            {
                target = frame.PlayerPoses[playerIndex].position;
                targetIsPlayer = true;
                targetPlayerIndex = playerIndex;
                return true;
            }

            foreach (var state in frame.WorldObjects)
            {
                if (!string.Equals(state.ObjectId, highlight.TargetId, StringComparison.Ordinal)) continue;
                target = state.Pose.position;
                targetObjectId = state.ObjectId;
                return true;
            }

            if (highlight.ActorPlayerIndex < 0 || highlight.ActorPlayerIndex >= frame.PlayerPoses.Count)
                return false;
            target = frame.PlayerPoses[highlight.ActorPlayerIndex].position;
            targetIsPlayer = true;
            targetPlayerIndex = highlight.ActorPlayerIndex;
            return true;
        }

        private float PlannedCctvScore(HighlightCctvCamera camera, CctvPlanSample sample)
        {
            var targetVisible = CanSeePlannedSubject(camera, sample.Target, sample.TargetIsPlayer, sample,
                sample.TargetPlayerIndex, sample.TargetObjectId);
            if (!targetVisible) return -1200f;
            var focus = sample.Target + Vector3.up * (sample.TargetIsPlayer ? 0.8f : 0.3f);
            var direction = focus - camera.transform.position;
            var score = CctvCompositionScore(direction) -
                        Vector3.Angle(camera.transform.forward, direction) * 0.02f;
            if (sample.Support.HasValue && Vector3.Distance(sample.Target, sample.Support.Value) < 8f)
            {
                if (!CanSeePlannedSubject(camera, sample.Support.Value, true, sample,
                        sample.SupportPlayerIndex, null)) score -= 350f;
                else score += CctvDistanceBonus(
                    Vector3.Distance(camera.transform.position, sample.Support.Value)) * 0.5f;
            }
            return score;
        }

        private static float CctvCompositionScore(Vector3 direction)
        {
            var horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            var downwardAngle = Mathf.Atan2(Mathf.Abs(direction.y), horizontalDistance) * Mathf.Rad2Deg;
            var topDownPenalty = Mathf.Max(0f, downwardAngle - TopDownPenaltyStartAngle) *
                                 TopDownPenaltyPerDegree;
            return CctvDistanceBonus(direction.magnitude) - topDownPenalty;
        }

        private static float CctvDistanceBonus(float distance)
        {
            // Distance only breaks ties between otherwise readable shots. Very close mounts receive
            // no extra reward, so a ceiling camera cannot win by showing only the top of the action.
            var clamped = Mathf.Clamp(distance, MinimumCctvDistance, MaximumCctvDistance);
            return MaximumCctvDistanceBonus *
                   (MaximumCctvDistance - clamped) /
                   (MaximumCctvDistance - MinimumCctvDistance);
        }

        private bool CanSeePlannedSubject(
            HighlightCctvCamera camera,
            Vector3 position,
            bool player,
            CctvPlanSample sample,
            int subjectPlayerIndex,
            string subjectObjectId)
        {
            var centre = position + Vector3.up * (player ? 0.8f : 0.3f);
            return CanSeePlannedPoint(camera, centre, sample, subjectPlayerIndex, subjectObjectId) ||
                   player && CanSeePlannedPoint(camera, position + Vector3.up * 1.25f, sample,
                       subjectPlayerIndex, subjectObjectId);
        }

        private bool CanSeePlannedPoint(
            HighlightCctvCamera camera,
            Vector3 point,
            CctvPlanSample sample,
            int subjectPlayerIndex,
            string subjectObjectId)
        {
            var local = camera.transform.InverseTransformPoint(point);
            if (local.z <= 0f) return false;
            var aspect = cameraTransform.TryGetComponent<Camera>(out var output) ? output.aspect : 16f / 9f;
            var halfHeight = local.z * Mathf.Tan(camera.FieldOfView * Mathf.Deg2Rad * 0.5f) * 0.88f;
            if (Mathf.Abs(local.y) > halfHeight || Mathf.Abs(local.x) > halfHeight * aspect) return false;
            var origin = camera.transform.position;
            foreach (var bounds in cctvOccluders)
            {
                var ray = new Ray(origin, (point - origin).normalized);
                if (!bounds.Contains(origin) && bounds.IntersectRay(ray, out var entry) &&
                    entry > 0.05f && entry < Vector3.Distance(origin, point) - 0.1f) return false;
            }
            for (var index = 0; index < sample.Frame.PlayerPoses.Count; index++)
            {
                if (index == subjectPlayerIndex || index == sample.SupportPlayerIndex) continue;
                if (BlocksPlannedView(origin, point,
                        sample.Frame.PlayerPoses[index].position + Vector3.up * 0.8f, 0.45f)) return false;
            }
            foreach (var state in sample.Frame.WorldObjects)
            {
                if (string.Equals(state.ObjectId, subjectObjectId, StringComparison.Ordinal)) continue;
                if (BlocksPlannedView(origin, point, state.Pose.position + Vector3.up * 0.25f, 0.22f))
                    return false;
            }
            return true;
        }

        private static bool BlocksPlannedView(Vector3 origin, Vector3 target, Vector3 obstacle, float radius)
        {
            var line = target - origin;
            var lengthSquared = line.sqrMagnitude;
            if (lengthSquared < 0.001f) return false;
            var t = Vector3.Dot(obstacle - origin, line) / lengthSquared;
            if (t <= 0.02f || t >= 0.98f) return false;
            return Vector3.SqrMagnitude(obstacle - (origin + line * t)) < radius * radius;
        }

        private static CctvPlanShot[] OptimizeCctvPlan(
            IReadOnlyList<HighlightCctvCamera> cameras,
            IReadOnlyList<CctvPlanSample> samples,
            float[,] scores,
            double duration)
        {
            var cameraCount = cameras.Count;
            var sampleCount = samples.Count;
            var prefix = new float[cameraCount, sampleCount + 1];
            for (var camera = 0; camera < cameraCount; camera++)
            for (var sample = 0; sample < sampleCount; sample++)
                prefix[camera, sample + 1] = prefix[camera, sample] + scores[camera, sample];
            float Segment(int camera, int start, int end) => prefix[camera, end] - prefix[camera, start];

            var bestScore = float.NegativeInfinity;
            var bestA = 0;
            var bestB = -1;
            var bestC = -1;
            var bestFirstCut = -1;
            var bestSecondCut = -1;
            for (var camera = 0; camera < cameraCount; camera++)
            {
                var score = Segment(camera, 0, sampleCount);
                if (score > bestScore) { bestScore = score; bestA = camera; }
            }

            for (var cut = 1; cut < sampleCount; cut++)
            {
                if (samples[cut].Time < MinimumPlannedShotSeconds ||
                    duration - samples[cut].Time < MinimumPlannedShotSeconds) continue;
                for (var left = 0; left < cameraCount; left++)
                for (var right = 0; right < cameraCount; right++)
                {
                    if (left == right) continue;
                    var score = Segment(left, 0, cut) + Segment(right, cut, sampleCount) - PlannedCutCost;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestA = left; bestB = right; bestC = -1;
                    bestFirstCut = cut; bestSecondCut = -1;
                }
            }

            for (var first = 1; first < sampleCount - 1; first++)
            {
                if (samples[first].Time < MinimumPlannedShotSeconds) continue;
                for (var second = first + 1; second < sampleCount; second++)
                {
                    if (samples[second].Time - samples[first].Time < MinimumPlannedShotSeconds ||
                        duration - samples[second].Time < MinimumPlannedShotSeconds) continue;
                    for (var middle = 0; middle < cameraCount; middle++)
                    {
                        var leftCamera = -1;
                        var leftScore = float.NegativeInfinity;
                        var rightCamera = -1;
                        var rightScore = float.NegativeInfinity;
                        for (var camera = 0; camera < cameraCount; camera++)
                        {
                            if (camera == middle) continue;
                            var left = Segment(camera, 0, first);
                            if (left > leftScore) { leftScore = left; leftCamera = camera; }
                            var right = Segment(camera, second, sampleCount);
                            if (right > rightScore) { rightScore = right; rightCamera = camera; }
                        }
                        var score = leftScore + Segment(middle, first, second) + rightScore - PlannedCutCost * 2f;
                        if (score <= bestScore) continue;
                        bestScore = score;
                        bestA = leftCamera; bestB = middle; bestC = rightCamera;
                        bestFirstCut = first; bestSecondCut = second;
                    }
                }
            }

            var plan = new List<CctvPlanShot>(3) { new(0d, cameras[bestA]) };
            if (bestB >= 0) plan.Add(new CctvPlanShot(samples[bestFirstCut].Time, cameras[bestB]));
            if (bestC >= 0) plan.Add(new CctvPlanShot(samples[bestSecondCut].Time, cameras[bestC]));
            return plan.ToArray();
        }

        private void ApplyCctvPlan(double playbackTime)
        {
            if (cctvPlan.Length == 0) return;
            var next = 0;
            for (var index = 1; index < cctvPlan.Length; index++)
            {
                if (playbackTime < cctvPlan[index].StartedAt) break;
                next = index;
            }
            if (next == cctvPlanIndex) return;
            cctvPlanIndex = next;
            SetCctv(cctvPlan[next].Camera);
        }

        private bool CanSwitchCctv()
        {
            if (cctvSwitchCount >= MaxCctvSwitches) return false;
            if (cctvSwitchCount == 0) return true;

            return currentPlaybackTime >=
                   currentHighlight.PlaybackDurationSeconds * FinalCctvSwitchStartRatio;
        }

        private bool CanCctvSeeSubjects(HighlightCctvCamera camera) =>
            CanCctvSee(camera, cctvTarget) &&
            (supportingPlayer == null || supportingPlayer == cctvTarget ||
             CanCctvSee(camera, supportingPlayer));

        private bool CanCctvSee(HighlightCctvCamera camera, Transform target)
        {
            if (target == null) return false;
            // Feet need not be visible: a clear body centre or upper body is enough.
            // Sample actual bounds so small carried objects are not tested above their mesh.
            var bounds = new Bounds(target.position + Vector3.up * 0.5f, Vector3.zero);
            var found = false;
            if (replayRenderers.TryGetValue(target, out var renderers))
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                        !renderer.gameObject.activeInHierarchy || renderer.bounds.size.sqrMagnitude < 0.001f) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            return CanCctvSeePoint(camera, target, bounds.center) ||
                CanCctvSeePoint(camera, target, bounds.center + Vector3.up * bounds.extents.y * 0.6f);
        }

        private bool CanCctvSeePoint(HighlightCctvCamera camera, Transform target, Vector3 point)
        {
            var local = camera.transform.InverseTransformPoint(point);
            if (local.z <= 0f) return false;
            var aspect = cameraTransform.TryGetComponent<Camera>(out var output) ? output.aspect : 16f / 9f;
            var halfHeight = local.z * Mathf.Tan(camera.FieldOfView * Mathf.Deg2Rad * 0.5f) * 0.9f;
            return Mathf.Abs(local.y) <= halfHeight && Mathf.Abs(local.x) <= halfHeight * aspect &&
                !IsCctvOccluded(camera.transform.position, point, target);
        }

        private float CctvScore(HighlightCctvCamera camera, Vector3 focus)
        {
            var direction = focus - camera.transform.position;
            var angle = Vector3.Angle(camera.transform.forward, direction);
            var score = CctvCompositionScore(direction) - angle * 0.01f;
            if (!CanCctvSee(camera, cctvTarget)) score -= 600f;
            if (supportingPlayer != null && supportingPlayer != cctvTarget &&
                !CanCctvSee(camera, supportingPlayer)) score -= 300f;
            return score;
        }
    }
}
