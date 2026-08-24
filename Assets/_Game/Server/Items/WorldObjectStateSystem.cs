using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Server.Items
{
    public readonly struct WorldObjectState
    {
        public WorldObjectState(string objectId, Pose pose)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                throw new ArgumentException("Object id is required.", nameof(objectId));
            }

            ObjectId = objectId.Trim();
            Pose = pose;
        }

        public string ObjectId { get; }
        public Pose Pose { get; }
    }

    public sealed class WorldObjectStateSystem
    {
        private readonly List<string> objectIds;
        private readonly Dictionary<string, WorldObjectState> states;

        public WorldObjectStateSystem(IReadOnlyList<WorldObjectState> initialStates)
        {
            if (initialStates == null)
            {
                throw new ArgumentNullException(nameof(initialStates));
            }

            objectIds = new List<string>(initialStates.Count);
            states = new Dictionary<string, WorldObjectState>(
                initialStates.Count,
                StringComparer.Ordinal);

            foreach (var state in initialStates)
            {
                if (string.IsNullOrWhiteSpace(state.ObjectId) ||
                    !states.TryAdd(state.ObjectId, state))
                {
                    throw new ArgumentException(
                        "World object ids must be unique.",
                        nameof(initialStates));
                }

                objectIds.Add(state.ObjectId);
            }
        }

        public bool TrySetPose(string objectId, Pose pose)
        {
            if (!TryGetState(objectId, out var state))
            {
                return false;
            }

            states[state.ObjectId] = new WorldObjectState(state.ObjectId, pose);
            return true;
        }

        public bool TryGetState(string objectId, out WorldObjectState state)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                state = default;
                return false;
            }

            return states.TryGetValue(objectId.Trim(), out state);
        }

        public WorldObjectState[] CaptureSnapshot()
        {
            var snapshot = new WorldObjectState[objectIds.Count];
            for (var index = 0; index < objectIds.Count; index++)
            {
                snapshot[index] = states[objectIds[index]];
            }

            return snapshot;
        }
    }
}
