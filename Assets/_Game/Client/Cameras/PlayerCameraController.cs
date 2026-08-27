using Game.Client.Players;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [SerializeField, Min(0.1f)]
        private float eyeHeightLerpSpeed = 8f;

        private InputActionMap playerMap;
        private InputAction lookAction;
        private InputAction toggleViewAction;
        private PlayerMovement followMovement;
        private Renderer[] bodyRenderers;
        private float currentEyeHeight;
        private float yaw;
        private float pitch;
        private bool isFirstPerson;
        private bool cursorCaptureEnabled = true;

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
                followTarget = player != null ? player.transform : null;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
            toggleViewAction = playerMap.FindAction("ToggleView", throwIfNotFound: true);

            if (followTarget != null)
            {
                SetFollowTarget(followTarget);
            }

            ApplyView();
        }

        /// <remarks>
        /// Locks to whatever capture is currently set rather than to true. A
        /// screen that handed the mouse to its UI, as the lobby does, would
        /// otherwise get the cursor captured again the next time this rig is
        /// re-enabled, and a captured cursor cannot press the buttons on it.
        /// </remarks>
        private void OnEnable()
        {
            playerMap?.Enable();
            SetCursorLocked(cursorCaptureEnabled);
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            SetCursorLocked(false);
        }

        private void Update()
        {
            if (PlayerMovement.IsTextInputFocused())
            {
                SetCursorLocked(false);
                return;
            }

            if (toggleViewAction.WasPressedThisFrame())
            {
                isFirstPerson = !isFirstPerson;
                ApplyView();
            }

            if (!cursorCaptureEnabled)
            {
                return;
            }

            // Esc로 커서 해제, 화면 클릭으로 다시 잠금.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked
                     && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                     && !IsPointerOverUi())
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = lookAction.ReadValue<Vector2>();
                yaw += look.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            }

        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            // 자세(서기/앉기/엎드리기)에 따라 눈높이를 부드럽게 따라간다.
            var targetEyeHeight = followMovement != null ? followMovement.CurrentEyeHeight : headOffset.y;
            currentEyeHeight = Mathf.Lerp(
                currentEyeHeight, targetEyeHeight, eyeHeightLerpSpeed * Time.deltaTime);

            var offset = new Vector3(headOffset.x, currentEyeHeight, headOffset.z);
            transform.SetPositionAndRotation(
                followTarget.position + offset,
                Quaternion.Euler(pitch, yaw, 0f));
        }

        public void SetFollowTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            followTarget = target;
            followMovement = target.GetComponent<PlayerMovement>();
            currentEyeHeight = followMovement != null ? followMovement.CurrentEyeHeight : headOffset.y;
            yaw = target.eulerAngles.y;

            // 1인칭 몸 숨김 대상 렌더러를 새 대상 기준으로 다시 수집한다.
            var visual = target.Find("Visual");
            bodyRenderers = visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0];
            ApplyView();
        }

        public void SetCursorCaptureEnabled(bool captureEnabled)
        {
            cursorCaptureEnabled = captureEnabled;
            SetCursorLocked(captureEnabled);
        }

        private void ApplyView()
        {
            thirdPersonCamera.Priority = isFirstPerson ? InactivePriority : ActivePriority;
            firstPersonCamera.Priority = isFirstPerson ? ActivePriority : InactivePriority;

            // 1인칭에서는 내 몸이 화면을 가리지 않게 숨긴다. 그림자는 남겨 존재감을 유지한다.
            if (bodyRenderers != null)
            {
                foreach (var bodyRenderer in bodyRenderers)
                {
                    bodyRenderer.shadowCastingMode = isFirstPerson
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        /// <summary>
        /// True while the mouse is over something the UI will handle.
        /// </summary>
        /// <remarks>
        /// A click aimed at a HUD button must not also recapture the mouse. The
        /// recapture lands in the same frame as the click and a captured cursor
        /// reports from the centre of the screen, so the button the player was
        /// pointing at can lose the very press meant for it. That is how the
        /// lobby's Leave button came to need two or three tries.
        /// </remarks>
        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
