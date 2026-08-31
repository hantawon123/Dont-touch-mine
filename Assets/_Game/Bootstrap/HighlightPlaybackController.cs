using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Client.Players;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using Game.SOAP.Config;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    public sealed class HighlightPlaybackController : ITickable
    {
        private readonly MatchSessionCoordinator session;
        private readonly HighlightReplayPlayer replayPlayer;
        private readonly HighlightCameraDirector cameraDirector;

        public HighlightPlaybackController(
            MatchSessionCoordinator session,
            HighlightReplayPlayer replayPlayer,
            HighlightCameraDirector cameraDirector)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.replayPlayer = replayPlayer ?? throw new ArgumentNullException(nameof(replayPlayer));
            this.cameraDirector = cameraDirector ??
                throw new ArgumentNullException(nameof(cameraDirector));
        }

        public bool IsPlaying => replayPlayer.IsPlaying;

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (!replayPlayer.IsPlaying && !TryStartCurrent())
            {
                return;
            }

            cameraDirector.Tick(deltaSeconds);
            if (replayPlayer.Advance(deltaSeconds))
            {
                return;
            }

            cameraDirector.ClearOccluders();
            session.CompleteCurrentHighlight();
            TryStartCurrent();
        }

        private bool TryStartCurrent()
        {
            for (var attempt = 0; attempt < Game.SOAP.Config.MatchRulesSO.MaxHighlightCount; attempt++)
            {
                if (!session.TryGetCurrentHighlight(out var highlight) ||
                    !session.TryCaptureCurrentHighlightReplay(out var clips))
                {
                    return false;
                }

                cameraDirector.Focus(highlight);
                if (replayPlayer.Start(clips))
                {
                    return true;
                }

                session.CompleteCurrentHighlight();
            }

            return false;
        }
    }

    public sealed class HighlightRuntimeFactory
    {
        public HighlightPlaybackController Create(
            MatchRuntimeComposition match,
            IReadOnlyList<Transform> playerTargets,
            IReadOnlyList<SceneWorldObjectReference> objectTargets,
            Transform cameraTransform,
            Transform fallbackTransform,
            int collisionLayerMask = 0)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (playerTargets == null)
            {
                throw new ArgumentNullException(nameof(playerTargets));
            }

            if (playerTargets.Count != match.Session.Players.Players.Count)
            {
                throw new InvalidOperationException(
                    "Highlight player targets must match the active session player count.");
            }

            var replayPlayer = new HighlightReplayPlayer(playerTargets, objectTargets);
            var cameraDirector = new HighlightCameraDirector(
                cameraTransform,
                fallbackTransform,
                playerTargets,
                objectTargets,
                collisionLayerMask: collisionLayerMask);

            return new HighlightPlaybackController(
                match.Session,
                replayPlayer,
                cameraDirector);
        }
    }

    /// <summary>
    /// Plays the authority-recorded replay on every peer. The local-only
    /// HighlightPlaybackController above remains available for isolated tests.
    /// </summary>
    public sealed class NetworkHighlightPlaybackController :
        IStartable,
        ITickable,
        IDisposable
    {
        private readonly INetworkMatchEvents network;
        private readonly RoomBrowserSystem room;
        private readonly INetworkMatchRuntimeSource clock;
        private readonly IHighlightTransitionView transition;
        private double highlightEndsAt;
        private double gameEndNoticeEndsAt = double.PositiveInfinity;
        private double appliedBodyTime;
        private bool readinessConfirmed;
        public double? PlaybackSourceTime { get; private set; }
        private int lastWarnedIndex = -1;
        private IReadOnlyList<HighlightReplayData> replay =
            Array.Empty<HighlightReplayData>();
        private HighlightReplayPlayer replayPlayer;
        private HighlightCameraDirector cameraDirector;
        private INetworkMatchHudView hud;
        private PlayerCameraController cameraRig;
        private GameObject fallbackObject;
        private MatchPhase phase = MatchPhase.Waiting;
        private int replayIndex;
        private readonly Dictionary<string, ReplayVisual> playerVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ReplayVisual> itemVisuals = new(StringComparer.Ordinal);

        public NetworkHighlightPlaybackController(
            INetworkMatchEvents network,
            RoomBrowserSystem room,
            INetworkMatchRuntimeSource clock,
            IHighlightTransitionView transition)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        public void Start()
        {
            hud = UnityEngine.Object.FindFirstObjectByType<NetworkMatchHudView>(
                FindObjectsInactive.Include);
            network.MatchStateReceived += OnMatchStateReceived;
            network.MatchResultReceived += OnMatchResultReceived;
            network.HighlightReplayReceived += OnHighlightReplayReceived;
            CaptureVisuals();
        }

        public void Dispose()
        {
            network.MatchStateReceived -= OnMatchStateReceived;
            network.MatchResultReceived -= OnMatchResultReceived;
            network.HighlightReplayReceived -= OnHighlightReplayReceived;
            StopPlayback();
            foreach (var visual in playerVisuals.Values) visual.Dispose();
            foreach (var visual in itemVisuals.Values) visual.Dispose();
            playerVisuals.Clear();
            itemVisuals.Clear();
            if (fallbackObject != null)
            {
                UnityEngine.Object.Destroy(fallbackObject);
            }
        }

        public void Tick()
        {
            if (phase != MatchPhase.Highlight)
            {
                CaptureVisuals();
                return;
            }

            // Keep the live camera visible for the end announcement/post-roll.
            if (!clock.IsRuntimeReady) return;
            if (clock.ServerTime < gameEndNoticeEndsAt)
            {
                transition.SetOpacity(0f);
                return;
            }

            var totalDuration = 0d;
            foreach (var data in replay)
                totalDuration += data.Candidate.PlaybackDurationSeconds + HighlightPresentationTiming.OverheadSeconds;
            // Prepare behind the black transition, then acknowledge this transfer.
            // The authority does not start the shared timeline until every peer is ready.
            if (!readinessConfirmed && replay.Count > 0)
            {
                CaptureVisuals();
                if (replayPlayer == null && !TryCreateScenePlayback(replay[0]))
                {
                    PlaybackSourceTime = null;
                    if (lastWarnedIndex != 0)
                    {
                        Debug.LogWarning("[Highlight] Waiting for player visuals and output camera before acknowledging readiness.");
                        lastWarnedIndex = 0;
                    }
                    transition.SetOpacity(1f);
                    return;
                }
                readinessConfirmed = network is INetworkHighlightReady ready && ready.TryConfirmHighlightReady();
            }
            var elapsed = clock.ServerTime - (highlightEndsAt - totalDuration);
            if (highlightEndsAt <= 0d || elapsed < 0d || replay.Count == 0)
            {
                PlaybackSourceTime = null;
                transition.SetOpacity(1f);
                return;
            }
            var index = 0;
            while (index < replay.Count && elapsed >= replay[index].Candidate.PlaybackDurationSeconds +
                       HighlightPresentationTiming.OverheadSeconds)
            {
                elapsed -= replay[index].Candidate.PlaybackDurationSeconds + HighlightPresentationTiming.OverheadSeconds;
                index++;
            }
            if (index >= replay.Count)
            {
                PlaybackSourceTime = null;
                transition.SetOpacity(1f);
                hud?.SetHighlightTitle(null);
                return;
            }
            if (replayPlayer == null || replayIndex != index)
            {
                cameraDirector?.ClearOccluders();
                replayPlayer = null;
                replayIndex = index;
                appliedBodyTime = 0d;
                if (!TryCreateScenePlayback(replay[index]))
                {
                    if (lastWarnedIndex != index)
                    {
                        Debug.LogWarning($"[Highlight] Cannot prepare {replay[index].Candidate.Type}: check replay frames, player visuals and output camera.");
                        lastWarnedIndex = index;
                    }
                    transition.SetOpacity(1f);
                    return;
                }
            }
            var duration = replay[index].Candidate.PlaybackDurationSeconds;
            var bodyTime = HighlightPresentationTiming.BodyTime(elapsed, duration);
            if (bodyTime < appliedBodyTime)
            {
                replayPlayer.Start(replay[index].Clips);
                appliedBodyTime = 0d;
            }
            var previousClip = replayPlayer.CurrentClipIndex;
            replayPlayer.Advance(Math.Max(0d, bodyTime - appliedBodyTime));
            appliedBodyTime = bodyTime;
            PlaybackSourceTime = bodyTime > 0d ? replayPlayer.SourceTime : null;
            if (previousClip != replayPlayer.CurrentClipIndex && replayPlayer.IsPlaying &&
                replay[index].Candidate.Type != HighlightType.LongestHidden)
                cameraDirector.Focus(replay[index].Candidate);
            cameraDirector.Tick(Time.unscaledDeltaTime);
            transition.SetOpacity(HighlightPresentationTiming.Opacity(elapsed, duration));
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            // A same-phase update schedules playback after the readiness barrier.
            if (snapshot.Phase == MatchPhase.Highlight) highlightEndsAt = snapshot.PhaseEndsAt;
            if (phase == snapshot.Phase) return;
            phase = snapshot.Phase;
            if (phase == MatchPhase.Highlight)
            {
                replayIndex = 0;
                transition.SetOpacity(!clock.IsRuntimeReady || clock.ServerTime < gameEndNoticeEndsAt ? 0f : 1f);
                return;
            }

            StopPlayback();
            if (phase == MatchPhase.Waiting || phase == MatchPhase.Hiding)
            {
                replay = Array.Empty<HighlightReplayData>();
                gameEndNoticeEndsAt = double.PositiveInfinity;
            }
        }

        private void OnMatchResultReceived(MatchResult result) =>
            gameEndNoticeEndsAt = result.EndedAt + HighlightPresentationTiming.PostRollSeconds;

        private void OnHighlightReplayReceived(IReadOnlyList<HighlightReplayData> received)
        {
            replay = received ?? Array.Empty<HighlightReplayData>();
            readinessConfirmed = false;
            PlaybackSourceTime = null;
            replayIndex = 0;
            lastWarnedIndex = -1;
            replayPlayer = null;
            cameraDirector?.ClearOccluders();
            cameraDirector = null;
            hud?.SetHighlightTitle(null);
        }

        private bool TryCreateScenePlayback(HighlightReplayData current)
        {
            var playerCount = GetRecordedPlayerCount(current.Clips);
            if (playerCount == 0)
            {
                return false;
            }

            cameraRig ??= UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>(
                FindObjectsInactive.Include);
            if (cameraRig == null)
            {
                return false;
            }

            var playerTargets = CapturePlayerTargets(playerCount);
            for (var index = 0; index < playerTargets.Length; index++)
            {
                if (playerTargets[index] == null)
                {
                    return false;
                }
            }

            var objectTargets = CaptureObjectTargets();
            foreach (var visual in playerVisuals.Values) visual.SetPlaying(true);
            foreach (var visual in itemVisuals.Values) visual.SetPlaying(true);
            var output = cameraRig.BeginReplay();
            if (output == null)
            {
                StopPlayback();
                return false;
            }
            var candidatePlayer = new HighlightReplayPlayer(playerTargets, objectTargets);
            if (!candidatePlayer.Start(current.Clips))
            {
                StopPlayback();
                return false;
            }

            if (fallbackObject == null)
            {
                fallbackObject = new GameObject("[Highlight Camera Fallback]");
            }

            fallbackObject.transform.SetPositionAndRotation(
                output.position,
                output.rotation);
            replayPlayer = candidatePlayer;
            cameraDirector = new HighlightCameraDirector(
                output,
                fallbackObject.transform,
                playerTargets,
                objectTargets);
            cameraDirector.Focus(current.Candidate);
            hud?.SetHighlightTitle(TitleOf(current.Candidate.Type));
            return true;
        }

        internal static string TitleOf(HighlightType type) => type switch
        {
            HighlightType.FirstBlood => "FIRST BLOOD",
            HighlightType.TteTanMulgun => "HOT ITEM",
            HighlightType.FinalMoment => "FINAL MOMENT",
            HighlightType.LongestHidden => "LONGEST HIDDEN",
            HighlightType.MostStunned => "MOST STUNNED",
            _ => type.ToString().ToUpperInvariant(),
        };

        private Transform[] CapturePlayerTargets(int playerCount)
        {
            var targets = new Transform[playerCount];
            var participants = room.MatchParticipants.CurrentValue;
            foreach (var participant in participants)
            {
                if (participant.PlayerIndex >= 0 && participant.PlayerIndex < targets.Length &&
                    playerVisuals.TryGetValue(participant.PlayerId, out var visual))
                    targets[participant.PlayerIndex] = visual.Target;
            }

            return targets;
        }

        private SceneWorldObjectReference[] CaptureObjectTargets()
        {
            var references = new List<SceneWorldObjectReference>();
            foreach (var pair in itemVisuals)
                references.Add(new SceneWorldObjectReference(pair.Key, pair.Value.Target));
            return references.ToArray();
        }

        private void CaptureVisuals()
        {
            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var id = avatar.PlayerId;
                if (id == null) continue;
                if (!playerVisuals.ContainsKey(id))
                    playerVisuals.Add(id, new ReplayVisual(avatar.transform, null));
            }
            foreach (var item in UnityEngine.Object.FindObjectsByType<CarryableItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!itemVisuals.ContainsKey(item.ObjectId))
                    itemVisuals.Add(item.ObjectId, new ReplayVisual(item.transform, null));
            }
        }

        private static int GetRecordedPlayerCount(
            IReadOnlyList<HighlightReplayClip> clips)
        {
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var frames = clips[clipIndex].Frames;
                if (frames.Count > 0)
                {
                    return frames[0].PlayerPoses.Count;
                }
            }

            return 0;
        }

        private void StopPlayback()
        {
            PlaybackSourceTime = null;
            readinessConfirmed = false;
            replayPlayer = null;
            cameraDirector?.ClearOccluders();
            cameraDirector = null;
            hud?.SetHighlightTitle(null);
            foreach (var visual in playerVisuals.Values) visual.SetPlaying(false);
            foreach (var visual in itemVisuals.Values) visual.SetPlaying(false);
            cameraRig?.EndReplay();
            transition.SetOpacity(phase == MatchPhase.Result ? 1f : 0f);
        }
    }

}
