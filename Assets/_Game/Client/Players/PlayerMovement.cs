using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Players
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField, Min(0f)]
        private float walkSpeed = 4f;

        [SerializeField, Min(0f)]
        private float rotationSpeedDegrees = 720f;

        [SerializeField, Min(0f)]
        private float jumpHeight = 1.1f;

        [SerializeField, Min(0.1f)]
        private float gravityMultiplier = 2f;

        // 접지 상태를 안정시키기 위해 바닥에 있을 때 아래로 살짝 눌러주는 속도.
        private const float GroundedStickVelocity = -2f;

        private CharacterController controller;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction jumpAction;
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

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
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
            var gravity = -Physics.gravity.y * gravityMultiplier;

            if (controller.isGrounded)
            {
                verticalVelocity = GroundedStickVelocity;

                if (jumpAction.WasPressedThisFrame())
                {
                    // 목표 높이(jumpHeight)에 도달하는 초기 속도: v = sqrt(2gh)
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                }
            }

            verticalVelocity -= gravity * Time.deltaTime;

            var velocity = direction * walkSpeed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
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
