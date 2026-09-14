using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Players;
using Game.SOAP.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Client.Players
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour, IPlayerInputIntentSource
    {
        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private MovementConfigSO movementConfig;

        [SerializeField]
        private Transform visualRoot;

        public PlayerPosture Posture { get; private set; } = PlayerPosture.Standing;

        public PlayerMovementSettings MovementSettings => new(
            movementConfig.WalkSpeed,
            movementConfig.SprintSpeed,
            movementConfig.RotationSpeedDegrees,
            movementConfig.JumpHeight,
            movementConfig.GravityMultiplier,
            movementConfig.CrouchSpeed,
            movementConfig.ProneSpeed,
            movementConfig.StandHeight,
            movementConfig.CrouchHeight,
            movementConfig.ProneHeight,
            movementConfig.MaxStamina,
            movementConfig.StaminaDrainPerSecond,
            movementConfig.StaminaRecoveryPerSecond);

        public float CurrentEyeHeight => Posture switch
        {
            PlayerPosture.Crouching => movementConfig.CrouchEyeHeight,
            PlayerPosture.Prone => movementConfig.ProneEyeHeight,
            _ => movementConfig.StandEyeHeight
        };

        private CharacterController controller;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction proneAction;
        private InputAction attackAction;
        private PlayerInteractor interactor;
        private ItemPlacementController placement;
        private Transform cameraTransform;
        private float verticalVelocity;
        private Vector3 externalVelocity;

        /// <summary>Esc 메뉴처럼 이동을 잠시 막을 때 사용한다.</summary>
        public bool IsMovementLocked { get; set; }

        /// <summary>기절이 이동을 막을 때 사용한다. 메뉴 잠금과 같은 플래그를 쓰지 않는다.</summary>
        public bool IsCombatLocked { get; set; }

        private bool IsLocomotionLocked => IsMovementLocked || IsCombatLocked;

        /// <summary>수평 이동 속력(m/s). 애니메이션 등 표현 계층이 읽는다.</summary>
        public float PlanarSpeed
        {
            get
            {
                var velocity = controller.velocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        /// <summary>
        /// First 클립 기준 수평 속도. +X가 왼쪽, +Y가 앞이다.
        /// Unity 로컬 +X(오른쪽)는 여기서 부호를 뒤집는다.
        /// </summary>
        public Vector2 PlanarVelocityLocal
        {
            get
            {
                if (controller == null)
                {
                    return Vector2.zero;
                }

                var velocity = controller.velocity;
                velocity.y = 0f;
                var local = transform.InverseTransformDirection(velocity);
                return new Vector2(-local.x, local.z);
            }
        }

        public bool IsGrounded => controller.isGrounded;

        public void ApplyNetworkPosture(PlayerPosture posture)
        {
            Posture = posture;
        }

        /// <summary>넉백 등 외부 충격을 가한다. 시간이 지나며 자연히 줄어든다.</summary>
        public void AddImpulse(Vector3 impulse)
        {
            externalVelocity += impulse;
        }

        /// <summary>
        /// 고정 카메라 무대(엔딩 유치장)용 조작 기준. 지정되면 WASD는 이 기준의 앞·오른쪽으로
        /// 움직이고, 몸은 카메라가 아니라 이동 방향을 향한다. 멈추면 지금 방향을 유지한다.
        /// </summary>
        /// <remarks>
        /// The normal rule (body faces the camera yaw) assumes the camera sits
        /// behind the player. On a stage the camera is fixed in front of the
        /// room, so that rule would turn everyone's back to the audience.
        /// </remarks>
        private Transform stageReference;

        public void SetStageControl(Transform reference) => stageReference = reference;

        public void ClearStageControl() => stageReference = null;

        public PlayerInputIntent CaptureInputIntent()
        {
            if (playerMap == null)
            {
                return default;
            }

            if (IsLocomotionLocked || IsTextInputFocused())
            {
                var heldYaw = stageReference == null && TryEnsureCamera()
                    ? cameraTransform.eulerAngles.y
                    : transform.eulerAngles.y;
                return new PlayerInputIntent(
                    0f,
                    0f,
                    heldYaw,
                    PlayerInputButtons.None);
            }

            if (!playerMap.enabled)
            {
                playerMap.Enable();
            }

            var move = moveAction.ReadValue<Vector2>();
            var buttons = PlayerInputButtons.None;

            if (jumpAction.IsPressed())
            {
                buttons |= PlayerInputButtons.Jump;
            }

            if (sprintAction.IsPressed())
            {
                buttons |= PlayerInputButtons.Sprint;
            }

            if (crouchAction.IsPressed())
            {
                buttons |= PlayerInputButtons.Crouch;
            }

            if (proneAction.IsPressed())
            {
                buttons |= PlayerInputButtons.Prone;
            }

            // 소지 중인 좌클릭은 던지기/배치 입력이므로 네트워크 공격으로 보내지 않는다.
            // Esc 메뉴 버튼을 누르는 좌클릭도 펀치로 나가지 않게 한다.
            if ((interactor == null || interactor.CarriedItem == null) &&
                attackAction.IsPressed() &&
                !ShouldIgnoreAttackInput() &&
                (placement == null || !placement.BlocksAttack))
            {
                buttons |= PlayerInputButtons.Attack;
            }

            if (stageReference != null)
            {
                // 화면 기준 방향을 월드 방향으로 바꾼 뒤 "그 방향으로 전진"으로 다시 표현한다.
                // 모터는 lookYaw 방향으로 몸을 돌리고 그 앞으로 걷게 되므로 결과가 같다.
                var world = ToReferenceRelativeDirection(stageReference, move);
                if (world.sqrMagnitude > 0.0001f)
                {
                    var yaw = Mathf.Atan2(world.x, world.z) * Mathf.Rad2Deg;
                    return new PlayerInputIntent(0f, Mathf.Min(1f, world.magnitude), yaw, buttons);
                }

                return new PlayerInputIntent(0f, 0f, transform.eulerAngles.y, buttons);
            }

            var lookYaw = TryEnsureCamera()
                ? cameraTransform.eulerAngles.y
                : transform.eulerAngles.y;
            return new PlayerInputIntent(move.x, move.y, lookYaw, buttons);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (inputActions == null)
            {
                Debug.LogError("PlayerMovement: InputActionAsset이 연결되지 않았습니다. Inspector에서 InputSystem_Actions를 연결하세요.", this);
                enabled = false;
                return;
            }

            if (movementConfig == null)
            {
                Debug.LogError("PlayerMovement: MovementConfigSO가 연결되지 않았습니다. Inspector에서 MovementConfig 에셋을 연결하세요.", this);
                enabled = false;
                return;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
            sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: true);
            crouchAction = playerMap.FindAction("Crouch", throwIfNotFound: true);
            proneAction = playerMap.FindAction("Prone", throwIfNotFound: true);
            attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
            interactor = GetComponent<PlayerInteractor>();
            placement = GetComponent<ItemPlacementController>();

            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual");
                if (visualRoot == null)
                {
                    Debug.LogWarning("PlayerMovement: visualRoot가 없어 자세 변경 시 겉모습이 그대로 유지됩니다.", this);
                }
            }
        }

        private void OnEnable()
        {
            playerMap?.Enable();
        }

        private void Update()
        {
            // 네트워크 플레이어는 KCC가 이동을 맡기고 CharacterController는 꺼 둔다.
            // 이 컴포넌트는 오너 입력 캡처용으로만 살아 있으므로 Move를 부르면 안 된다.
            if (controller == null || !controller.enabled)
            {
                return;
            }

            var inputLocked = IsLocomotionLocked || IsTextInputFocused();
            var input = inputLocked ? Vector2.zero : moveAction.ReadValue<Vector2>();
            var direction = ToCameraRelativeDirection(input);

            if (!inputLocked)
            {
                HandlePostureInput();
            }

            var jumpRequested = !inputLocked &&
                jumpAction.WasPressedThisFrame() &&
                Posture == PlayerPosture.Standing;
            verticalVelocity = PlayerMovementKinematics.StepVerticalVelocity(
                verticalVelocity,
                controller.isGrounded,
                jumpRequested,
                Physics.gravity.y,
                Time.deltaTime,
                MovementSettings);

            // 넉백 등 외부 충격은 시간이 지나며 감쇠한다.
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 12f * Time.deltaTime);

            var moveSpeed = GetMoveSpeed();
            var velocity = direction * moveSpeed + externalVelocity;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            // 몸은 항상 카메라가 보는 방향(좌우)을 향한다. 조준 기반 게임의 표준 방식.
            // 무대 모드에서는 이동 방향을 향하고, 멈추면 지금 방향을 유지한다.
            var lookForward = stageReference != null ? direction : GetCameraFlatForward();
            if (!inputLocked && lookForward.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(lookForward);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, movementConfig.RotationSpeedDegrees * Time.deltaTime);
            }
        }

        /// <summary>
        /// 커서가 풀려 있거나 UI 위를 누른 좌클릭은 펀치가 아니다.
        /// </summary>
        public static bool ShouldIgnoreAttackInput()
        {
            if (!Game.Client.Common.WebPointerInput.IsLocked)
            {
                return true;
            }

            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// UI 입력창에 커서가 있을 때 키 입력이 이동 명령으로도 전달되는 것을 막는다.
        /// 레거시 Lobby 채팅과 TMP 기반 화면을 같은 규칙으로 처리한다.
        /// </summary>
        public static bool IsTextInputFocused()
        {
            if (MatchChatView.BlocksPlayerInput)
            {
                return true;
            }

            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            var legacyInput = selected.GetComponentInParent<InputField>();
            if (legacyInput != null && legacyInput.isFocused)
            {
                return true;
            }

            var tmpInput = selected.GetComponentInParent<TMP_InputField>();
            return tmpInput != null && tmpInput.isFocused;
        }

        private void HandlePostureInput()
        {
            // 공중에서는 자세를 바꿀 수 없다.
            if (!controller.isGrounded)
            {
                return;
            }

            if (crouchAction.WasPressedThisFrame())
            {
                SetPosture(Posture == PlayerPosture.Crouching
                    ? PlayerPosture.Standing
                    : PlayerPosture.Crouching);
            }

            if (proneAction.WasPressedThisFrame())
            {
                SetPosture(Posture == PlayerPosture.Prone
                    ? PlayerPosture.Standing
                    : PlayerPosture.Prone);
            }

            // 앉기/엎드리기 중 점프 키는 일어서기로 동작한다.
            if (jumpAction.WasPressedThisFrame() && Posture != PlayerPosture.Standing)
            {
                SetPosture(PlayerPosture.Standing);
            }
        }

        private void SetPosture(PlayerPosture posture)
        {
            if (Posture == posture)
            {
                return;
            }

            var targetHeight = GetPostureHeight(posture);
            if (!HasHeadroom(targetHeight))
            {
                return;
            }

            Posture = posture;
            ApplyPostureShape(posture, targetHeight);
        }

        // 몸을 세울 때 머리 위 공간이 있는지 검사한다. (책상 밑 등에서는 일어설 수 없다)
        private bool HasHeadroom(float targetHeight)
        {
            if (targetHeight <= controller.height)
            {
                return true;
            }

            var radius = controller.radius * 0.95f;
            var topSphereCenter = transform.position + controller.center
                + Vector3.up * (controller.height * 0.5f - controller.radius);
            var castDistance = targetHeight - controller.height;

            // 자기 자신과 겹친 상태에서 시작하는 캐스트는 자기 콜라이더를 무시한다.
            return !Physics.SphereCast(
                topSphereCenter, radius, Vector3.up, out _,
                castDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private float GetPostureHeight(PlayerPosture posture)
        {
            return posture switch
            {
                PlayerPosture.Crouching => movementConfig.CrouchHeight,
                PlayerPosture.Prone => movementConfig.ProneHeight,
                _ => movementConfig.StandHeight
            };
        }

        private float GetMoveSpeed()
        {
            return Posture switch
            {
                PlayerPosture.Crouching => movementConfig.CrouchSpeed,
                PlayerPosture.Prone => movementConfig.ProneSpeed,
                _ => PlayerMovementKinematics.MoveSpeed(
                    MovementSettings,
                    sprintAction.IsPressed())
            };
        }

        private void ApplyPostureShape(PlayerPosture posture, float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (visualRoot == null)
            {
                return;
            }

            // 애니메이션이 있는 캐릭터 모델은 찌그러뜨리지 않는다.
            // 자세 표현은 애니메이션이 담당하고, 여기서는 충돌체만 조정한다.
            if (visualRoot.GetComponentInChildren<Animator>() != null)
            {
                return;
            }

            // 임시 캡슐 비주얼: 자세를 스케일과 회전으로 표현한다. (기본 캡슐 높이 2m 기준)
            var scale = visualRoot.localScale;
            if (posture == PlayerPosture.Prone)
            {
                // 몸 길이는 서 있을 때 키를 유지한 채 앞으로 눕힌다.
                var bodyRadius = height * 0.5f;
                visualRoot.localScale = new Vector3(scale.x, movementConfig.StandHeight * 0.5f, scale.z);
                visualRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visualRoot.localPosition = new Vector3(0f, bodyRadius, 0f);
            }
            else
            {
                visualRoot.localScale = new Vector3(scale.x, height * 0.5f, scale.z);
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localPosition = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private Vector3 GetCameraFlatForward()
        {
            if (!TryEnsureCamera())
            {
                return Vector3.zero;
            }

            var forward = cameraTransform.forward;
            forward.y = 0f;
            return forward.normalized;
        }

        private bool TryEnsureCamera()
        {
            if (cameraTransform != null)
            {
                return true;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            cameraTransform = mainCamera.transform;
            return true;
        }

        private Vector3 ToCameraRelativeDirection(Vector2 input)
        {
            if (stageReference != null)
            {
                return ToReferenceRelativeDirection(stageReference, input);
            }

            if (!TryEnsureCamera())
            {
                return new Vector3(input.x, 0f, input.y);
            }

            return ToReferenceRelativeDirection(cameraTransform, input);
        }

        private static Vector3 ToReferenceRelativeDirection(Transform reference, Vector2 input)
        {
            var forward = reference.forward;
            forward.y = 0f;
            forward.Normalize();

            var right = reference.right;
            right.y = 0f;
            right.Normalize();

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
