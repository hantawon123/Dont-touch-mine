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
        private IReadOnlyList<HighlightReplayData> replay =
            Array.Empty<HighlightReplayData>();
        private HighlightReplayPlayer replayPlayer;
        private HighlightCameraDirector cameraDirector;
        private INetworkMatchHudView hud;
        private PlayerCameraController cameraRig;
        private GameObject fallbackObject;
        private MatchPhase phase = MatchPhase.Waiting;
        private int replayIndex;
        private bool cameraWasEnabled;
        private bool cameraOverridden;
        private readonly Dictionary<string, ReplayVisual> playerVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ReplayVisual> itemVisuals = new(StringComparer.Ordinal);

        public NetworkHighlightPlaybackController(
            INetworkMatchEvents network,
            RoomBrowserSystem room)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public void Start()
        {
            hud = UnityEngine.Object.FindFirstObjectByType<NetworkMatchHudView>(
                FindObjectsInactive.Include);
            network.MatchStateReceived += OnMatchStateReceived;
            network.HighlightReplayReceived += OnHighlightReplayReceived;
            CaptureVisuals();
        }

        public void Dispose()
        {
            network.MatchStateReceived -= OnMatchStateReceived;
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

            if (replayPlayer == null && !TryStartNext())
            {
                return;
            }

            var playing = replayPlayer.Advance(Time.unscaledDeltaTime);
            cameraDirector.Tick(Time.unscaledDeltaTime);
            if (playing)
            {
                return;
            }

            replayPlayer = null;
            cameraDirector?.ClearOccluders();
            cameraDirector = null;
            replayIndex++;
            if (!TryStartNext())
            {
                hud?.SetHighlightTitle(null);
            }
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            if (phase == snapshot.Phase) return;
            phase = snapshot.Phase;
            if (phase == MatchPhase.Highlight)
            {
                replayIndex = 0;
                TryStartNext();
                return;
            }

            StopPlayback();
            if (phase == MatchPhase.Waiting || phase == MatchPhase.Hiding)
            {
                replay = Array.Empty<HighlightReplayData>();
            }
        }

        private void OnHighlightReplayReceived(IReadOnlyList<HighlightReplayData> received)
        {
            replay = received ?? Array.Empty<HighlightReplayData>();
            replayIndex = 0;
            replayPlayer = null;
            cameraDirector?.ClearOccluders();
            cameraDirector = null;
            hud?.SetHighlightTitle(null);
            if (phase == MatchPhase.Highlight)
            {
                TryStartNext();
            }
        }

        private bool TryStartNext()
        {
            while (phase == MatchPhase.Highlight && replayIndex < replay.Count)
            {
                var current = replay[replayIndex];
                if (TryCreateScenePlayback(current))
                {
                    return true;
                }

                replayIndex++;
            }

            return false;
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
                Debug.LogError("[Highlight] No output camera is available.");
                return false;
            }
            var candidatePlayer = new HighlightReplayPlayer(playerTargets, objectTargets);
            if (!candidatePlayer.Start(current.Clips))
            {
                return false;
            }

            if (fallbackObject == null)
            {
                fallbackObject = new GameObject("[Highlight Camera Fallback]");
            }

            fallbackObject.transform.SetPositionAndRotation(
                output.position,
                output.rotation);
            if (!cameraOverridden)
            {
                cameraWasEnabled = cameraRig.enabled;
                cameraRig.enabled = false;
                cameraOverridden = true;
            }

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
            foreach (var avatar in UnityEngine.Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                var id = PlayerRegistry.IdOf(avatar.Owner);
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
            replayPlayer = null;
            cameraDirector?.ClearOccluders();
            cameraDirector = null;
            hud?.SetHighlightTitle(null);
            foreach (var visual in playerVisuals.Values) visual.SetPlaying(false);
            foreach (var visual in itemVisuals.Values) visual.SetPlaying(false);
            cameraRig?.EndReplay();
            if (cameraRig != null && cameraOverridden)
            {
                cameraOverridden = false;
            }
        }
    }

    /// <summary>Shows the result phase briefly, then returns the whole room to its lobby.</summary>
    public sealed class NetworkResultLobbyReturnController :
        IStartable,
        ITickable,
        IDisposable
    {
        private const double ResultDisplaySeconds = 5d;
        private readonly INetworkMatchEvents events;
        private readonly NetworkRunnerService network;
        private double returnAt = -1d;

        public NetworkResultLobbyReturnController(
            INetworkMatchEvents events,
            NetworkRunnerService network)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public void Start() => events.MatchStateReceived += OnMatchStateReceived;

        public void Dispose() => events.MatchStateReceived -= OnMatchStateReceived;

        public void Tick()
        {
            if (returnAt < 0d || Time.unscaledTimeAsDouble < returnAt || !network.IsServer)
            {
                return;
            }

            if (network.RequestReturnToLobby())
            {
                returnAt = -1d;
            }
        }

        private void OnMatchStateReceived(MatchStateSnapshot snapshot)
        {
            returnAt = snapshot.Phase == MatchPhase.Result && network.IsServer
                ? Time.unscaledTimeAsDouble + ResultDisplaySeconds
                : -1d;
        }
    }
}
