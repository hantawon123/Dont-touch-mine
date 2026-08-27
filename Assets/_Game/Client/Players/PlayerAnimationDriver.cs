using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>
    /// 이동 상태를 읽어 애니메이터 파라미터로 전달한다.
    /// 코드가 이동을 소유하고 애니메이션은 표현만 담당한다. (루트 모션 미사용)
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int IsAirborneId = Animator.StringToHash("IsAirborne");

        private const float SpeedDampTime = 0.1f;

        private PlayerMovement movement;
        private Animator animator;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning("PlayerAnimationDriver: 자식에서 Animator를 찾지 못해 비활성화합니다.", this);
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
        }

        private void Update()
        {
            animator.SetFloat(SpeedId, movement.PlanarSpeed, SpeedDampTime, Time.deltaTime);
            animator.SetBool(IsAirborneId, !movement.IsGrounded);
        }
    }
}
