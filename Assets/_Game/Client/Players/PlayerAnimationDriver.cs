using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Core.Players;
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
        private const string PunchState = "Punch";
        private const string HitState = "Hit";
        private const string StunnedState = "Stunned";
        private const string JumpState = "Jump";
        private const string CarryJumpState = "Carry_TwoHands_Jump";
        private const string AirborneState = "Fall";
        private const string LandState = "Land";
        private const string CarryLandState = "Carry_TwoHands_Land";
        private const float SpeedDampTime = 0.1f;
        private const float CrossFadeSeconds = 0.15f;
        private const float DirectionDeadZone = 0.2f;
        private const float JumpSeconds = 32f / 30f;
        private const float LandSeconds = 20f / 30f;
        private const float HitSeconds = 30f / 30f;
        private const float MinLocomotionPlayback = 0.5f;
        private const float MaxLocomotionPlayback = 2f;

        [SerializeField, Min(0.1f), Tooltip("펀치 모션 유지 시간(초). CombatConfig가 있으면 그 값을 우선한다")]
        private float punchDurationSeconds = 0.5f;

        [SerializeField, Min(0.1f), Tooltip("이동 모션 재생 배율. 1이면 설정 걷기/달리기 속도에서 1배")]
        private float locomotionPlaybackScale = 1f;

        private float PunchDuration =>
            combatant != null && combatant.Config != null
                ? combatant.Config.PunchMotionSeconds
                : punchDurationSeconds;

        private PlayerMovement movement;
        private PlayerCombatant combatant;
        private PlayerInteractor interactor;
        private Animator animator;
        private string currentState;
        private float punchUntilTime;
        private float punchStartedTime;
        private bool leftPunch;
        private float hitUntilTime;
        private float oneShotUntilTime;
        private string oneShotState;
        private bool wasGrounded = true;
        private PlayerPosture lastPosture = PlayerPosture.Standing;
        private bool usesNetworkState;
        private float networkSpeed;
        private bool networkGrounded;
        private int networkAttackSequence;
        private Vector2 networkMoveLocal;
        private bool networkCarrying;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            combatant = GetComponent<PlayerCombatant>();
            interactor = GetComponent<PlayerInteractor>();
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning("PlayerAnimationDriver: 자식에서 Animator를 찾지 못해 비활성화합니다.", this);
                enabled = false;
                return;
            }

            animator.applyRootMotion = false;
            lastPosture = movement.Posture;
        }

        private void OnEnable()
        {
            if (combatant != null)
            {
                combatant.AttackPerformed += OnAttackPerformed;
                combatant.HitReceived += OnHitReceived;
            }
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.AttackPerformed -= OnAttackPerformed;
                combatant.HitReceived -= OnHitReceived;
            }

            if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        private void OnAttackPerformed()
        {
            if (usesNetworkState)
            {
                return;
            }

            PlayPunch();
        }

        private void OnHitReceived() => PlayHit();

        private void PlayPunch()
        {
            // Network peers choose the same hand, even if an attack update was skipped.
            leftPunch = usesNetworkState
                ? (networkAttackSequence & 1) == 0
                : Time.time <= punchUntilTime + 0.35f && punchUntilTime > 0f && !leftPunch;
            punchStartedTime = Time.time;
            punchUntilTime = Time.time + PunchDuration;
            hitUntilTime = 0f;
            oneShotUntilTime = 0f;
            var clip = ResolveCurrentCombatClip(isHit: false);
            currentState = clip;
            animator.CrossFadeInFixedTime(clip, 0.05f, 0, 0f);
        }

        private void PlayHit()
        {
            if (combatant != null && combatant.IsStunned)
            {
                return;
            }

            hitUntilTime = Time.time + HitSeconds;
            punchUntilTime = 0f;
            oneShotUntilTime = 0f;
            var clip = ResolveCurrentCombatClip(isHit: true);
            currentState = clip;
            animator.CrossFadeInFixedTime(clip, 0.05f, 0, 0f);
        }

        private string ResolveCurrentCombatClip(bool isHit)
        {
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            return isHit
                ? ResolveHitClip(movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed)
                : ResolvePunchClip(movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed, leftPunch);
        }

        public void PlayPickup()
        {
            var clip = ResolvePickupClip(movement.Posture);
            PlayOneShot(clip, ClipSeconds(clip));
        }

        public void PlayPutDown()
        {
            var clip = ResolvePutDownClip(movement.Posture);
            PlayOneShot(clip, ClipSeconds(clip));
        }

        public void PlayThrow()
        {
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var clip = ResolveThrowClip(
                movement.Posture, speed, settings.WalkSpeed, settings.SprintSpeed);
            PlayOneShot(clip, 24f / 30f);
        }

        internal static string ResolvePickupClip(PlayerPosture posture) => posture switch
        {
            PlayerPosture.Crouching => "PutUp_TwoHands_Crouch",
            PlayerPosture.Prone => "PutUp_TwoHands_Prone",
            _ => "PutUp_TwoHands",
        };

        internal static string ResolvePutDownClip(PlayerPosture posture) => posture switch
        {
            PlayerPosture.Crouching => "PutDown_TwoHands_Crouch",
            PlayerPosture.Prone => "PutDown_TwoHands_Prone",
            _ => "PutDown_TwoHands",
        };

        private static float ClipSeconds(string clip)
        {
            if (clip.StartsWith("PutUp_TwoHands", System.StringComparison.Ordinal) ||
                clip.StartsWith("PutDown_TwoHands", System.StringComparison.Ordinal))
            {
                return 20f / 30f;
            }

            return clip switch
            {
                "Pickup_Low" or "PutDown_Low" => 60f / 30f,
                _ => 48f / 30f,
            };
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence)
        {
            ApplyNetworkState(planarSpeed, grounded, attackSequence, Vector2.zero, false);
        }

        public void ApplyNetworkState(
            float planarSpeed,
            bool grounded,
            int attackSequence,
            Vector2 planarDirectionLocal,
            bool carrying)
        {
            if (!usesNetworkState)
            {
                usesNetworkState = true;
                networkAttackSequence = attackSequence;
            }
            else if (networkAttackSequence != attackSequence)
            {
                networkAttackSequence = attackSequence;
                PlayPunch();
            }

            networkSpeed = Mathf.Max(0f, planarSpeed);
            networkGrounded = grounded;
            networkMoveLocal = planarDirectionLocal;
            networkCarrying = carrying;
        }

        private void Update()
        {
            if (animator.runtimeAnimatorController != null &&
                HasParameter(animator, "Speed"))
            {
                animator.SetFloat(
                    "Speed",
                    usesNetworkState ? networkSpeed : movement.PlanarSpeed,
                    SpeedDampTime,
                    Time.deltaTime);
            }

            var desiredState = ResolveDesiredState();
            if (desiredState != currentState)
            {
                // Changing posture/speed during the same punch must not restart its wind-up.
                var punchOffset = desiredState.StartsWith("Punch", System.StringComparison.Ordinal) &&
                                  currentState != null && currentState.StartsWith("Punch", System.StringComparison.Ordinal)
                    ? Mathf.Clamp(Time.time - punchStartedTime, 0f, 24f / 30f)
                    : 0f;
                currentState = desiredState;
                animator.CrossFadeInFixedTime(desiredState, CrossFadeSeconds, 0, punchOffset);
            }

            var planarSpeed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var settings = movement.MovementSettings;
            animator.speed = ResolvePlaybackSpeed(
                desiredState,
                planarSpeed,
                settings.WalkSpeed,
                settings.SprintSpeed,
                settings.CrouchSpeed,
                settings.ProneSpeed,
                locomotionPlaybackScale);
        }

        private string ResolveDesiredState()
        {
            if (combatant != null && combatant.IsStunned)
            {
                ClearOneShot();
                punchUntilTime = 0f;
                hitUntilTime = 0f;
                lastPosture = movement.Posture;
                wasGrounded = usesNetworkState ? networkGrounded : movement.IsGrounded;
                return StunnedState;
            }

            if (Time.time < hitUntilTime)
            {
                return ResolveCurrentCombatClip(isHit: true);
            }

            if (Time.time < punchUntilTime)
            {
                return ResolveCurrentCombatClip(isHit: false);
            }

            var posture = movement.Posture;
            var settings = movement.MovementSettings;
            var speed = usesNetworkState ? networkSpeed : movement.PlanarSpeed;
            var move = usesNetworkState ? networkMoveLocal : movement.PlanarVelocityLocal;
            var carrying = usesNetworkState
                ? networkCarrying
                : interactor != null && interactor.CarriedItem != null;
            var grounded = usesNetworkState ? networkGrounded : movement.IsGrounded;

            if (!grounded)
            {
                var leftGround = wasGrounded;
                wasGrounded = false;
                lastPosture = posture;
                // 웅크리기·엎드리기는 콜라이더 출렁임으로 잠깐 떠도 서서 Fall/Land를 쓰지 않는다.
                if (posture != PlayerPosture.Standing)
                {
                    ClearOneShot();
                    return ResolveLocomotionClip(
                        posture, carrying, 0f, Vector2.zero, settings.WalkSpeed, settings.SprintSpeed);
                }

                if (leftGround)
                {
                    PlayOneShot(ResolveJumpClip(carrying), JumpSeconds);
                }

                if (Time.time < oneShotUntilTime && IsJumpState(oneShotState))
                {
                    return oneShotState;
                }

                // 들고 점프 클립이 끝난 뒤에도 손 든 포즈를 유지한다(Carry_Fall 없음).
                return carrying ? CarryJumpState : AirborneState;
            }

            if (!wasGrounded)
            {
                wasGrounded = true;
                if (posture == PlayerPosture.Standing)
                {
                    PlayOneShot(ResolveLandClip(carrying), LandSeconds);
                }
            }

            if (posture != lastPosture)
            {
                var transition = TransitionClip(lastPosture, posture, carrying);
                lastPosture = posture;
                if (transition != null)
                {
                    PlayOneShot(transition, TransitionSeconds(transition));
                }
            }

            var locomotion = ResolveLocomotionClip(
                posture,
                carrying,
                speed,
                move,
                settings.WalkSpeed,
                settings.SprintSpeed);

            if (Time.time < oneShotUntilTime && !string.IsNullOrEmpty(oneShotState))
            {
                // 집기·내려놓기 중 이동하면 바로 들고 걷기/기어가기로 넘긴다.
                if (IsMovementInterruptible(oneShotState) && IsLocomotionMoving(locomotion))
                {
                    ClearOneShot();
                    return locomotion;
                }

                return oneShotState;
            }

            return locomotion;
        }

        private void PlayOneShot(string state, float seconds)
        {
            oneShotState = state;
            oneShotUntilTime = Time.time + seconds;
        }

        private void ClearOneShot()
        {
            oneShotState = null;
            oneShotUntilTime = 0f;
        }

        internal static string ResolveJumpClip(bool carrying) =>
            carrying ? CarryJumpState : JumpState;

        internal static string ResolveLandClip(bool carrying) =>
            carrying ? CarryLandState : LandState;

        internal static string ResolveThrowClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed)
        {
            if (posture == PlayerPosture.Prone)
            {
                return planarSpeed > 0.15f ? "Throw_TwoHands_Crawl" : "Throw_TwoHands_Prone";
            }

            return ResolveCombatLocomotionClip(
                "Throw_TwoHands", posture, planarSpeed, walkSpeed, sprintSpeed);
        }

        internal static string ResolvePunchClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed,
            bool leftHand = false) =>
            ResolveCombatLocomotionClip(leftHand ? "Punch_Left" : "Punch", posture, planarSpeed, walkSpeed, sprintSpeed);

        internal static string ResolveHitClip(
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed) =>
            ResolveCombatLocomotionClip("Hit", posture, planarSpeed, walkSpeed, sprintSpeed);

        internal static string ResolveCombatLocomotionClip(
            string prefix,
            PlayerPosture posture,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed)
        {
            if (posture == PlayerPosture.Crouching)
            {
                return planarSpeed > 0.15f ? $"{prefix}_Crouch_Walk" : $"{prefix}_Crouch";
            }

            if (planarSpeed >= sprintSpeed * 0.85f)
            {
                return $"{prefix}_Run";
            }

            if (planarSpeed >= walkSpeed * 0.35f)
            {
                return $"{prefix}_Walk";
            }

            return prefix;
        }

        internal static bool IsJumpState(string state) =>
            state == JumpState || state == CarryJumpState;

        internal static bool IsMovementInterruptible(string state) =>
            !string.IsNullOrEmpty(state) &&
            (state.StartsWith("Pickup", System.StringComparison.Ordinal) ||
             state.StartsWith("PutUp", System.StringComparison.Ordinal) ||
             state.StartsWith("PutDown", System.StringComparison.Ordinal) ||
             IsLandState(state));

        internal static bool IsLandState(string state) =>
            state == LandState || state == CarryLandState;

        internal static bool IsLocomotionMoving(string state) =>
            !string.IsNullOrEmpty(state) &&
            (state.IndexOf("Walk", System.StringComparison.Ordinal) >= 0 ||
             state.IndexOf("Run", System.StringComparison.Ordinal) >= 0 ||
             state.IndexOf("Crawl", System.StringComparison.Ordinal) >= 0);

        internal static string ResolveLocomotionClip(
            PlayerPosture posture,
            bool carrying,
            float speed,
            Vector2 localMove,
            float walkSpeed,
            float sprintSpeed)
        {
            var direction = ResolveDirection(localMove);
            var moving = direction != MoveDirection.Neutral && speed >= 0.35f;

            if (posture == PlayerPosture.Prone)
            {
                if (!moving)
                {
                    return carrying ? "Carry_TwoHands_Prone_Idle" : "Prone_Idle";
                }

                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Crawl_Forward",
                        "Carry_TwoHands_Crawl_Back",
                        "Carry_TwoHands_Crawl_Left",
                        "Carry_TwoHands_Crawl_Right",
                        direction)
                    : DirectionClip("Crawl_Forward", "Crawl_Back", "Crawl_Left", "Crawl_Right", direction);
            }

            if (posture == PlayerPosture.Crouching)
            {
                if (!moving)
                {
                    return carrying ? "Carry_TwoHands_Crouch_Idle" : "Crouch_Idle";
                }

                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Crouch_Walk_Forward",
                        "Carry_TwoHands_Crouch_Walk_Back",
                        "Carry_TwoHands_Crouch_Walk_Left",
                        "Carry_TwoHands_Crouch_Walk_Right",
                        direction)
                    : DirectionClip(
                        "Crouch_Walk_Forward",
                        "Crouch_Walk_Back",
                        "Crouch_Walk_Left",
                        "Crouch_Walk_Right",
                        direction);
            }

            if (!moving)
            {
                return carrying ? "Carry_TwoHands" : "Idle";
            }

            if (speed >= (walkSpeed + sprintSpeed) * 0.5f)
            {
                return carrying
                    ? DirectionClip(
                        "Carry_TwoHands_Run_Forward",
                        "Carry_TwoHands_Run_Back",
                        "Carry_TwoHands_Run_Left",
                        "Carry_TwoHands_Run_Right",
                        direction)
                    : DirectionClip("Run_Forward", "Run_Back", "Run_Left", "Run_Right", direction);
            }

            return carrying
                ? DirectionClip(
                    "Carry_TwoHands_Walk_Forward",
                    "Carry_TwoHands_Walk_Back",
                    "Carry_TwoHands_Walk_Left",
                    "Carry_TwoHands_Walk_Right",
                    direction)
                : DirectionClip("Walk_Forward", "Walk_Back", "Walk_Left", "Walk_Right", direction);
        }

        internal static MoveDirection ResolveDirection(Vector2 localMove)
        {
            if (localMove.sqrMagnitude < DirectionDeadZone * DirectionDeadZone)
            {
                return MoveDirection.Neutral;
            }

            if (Mathf.Abs(localMove.x) > Mathf.Abs(localMove.y))
            {
                return localMove.x > 0f ? MoveDirection.Left : MoveDirection.Right;
            }

            return localMove.y > 0f ? MoveDirection.Forward : MoveDirection.Back;
        }

        private static string DirectionClip(
            string forward,
            string back,
            string left,
            string right,
            MoveDirection direction) => direction switch
        {
            MoveDirection.Back => back,
            MoveDirection.Left => left,
            MoveDirection.Right => right,
            _ => forward
        };

        /// <summary>
        /// 이동 클립만 실제 속도 / 기준 속도로 재생 배율을 맞춘다.
        /// Idle·원샷·기절 등은 1배를 유지한다.
        /// </summary>
        internal static float ResolvePlaybackSpeed(
            string state,
            float planarSpeed,
            float walkSpeed,
            float sprintSpeed,
            float crouchSpeed,
            float proneSpeed,
            float scale = 1f)
        {
            if (string.IsNullOrEmpty(state))
            {
                return 1f;
            }

            // Punch_Walk / Hit_Run 등은 하체가 섞여 있어도 임팩트 타이밍을 늘리지 않는다.
            if (state.StartsWith("Punch", System.StringComparison.Ordinal) ||
                state.StartsWith("Hit", System.StringComparison.Ordinal) ||
                state.StartsWith("Throw", System.StringComparison.Ordinal))
            {
                return 1f;
            }

            var reference = LocomotionReferenceSpeed(
                state, walkSpeed, sprintSpeed, crouchSpeed, proneSpeed);
            if (reference <= 0f)
            {
                return 1f;
            }

            var rate = (planarSpeed / reference) * Mathf.Max(0.1f, scale);
            return Mathf.Clamp(rate, MinLocomotionPlayback, MaxLocomotionPlayback);
        }

        private static float LocomotionReferenceSpeed(
            string state,
            float walkSpeed,
            float sprintSpeed,
            float crouchSpeed,
            float proneSpeed)
        {
            if (state.IndexOf("Crawl", System.StringComparison.Ordinal) >= 0)
            {
                return proneSpeed;
            }

            if (state.IndexOf("Crouch_Walk", System.StringComparison.Ordinal) >= 0)
            {
                return crouchSpeed;
            }

            if (state.IndexOf("Run", System.StringComparison.Ordinal) >= 0)
            {
                return sprintSpeed;
            }

            if (state.IndexOf("Walk", System.StringComparison.Ordinal) >= 0)
            {
                return walkSpeed;
            }

            return 0f;
        }

        internal static string TransitionClip(PlayerPosture from, PlayerPosture to, bool carrying) => (from, to) switch
        {
            (PlayerPosture.Standing, PlayerPosture.Crouching) =>
                carrying ? "Carry_TwoHands_Crouch_Start" : "Crouch_Start",
            (PlayerPosture.Crouching, PlayerPosture.Standing) =>
                carrying ? "Carry_TwoHands_Crouch_End" : "Crouch_End",
            (PlayerPosture.Standing, PlayerPosture.Prone) =>
                carrying ? "Carry_TwoHands_Prone_Start" : "Prone_Start",
            (PlayerPosture.Crouching, PlayerPosture.Prone) =>
                carrying ? "Carry_TwoHands_Crouch_To_Prone" : "Crouch_To_Prone",
            (PlayerPosture.Prone, PlayerPosture.Standing) =>
                carrying ? "Carry_TwoHands_Prone_End" : "Prone_End",
            (PlayerPosture.Prone, PlayerPosture.Crouching) =>
                carrying ? "Carry_TwoHands_Prone_To_Crouch" : "Prone_To_Crouch",
            _ => null
        };

        private static float TransitionSeconds(string clip)
        {
            if (clip.IndexOf("Crouch_To_Prone", System.StringComparison.Ordinal) >= 0 ||
                clip.IndexOf("Prone_To_Crouch", System.StringComparison.Ordinal) >= 0)
            {
                return 36f / 30f;
            }

            return 24f / 30f;
        }

        private static bool HasParameter(Animator target, string name)
        {
            foreach (var parameter in target.parameters)
            {
                if (parameter.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        internal enum MoveDirection
        {
            Neutral,
            Forward,
            Back,
            Left,
            Right
        }
    }
}
