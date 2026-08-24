using System;
using System.Collections.Generic;
using Game.Server.Items;
using Game.Server.Match;
using Game.SOAP.Config;
using UnityEngine;

namespace Game.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class MatchRuntimeContext : MonoBehaviour, IMatchRuntimeContext
    {
        [SerializeField]
        private Transform[] players = Array.Empty<Transform>();

        [SerializeField]
        private SceneWorldObjectReference[] replayObjects =
            Array.Empty<SceneWorldObjectReference>();

        private Vector3[] playerPositions = Array.Empty<Vector3>();
        private Pose[] playerPoses = Array.Empty<Pose>();
        private WorldObjectState[] worldObjectStates = Array.Empty<WorldObjectState>();

        public double ServerTime => Time.timeAsDouble;

        public IReadOnlyList<Vector3> PlayerPositions
        {
            get
            {
                CapturePlayers(players, ref playerPositions, ref playerPoses);
                return playerPositions;
            }
        }

        public IReadOnlyList<Pose> PlayerPoses
        {
            get
            {
                CapturePlayers(players, ref playerPositions, ref playerPoses);
                return playerPoses;
            }
        }

        public IReadOnlyList<WorldObjectState> ReplayObjects
        {
            get
            {
                CaptureWorldObjects(replayObjects, ref worldObjectStates);
                return worldObjectStates;
            }
        }

        public static void CapturePlayers(
            IReadOnlyList<Transform> playerTransforms,
            ref Vector3[] positions,
            ref Pose[] poses)
        {
            if (playerTransforms == null || playerTransforms.Count != MatchRulesSO.PlayerCount)
            {
                throw new InvalidOperationException(
                    $"Exactly {MatchRulesSO.PlayerCount} player Transforms are required.");
            }

            if (positions == null || positions.Length != playerTransforms.Count)
            {
                positions = new Vector3[playerTransforms.Count];
            }

            if (poses == null || poses.Length != playerTransforms.Count)
            {
                poses = new Pose[playerTransforms.Count];
            }

            for (var index = 0; index < playerTransforms.Count; index++)
            {
                var player = playerTransforms[index];
                if (player == null)
                {
                    throw new InvalidOperationException("Every player Transform must be assigned.");
                }

                positions[index] = player.position;
                poses[index] = new Pose(player.position, player.rotation);
            }
        }

        public static void CaptureWorldObjects(
            IReadOnlyList<SceneWorldObjectReference> references,
            ref WorldObjectState[] states)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            if (states == null || states.Length != references.Count)
            {
                states = new WorldObjectState[references.Count];
            }

            for (var index = 0; index < references.Count; index++)
            {
                var reference = references[index];
                if (reference == null ||
                    reference.Target == null ||
                    string.IsNullOrWhiteSpace(reference.ObjectId))
                {
                    throw new InvalidOperationException(
                        "Every replay object requires an id and Transform.");
                }

                states[index] = new WorldObjectState(
                    reference.ObjectId,
                    new Pose(reference.Target.position, reference.Target.rotation));
            }
        }
    }
}
