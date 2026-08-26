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
        private readonly List<RoomParticipant> _room = new List<RoomParticipant>();
        private readonly List<MatchParticipant> _playing = new List<MatchParticipant>();
        private readonly InteractionAuthorityRules _interactionRules =
            new InteractionAuthorityRules();

        private IMatchStartSink _sink;
        private PlayerRoster _roster;

        /// <summary>
        /// The room's object, remembered as it reports itself. Anything wanting
        /// to ask for a match needs a handle on it, and only this peer's copy of
        /// it can carry the request.
        /// </summary>
        private MatchSessionState _state;
        private MatchSessionCoordinator _session;
        private Pose _shredderEjectionPose;
        private bool _hasShredderEjectionPose;

        public event Action<MatchStateSnapshot> MatchStateReceived;
        public event Action<string> ItemAssignmentReceived;
        public event Action<IReadOnlyList<MatchObjectStateSnapshot>> ObjectStatesReceived;
        public event Action<PlayerItemDestroyedEvent> ItemDestroyedReceived;
        public event Action<PlayerStunnedEvent> PlayerStunnedReceived;
        public event Action<ObjectThrownEvent> ObjectThrownReceived;
        public event Action<FinalWarningStartedEvent> FinalWarningReceived;
        public event Action<IReadOnlyList<bool>> ParticipantActivityReceived;
        public event Action<MatchResult> MatchResultReceived;
        public event Action<IReadOnlyList<MatchParticipant>> LineUpReceived;
        public event Action SimulationTick;

        public void Bind(IMatchStartSink sink, PlayerRoster roster)
        {
            _sink = sink;
            _roster = roster;
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

        public bool TryPublishItemAssignments(
            IReadOnlyList<PlayerItemAssignment> assignments)
        {
            if (_state == null || _roster == null || assignments == null ||
                assignments.Count != _playing.Count)
            {
                return false;
            }

            var targets = new PlayerRef[assignments.Count];
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                if (assignment.PlayerIndex != index ||
                    string.IsNullOrWhiteSpace(assignment.Item.ItemId) ||
                    !_roster.TryGetPlayer(_playing[index].PlayerId, out targets[index]))
                {
                    return false;
                }
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                if (!_state.TrySendItemAssignment(
                        targets[index], assignments[index].Item.ItemId))
                {
                    return false;
                }
            }

            return true;
        }

        public void PublishItemAssignment(string itemId)
        {
            ItemAssignmentReceived?.Invoke(itemId);
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
            _session = session;
            _session.PlayerItemDestroyed += OnPlayerItemDestroyed;
            _session.PlayerStunned += OnPlayerStunned;
            _session.ObjectThrown += OnObjectThrown;
            _session.FinalWarningStarted += OnFinalWarningStarted;
            _session.MatchEnded += OnMatchEnded;
            _shredderEjectionPose = shredderEjectionPose;
            _hasShredderEjectionPose = true;
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

        public bool TryHoldObject(PlayerRef source, string objectId)
        {
            if (!TryGetPlayerIndex(source, out var playerIndex) ||
                !TryGetPlayerPose(playerIndex, out var playerPose) ||
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
                return _state.TrySetObjectDestroyed(objectId);
            }

            return _session.TryUseShredderOnHeldMapObject(
                       playerIndex,
                       _shredderEjectionPose,
                       now) &&
                   _state.TrySetObjectReleased(objectId, _shredderEjectionPose);
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

            _session.TryGetHeldObjectId(playerIndex, out var heldObjectId);
            if (!_session.TryHandlePlayerLeft(playerIndex, lastKnownPose, ServerTime))
            {
                return false;
            }

            _state.TrySetParticipantInactive(playerIndex);

            if (heldObjectId != null)
            {
                var releasedPose = lastKnownPose;
                var foundPose = false;
                foreach (var assignment in _session.Assignments)
                {
                    if (string.Equals(
                            assignment.Item.ItemId,
                            heldObjectId,
                            StringComparison.Ordinal) &&
                        _session.TryGetItemPlacement(
                            assignment.PlayerIndex,
                            out var placement))
                    {
                        releasedPose = placement.Pose;
                        foundPose = true;
                        break;
                    }
                }

                if (!foundPose &&
                    _session.TryGetWorldObjectState(heldObjectId, out var worldObject))
                {
                    releasedPose = worldObject.Pose;
                }

                _state.TrySetObjectReleased(heldObjectId, releasedPose);
            }

            var ownItemId = _session.Assignments[playerIndex].Item.ItemId;
            if (!string.Equals(ownItemId, heldObjectId, StringComparison.Ordinal) &&
                _session.TryGetItemPlacement(playerIndex, out var ownPlacement) &&
                ownPlacement.WasAutoPlaced)
            {
                _state.TrySetObjectReleased(ownItemId, ownPlacement.Pose);
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

        private void OnFinalWarningStarted(FinalWarningStartedEvent confirmedEvent)
        {
            _state?.RPC_NotifyFinalWarning(
                confirmedEvent.StartedAt,
                confirmedEvent.EndsAt);
        }

        private void OnMatchEnded(MatchResult result)
        {
            _state?.TrySetResult(result);
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
            _session.FinalWarningStarted -= OnFinalWarningStarted;
            _session.MatchEnded -= OnMatchEnded;
            _session = null;
        }

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

            if (_room.Count < RoomSettings.MinPlayerCount)
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
