using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Lobby;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using Game.Network.Players;
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

        public event Action<MatchStateSnapshot> MatchStateReceived;

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

        /// <summary>Reports a refusal on the peer that asked.</summary>
        public void Refused(RoomStartResult reason)
        {
            _sink?.MatchStartRefused(reason);
        }

        /// <summary>Forgets the room. The line-up goes with the session.</summary>
        public void Clear()
        {
            _state = null;
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
