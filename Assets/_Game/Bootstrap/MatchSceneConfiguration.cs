using System;
using System.Collections.Generic;
using Game.Server.Items;
using Game.SOAP.Config;
using UnityEngine;

namespace Game.Bootstrap
{
    [Serializable]
    public sealed class SceneWorldObjectReference
    {
        [SerializeField]
        private string objectId;

        [SerializeField]
        private Transform target;

        public SceneWorldObjectReference(string objectId, Transform target)
        {
            this.objectId = objectId;
            this.target = target;
        }

        public string ObjectId => objectId;
        public Transform Target => target;
    }

    [DisallowMultipleComponent]
    public sealed class MatchSceneConfiguration : MonoBehaviour
    {
        [SerializeField]
        private Transform[] spawnPoints = Array.Empty<Transform>();

        [SerializeField]
        private SceneWorldObjectReference[] worldObjects =
            Array.Empty<SceneWorldObjectReference>();

        public Pose[] CaptureSpawnPoses()
        {
            return CaptureSpawnPoses(spawnPoints);
        }

        public WorldObjectState[] CaptureWorldObjectStates()
        {
            return CaptureWorldObjectStates(worldObjects);
        }

        public static Pose[] CaptureSpawnPoses(IReadOnlyList<Transform> transforms)
        {
            if (transforms == null || transforms.Count < MatchRulesSO.PlayerCount)
            {
                throw new InvalidOperationException(
                    $"At least {MatchRulesSO.PlayerCount} spawn points are required.");
            }

            var positions = new HashSet<Vector3>();
            var poses = new Pose[transforms.Count];
            for (var index = 0; index < transforms.Count; index++)
            {
                var spawnPoint = transforms[index];
                if (spawnPoint == null || !positions.Add(spawnPoint.position))
                {
                    throw new InvalidOperationException(
                        "Spawn points must be assigned and have unique positions.");
                }

                poses[index] = new Pose(spawnPoint.position, spawnPoint.rotation);
            }

            return poses;
        }

        public static WorldObjectState[] CaptureWorldObjectStates(
            IReadOnlyList<SceneWorldObjectReference> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            var states = new WorldObjectState[references.Count];
            for (var index = 0; index < references.Count; index++)
            {
                var reference = references[index];
                if (reference == null ||
                    reference.Target == null ||
                    string.IsNullOrWhiteSpace(reference.ObjectId))
                {
                    throw new InvalidOperationException(
                        "Every world object requires an id and Transform.");
                }

                var objectId = reference.ObjectId.Trim();
                if (!objectIds.Add(objectId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate world object id: {objectId}");
                }

                states[index] = new WorldObjectState(
                    objectId,
                    new Pose(reference.Target.position, reference.Target.rotation));
            }

            return states;
        }
    }
}
