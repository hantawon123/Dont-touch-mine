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

            var participantIds = new string[_room.Count];
            for (var index = 0; index < _room.Count; index++)
            {
                participantIds[index] = _room[index].PlayerId;
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

            if (_sink == null)
            {
                return;
            }

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

            _sink.MatchStarted(_playing);
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

        public void BindSession(
            MatchSessionCoordinator session,
            Pose shredderEjectionPose)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _shredderEjectionPose = shredderEjectionPose;
            _hasShredderEjectionPose = true;
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
            return TryGetPlayerIndex(source, out var playerIndex) &&
                   _session.TryHoldObject(playerIndex, objectId, ServerTime);
        }

        public bool TryReleaseHeldObject(PlayerRef source, Pose pose)
        {
            return TryGetPlayerIndex(source, out var playerIndex) &&
                   _session.TryReleaseHeldObject(playerIndex, pose, ServerTime);
        }

        public bool TryThrowHeldObject(
            PlayerRef source,
            Pose pose,
            Vector3 initialVelocity)
        {
            return TryGetPlayerIndex(source, out var playerIndex) &&
                   _session.TryThrowHeldObject(
                       playerIndex,
                       pose,
                       initialVelocity,
                       ServerTime);
        }

        public bool TryHitPlayer(PlayerRef source, int targetPlayerIndex)
        {
            if (!TryGetPlayerIndex(source, out var attackerPlayerIndex) ||
                targetPlayerIndex < 0 ||
                targetPlayerIndex >= _session.Players.Players.Count)
            {
                return false;
            }

            var target = _session.Players.GetPlayer(targetPlayerIndex);
            if (_roster == null || !_roster.TryGetPose(target.PlayerId, out var pose))
            {
                return false;
            }

            return _session.RegisterHit(
                       attackerPlayerIndex,
                       targetPlayerIndex,
                       pose.position,
                       ServerTime) != Game.Core.Players.HitResult.Ignored;
        }

        public bool TryUseShredder(PlayerRef source)
        {
            if (!_hasShredderEjectionPose ||
                !TryGetPlayerIndex(source, out var playerIndex))
            {
                return false;
            }

            var now = ServerTime;
            return _session.TryDestroyHeldPlayerItem(playerIndex, now) ||
                   _session.TryUseShredderOnHeldMapObject(
                       playerIndex,
                       _shredderEjectionPose,
                       now);
        }

        private double ServerTime => _state.Runner.SimulationTime;

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

        /// <summary>Reports a refusal on the peer that asked.</summary>
        public void Refused(RoomStartResult reason)
        {
            _sink?.MatchStartRefused(reason);
        }

        /// <summary>Forgets the room. The line-up goes with the session.</summary>
        public void Clear()
        {
            _state = null;
            _session = null;
            _hasShredderEjectionPose = false;
            _playing.Clear();
            _room.Clear();
            _sink?.MatchStarted(_playing);
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
