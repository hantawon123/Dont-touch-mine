using System;
using System.Collections.Generic;
using R3;

namespace Game.Core.Lobby
{
    public readonly struct LobbyParticipant : IEquatable<LobbyParticipant>
    {
        public LobbyParticipant(string id, string displayName, bool isHost)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Participant id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            Id = id.Trim();
            DisplayName = displayName.Trim();
            IsHost = isHost;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public bool IsHost { get; }

        public bool Equals(LobbyParticipant other) =>
            string.Equals(Id, other.Id, StringComparison.Ordinal) &&
            string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
            IsHost == other.IsHost;

        public override bool Equals(object obj) =>
            obj is LobbyParticipant other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Id, DisplayName, IsHost);
    }

    public interface ILobbyParticipantList
    {
        ReadOnlyReactiveProperty<IReadOnlyList<LobbyParticipant>> Participants { get; }
    }

    public sealed class LobbyParticipantList : ILobbyParticipantList, IDisposable
    {
        private readonly ReactiveProperty<IReadOnlyList<LobbyParticipant>> participants;

        public LobbyParticipantList(IReadOnlyList<LobbyParticipant> initialParticipants = null)
        {
            participants = new ReactiveProperty<IReadOnlyList<LobbyParticipant>>(
                Clone(initialParticipants));
        }

        public ReadOnlyReactiveProperty<IReadOnlyList<LobbyParticipant>> Participants =>
            participants;

        public void Replace(IReadOnlyList<LobbyParticipant> nextParticipants)
        {
            participants.Value = Clone(nextParticipants);
        }

        public void Dispose() => participants.Dispose();

        private static IReadOnlyList<LobbyParticipant> Clone(
            IReadOnlyList<LobbyParticipant> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<LobbyParticipant>();
            }

            var copy = new LobbyParticipant[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }
}
