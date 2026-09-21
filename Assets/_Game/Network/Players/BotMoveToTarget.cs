using Game.Core.Players;
using UnityEngine;

namespace Game.Network.Players
{
    [DisallowMultipleComponent]
    public sealed class BotMoveToTarget : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField, Min(0.1f)]
        private float stoppingDistance = 0.8f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public PlayerInputIntent CreateInput(Vector3 currentPosition)
        {
            // 목표가 없거나 이 기능이 꺼져 있으면 정지한다.
            if (!isActiveAndEnabled || target == null)
            {
                return default;
            }

            Vector3 offset = target.position - currentPosition;

            // 이번에는 바닥 위 이동만 다룬다.
            offset.y = 0f;

            // 목표에 충분히 가까워졌으면 정지한다.
            if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                return default;
            }

            // 목표가 있는 방향을 회전 각도로 바꾼다.
            float targetYaw =
                Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

            // 목표 방향을 바라보며 앞으로 이동하라는 조작이다.
            return new PlayerInputIntent(
                moveX: 0f,
                moveY: 1f,
                lookYawDegrees: targetYaw,
                buttons: PlayerInputButtons.None);
        }
    }
}