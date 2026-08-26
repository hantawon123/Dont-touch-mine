using System;
using System.Collections.Generic;
using Game.Core.Lobby;
using Game.Core.Rooms;
using R3;

namespace Game.Bootstrap
{
    /// <summary>
    /// Feeds the lobby player list from the room this peer is actually in,
    /// replacing the sample list the screen was built against.
    /// </summary>
    /// <remarks>
    /// The lobby speaks <see cref="LobbyParticipant"/> and the session speaks
    /// <see cref="RoomParticipant"/>. Projecting here is what keeps
    /// <c>Game.Client</c> from learning about the session and the session from
    /// learning about the lobby screen; this is the only place the two
    /// vocabularies meet.
    /// <para>
    /// Order is left alone. <see cref="RoomBrowserSystem.Participants"/> is
    /// already seat-ordered so every screen agrees on the row order.
    /// </para>
    /// </remarks>
    public sealed class NetworkLobbyParticipantList : ILobbyParticipantList, IDisposable
    {
        private readonly LobbyParticipantList projected = new LobbyParticipantList();
        private readonly IDisposable subscription;

        public NetworkLobbyParticipantList(RoomBrowserSystem room)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            // Replays the current value, so the screen is correct even when it
            // subscribes after the room has already filled up.
            subscription = room.Participants.Subscribe(Project);
        }

        public ReadOnlyReactiveProperty<IReadOnlyList<LobbyParticipant>> Participants =>
            projected.Participants;

        public void Dispose()
        {
            subscription.Dispose();
            projected.Dispose();
        }

        private void Project(IReadOnlyList<RoomParticipant> seated)
        {
            if (seated == null || seated.Count == 0)
            {
                projected.Replace(Array.Empty<LobbyParticipant>());
                return;
            }

            var rows = new List<LobbyParticipant>(seated.Count);

            foreach (var one in seated)
            {
                // A character whose owner has not replicated yet has no id, the
                // same transient hole PlayerRoster skips when an avatar is gone.
                // Nothing can be shown for a row that cannot be identified.
                if (string.IsNullOrWhiteSpace(one.PlayerId))
                {
                    continue;
                }

                rows.Add(new LobbyParticipant(
                    one.PlayerId,
                    DisplayNameOf(one),
                    one.IsHost));
            }

            projected.Replace(rows);
        }

        /// <summary>
        /// A nickname arrives after the participant does, and
        /// <see cref="LobbyParticipant"/> refuses a blank name, so the id stands
        /// in until the name lands. The list is reactive, so the row corrects
        /// itself when it does.
        /// </summary>
        private static string DisplayNameOf(RoomParticipant participant)
        {
            return string.IsNullOrWhiteSpace(participant.Nickname)
                ? participant.PlayerId
                : participant.Nickname;
        }
    }
}
