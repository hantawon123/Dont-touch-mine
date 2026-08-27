using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Items;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
using Game.Server.Match;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// Decides whether a match may start, and reports the confirmed line-up.
    /// </summary>
    /// <remarks>
    /// Lives on the runner object for the same reason the roster does: the
    /// networked object that receives the request is spawned by Fusion and
    /// cannot be injected.
    /// <para>
    /// Every check runs on the authority. A client asking is only a request, so
    /// a peer that wrongly believes it is the host cannot start a match by
    /// skipping its own check.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MatchStarter : MonoBehaviour
    {
        private static readonly Vector3 ShredderEjectionLocalVelocity =
            new(0f, 1.5f, 4f);

        private readonly List<RoomParticipant> _room = new List<RoomParticipant>();
        private readonly List<MatchParticipant> _playing = new List<MatchParticipant>();
        private readonly InteractionAuthorityRules _interactionRules =
            new InteractionAuthorityRules();

        private IMatchStartSink _sink;
        private PlayerRoster _roster;

        /// <summary>
        /// Takes the room into the map once a match is confirmed. Held as an
        /// interface so that this stays a judge of whether a match may start and
        /// never learns what a scene is.
        /// </summary>
        private IMatchSceneDirector _sceneDirector;

        /// <summary>
        /// The room's object, remembered as it reports itself. Anything wanting
        /// to ask for a match needs a handle on it, and only this peer's copy of
        /// it can carry the request.
        /// </summary>
        private MatchSessionState _state;
        private MatchSessionCoordinator _session;
        private Pose _shredderEjectionPose;
        private bool _hasShredderEjectionPose;
        private bool _returningToLobby;

        public event Action<MatchStateSnapshot> MatchStateReceived;
        public event Action<LobbyChatMessage> LobbyChatReceived;
        public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;
        public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        public event Action<PlayerStunnedEvent> PlayerStunnedReceived;
        public event Action<ObjectThrownEvent> ObjectThrownReceived;
        public event Action<FinalWarningStartedEvent> FinalWarningReceived;
        public event Action<IReadOnlyList<bool>> ParticipantActivityReceived;
        public event Action<IReadOnlyList<PlayerInteractionStateSnapshot>>
            PlayerInteractionStatesReceived;
        public event Action<MatchResult> MatchResultReceived;
        public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        public event Action SimulationTick;

        public void Bind(
            IMatchStartSink sink,
            PlayerRoster roster,
            IMatchSceneDirector sceneDirector)
        {
            _sink = sink;
            _roster = roster;
            _sceneDirector = sceneDirector;
        }

        /// <summary>
        /// Starts a match if the room allows it.
        /// </summary>
        /// <remarks>
        /// Nothing crosses the network to ask. Only the authority can write the
        /// decision, so a peer that is not the authority has no way to start one
        /// and is told so without a round trip. When a client genuinely needs to
        /// ask the authority for something, that will need an RPC.
        /// </remarks>
        public void RequestStart(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning)
            {
                return;
            }

            if (!runner.IsServer)
            {
                Debug.Log("[Match] Only the host can start the match.");
                Refused(RoomStartResult.NotHost);
                return;
            }

            if (_state == null)
            {
                Debug.LogWarning("[Match] The room is not ready to start yet.");
                return;
            }

            var refusal = Evaluate(_state, runner);

            if (refusal != RoomStartResult.Started)
            {
                Debug.Log($"[Match] The match cannot start: {refusal}.");
                Refused(refusal);
                return;
            }

            var state = _state;

            var participants = MatchParticipant.FromRoomParticipants(_room);
            var participantIds = new string[participants.Length];
            for (var index = 0; index < participants.Length; index++)
            {
                participantIds[index] = participants[index].PlayerId;
            }

            state.Confirm(participantIds);
            Debug.Log($"[Match] Started with {participantIds.Length} players.");

            // After the line-up is frozen, not before: the map replaces this
            // scene, and a load that began first could tear down the objects
            // this method is still reading.
            _sceneDirector?.EnterMatchScene(runner);
        }

        /// <summary>
        /// Reports the room's current answer. Called on every peer as the
        /// decision replicates, so presentation does not have to ask.
        /// </summary>
        public void Publish(MatchSessionState state)
        {
            if (state == null)
            {
                return;
            }

            _state = state;

            _playing.Clear();

            if (state.IsStarted)
            {
                var count = Mathf.Min(state.ParticipantCount, MatchSessionState.MaxParticipants);

                for (var index = 0; index < count; index++)
                {
                    // The position in the replicated array is the playerIndex.
                    // Seat numbers are not used here: they are reused as people
                    // come and go and can leave gaps.
                    _playing.Add(new MatchParticipant(state.Participants.Get(index).ToString(), index));
                }
            }

            _sink?.MatchStarted(_playing);
            LineUpReceived?.Invoke(_playing);
        }

        public bool TryPublishSnapshot(MatchStateSnapshot snapshot)
        {
            return _state != null && _state.TrySetSnapshot(snapshot);
        }

        public void PublishSnapshot(MatchStateSnapshot snapshot)
        {
            MatchStateReceived?.Invoke(snapshot);
        }

        public bool TrySetPlayerControls(int playerIndex, bool enabled)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            return motor != null && motor.TrySetControlsEnabled(enabled);
        }

        public bool TryTeleportPlayer(int playerIndex, Pose pose)
        {
            if (!TryGetPlayingAvatar(playerIndex, out var avatar))
            {
                Debug.LogWarning(
                    $"[PlayerTeleport] No avatar mapped to playerIndex={playerIndex}, " +
                    $"target={pose.position}.");
                return false;
            }

            var motor = avatar.GetComponent<NetworkPlayerMotor>();
            var teleported = motor != null && motor.TryTeleport(pose);
            Debug.Log(
                $"[PlayerTeleport] playerIndex={playerIndex}, " +
                $"playerId={_playing[playerIndex].PlayerId}, " +
                $"avatarOwner={avatar.Owner}, target={pose.position}, " +
                $"success={teleported}.");
            return teleported;
        }

        public bool TryInitializeAssignedItems(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (_state == null || _session == null || assignments == null ||
                assignments.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var playerIndex = assignment.PlayerIndex;
                if (playerIndex < 0 ||
                    playerIndex >= _session.Assignments.Count ||
                    !string.Equals(
                        _session.Assignments[playerIndex].Item.ItemId,
                        assignment.Item.ItemId,
                        StringComparison.Ordinal) ||
                    !_state.CanHoldObject(assignment.Item.ItemId) ||
                    !_session.TryInitializeAssignedItem(playerIndex) ||
                    !_state.TrySetObjectHeld(assignment.Item.ItemId, playerIndex))
                {
                    return false;
                }
            }

            return true;
        }

        public void PublishObjectStates(IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            ObjectStatesReceived?.Invoke(states);
        }

        public void PublishItemDestroyed(PlayerItemDestroyedEvent confirmedEvent)
        {
            ItemDestroyedReceived?.Invoke(confirmedEvent);
        }

        public void PublishPlayerStunned(PlayerStunnedEvent confirmedEvent)
        {
            PlayerStunnedReceived?.Invoke(confirmedEvent);
        }

        public void PublishObjectThrown(ObjectThrownEvent confirmedEvent)
        {
            ObjectThrownReceived?.Invoke(confirmedEvent);
        }

        public void PublishFinalWarning(FinalWarningStartedEvent confirmedEvent)
        {
            FinalWarningReceived?.Invoke(confirmedEvent);
        }

        public void PublishParticipantActivity(IReadOnlyList<bool> active)
        {
            ParticipantActivityReceived?.Invoke(active);
        }

        public void PublishPlayerInteractionStates(
            IReadOnlyList<PlayerInteractionStateSnapshot> states)
        {
            PlayerInteractionStatesReceived?.Invoke(states);
        }

        public void PublishMatchResult(MatchResult result)
        {
            MatchResultReceived?.Invoke(result);
        }

        public void PublishSimulationTick()
        {
            SimulationTick?.Invoke();
        }

        public void BindSession(
            MatchSessionCoordinator session,
            Pose shredderEjectionPose)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            UnbindSession();
            _returningToLobby = false;
            _session = session;
            _session.PlayerItemDestroyed += OnPlayerItemDestroyed;
            _session.PlayerStunned += OnPlayerStunned;
            _session.ObjectThrown += OnObjectThrown;
            _session.ObjectAutoReleased += OnObjectAutoReleased;
            _session.MapObjectEjected += OnMapObjectEjected;
            _session.FinalWarningStarted += OnFinalWarningStarted;
            _session.MatchEnded += OnMatchEnded;
            _shredderEjectionPose = shredderEjectionPose;
            _hasShredderEjectionPose = true;

            var remainingUses = new int[_session.Players.Players.Count];
            for (var playerIndex = 0;
                 playerIndex < remainingUses.Length;
                 playerIndex++)
            {
                remainingUses[playerIndex] =
                    _session.GetRemainingDestructionUses(playerIndex);
            }

            if (!_state.TryInitializePlayerInteractionStates(remainingUses))
            {
                throw new InvalidOperationException(
                    "The authority could not initialize player interaction state.");
            }
        }

        public bool UnbindSession(MatchSessionCoordinator session)
        {
            if (!ReferenceEquals(_session, session))
            {
                return false;
            }

            UnbindSession();
            return true;
        }

        public bool RequestHoldObject(string objectId)
        {
            if (_state == null || string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            _state.RPC_RequestHold(objectId.Trim());
            return true;
        }

        public bool RequestReleaseHeldObject(Pose pose)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestRelease(pose.position, pose.rotation);
            return true;
        }

        public bool RequestDropHeldObject(Pose pose)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestDrop(pose.position, pose.rotation);
            return true;
        }

        public bool RequestThrowHeldObject(Pose pose, Vector3 initialVelocity)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestThrow(pose.position, pose.rotation, initialVelocity);
            return true;
        }

        public bool RequestHitPlayer(int targetPlayerIndex)
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestHit(targetPlayerIndex);
            return true;
        }

        public bool RequestUseShredder()
        {
            if (_state == null)
            {
                return false;
            }

            _state.RPC_RequestShredder();
            return true;
        }

        public bool RequestReturnToLobby()
        {
            if (_state == null || _returningToLobby)
            {
                return false;
            }

            _state.RPC_RequestReturnToLobby();
            return true;
        }

        public bool RequestLobbyChat(string text)
        {
            if (_state == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            _state.RPC_RequestLobbyChat(LobbyChatMessage.ClampText(text.Trim()));
            return true;
        }

        public bool TryRelayLobbyChat(PlayerRef source, string text)
        {
            if (_state == null || _state.Object == null ||
                !_state.Object.HasStateAuthority || _roster == null ||
                string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!source.IsRealPlayer && _state.Runner.IsServer)
            {
                source = _state.Runner.LocalPlayer;
            }

            var playerId = PlayerRegistry.IdOf(source);
            _room.Clear();
            _roster.Capture(_room);
            for (var index = 0; index < _room.Count; index++)
            {
                var participant = _room[index];
                if (!string.Equals(
                        participant.PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var nickname = string.IsNullOrEmpty(participant.Nickname)
                    ? participant.PlayerId
                    : participant.Nickname;
                _state.RPC_NotifyLobbyChat(
                    participant.PlayerId,
                    nickname,
                    LobbyChatMessage.ClampText(text.Trim()));
                return true;
            }

            return false;
        }

        public void PublishLobbyChat(LobbyChatMessage message)
        {
            LobbyChatReceived?.Invoke(message);
        }

        public bool TryHoldObject(PlayerRef source, string objectId)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_state.CanHoldObject(objectId) ||
                !IsObjectWithinReach(playerIndex, objectId, playerPose.position) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryHoldObject(playerIndex, objectId, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectHeld(objectId, playerIndex);
        }

        public bool TryReleaseHeldObject(PlayerRef source, Pose pose)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidRelease(playerPose, pose) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryReleaseHeldObject(playerIndex, pose, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose);
        }

        public bool TryDropHeldObject(PlayerRef source, Pose pose)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidRelease(playerPose, pose) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryDropHeldObject(playerIndex, pose, ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose);
        }

        public bool TryThrowHeldObject(
            PlayerRef source,
            Pose pose,
            Vector3 initialVelocity)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsValidThrow(
                    playerPose,
                    pose,
                    initialVelocity) ||
                !_session.TryGetHeldObjectId(playerIndex, out var objectId) ||
                !_state.CanTrackObject(objectId) ||
                !_session.TryThrowHeldObject(
                    playerIndex,
                    pose,
                    initialVelocity,
                    ServerTime))
            {
                return false;
            }

            return _state.TrySetObjectReleased(objectId, pose, initialVelocity);
        }

        public bool TryConfirmObjectSettled(
            string objectId,
            Pose pose,
            int expectedVersion)
        {
            if (_state == null || _session == null ||
                !_session.TryGetObjectPose(objectId, out _) ||
                !_state.TrySetObjectSettled(objectId, pose, expectedVersion))
            {
                return false;
            }

            if (!_session.TryConfirmReleasedObjectPose(objectId, pose))
            {
                throw new InvalidOperationException(
                    $"The settled pose could not be stored for '{objectId}'.");
            }

            return true;
        }

        public bool TryHitPlayer(PlayerRef source, int targetPlayerIndex)
        {
            if (!TryGetPlayerIndex(source, out var attackerPlayerIndex) ||
                targetPlayerIndex < 0 ||
                targetPlayerIndex >= _session.Players.Players.Count)
            {
                return false;
            }

            if (!TryGetPlayerPose(attackerPlayerIndex, out var attackerPose) ||
                !TryGetPlayerPose(targetPlayerIndex, out var targetPose) ||
                !_interactionRules.IsWithinInteractionDistance(
                    attackerPose.position,
                    targetPose.position))
            {
                return false;
            }

            _session.TryGetHeldObjectId(targetPlayerIndex, out var droppedObjectId);
            if (droppedObjectId != null && !_state.CanTrackObject(droppedObjectId))
            {
                return false;
            }

            var result = _session.RegisterHit(
                attackerPlayerIndex,
                targetPlayerIndex,
                targetPose.position,
                ServerTime);
            if (result == Game.Core.Players.HitResult.Stunned && droppedObjectId != null)
            {
                _state.TrySetObjectReleased(
                    droppedObjectId,
                    new Pose(targetPose.position, Quaternion.identity));
            }

            return result != Game.Core.Players.HitResult.Ignored;
        }

        public bool TryUseShredder(PlayerRef source)
        {
            if (!_hasShredderEjectionPose ||
                !TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
                !_interactionRules.IsWithinInteractionDistance(
                    playerPose.position,
                    _shredderEjectionPose.position))
            {
                return false;
            }

            var now = ServerTime;
            if (!_session.TryGetHeldObjectId(playerIndex, out var objectId))
            {
                return false;
            }

            if (!_state.CanTrackObject(objectId))
            {
                return false;
            }

            if (_session.TryDestroyHeldPlayerItem(playerIndex, now))
            {
                PublishRemainingDestructionUses(playerIndex);
                return _state.TrySetObjectDestroyed(objectId);
            }

            if (!_session.TryUseShredderOnHeldMapObject(
                    playerIndex,
                    _shredderEjectionPose,
                    now))
            {
                return false;
            }

            PublishRemainingDestructionUses(playerIndex);
            return _state.TrySetObjectPendingEjection(
                objectId,
                _shredderEjectionPose);
        }

        public bool TryHandlePlayerLeft(PlayerRef player)
        {
            if (!TryGetPlayerIndex(player, out var playerIndex))
            {
                return false;
            }

            var playerId = PlayerRegistry.IdOf(player);
            var lastKnownPose = Pose.identity;
            if (_roster == null || !_roster.TryGetPose(playerId, out lastKnownPose))
            {
                Debug.LogWarning(
                    $"[Match] No last pose was found for leaving player {playerId}.");
            }

            if (!_session.TryHandlePlayerLeft(playerIndex, lastKnownPose, ServerTime))
            {
                return false;
            }

            _state.TrySetParticipantInactive(playerIndex);
            return true;
        }

        public bool TryReturnToLobby(PlayerRef source)
        {
            if (!TryGetPlayerIndex(source, out _))
            {
                return false;
            }

            return ReturnToLobby();
        }

        private bool ReturnToLobby()
        {
            if (_returningToLobby || _sceneDirector == null || _state == null ||
                _session == null || _session.CurrentPhase != MatchPhase.Result)
            {
                return false;
            }

            if (!_sceneDirector.EnterLobbyScene(_state.Runner))
            {
                return false;
            }

            _returningToLobby = true;
            if (!_state.TryResetForRematch())
            {
                throw new InvalidOperationException(
                    "The authority could not reset the completed match state.");
            }

            return true;
        }

        private double ServerTime => _state.Runner.SimulationTime;

        private void OnPlayerItemDestroyed(PlayerItemDestroyedEvent confirmedEvent)
        {
            _state?.RPC_NotifyItemDestroyed(
                confirmedEvent.DestroyerPlayerIndex,
                confirmedEvent.ItemId,
                confirmedEvent.DestroyedAt);
        }

        private void OnPlayerStunned(PlayerStunnedEvent confirmedEvent)
        {
            _state?.TrySetStunEndsAt(
                confirmedEvent.TargetPlayerIndex,
                confirmedEvent.StunEndsAt);
            _state?.RPC_NotifyPlayerStunned(
                confirmedEvent.AttackerPlayerIndex,
                confirmedEvent.TargetPlayerIndex,
                confirmedEvent.DroppedObjectId ?? string.Empty,
                confirmedEvent.StunnedAt,
                confirmedEvent.StunEndsAt);
        }

        private void OnObjectThrown(ObjectThrownEvent confirmedEvent)
        {
            _state?.RPC_NotifyObjectThrown(
                confirmedEvent.PlayerIndex,
                confirmedEvent.ObjectId,
                confirmedEvent.ReleasePose.position,
                confirmedEvent.ReleasePose.rotation,
                confirmedEvent.InitialVelocity,
                confirmedEvent.ThrownAt);
        }

        private void OnObjectAutoReleased(ObjectAutoReleasedEvent confirmedEvent)
        {
            _state?.TrySetObjectReleased(
                confirmedEvent.ObjectId,
                confirmedEvent.Pose);
        }

        private void OnMapObjectEjected(MapObjectEjectedEvent confirmedEvent)
        {
            if (_state == null || !_state.TrySetObjectReleased(
                    confirmedEvent.ObjectId,
                    confirmedEvent.Pose,
                    CalculateShredderEjectionVelocity(confirmedEvent.Pose.rotation)))
            {
                throw new InvalidOperationException(
                    $"The shredder could not eject '{confirmedEvent.ObjectId}'.");
            }
        }

        private void OnFinalWarningStarted(FinalWarningStartedEvent confirmedEvent)
        {
            _state?.RPC_NotifyFinalWarning(
                confirmedEvent.StartedAt,
                confirmedEvent.EndsAt);
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_state?.TrySetResult(result) == true &&
                result.EndReason == MatchEndReason.LastPlayerStanding)
            {
                ReturnToLobby();
            }
        }

        private void PublishRemainingDestructionUses(int playerIndex)
        {
            _state?.TrySetRemainingDestructionUses(
                playerIndex,
                _session.GetRemainingDestructionUses(playerIndex));
        }

        private void UnbindSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.PlayerItemDestroyed -= OnPlayerItemDestroyed;
            _session.PlayerStunned -= OnPlayerStunned;
            _session.ObjectThrown -= OnObjectThrown;
            _session.ObjectAutoReleased -= OnObjectAutoReleased;
            _session.MapObjectEjected -= OnMapObjectEjected;
            _session.FinalWarningStarted -= OnFinalWarningStarted;
            _session.MatchEnded -= OnMatchEnded;
            _session = null;
        }

        internal static Vector3 CalculateShredderEjectionVelocity(Quaternion rotation) =>
            rotation * ShredderEjectionLocalVelocity;

        private bool TryGetPlayerIndex(PlayerRef source, out int playerIndex)
        {
            if (_session == null || _state == null || _state.Runner == null)
            {
                playerIndex = -1;
                return false;
            }

            if (!source.IsRealPlayer && _state.Runner.IsServer)
            {
                source = _state.Runner.LocalPlayer;
            }

            playerIndex = -1;
            return source.IsRealPlayer &&
                   _session.Players.TryGetPlayerIndex(
                       PlayerRegistry.IdOf(source),
                       out playerIndex);
        }

        private bool TryGetPlayerPose(int playerIndex, out Pose pose)
        {
            if (_session != null &&
                _roster != null &&
                playerIndex >= 0 &&
                playerIndex < _session.Players.Players.Count)
            {
                var player = _session.Players.GetPlayer(playerIndex);
                return _roster.TryGetPose(player.PlayerId, out pose);
            }

            pose = default;
            return false;
        }

        private bool TryGetPlayingAvatar(int playerIndex, out PlayerAvatar avatar)
        {
            if (_state == null || _state.Object == null ||
                !_state.Object.HasStateAuthority || _roster == null ||
                playerIndex < 0 || playerIndex >= _playing.Count)
            {
                avatar = null;
                return false;
            }

            return _roster.TryGetAvatar(_playing[playerIndex].PlayerId, out avatar);
        }

        private bool IsObjectWithinReach(
            int playerIndex,
            string objectId,
            Vector3 playerPosition)
        {
            if (_session.TryGetObjectPose(objectId, out var objectPose))
            {
                return _interactionRules.IsWithinInteractionDistance(
                    playerPosition,
                    objectPose.position);
            }

            // Before its hiding turn placement, the assigned item is treated as
            // already being in its owner's hand.
            return playerIndex >= 0 &&
                   playerIndex < _session.Assignments.Count &&
                   string.Equals(
                       _session.Assignments[playerIndex].Item.ItemId,
                       objectId,
                       StringComparison.Ordinal) &&
                   !_session.TryGetItemPlacement(playerIndex, out _);
        }

        /// <summary>Reports a refusal on the peer that asked.</summary>
        public void Refused(RoomStartResult reason)
        {
            _sink?.MatchStartRefused(reason);
        }

        /// <summary>Forgets the room. The line-up goes with the session.</summary>
        public void Clear()
        {
            _state = null;
            UnbindSession();
            _hasShredderEjectionPose = false;
            _returningToLobby = false;
            _playing.Clear();
            _room.Clear();
            _sink?.MatchStarted(_playing);
            LineUpReceived?.Invoke(_playing);
        }

        /// <summary>
        /// Fills <see cref="_room"/> with the line-up if the match may start,
        /// and says why not otherwise.
        /// </summary>
        private RoomStartResult Evaluate(MatchSessionState state, NetworkRunner runner)
        {
            if (state.IsStarted)
            {
                return RoomStartResult.AlreadyStarted;
            }

            _room.Clear();
            _roster?.Capture(_room);

            if (_room.Count < RoomSettings.MinMatchPlayerCount)
            {
                return RoomStartResult.NotEnoughPlayers;
            }

            // Someone can be in the room before their character exists. Starting
            // then would leave a hole where the match rules expect a position for
            // every player, so it waits rather than starting short.
            var inRoom = 0;
            foreach (var _ in runner.ActivePlayers)
            {
                inRoom++;
            }

            if (inRoom != _room.Count)
            {
                Debug.Log(
                    $"[Match] {inRoom} in the room but {_room.Count} characters " +
                    "exist. Waiting for everyone to appear.");

                return RoomStartResult.NotEnoughPlayers;
            }

            return RoomStartResult.Started;
        }
    }
}
