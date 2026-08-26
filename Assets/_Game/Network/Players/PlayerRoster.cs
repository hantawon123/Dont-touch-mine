using System.Collections.Generic;
using Fusion;
using Game.Core.Ports;
using Game.Core.Rooms;
using UnityEngine;

namespace Game.Network.Players
{
    /// <summary>
    /// Works out who is in the room and reports it outward.
    /// </summary>
    /// <remarks>
    /// Lives on the runner object rather than in the container because Fusion
    /// spawns characters itself and they cannot be injected. Reaching this from
    /// a spawned object is <c>Runner.GetComponent</c>.
    /// <para>
    /// Characters put themselves on this list as they spawn instead of being
    /// looked up through <c>GetPlayerObject</c>. That mapping is only set after
    /// <c>Spawn</c> returns, which is after the character has already run
    /// <c>Spawned</c>, so a lookup at that moment finds nothing.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerRoster : MonoBehaviour
    {
        private readonly List<PlayerAvatar> _avatars = new List<PlayerAvatar>();
        private readonly List<RoomParticipant> _buffer = new List<RoomParticipant>();

        private IRoomParticipantSink _sink;

        public void Bind(IRoomParticipantSink sink)
        {
            _sink = sink;
        }

        public void Add(PlayerAvatar avatar)
        {
            if (avatar == null || _avatars.Contains(avatar))
            {
                return;
            }

            _avatars.Add(avatar);
            Publish(avatar.Runner);
        }

        public void Remove(PlayerAvatar avatar, NetworkRunner runner)
        {
            if (_avatars.Remove(avatar))
            {
                Publish(runner);
            }
        }

        /// <summary>Empties the list when the room is gone.</summary>
        public void Clear()
        {
            _avatars.Clear();

            if (_sink == null)
            {
                return;
            }

            _buffer.Clear();
            _sink.SetParticipants(_buffer);
            _sink.SetLocalPlayer(null);
        }

        private void Publish(NetworkRunner runner)
        {
            if (_sink == null)
            {
                return;
            }

            _buffer.Clear();

            for (var index = 0; index < _avatars.Count; index++)
            {
                var avatar = _avatars[index];

                // A character despawned between callbacks leaves a hole here.
                if (avatar == null)
                {
                    continue;
                }

                _buffer.Add(new RoomParticipant(
                    PlayerRegistry.IdOf(avatar.Owner),
                    avatar.Seat,
                    avatar.IsHost));
            }

            // Seat order, not arrival order. Characters replicate in whatever
            // order a peer received them, and a room list that reorders itself
            // per screen is confusing.
            _buffer.Sort(CompareBySeat);

            _sink.SetParticipants(_buffer);

            if (runner != null)
            {
                _sink.SetLocalPlayer(PlayerRegistry.IdOf(runner.LocalPlayer));
            }
        }

        private static int CompareBySeat(RoomParticipant left, RoomParticipant right)
        {
            return left.Seat.CompareTo(right.Seat);
        }
    }
}
