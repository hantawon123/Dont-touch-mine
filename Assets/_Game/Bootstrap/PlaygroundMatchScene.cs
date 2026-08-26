using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Client.Interactions;
using Game.Core.Items;
using Game.Server.Items;
using Game.Server.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Game.Architecture.Tests")]

namespace Game.Bootstrap
{
    internal sealed class PlaygroundMatchScene
    {
        // Replay frames are sampled ten times per second. Keeping this smaller
        // than the live state capacity prevents a larger map from multiplying
        // highlight memory and reliable-transfer bandwidth.
        internal const int MaxReplayObjectCount = 64;

        private PlaygroundMatchScene(
            IMatchRuntimeContext runtimeContext,
            NetworkMatchRuntimeConfiguration networkConfiguration)
        {
            RuntimeContext = runtimeContext;
            NetworkConfiguration = networkConfiguration;
        }

        public IMatchRuntimeContext RuntimeContext { get; }
        public NetworkMatchRuntimeConfiguration NetworkConfiguration { get; }

        public static PlaygroundMatchScene Capture(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("A loaded Playground scene is required.", nameof(scene));
            }

            var items = CaptureUniqueItems(scene);
            var assignments = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in ItemCatalog.Definitions)
            {
                assignments.Add(definition.ItemId);
                if (!items.ContainsKey(definition.ItemId))
                {
                    throw new InvalidOperationException(
                        $"Playground is missing item '{definition.ItemId}'.");
                }
            }

            var worldObjects = new List<WorldObjectState>();
            var worldItems = new List<CarryableItem>();
            foreach (var pair in items)
            {
                if (assignments.Contains(pair.Key))
                {
                    continue;
                }

                var item = pair.Value;
                worldObjects.Add(new WorldObjectState(
                    pair.Key,
                    new Pose(item.transform.position, item.transform.rotation)));
                worldItems.Add(item);
            }

            var volumes = new List<PlacementVolume>();
            var replayItems = new List<CarryableItem>(MaxReplayObjectCount);
            foreach (var definition in ItemCatalog.Definitions)
            {
                var item = items[definition.ItemId];
                volumes.Add(CaptureVolume(item));
                replayItems.Add(item);
            }

            foreach (var item in worldItems)
            {
                volumes.Add(CaptureVolume(item));
                if (replayItems.Count < MaxReplayObjectCount)
                {
                    replayItems.Add(item);
                }
            }

            var ejectionPoint = FindTransform(scene, "ShredderSpot");
            var configuration = new NetworkMatchRuntimeConfiguration(
                new PhysicsPlacementValidator(
                    volumes,
                    Physics.DefaultRaycastLayers,
                    Physics.DefaultRaycastLayers),
                CaptureSpawnPoints(scene),
                ItemCatalog.Definitions,
                worldObjects,
                new Pose(ejectionPoint.position, ejectionPoint.rotation));

            return new PlaygroundMatchScene(
                new PlaygroundRuntimeContext(replayItems),
                configuration);
        }

        private static SortedDictionary<string, CarryableItem> CaptureUniqueItems(Scene scene)
        {
            var items = new SortedDictionary<string, CarryableItem>(StringComparer.Ordinal);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var item in root.GetComponentsInChildren<CarryableItem>(
                             includeInactive: true))
                {
                    var id = item.ObjectId;
                    if (!items.TryAdd(id, item) && !item.HasExplicitObjectId)
                    {
                        item.UseSceneInstanceObjectId();
                        id = item.ObjectId;
                    }

                    if (!items.TryAdd(id, item) && !ReferenceEquals(items[id], item))
                    {
                        throw new InvalidOperationException(
                            $"Carryable object id '{id}' is duplicated.");
                    }
                }
            }

            return items;
        }

        private static Pose[] CaptureSpawnPoints(Scene scene)
        {
            var poses = new Pose[6];
            for (var index = 0; index < poses.Length; index++)
            {
                var point = FindTransform(scene, $"SpawnPoint_{index + 1}");
                poses[index] = new Pose(point.position, point.rotation);
            }

            return poses;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(
                             includeInactive: true))
                {
                    if (string.Equals(transform.name, objectName, StringComparison.Ordinal))
                    {
                        return transform;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Playground is missing required object '{objectName}'.");
        }

        private static PlacementVolume CaptureVolume(CarryableItem item)
        {
            var colliders = item.GetComponentsInChildren<Collider>(includeInactive: true);
            Bounds? bounds = null;
            foreach (var itemCollider in colliders)
            {
                if (!itemCollider.isTrigger)
                {
                    bounds = bounds.HasValue
                        ? Encapsulate(bounds.Value, itemCollider.bounds)
                        : itemCollider.bounds;
                }
            }

            if (!bounds.HasValue)
            {
                throw new InvalidOperationException(
                    $"Carryable '{item.ObjectId}' has no solid collider.");
            }

            var captured = bounds.Value;
            var centerOffset = Quaternion.Inverse(item.transform.rotation) *
                               (captured.center - item.transform.position);
            var halfExtents = captured.extents;
            halfExtents.x = Mathf.Max(halfExtents.x, 0.02f);
            halfExtents.y = Mathf.Max(halfExtents.y, 0.02f);
            halfExtents.z = Mathf.Max(halfExtents.z, 0.02f);
            return new PlacementVolume(item.ObjectId, centerOffset, halfExtents);
        }

        private static Bounds Encapsulate(Bounds current, Bounds added)
        {
            current.Encapsulate(added);
            return current;
        }

        private sealed class PlaygroundRuntimeContext : IMatchRuntimeContext
        {
            private static readonly Vector3[] NoPlayerPositions = Array.Empty<Vector3>();
            private static readonly Pose[] NoPlayerPoses = Array.Empty<Pose>();
            private readonly IReadOnlyList<CarryableItem> replayItems;
            private readonly List<WorldObjectState> replayObjects = new();

            public PlaygroundRuntimeContext(IReadOnlyList<CarryableItem> replayItems)
            {
                this.replayItems = replayItems;
            }

            public double ServerTime => Time.timeAsDouble;
            public IReadOnlyList<Vector3> PlayerPositions => NoPlayerPositions;
            public IReadOnlyList<Pose> PlayerPoses => NoPlayerPoses;

            public IReadOnlyList<WorldObjectState> ReplayObjects
            {
                get
                {
                    replayObjects.Clear();
                    foreach (var item in replayItems)
                    {
                        if (item != null)
                        {
                            replayObjects.Add(new WorldObjectState(
                                item.ObjectId,
                                new Pose(item.transform.position, item.transform.rotation)));
                        }
                    }

                    return replayObjects;
                }
            }
        }
    }
}
