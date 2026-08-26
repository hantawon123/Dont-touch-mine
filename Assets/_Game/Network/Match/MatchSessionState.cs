using Fusion;
using Game.Core.Lobby;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>
    /// The room's shared answer to "has the match started, and who is playing".
    /// One of these exists per room, spawned by the authority when the session
    /// opens.
    /// </summary>
    /// <remarks>
    /// Separate from the characters because it outlives any one of them and
    /// belongs to the room rather than a player. Holding it on a character would
    /// tie the match to whoever happened to spawn first.
    /// <para>
    /// The roster is replicated rather than worked out per peer. Every peer can
    /// see the seats, but they would each freeze them at a slightly different
    /// moment, and a match where two players believe they are the same
    /// <c>playerIndex</c> is unrecoverable.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MatchSessionState : NetworkBehaviour
    {
        /// <summary>Largest room the rules allow, so the array never resizes.</summary>
        public const int MaxParticipants = RoomSettings.MaxPlayerCount;

        [Networked]
        public bool IsStarted { get; set; }

        /// <summary>
        /// How much of <see cref="Participants"/> is in use. The array is a
        /// fixed size, so its length says nothing about the room.
        /// </summary>
        [Networked]
        public int ParticipantCount { get; set; }

        /// <summary>
        /// Player ids in play order. The position in this array is the
        /// <c>playerIndex</c> the match rules use, and it never moves once the
        /// match has started.
        /// </summary>
        /// <remarks>
        /// Not the seat number. Seats are reused as people come and go and can
        /// have gaps, so seat 5 may well be index 2.
        /// </remarks>
        [Networked, Capacity(MaxParticipants)]
        public NetworkArray<NetworkString<_16>> Participants => default;

        private bool _publishedStarted;
        private int _publishedCount = -1;

        public override void Spawned()
        {
            Publish();
        }

        /// <summary>
        /// Watches for the authority's decision arriving. Compared against what
        /// was last reported rather than using a change detector, because the
        /// two fields that matter are cheap to compare and the ids behind them
        /// never change after the match starts.
        /// </summary>
        public override void Render()
        {
            if (_publishedStarted == IsStarted && _publishedCount == ParticipantCount)
            {
                return;
            }

            Publish();
        }

        /// <summary>
        /// Writes the confirmed line-up. Authority only; everyone else receives
        /// it through replication.
        /// </summary>
        public void Confirm(string[] participantIds)
        {
            var count = Mathf.Min(participantIds.Length, MaxParticipants);

            for (var index = 0; index < count; index++)
            {
                Participants.Set(index, participantIds[index]);
            }

            ParticipantCount = count;
            IsStarted = true;
        }

        /// <summary>
        /// Reports the room's answer to whoever is listening on this peer.
        /// </summary>
        private void Publish()
        {
            _publishedStarted = IsStarted;
            _publishedCount = ParticipantCount;

            StarterOf(Runner)?.Publish(this);
        }

        /// <summary>
        /// The starter sits on the runner object, which is the one place a
        /// Fusion-spawned object can reach without being injected.
        /// </summary>
        private static MatchStarter StarterOf(NetworkRunner runner)
        {
            return runner == null ? null : runner.GetComponent<MatchStarter>();
        }
    }
}
