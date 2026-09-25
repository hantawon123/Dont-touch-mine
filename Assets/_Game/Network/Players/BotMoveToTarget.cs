using Game.Core.Players;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Network.Players
{
    /// <summary>
    /// 봇의 다리. NavMesh 경로를 따라 이동 입력을 만든다. 목적지는 씬의 Transform이거나
    /// 실행부가 넘긴 좌표 하나다. 좌표 목적지는 "관측 당시 위치"처럼 나중에 바뀔 수 있는
    /// 대상을 따라가지 않게 하려는 것이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotMoveToTarget : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField, Min(0.1f)]
        private float stoppingDistance = 0.8f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Stopping distance when the target was off the NavMesh (on a table) and projected to the floor. Closer, to stay within hand reach.")]
        private float offMeshStoppingDistance = 0.3f;

        [SerializeField, Min(0.05f)]
        private float cornerReachDistance = 0.35f;

        [SerializeField, Min(0.05f)]
        private float pathRefreshSeconds = 0.25f;

        private NavMeshPath path;

        private int nextCornerIndex;
        private float nextPathRefreshTime;
        private bool hasPath;
        private bool hasDestination;
        private Vector3 destination;
        private bool hasIdleYaw;
        private float idleYaw;

        // The requested point projected onto the NavMesh. A prop on a table is not walkable; the bot must
        // steer to, and consider itself arrived at, the nearest walkable point instead of pushing into the table.
        private bool hasProjectedDestination;
        private Vector3 projectedDestination;

        public float StoppingDistance => stoppingDistance;

        /// <summary>
        /// Stopping distance for the current destination: the normal value for walkable targets, a shorter one
        /// when the target was off the NavMesh and has been projected to the nearest floor point.
        /// </summary>
        private float EffectiveStoppingDistance(Vector3 requested)
        {
            if (!hasProjectedDestination)
            {
                return stoppingDistance;
            }

            var shift = projectedDestination - requested;
            shift.y = 0f;
            return shift.sqrMagnitude > 0.3f * 0.3f ? Mathf.Min(stoppingDistance, offMeshStoppingDistance) : stoppingDistance;
        }

        /// <summary>Transform 목적지든 좌표 목적지든 하나라도 있으면 true.</summary>
        public bool HasDestination => target != null || hasDestination;

        /// <summary>마지막 경로 계산이 완전한 경로를 찾았는지. 목적지가 없으면 false.</summary>
        public bool HasCompletePath => hasPath;

        /// <summary>Diagnostics: the point the last walking input steered to, and whether one was produced.</summary>
        public bool HasSteeringPoint { get; private set; }
        public Vector3 LastSteeringPoint { get; private set; }

        private void Awake()
        {
            path = new NavMeshPath();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            hasDestination = false;
            ResetPath();
        }

        /// <summary>좌표 하나를 목적지로 삼는다. 이전 Transform 목적지와 제자리 회전 지시는 해제된다.</summary>
        public void SetDestination(Vector3 worldPosition)
        {
            target = null;
            destination = worldPosition;
            hasDestination = true;
            hasIdleYaw = false;
            ResetPath();
        }

        public void ClearDestination()
        {
            target = null;
            hasDestination = false;
            ResetPath();
        }

        /// <summary>
        /// 목적지가 없을 때 바라볼 각도. Explore(제자리 회전 관찰)가 쓴다.
        /// 목적지를 새로 주면 해제된다.
        /// </summary>
        public void SetIdleYaw(float yawDegrees)
        {
            idleYaw = yawDegrees;
            hasIdleYaw = float.IsFinite(yawDegrees);
        }

        public void ClearIdleYaw()
        {
            hasIdleYaw = false;
        }

        /// <summary>현재 위치가 목적지의 정지 거리 안인지(높이 무시).</summary>
        public bool IsAtDestination(Vector3 currentPosition)
        {
            if (!TryGetDestination(out var point))
            {
                return false;
            }

            var stop = EffectiveStoppingDistance(point);
            if (hasProjectedDestination)
            {
                point = projectedDestination;
            }

            var offset = point - currentPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= stop * stop;
        }

        /// <summary>
        /// 이동 입력을 만든다. 목적지가 없거나 도착했거나 길이 없으면 "제자리에서 현재 각도 유지"
        /// 입력을 돌려준다. 기본값(0도)을 돌려주면 모터가 매 틱 봇을 +Z로 돌려 세우므로,
        /// 스폰 회전과 도착 후 시선이 유지되지 않는다.
        /// </summary>
        public PlayerInputIntent CreateInput(Vector3 currentPosition, float currentYawDegrees)
        {
            HasSteeringPoint = false;
            var hold = HoldStill(hasIdleYaw ? idleYaw : currentYawDegrees);
            if (!isActiveAndEnabled || !TryGetDestination(out var destinationPoint))
            {
                return hold;
            }

            Vector3 targetOffset = destinationPoint - currentPosition;
            targetOffset.y = 0f;

            if (targetOffset.sqrMagnitude <=
                stoppingDistance * stoppingDistance)
            {
                return hold;
            }

            if (!hasPath || Time.time >= nextPathRefreshTime)
            {
                RefreshPath(currentPosition, destinationPoint);
            }

            if (!hasPath)
            {
                return hold;
            }

            if (hasProjectedDestination)
            {
                var stop = EffectiveStoppingDistance(destinationPoint);
                destinationPoint = projectedDestination;
                var projectedOffset = destinationPoint - currentPosition;
                projectedOffset.y = 0f;
                if (projectedOffset.sqrMagnitude <= stop * stop)
                {
                    return hold;
                }
            }

            // 이미 도착한 모퉁이는 건너뛴다.
            while (nextCornerIndex < path.corners.Length)
            {
                Vector3 cornerOffset =
                    path.corners[nextCornerIndex] - currentPosition;
                cornerOffset.y = 0f;

                if (cornerOffset.sqrMagnitude >
                    cornerReachDistance * cornerReachDistance)
                {
                    break;
                }

                nextCornerIndex++;
            }

            Vector3 steeringPoint =
                nextCornerIndex < path.corners.Length
                    ? path.corners[nextCornerIndex]
                    : destinationPoint;

            Vector3 moveOffset = steeringPoint - currentPosition;
            moveOffset.y = 0f;

            if (moveOffset.sqrMagnitude < 0.001f)
            {
                return hold;
            }

            float targetYaw =
                Mathf.Atan2(moveOffset.x, moveOffset.z) *
                Mathf.Rad2Deg;
            HasSteeringPoint = true;
            LastSteeringPoint = steeringPoint;

            return new PlayerInputIntent(
                moveX: 0f,
                moveY: 1f,
                lookYawDegrees: targetYaw,
                buttons: PlayerInputButtons.None);
        }

        private static PlayerInputIntent HoldStill(float yawDegrees) =>
            new PlayerInputIntent(
                moveX: 0f,
                moveY: 0f,
                lookYawDegrees: float.IsFinite(yawDegrees) ? yawDegrees : 0f,
                buttons: PlayerInputButtons.None);

        private bool TryGetDestination(out Vector3 point)
        {
            if (target != null)
            {
                point = target.position;
                return true;
            }

            point = destination;
            return hasDestination;
        }

        private void ResetPath()
        {
            hasPath = false;
            hasProjectedDestination = false;
            nextPathRefreshTime = 0f;
        }

        private void RefreshPath(Vector3 currentPosition, Vector3 destinationPoint)
        {
            nextPathRefreshTime = Time.time + pathRefreshSeconds;
            hasPath = false;

            // 캐릭터와 목표의 실제 위치를 가장 가까운 파란 길 위로 맞춘다.
            if (!NavMesh.SamplePosition(
                    currentPosition,
                    out NavMeshHit startHit,
                    2f,
                    NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(
                    destinationPoint,
                    out NavMeshHit targetHit,
                    2f,
                    NavMesh.AllAreas))
            {
                return;
            }

            path.ClearCorners();

            if (!NavMesh.CalculatePath(
                    startHit.position,
                    targetHit.position,
                    NavMesh.AllAreas,
                    path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return;
            }

            // 첫 점은 대개 현재 위치이므로 그다음 모퉁이부터 향한다.
            nextCornerIndex = path.corners.Length > 1 ? 1 : 0;
            hasPath = path.corners.Length > 0;
            projectedDestination = targetHit.position;
            hasProjectedDestination = hasPath;
        }
    }
}
