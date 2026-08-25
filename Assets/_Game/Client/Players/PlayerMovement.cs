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

        private CharacterController controller;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private Transform cameraTransform;

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

            // SimpleMove는 중력을 내장 적용한다. 점프 구현 시 Move + 수동 중력으로 교체 예정.
            controller.SimpleMove(direction * walkSpeed);

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
