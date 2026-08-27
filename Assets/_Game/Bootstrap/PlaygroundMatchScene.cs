using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Client.Interactions;
using Game.Core.Items;
using Game.Server.Items;
using Game.Server.Match;
using Game.SOAP.Config;
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
            // isLoaded는 씬 오브젝트들의 Awake 시점에는 아직 false라서 검사할 수 없다.
            // (LifetimeScope.Awake에서 호출되므로) 유효성과 내용물 존재만 확인한다.
            if (!scene.IsValid() || scene.rootCount == 0)
            {
                throw new ArgumentException("A loaded Playground scene is required.", nameof(scene));
            }

            var items = CaptureUniqueItems(scene);
            var assignmentDefinitions = new ItemDefinition[ItemCatalog.Definitions.Count];
            var assignments = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ItemCatalog.Definitions.Count; index++)
            {
                var definition = ItemCatalog.Definitions[index];
                if (!items.ContainsKey(definition.ItemId))
                {
                    throw new InvalidOperationException(
                        $"Playground is missing item '{definition.ItemId}'.");
                }

                var assignedDefinition = ItemCatalog.AssignedDefinition(index);
                assignmentDefinitions[index] = assignedDefinition;
                assignments.Add(assignedDefinition.ItemId);

                if (!items.ContainsKey(assignedDefinition.ItemId))
                {
                    var copy = CreateAssignedCopy(
                        items[definition.ItemId],
                        assignedDefinition.ItemId);
                    items.Add(assignedDefinition.ItemId, copy);
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
            for (var index = 0; index < assignmentDefinitions.Length; index++)
            {
                var definition = assignmentDefinitions[index];
                var copy = items[definition.ItemId];
                var source = items[ItemCatalog.Definitions[index].ItemId];
                volumes.Add(CaptureVolume(source, definition.ItemId));
                replayItems.Add(copy);
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
            var spawnPoints = CaptureSpawnPoints(scene);
            var configuration = new NetworkMatchRuntimeConfiguration(
                new PhysicsPlacementValidator(
                    volumes,
                    Physics.DefaultRaycastLayers,
                    Physics.DefaultRaycastLayers),
                spawnPoints,
                assignmentDefinitions,
                worldObjects,
                new Pose(ejectionPoint.position, ejectionPoint.rotation),
                CaptureWaitingSpawnPoints(scene, spawnPoints));

            return new PlaygroundMatchScene(
                new PlaygroundRuntimeContext(replayItems),
                configuration);
        }

        private static CarryableItem CreateAssignedCopy(
            CarryableItem source,
            string objectId)
        {
            var copyObject = UnityEngine.Object.Instantiate(
                source.gameObject,
                source.transform.parent);
            copyObject.name = $"{objectId}_{source.name}";

            var copy = copyObject.GetComponent<CarryableItem>();
            copy.UseObjectId(objectId);

            // The copy has no world position before the match starts. It becomes
            // visible when the replicated held state attaches it to a HoldPoint.
            copyObject.SetActive(false);
            return copy;
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

        private static Pose[] CaptureWaitingSpawnPoints(
            Scene scene,
            IReadOnlyList<Pose> fallbackPoints)
        {
            var poses = new Pose[MatchRulesSO.MaxPlayerCount];
            for (var index = 0; index < poses.Length; index++)
            {
                poses[index] = TryFindTransform(
                    scene,
                    $"WaitingSpawnPoint_{index + 1}",
                    out var point)
                    ? new Pose(point.position, point.rotation)
                    : fallbackPoints[index];
            }

            return poses;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            if (TryFindTransform(scene, objectName, out var found))
            {
                return found;
            }

            throw new InvalidOperationException(
                $"Playground is missing required object '{objectName}'.");
        }

        private static bool TryFindTransform(
            Scene scene,
            string objectName,
            out Transform found)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(
                             includeInactive: true))
                {
                    if (string.Equals(transform.name, objectName, StringComparison.Ordinal))
                    {
                        found = transform;
                        return true;
                    }
                }
            }

            found = null;
            return false;
        }

        private static PlacementVolume CaptureVolume(
            CarryableItem item,
            string objectId = null)
        {
            return new PlacementVolume(
                string.IsNullOrWhiteSpace(objectId) ? item.ObjectId : objectId,
                item.PlacementCenterOffset,
                item.PlacementHalfExtents);
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
                        if (item != null && item.gameObject.activeInHierarchy)
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
