using System.Collections.Generic;
using Game.Server.Items;
using UnityEngine;

namespace Game.Network.Match
{
    /// <summary>Room-scoped ownership; player IDs remain stable when seats are reused.</summary>
    public sealed class LobbyObjectAuthority
    {
        private readonly WorldObjectStateSystem objects;
        private readonly Dictionary<string, string> held = new();
        private readonly HashSet<string> occupied = new();
        private readonly InteractionAuthorityRules rules = new();

        public LobbyObjectAuthority(IReadOnlyList<WorldObjectState> initial) =>
            objects = new WorldObjectStateSystem(initial);

        public bool TryHold(string playerId, string objectId, Pose playerPose)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(objectId) ||
                held.ContainsKey(playerId) || occupied.Contains(objectId) ||
                !objects.TryGetState(objectId, out var state) ||
                !rules.IsWithinInteractionDistance(playerPose.position, state.Pose.position)) return false;
            held.Add(playerId, objectId);
            occupied.Add(objectId);
            return true;
        }

        public bool TryGetHeld(string playerId, out string objectId) => held.TryGetValue(playerId, out objectId);

        public bool TryRelease(string playerId, Pose playerPose, Pose pose, Vector3 velocity, bool throwing)
        {
            if (!(throwing ? rules.IsValidThrow(playerPose, pose, velocity) : rules.IsValidRelease(playerPose, pose)) ||
                !held.TryGetValue(playerId, out var objectId)) return false;
            Forget(playerId);
            objects.TrySetPose(objectId, pose);
            return true;
        }

        public bool TrySetPose(string objectId, Pose pose) => objects.TrySetPose(objectId, pose);

        public bool Forget(string playerId)
        {
            if (!held.TryGetValue(playerId, out var objectId)) return false;
            held.Remove(playerId);
            occupied.Remove(objectId);
            return true;
        }
    }
}
