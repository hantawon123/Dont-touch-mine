using UnityEngine;

namespace Game.Training
{
    [RequireComponent(typeof(CharacterController))]
    public class TrainingMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float moveSpeed = 3f;

        [SerializeField, Min(0f)]
        private float rotationSpeed = 360f;

        [SerializeField]
        private Vector2 moveInput;

        private CharacterController controller;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
            Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);

            // 바닥 위에서 이동할 방향
            Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

            // 이동 명령이 있을 때만 몸을 돌린다.
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(moveDirection, Vector3.up);

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime);
            }

            Vector3 velocity = moveDirection * moveSpeed;

            // 바닥에 서 있을 때 아래 방향 속도가 계속 쌓이지 않게 한다.
            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }

            verticalSpeed += Physics.gravity.y * Time.fixedDeltaTime;
            velocity.y = verticalSpeed;

            controller.Move(velocity * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            verticalSpeed = 0f;
        }
    }
}