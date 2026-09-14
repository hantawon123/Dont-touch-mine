using System;
using System.Collections.Generic;
using System.Linq;
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
            var catalog = ItemCatalogSO.Load();
            var assignmentDefinitions = ItemCatalog.AssignmentDefinitions.ToArray();
            var assignments = new HashSet<string>(assignmentDefinitions.Select(d => d.ItemId), StringComparer.Ordinal);
            var sources = catalog.categories.Where(c => c.enabled).SelectMany(c => c.items.Where(i => i.enabled));
            foreach (var source in sources)
            {
                if (items.ContainsKey(source.id)) continue;
                var sourceItem = source.prefab.GetComponent<CarryableItem>();
                if (sourceItem == null) throw new InvalidOperationException($"{source.id}: prefab requires CarryableItem.");
                var copy = CreateAssignedCopy(sourceItem, source.id);
                SceneManager.MoveGameObjectToScene(copy.gameObject, scene);
                items.Add(source.id, copy);
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
                volumes.Add(CaptureVolume(copy, definition.ItemId));
                replayItems.Add(copy);
            }

            foreach (var item in worldItems)
            {
                volumes.Add(CaptureVolume(item));
                replayItems.Add(item);
            }

            // 파쇄기가 여러 대면 'ShredderSpot' 이름의 튕김 지점도 여러 개다. 전부 모아 서버가 가장 가까운 것을 고르게 한다.
            // Index names once instead of scanning the entire map for every spawn point.
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(includeInactive: true))
                .ToLookup(value => value.name, StringComparer.Ordinal);
            var ejectionPoints = transforms["ShredderSpot"].ToList();
            if (ejectionPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Match scene '{scene.name}' is missing required object 'ShredderSpot'.");
            }

            var ejectionPoses = new Pose[ejectionPoints.Count];
            for (var index = 0; index < ejectionPoints.Count; index++)
            {
                ejectionPoses[index] = new Pose(ejectionPoints[index].position, ejectionPoints[index].rotation);
            }

            var spawnPoints = CaptureSpawnPoints(scene, transforms);
            var configuration = new NetworkMatchRuntimeConfiguration(
                new PhysicsPlacementValidator(
                    volumes,
                    Physics.DefaultRaycastLayers,
                    Physics.DefaultRaycastLayers),
                spawnPoints,
                assignmentDefinitions,
                worldObjects,
                ejectionPoses,
                CaptureWaitingSpawnPoints(transforms, spawnPoints));

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

        /// <summary>
        /// <c>SpawnPoint_1</c>부터 번호가 끊기기 전까지 전부 읽는다. 최소 6개(최대 인원)는 있어야 하고,
        /// 마트처럼 10개를 둔 맵은 10개 모두 숨기기·탐색 시작 위치 후보가 된다.
        /// </summary>
        private static Pose[] CaptureSpawnPoints(Scene scene, ILookup<string, Transform> transforms)
        {
            var poses = new List<Pose>();
            for (var index = 1; ; index++)
            {
                if (index <= MatchRulesSO.MaxPlayerCount)
                {
                    var required = FindTransform(scene, transforms, $"SpawnPoint_{index}");
                    poses.Add(new Pose(required.position, required.rotation));
                    continue;
                }

                if (!TryFindTransform(transforms, $"SpawnPoint_{index}", out var optional))
                {
                    break;
                }

                poses.Add(new Pose(optional.position, optional.rotation));
            }

            return poses.ToArray();
        }

        private static Pose[] CaptureWaitingSpawnPoints(
            ILookup<string, Transform> transforms,
            IReadOnlyList<Pose> fallbackPoints)
        {
            // 런타임 구성은 대기 지점이 스폰 지점 수 이상이길 요구한다. 스폰 지점이 최대 인원보다 많은 맵(마트 10개)은
            // 그만큼 채우고, 없는 번호는 같은 번호의 스폰 지점으로 대신한다.
            var poses = new Pose[Math.Max(MatchRulesSO.MaxPlayerCount, fallbackPoints.Count)];
            for (var index = 0; index < poses.Length; index++)
            {
                poses[index] = TryFindTransform(
                    transforms,
                    $"WaitingSpawnPoint_{index + 1}",
                    out var point)
                    ? new Pose(point.position, point.rotation)
                    : fallbackPoints[index];
            }

            return poses;
        }

        private static Transform FindTransform(Scene scene, ILookup<string, Transform> transforms, string objectName)
        {
            if (TryFindTransform(transforms, objectName, out var found))
            {
                return found;
            }

            throw new InvalidOperationException(
                $"Match scene '{scene.name}' is missing required object '{objectName}'.");
        }

        private static bool TryFindTransform(
            ILookup<string, Transform> transforms,
            string objectName,
            out Transform found)
        {
            found = transforms[objectName].FirstOrDefault();
            return found != null;
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
                            if (replayObjects.Count == MaxReplayObjectCount) break;
                        }
                    }

                    return replayObjects;
                }
            }
        }
    }
}
