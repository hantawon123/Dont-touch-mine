using Game.Client.Players;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Cameras
{
    public sealed class PlayerCameraController : MonoBehaviour
    {
        private const int ActivePriority = 20;
        private const int InactivePriority = 10;

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private CinemachineCamera thirdPersonCamera;

        [SerializeField]
        private CinemachineCamera firstPersonCamera;

        [SerializeField]
        private Transform followTarget;

        [SerializeField]
        private Vector3 headOffset = new(0f, 1.6f, 0f);

        [SerializeField, Min(0.01f)]
        private float lookSensitivity = 0.12f;

        [SerializeField, Range(-89f, 0f)]
        private float minPitch = -60f;

        [SerializeField, Range(0f, 89f)]
        private float maxPitch = 75f;

        private InputActionMap playerMap;
        private InputAction lookAction;
        private InputAction toggleViewAction;
        private float yaw;
        private float pitch;
        private bool isFirstPerson;

        private void Awake()
        {
            if (inputActions == null || thirdPersonCamera == null || firstPersonCamera == null)
            {
                Debug.LogError("PlayerCameraController: Inspector 참조(InputActions/카메라 2대)가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            if (followTarget == null)
            {
                var player = FindAnyObjectByType<PlayerMovement>();
                if (player == null)
                {
                    Debug.LogError("PlayerCameraController: 따라갈 대상이 없습니다. followTarget을 연결하거나 씬에 PlayerMovement를 배치하세요.", this);
                    enabled = false;
                    return;
                }

                followTarget = player.transform;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
            toggleViewAction = playerMap.FindAction("ToggleView", throwIfNotFound: true);

            yaw = followTarget.eulerAngles.y;
            ApplyView();
        }

        private void OnEnable()
        {
            playerMap?.Enable();
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            SetCursorLocked(false);
        }

        private void Update()
        {
            // Esc로 커서 해제, 화면 클릭으로 다시 잠금.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked
                     && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = lookAction.ReadValue<Vector2>();
                yaw += look.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            }

            if (toggleViewAction.WasPressedThisFrame())
            {
                isFirstPerson = !isFirstPerson;
                ApplyView();
            }
        }

        private void LateUpdate()
        {
            transform.SetPositionAndRotation(
                followTarget.position + headOffset,
                Quaternion.Euler(pitch, yaw, 0f));
        }

        private void ApplyView()
        {
            thirdPersonCamera.Priority = isFirstPerson ? InactivePriority : ActivePriority;
            firstPersonCamera.Priority = isFirstPerson ? ActivePriority : InactivePriority;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
