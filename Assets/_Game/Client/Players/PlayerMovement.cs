using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Players
{
    public enum PlayerPosture
    {
        Standing,
        Crouching,
        Prone
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private MovementConfigSO movementConfig;

        [SerializeField]
        private Transform visualRoot;

        public PlayerPosture Posture { get; private set; } = PlayerPosture.Standing;

        public float CurrentEyeHeight => Posture switch
        {
            PlayerPosture.Crouching => movementConfig.CrouchEyeHeight,
            PlayerPosture.Prone => movementConfig.ProneEyeHeight,
            _ => movementConfig.StandEyeHeight
        };

        // 접지 상태를 안정시키기 위해 바닥에 있을 때 아래로 살짝 눌러주는 속도.
        private const float GroundedStickVelocity = -2f;

        private CharacterController controller;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction proneAction;
        private Transform cameraTransform;
        private float verticalVelocity;

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

        private void OnDisable()
        {
            playerMap?.Disable();
        }

        private void Update()
        {
            var input = moveAction.ReadValue<Vector2>();
            var direction = ToCameraRelativeDirection(input);
            var gravity = -Physics.gravity.y * movementConfig.GravityMultiplier;

            HandlePostureInput();

            if (controller.isGrounded)
            {
                verticalVelocity = GroundedStickVelocity;

                if (jumpAction.WasPressedThisFrame() && Posture == PlayerPosture.Standing)
                {
                    // 목표 높이(JumpHeight)에 도달하는 초기 속도: v = sqrt(2gh)
                    verticalVelocity = Mathf.Sqrt(2f * gravity * movementConfig.JumpHeight);
                }
            }

            verticalVelocity -= gravity * Time.deltaTime;

            var moveSpeed = GetMoveSpeed();
            var velocity = direction * moveSpeed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            // 몸은 항상 카메라가 보는 방향(좌우)을 향한다. 조준 기반 게임의 표준 방식.
            var lookForward = GetCameraFlatForward();
            if (lookForward.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(lookForward);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, movementConfig.RotationSpeedDegrees * Time.deltaTime);
            }
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
                _ => sprintAction.IsPressed() ? movementConfig.SprintSpeed : movementConfig.WalkSpeed
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
            if (!TryEnsureCamera())
            {
                return new Vector3(input.x, 0f, input.y);
            }

            var forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            var right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
