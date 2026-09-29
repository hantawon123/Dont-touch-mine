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

    [Serializable]
    public sealed class SceneHighlightOcclusionReference
    {
        [SerializeField]
        private float visibleFromHeight;

        [SerializeField]
        private Transform[] contentRoots = Array.Empty<Transform>();

        [NonSerialized]
        private Renderer[] renderers;

        public SceneHighlightOcclusionReference(
            float visibleFromHeight,
            params Transform[] contentRoots)
        {
            if (!float.IsFinite(visibleFromHeight))
                throw new ArgumentOutOfRangeException(nameof(visibleFromHeight));
            this.visibleFromHeight = visibleFromHeight;
            this.contentRoots = contentRoots ??
                throw new ArgumentNullException(nameof(contentRoots));
        }

        public float VisibleFromHeight => visibleFromHeight;
        public IReadOnlyList<Renderer> Renderers => renderers ??= CaptureRenderers();

        private Renderer[] CaptureRenderers()
        {
            var captured = new List<Renderer>();
            foreach (var root in contentRoots)
            {
                if (root != null)
                    captured.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }

            return captured.ToArray();
        }
    }

    [DisallowMultipleComponent]
    public sealed class MatchSceneConfiguration : MonoBehaviour
    {
        [SerializeField]
        private Transform[] spawnPoints = Array.Empty<Transform>();

        [SerializeField]
        [Tooltip("켜면 씬이 로드될 때마다 스폰 지점 순서를 한 번 섞어 자리(seat)에 배정한다. " +
                 "지점이 플레이어보다 많을 때 매번 다른 자리가 쓰이고, 같은 씬 인스턴스 안에서는 " +
                 "스포너·매치 런타임이 같은 순서를 본다. 끄면 SpawnPoint_1부터 순서대로.")]
        private bool shuffleSpawnPoints;

        [SerializeField]
        [Tooltip("숨기기 차례가 아닌 플레이어가 대기하는 지점. 비우면 일반 스폰 지점을 대신 사용한다.")]
        private Transform[] waitingSpawnPoints = Array.Empty<Transform>();

        [NonSerialized]
        private int[] spawnOrder;

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

        [SerializeField]
        [Tooltip("하이라이트에서 피사체보다 위에 있으면 통째로 숨길 층과 지붕 묶음입니다.")]
        private SceneHighlightOcclusionReference[] highlightOcclusionGroups =
            Array.Empty<SceneHighlightOcclusionReference>();

        public IReadOnlyList<SceneHighlightOcclusionReference> HighlightOcclusionGroups =>
            highlightOcclusionGroups ?? Array.Empty<SceneHighlightOcclusionReference>();

        /// <summary>
        /// 스폰 지점 포즈. <see cref="shuffleSpawnPoints"/>가 켜져 있으면 이 컴포넌트 인스턴스가 살아 있는 동안
        /// 한 번 정한 무작위 순서로 돌려준다(첫 호출에서 섞고 이후 호출은 같은 순서). 권위(호스트)만 배치를
        /// 결정하므로 피어마다 순서가 달라도 문제없다.
        /// </summary>
        public Pose[] CaptureSpawnPoses()
        {
            var poses = CaptureSpawnPoses(spawnPoints);
            if (!shuffleSpawnPoints || poses.Length < 2)
            {
                return poses;
            }

            if (spawnOrder == null || spawnOrder.Length != poses.Length)
            {
                spawnOrder = CreateShuffledOrder(poses.Length, new System.Random());
            }

            return ApplyOrder(poses, spawnOrder);
        }

        public bool ShufflesSpawnPoints => shuffleSpawnPoints;

        /// <summary>0..count-1의 순열(피셔-예이츠).</summary>
        public static int[] CreateShuffledOrder(int count, System.Random random)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var order = new int[count];
            for (var index = 0; index < count; index++) order[index] = index;
            for (var index = count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (order[index], order[swap]) = (order[swap], order[index]);
            }

            return order;
        }

        public static Pose[] ApplyOrder(Pose[] poses, int[] order)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            if (order == null || order.Length != poses.Length)
            {
                throw new ArgumentException("Order must be a permutation of the pose indices.", nameof(order));
            }

            var result = new Pose[poses.Length];
            for (var index = 0; index < poses.Length; index++)
            {
                result[index] = poses[order[index]];
            }

            return result;
        }

        /// <summary>숨기기 대기 지점. 설정하지 않으면 null을 돌려주고 일반 스폰 지점이 대신 쓰인다.</summary>
        public Pose[] CaptureWaitingSpawnPoses()
        {
            if (waitingSpawnPoints == null || waitingSpawnPoints.Length == 0)
            {
                Debug.LogWarning(
                    "[Match] 대기 스폰 지점이 설정되지 않아 일반 스폰 지점을 대신 사용합니다. " +
                    "MatchSceneConfiguration의 Waiting Spawn Points를 연결하세요.",
                    this);
                return null;
            }

            return CaptureSpawnPoses(waitingSpawnPoints);
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
