using Game.Client.Combat;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>
    /// 이동·전투 상태를 읽어 애니메이터 상태를 직접 지시한다.
    /// 전환 조건을 애니메이터 그래프가 아니라 코드가 소유한다. (루트 모션 미사용)
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");

        private const string LocomotionState = "Locomotion";
        private const string AirborneState = "Airborne";
        private const string CrouchMoveState = "CrouchMove";
        private const string CrawlState = "Crawl";
        private const string PunchState = "Punch";
        private const string StunnedState = "Stunned";

        private const float SpeedDampTime = 0.1f;
        private const float CrossFadeSeconds = 0.15f;

        [SerializeField, Min(0.1f), Tooltip("펀치 모션 유지 시간(초)")]
        private float punchDurationSeconds = 0.5f;

        private PlayerMovement movement;
        private PlayerCombatant combatant;
        private Animator animator;
        private string currentState;
        private float punchUntilTime;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            combatant = GetComponent<PlayerCombatant>();
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning("PlayerAnimationDriver: 자식에서 Animator를 찾지 못해 비활성화합니다.", this);
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            if (combatant != null)
            {
                combatant.AttackPerformed += OnAttackPerformed;
            }
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.AttackPerformed -= OnAttackPerformed;
            }
        }

        private void OnAttackPerformed()
        {
            punchUntilTime = Time.time + punchDurationSeconds;
        }

        private void Update()
        {
            animator.SetFloat(SpeedId, movement.PlanarSpeed, SpeedDampTime, Time.deltaTime);

            var desiredState = ResolveDesiredState();
            if (desiredState != currentState)
            {
                currentState = desiredState;
                animator.CrossFadeInFixedTime(desiredState, CrossFadeSeconds);
            }
        }

        private string ResolveDesiredState()
        {
            if (combatant != null && combatant.IsStunned)
            {
                return StunnedState;
            }

            if (Time.time < punchUntilTime)
            {
                return PunchState;
            }

            if (!movement.IsGrounded)
            {
                return AirborneState;
            }

            return movement.Posture switch
            {
                PlayerPosture.Crouching => CrouchMoveState,
                PlayerPosture.Prone => CrawlState,
                _ => LocomotionState
            };
        }
    }
}
