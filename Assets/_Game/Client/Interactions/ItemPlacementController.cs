using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 정밀 배치 모드(러스트식 홀로그램):
    /// 물건을 든 채 우클릭으로 켜고 끄며, 반투명 고스트가 배치될 자리를 미리 보여준다.
    /// Q/E 좌우 회전, 스크롤 위아래 조절, 좌클릭으로 확정한다.
    /// 배치 불가능한 위치(겹침·손이 닿지 않는 곳)에서는 고스트가 빨간색이 되고 확정할 수 없다.
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public sealed class ItemPlacementController : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private InteractionConfigSO interactionConfig;

        [SerializeField]
        private Material ghostValidMaterial;

        [SerializeField]
        private Material ghostInvalidMaterial;

        public bool IsPlacing { get; private set; }

        private PlayerInteractor interactor;
        private InputActionMap playerMap;
        private InputAction placementModeAction;
        private InputAction rotateAction;
        private InputAction heightAction;
        private InputAction confirmAction;
        private Transform cameraTransform;

        private GameObject ghost;
        private Renderer[] ghostRenderers;
        private float yawOffset;
        private float heightOffset;
        private bool isCurrentPoseValid;
        private Vector3 previewPosition;
        private Quaternion previewRotation;

        private void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();

            if (inputActions == null || interactionConfig == null
                || ghostValidMaterial == null || ghostInvalidMaterial == null)
            {
                Debug.LogError("ItemPlacementController: Inspector 참조(InputActions/Config/고스트 머티리얼 2개)가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            placementModeAction = playerMap.FindAction("PlacementMode", throwIfNotFound: true);
            rotateAction = playerMap.FindAction("RotateObject", throwIfNotFound: true);
            heightAction = playerMap.FindAction("AdjustHeight", throwIfNotFound: true);
            confirmAction = playerMap.FindAction("Attack", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            playerMap?.Enable();
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            ExitPlacementMode();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                ExitPlacementMode();
                return;
            }

            // 물건이 없어지면(놓기/뺏김 등) 배치 모드를 자동 종료한다.
            if (IsPlacing && interactor.CarriedItem == null)
            {
                ExitPlacementMode();
            }

            if (placementModeAction.WasPressedThisFrame() && interactor.CarriedItem != null)
            {
                if (IsPlacing)
                {
                    ExitPlacementMode();
                }
                else
                {
                    EnterPlacementMode();
                }
            }

            if (!IsPlacing)
            {
                return;
            }

            ReadAdjustInput();
            UpdatePreviewPose();

            if (confirmAction.WasPressedThisFrame() && isCurrentPoseValid)
            {
                ConfirmPlacement();
            }
        }

        private void EnterPlacementMode()
        {
            IsPlacing = true;
            interactor.IsThrowSuppressed = true;
            yawOffset = 0f;
            heightOffset = 0f;
            CreateGhost(interactor.CarriedItem);
        }

        private void ExitPlacementMode()
        {
            if (!IsPlacing && ghost == null)
            {
                return;
            }

            IsPlacing = false;
            if (interactor != null)
            {
                interactor.IsThrowSuppressed = false;
            }

            if (ghost != null)
            {
                Destroy(ghost);
                ghost = null;
                ghostRenderers = null;
            }
        }

        private void ReadAdjustInput()
        {
            // Q/E: 좌우 회전 (누르는 동안 회전)
            var rotateInput = rotateAction.ReadValue<float>();
            yawOffset += rotateInput * interactionConfig.PlacementRotateSpeedDegrees * Time.deltaTime;

            // 스크롤: 위아래 오프셋 (한 칸에 한 스텝)
            var scroll = heightAction.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                heightOffset = Mathf.Clamp(
                    heightOffset + Mathf.Sign(scroll) * interactionConfig.PlacementHeightStep,
                    0f, interactionConfig.PlacementMaxHeightOffset);
            }
        }

        private void UpdatePreviewPose()
        {
            if (!TryEnsureCamera() || ghost == null)
            {
                return;
            }

            // 크로스헤어가 가리키는 표면을 기준점으로 삼는다. 표면이 없으면 최대 거리 지점.
            var cameraToPlayer = Vector3.Distance(cameraTransform.position, transform.position);
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            var maxDistance = interactionConfig.InteractionDistance + cameraToPlayer;

            Vector3 surfacePoint;
            var hasSurface = Physics.Raycast(ray, out var hit, maxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform);
            surfacePoint = hasSurface ? hit.point : ray.GetPoint(maxDistance);

            previewPosition = surfacePoint + Vector3.up * heightOffset;
            previewRotation = Quaternion.Euler(0f, transform.eulerAngles.y + yawOffset, 0f);

            ghost.transform.SetPositionAndRotation(previewPosition, previewRotation);

            var withinReach = Vector3.Distance(previewPosition, transform.position)
                <= interactionConfig.InteractionDistance + interactionConfig.PlacementMaxHeightOffset;
            isCurrentPoseValid = withinReach && !IsOverlapping();
            ApplyGhostMaterial(isCurrentPoseValid ? ghostValidMaterial : ghostInvalidMaterial);
        }

        // 고스트가 차지할 공간에 다른 물체가 있는지 검사한다.
        // 바닥에 붙여 놓는 경우 표면 자체에 닿는 것은 허용해야 하므로 검사 상자를 살짝 줄이고 띄운다.
        private bool IsOverlapping()
        {
            if (ghostRenderers == null || ghostRenderers.Length == 0)
            {
                return false;
            }

            var bounds = ghostRenderers[0].bounds;
            for (var i = 1; i < ghostRenderers.Length; i++)
            {
                bounds.Encapsulate(ghostRenderers[i].bounds);
            }

            var center = bounds.center + Vector3.up * 0.02f;
            var extents = bounds.extents * 0.85f;

            var overlaps = Physics.OverlapBox(
                center, extents, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (var overlap in overlaps)
            {
                // 플레이어 자신은 겹침으로 치지 않는다. (들고 있는 물건의 콜라이더는 꺼져 있어 잡히지 않는다)
                if (!overlap.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void ConfirmPlacement()
        {
            var item = interactor.ReleaseCarriedItem();
            if (item == null)
            {
                ExitPlacementMode();
                return;
            }

            item.OnPlaced(previewPosition, previewRotation);
            ExitPlacementMode();
        }

        // 들고 있는 물건의 겉모습만 복제해 홀로그램 고스트를 만든다.
        private void CreateGhost(CarryableItem item)
        {
            ghost = Instantiate(item.gameObject);
            ghost.name = "PlacementGhost";

            foreach (var component in ghost.GetComponentsInChildren<Collider>())
            {
                Destroy(component);
            }

            if (ghost.TryGetComponent<CarryableItem>(out var ghostItem))
            {
                Destroy(ghostItem);
            }

            if (ghost.TryGetComponent<Rigidbody>(out var ghostBody))
            {
                Destroy(ghostBody);
            }

            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            ApplyGhostMaterial(ghostValidMaterial);
        }

        private void ApplyGhostMaterial(Material material)
        {
            if (ghostRenderers == null)
            {
                return;
            }

            foreach (var ghostRenderer in ghostRenderers)
            {
                var materials = ghostRenderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                ghostRenderer.sharedMaterials = materials;
            }
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
    }
}
