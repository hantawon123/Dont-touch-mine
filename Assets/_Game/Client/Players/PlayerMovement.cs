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

            if (direction.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, movementConfig.RotationSpeedDegrees * Time.deltaTime);
            }
        }

        private void HandlePostureInput()
        {
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

            Posture = posture;
            ApplyPostureShape(GetPostureHeight(posture));
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

        private void ApplyPostureShape(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (visualRoot != null)
            {
                // 임시 캡슐 비주얼: 세로 스케일로 자세를 표현한다. (기본 캡슐 높이 2m 기준)
                var scale = visualRoot.localScale;
                visualRoot.localScale = new Vector3(scale.x, height * 0.5f, scale.z);
                visualRoot.localPosition = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private Vector3 ToCameraRelativeDirection(Vector2 input)
        {
            if (cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return new Vector3(input.x, 0f, input.y);
                }

                cameraTransform = mainCamera.transform;
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
