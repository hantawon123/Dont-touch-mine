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

    [Serializable]
    public sealed class ScenePlacementVolumeReference
    {
        [SerializeField]
        private string objectId;

        [SerializeField]
        private Vector3 centerOffset;

        [SerializeField]
        private Vector3 halfExtents = Vector3.one * 0.25f;

        public ScenePlacementVolumeReference(
            string objectId,
            Vector3 centerOffset,
            Vector3 halfExtents)
        {
            this.objectId = objectId;
            this.centerOffset = centerOffset;
            this.halfExtents = halfExtents;
        }

        public PlacementVolume Capture()
        {
            return new PlacementVolume(objectId, centerOffset, halfExtents);
        }
    }

    [DisallowMultipleComponent]
    public sealed class MatchSceneConfiguration : MonoBehaviour
    {
        [SerializeField]
        private Transform[] spawnPoints = Array.Empty<Transform>();

        [SerializeField]
        private SceneWorldObjectReference[] worldObjects =
            Array.Empty<SceneWorldObjectReference>();

        [SerializeField]
        private ScenePlacementVolumeReference[] placementVolumes =
            Array.Empty<ScenePlacementVolumeReference>();

        [SerializeField]
        private LayerMask placementBlockingLayers;

        [SerializeField]
        private LayerMask placementSupportLayers;

        [SerializeField]
        private Transform shredderEjectionPoint;

        public Pose[] CaptureSpawnPoses()
        {
            return CaptureSpawnPoses(spawnPoints);
        }

        public WorldObjectState[] CaptureWorldObjectStates()
        {
            return CaptureWorldObjectStates(worldObjects);
        }

        public IPlacementValidator CreatePlacementValidator()
        {
            return new PhysicsPlacementValidator(
                CapturePlacementVolumes(placementVolumes),
                placementBlockingLayers,
                placementSupportLayers);
        }

        public Pose CaptureShredderEjectionPose()
        {
            if (shredderEjectionPoint == null)
            {
                throw new InvalidOperationException(
                    "A shredder ejection point is required.");
            }

            return new Pose(
                shredderEjectionPoint.position,
                shredderEjectionPoint.rotation);
        }

        public static Pose[] CaptureSpawnPoses(IReadOnlyList<Transform> transforms)
        {
            if (transforms == null || transforms.Count < MatchRulesSO.MaxPlayerCount)
            {
                throw new InvalidOperationException(
                    $"At least {MatchRulesSO.MaxPlayerCount} spawn points are required.");
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

        public static PlacementVolume[] CapturePlacementVolumes(
            IReadOnlyList<ScenePlacementVolumeReference> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            var volumes = new PlacementVolume[references.Count];
            for (var index = 0; index < references.Count; index++)
            {
                volumes[index] = references[index]?.Capture() ??
                    throw new InvalidOperationException(
                        "Every placement volume must be assigned.");
            }

            return volumes;
        }
    }
}
