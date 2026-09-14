using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Client.Players;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Rooms;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Match;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private Game.Core.Settings.InterfacePresentation presentation;
        [VContainer.Inject]
        public void BindPresentation(Game.Core.Settings.InterfacePresentation value) => presentation = value;
        private const double TimelineEpsilonSeconds = 0.000001d;

        private readonly INetworkMatchEvents network;
        private readonly RoomBrowserSystem room;
        private readonly INetworkMatchRuntimeSource clock;
        private readonly IHighlightTransitionView transition;
        private double highlightEndsAt;
        private double gameEndNoticeEndsAt = double.PositiveInfinity;
        private double matchEndedAt = double.NaN;
        private double localSkipOffset;
        private double appliedBodyTime;
        private bool readinessConfirmed;
        private bool skippedAll;
        private bool localViewingCompletionStarted;
        private bool localViewingComplete;
        public double? PlaybackSourceTime { get; private set; }
        private int lastWarnedIndex = -1;
        private IReadOnlyList<HighlightReplayData> replay =
            Array.Empty<HighlightReplayData>();
        private HighlightReplayPlayer replayPlayer;
        private HighlightCameraDirector cameraDirector;
        private INetworkMatchHudView hud;
        private bool disposed;
        private bool sceneBound;
        private bool HasLiveHud => hud != null &&
            !(hud is UnityEngine.Object target && target == null);
        private float[] highlightBarFills = Array.Empty<float>();
        private double[] highlightClipDurations = Array.Empty<double>();
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

        private IMatchRuntimeContext sceneContext;
        private IReadOnlyList<SceneHighlightOcclusionReference> sceneOcclusionGroups;
        private readonly HashSet<string> recordedObjectIds = new(StringComparer.Ordinal);

        public void BindScene(UnityEngine.SceneManagement.Scene scene, IMatchRuntimeContext context)
        {
            sceneContext = context;
            sceneBound = true;
            hud = null;
            var groups = new List<SceneHighlightOcclusionReference>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (hud == null) hud = root.GetComponentInChildren<NetworkMatchHudView>(true);
                foreach (var config in root.GetComponentsInChildren<MatchSceneConfiguration>(true))
                    groups.AddRange(config.HighlightOcclusionGroups);
            }
            sceneOcclusionGroups = groups;
        }

        public void Start()
        {
            if (disposed) return;
            if (!sceneBound) hud = UnityEngine.Object.FindFirstObjectByType<NetworkMatchHudView>(
                FindObjectsInactive.Include);
            network.MatchStateReceived += OnMatchStateReceived;
            network.MatchResultReceived += OnMatchResultReceived;
            network.HighlightReplayReceived += OnHighlightReplayReceived;
            CaptureVisuals();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            hud = null;
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

        public bool SkipCurrent()
        {
            if (!TryGetLocalPlaybackPosition(out _, out var remaining)) return false;
            MatchTransitionDiagnostics.Dump("skip-current");
            localSkipOffset += remaining;
            return true;
        }

        public bool SkipAll()
        {
            if (phase != MatchPhase.Highlight || !clock.IsRuntimeReady ||
                clock.ServerTime < gameEndNoticeEndsAt || replay.Count == 0 || skippedAll)
            {
                return false;
            }

            MatchTransitionDiagnostics.Dump("skip-all-before-stop");
            skippedAll = true;
            var wasReady = readinessConfirmed;
            StopPlayback();
            readinessConfirmed = wasReady;
            TryFinishLocalViewing();
            MatchTransitionDiagnostics.Dump("skip-all-after-stop");
            return true;
        }

        public void Tick()
        {
            if (disposed) return;
            if (phase != MatchPhase.Highlight)
            {
                CaptureVisuals();
                return;
            }

            // Fade the live camera out, then let Result fade in over it.
            if (!clock.IsRuntimeReady) return;
            if (network is INetworkResultNavigation { IsResultSceneLoaded: true })
            {
                HideHighlightHud();
                return;
            }
            if (clock.ServerTime < gameEndNoticeEndsAt)
            {
                HideHighlightHud();
                var fadeElapsed = double.IsNaN(matchEndedAt)
                    ? 0d
                    : clock.ServerTime - matchEndedAt;
                transition.SetOpacity(HighlightPresentationTiming.MatchEndFadeOutOpacity(fadeElapsed));
                return;
            }
            var keyboard = Keyboard.current;
            var shortcutPressed = keyboard != null &&
                (keyboard.tabKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            var acceptsShortcut = shortcutPressed && !IsTextInputFocused();
            if (acceptsShortcut && keyboard.tabKey.wasPressedThisFrame) SkipAll();
            if (skippedAll)
            {
                HideHighlightHud();
                if (!readinessConfirmed && network is INetworkHighlightReady skippedReady)
                    readinessConfirmed = skippedReady.TryConfirmHighlightReady();
                TryFinishLocalViewing();
                return;
            }

            var totalDuration = TotalDuration();
            // Prepare behind the black transition, then acknowledge this transfer.
            // The authority does not start the shared timeline until every peer is ready.
            if (!readinessConfirmed && replay.Count == 0)
            {
                readinessConfirmed = network is INetworkHighlightReady emptyReady &&
                                     emptyReady.TryConfirmHighlightReady();
                PlaybackSourceTime = null;
                transition.SetOpacity(1f);
                PublishHighlightHud(-1, 0d);
                return;
            }
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
                    PublishHighlightHud(0, 0d);
                    return;
                }
                readinessConfirmed = network is INetworkHighlightReady ready && ready.TryConfirmHighlightReady();
            }
            if (acceptsShortcut && keyboard.spaceKey.wasPressedThisFrame) SkipCurrent();

            var elapsed = clock.ServerTime - (highlightEndsAt - totalDuration) + localSkipOffset;
            if (highlightEndsAt <= 0d || elapsed < -TimelineEpsilonSeconds || replay.Count == 0)
            {
                PlaybackSourceTime = null;
                transition.SetOpacity(1f);
                PublishHighlightHud(-1, 0d);
                return;
            }
            elapsed = Math.Max(0d, elapsed);
            var index = 0;
            while (index < replay.Count && elapsed >= replay[index].Candidate.PlaybackDurationSeconds +
                       HighlightPresentationTiming.OverheadSeconds)
            {
                elapsed -= replay[index].Candidate.PlaybackDurationSeconds + HighlightPresentationTiming.OverheadSeconds;
                index++;
            }
            if (index >= replay.Count)
            {
                skippedAll = true;
                StopPlayback();
                TryFinishLocalViewing();
                return;
            }
            if (replayPlayer == null || replayIndex != index)
            {
                var changedHighlight = replayPlayer != null && replayIndex != index;
                // Dispose the previous high-priority replay camera before replacing its owner.
                cameraDirector?.Dispose();
                cameraDirector = null;
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
                    PublishHighlightHud(index, elapsed);
                    return;
                }
                if (changedHighlight)
                {
                    PlaybackSourceTime = null;
                    transition.SetOpacity(1f);
                    PublishHighlightHud(index, elapsed);
                    return;
                }
            }
            var duration = replay[index].Candidate.PlaybackDurationSeconds;
            var bodyTime = HighlightPresentationTiming.BodyTime(elapsed, duration);
            var playbackTime = HighlightPlaybackPacing.Map(replay[index].Candidate, bodyTime);
            if (playbackTime < appliedBodyTime)
            {
                replayPlayer.Start(replay[index].Clips);
                appliedBodyTime = 0d;
            }
            replayPlayer.Advance(Math.Max(0d, playbackTime - appliedBodyTime));
            appliedBodyTime = playbackTime;
            PlaybackSourceTime = playbackTime > 0d ? replayPlayer.SourceTime : null;
            PublishHighlightHud(index, elapsed);
            cameraDirector.SetPlaybackTime(playbackTime);
            cameraDirector.Tick(Time.unscaledDeltaTime);
            transition.SetOpacity(Mathf.Max(
                HighlightPresentationTiming.Opacity(elapsed, duration),
                HighlightReplayPlayer.CutOpacity(replay[index].Clips, playbackTime)));
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            if (disposed) return;
            // A same-phase update schedules playback after the readiness barrier.
            if (snapshot.Phase == MatchPhase.Highlight) highlightEndsAt = snapshot.PhaseEndsAt;
            if (phase == snapshot.Phase) return;
            var previousPhase = phase;
            phase = snapshot.Phase;
            if (phase == MatchPhase.Highlight)
            {
                replayIndex = 0;
                localSkipOffset = 0d;
                skippedAll = false;
                localViewingCompletionStarted = false;
                localViewingComplete = false;
                transition.SetOpacity(!clock.IsRuntimeReady || clock.ServerTime < gameEndNoticeEndsAt ? 0f : 1f);
                return;
            }

            // The authority can end the shared timeline before this client's
            // final presentation Tick. Cover the live camera before restoring it.
            if (previousPhase == MatchPhase.Highlight && !skippedAll)
                transition.SetOpacity(1f);
            StopPlayback();
            if (phase == MatchPhase.Hiding) transition.SetOpacity(0f);
            if (phase == MatchPhase.Waiting || phase == MatchPhase.Hiding)
            {
                replay = Array.Empty<HighlightReplayData>();
                gameEndNoticeEndsAt = double.PositiveInfinity;
                matchEndedAt = double.NaN;
            }
        }

        private void OnMatchResultReceived(MatchResult result)
        {
            if (disposed) return;
            matchEndedAt = result.EndedAt;
            gameEndNoticeEndsAt = result.EndedAt + HighlightPresentationTiming.FadeSeconds;
        }

        private void OnHighlightReplayReceived(IReadOnlyList<HighlightReplayData> received)
        {
            if (disposed) return;
            replay = received ?? Array.Empty<HighlightReplayData>();
            readinessConfirmed = false;
            PlaybackSourceTime = null;
            replayIndex = 0;
            localSkipOffset = 0d;
            skippedAll = false;
            localViewingCompletionStarted = false;
            localViewingComplete = false;
            lastWarnedIndex = -1;
            replayPlayer = null;
            cameraDirector?.Dispose();
            cameraDirector = null;
            HideHighlightHud();
        }

        private double TotalDuration()
        {
            var total = 0d;
            foreach (var data in replay)
                total += data.Candidate.PlaybackDurationSeconds +
                    HighlightPresentationTiming.OverheadSeconds;
            return total;
        }

        private bool TryGetLocalPlaybackPosition(out int index, out double remaining)
        {
            index = -1;
            remaining = 0d;
            if (phase != MatchPhase.Highlight || skippedAll || !clock.IsRuntimeReady ||
                clock.ServerTime < gameEndNoticeEndsAt || highlightEndsAt <= 0d || replay.Count == 0)
            {
                return false;
            }

            var elapsed = clock.ServerTime - (highlightEndsAt - TotalDuration()) + localSkipOffset;
            if (elapsed < -TimelineEpsilonSeconds) return false;
            elapsed = Math.Max(0d, elapsed);
            for (var replayPosition = 0; replayPosition < replay.Count; replayPosition++)
            {
                var duration = replay[replayPosition].Candidate.PlaybackDurationSeconds +
                    HighlightPresentationTiming.OverheadSeconds;
                if (elapsed < duration)
                {
                    index = replayPosition;
                    remaining = duration - elapsed;
                    return true;
                }

                elapsed -= duration;
            }

            return false;
        }

        private static bool IsTextInputFocused()
        {
            var chat = UnityEngine.Object.FindFirstObjectByType<MatchChatView>(
                FindObjectsInactive.Include);
            return chat != null && chat.IsInputFocused;
        }

        private bool TryFinishLocalViewing()
        {
            if (localViewingComplete) return true;

            // Claim the cover once before publishing completion. The client can
            // receive its completion acknowledgement between Update methods;
            // after this point LobbyLifetimeScope alone owns the cover handoff.
            if (!localViewingCompletionStarted)
            {
                transition.SetOpacity(1f);
                localViewingCompletionStarted = true;
            }
            localViewingComplete = network is INetworkResultNavigation navigation &&
                                   navigation.CompleteLocalHighlightViewing();
            return localViewingComplete;
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
                objectTargets,
                occlusionGroups: sceneOcclusionGroups);
            cameraDirector.Focus(current.Candidate);
            Debug.Log($"[Highlight] Playback ready: type={current.Candidate.Type}, players={playerTargets.Length}, objects={objectTargets.Length}, camera={output.name}.");
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

        internal static string TitleOf(
            HighlightCandidate candidate,
            IReadOnlyList<MatchParticipant> matchParticipants,
            IReadOnlyList<RoomParticipant> roomParticipants, Game.Core.Settings.InterfacePresentation presentation = null)
        {
            var title = TitleOf(candidate.Type);
            if (candidate.ActorPlayerIndex < 0 ||
                matchParticipants == null ||
                roomParticipants == null)
            {
                return title;
            }

            string playerId = null;
            foreach (var participant in matchParticipants)
            {
                if (participant.PlayerIndex != candidate.ActorPlayerIndex) continue;
                playerId = participant.PlayerId;
                break;
            }

            if (playerId == null) return title;
            foreach (var participant in roomParticipants)
            {
                if (!string.Equals(participant.PlayerId, playerId, StringComparison.Ordinal))
                    continue;
                var displayName = string.IsNullOrWhiteSpace(participant.Nickname)
                    ? participant.PlayerId
                    : participant.Nickname;
                if (presentation != null) displayName = presentation.Name(participant.PlayerId, displayName);
                return string.IsNullOrEmpty(displayName) ? title : $"{title} : {displayName}";
            }

            return title;
        }

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
            if (phase == MatchPhase.Waiting) return;
            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var id = avatar.PlayerId;
                if (id == null) continue;
                if (!playerVisuals.ContainsKey(id))
                    playerVisuals.Add(id, new ReplayVisual(avatar.transform, null));
            }
            // A large map can contain thousands of products, but only recorded objects need copies.
            recordedObjectIds.Clear();
            if (sceneContext != null)
                foreach (var state in sceneContext.ReplayObjects) recordedObjectIds.Add(state.ObjectId);
            foreach (var data in replay)
                foreach (var clip in data.Clips)
                    foreach (var frame in clip.Frames)
                        foreach (var state in frame.WorldObjects) recordedObjectIds.Add(state.ObjectId);
            var lobbies = UnityEngine.Object.FindObjectsByType<LobbyLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in UnityEngine.Object.FindObjectsByType<CarryableItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var belongsToLobby = false;
                foreach (var lobby in lobbies) if (lobby.OwnsItem(item)) { belongsToLobby = true; break; }
                if (belongsToLobby) continue;
                if (sceneContext != null && !item.IsPlayerItem && !recordedObjectIds.Contains(item.ObjectId)) continue;
                if (!itemVisuals.ContainsKey(item.ObjectId))
                    itemVisuals.Add(item.ObjectId, new ReplayVisual(item.transform, null));
            }
        }

        private void HideHighlightHud()
        {
            if (HasLiveHud) hud.SetHighlightHud(false, null, Array.Empty<float>());
        }

        private void PublishHighlightHud(int index, double clipElapsed)
        {
            if (!HasLiveHud)
            {
                return;
            }

            if (highlightClipDurations.Length != replay.Count)
            {
                highlightClipDurations = replay.Count == 0
                    ? Array.Empty<double>()
                    : new double[replay.Count];
            }

            for (var clipIndex = 0; clipIndex < replay.Count; clipIndex++)
            {
                highlightClipDurations[clipIndex] =
                    replay[clipIndex].Candidate.PlaybackDurationSeconds +
                    HighlightPresentationTiming.OverheadSeconds;
            }

            var visibleCount = HighlightHudView.VisibleBarCount(replay.Count);
            if (highlightBarFills.Length != visibleCount)
            {
                highlightBarFills = visibleCount == 0
                    ? Array.Empty<float>()
                    : new float[visibleCount];
            }

            HighlightHudView.WriteFills(
                highlightBarFills,
                highlightClipDurations,
                index,
                clipElapsed);
            var subtitle = index >= 0 && index < replay.Count
                ? TitleOf(
                    replay[index].Candidate,
                    room.MatchParticipants.CurrentValue,
                    room.Participants.CurrentValue,
                    presentation)
                : null;
            hud.SetHighlightHud(true, subtitle, highlightBarFills);
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
            cameraDirector?.Dispose();
            cameraDirector = null;
            HideHighlightHud();
            foreach (var visual in playerVisuals.Values) visual.SetPlaying(false);
            foreach (var visual in itemVisuals.Values) visual.SetPlaying(false);
            if (cameraRig != null) cameraRig.EndReplay();
        }
    }

}
