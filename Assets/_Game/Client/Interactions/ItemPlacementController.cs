using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 정밀 배치 모드(러스트식 홀로그램):
    /// 물건을 든 채 우클릭으로 켜고 끄며, 반투명 고스트가 배치될 자리를 미리 보여준다.
    /// Q/E 부드러운 좌우 회전(요), 스크롤 15도 단위 앞뒤 기울이기(피치), 좌클릭으로 확정한다.
    /// 배치 불가능한 위치(겹침·손이 닿지 않는 곳)에서는 고스트가 빨간색이 되고 확정할 수 없다.
    /// <para>
    /// 조준 규칙(2026-09-12): 조준점이 손 닿는 수평 거리를 넘으면 예전처럼 그 방향으로 한계선 안쪽까지 끌어와
    /// "지금 놓을 수 있는 자리"를 보여 준다(바닥·가까운 선반은 항상 초록). 다만 수직면(선반 앞면·상품 옆면)을
    /// 조준했을 때는 그 면 조금 안쪽의 윗면(선반 판)을 먼저 찾아, 진열대 칸 안에도 놓을 수 있게 했다.
    /// 한때 거리 초과를 빨간 고스트로만 보여 주는 방식을 시도했지만 바닥 조준까지 빨갱이 되어 되돌렸다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public sealed class ItemPlacementController : MonoBehaviour
    {
        private const float AutoLiftStep = 0.05f;
        private const float AutoLiftMax = 0.75f;
        private const float PlacementSkinWidth = 0.01f;
        private const float MaxSupportDistance = 0.05f;

        /// <summary>법선의 y가 이 값보다 크면 윗면, 작으면(절댓값) 수직면으로 본다.</summary>
        private const float VerticalFaceNormalLimit = 0.5f;

        /// <summary>수직면을 조준했을 때 광선 방향으로 이만큼 안쪽을 조사해 선반 판을 찾는다.</summary>
        private const float ShelfInsetDistance = 0.15f;

        /// <summary>안쪽 조사점 위·아래로 이만큼 범위에서 윗면을 찾는다(선반 칸 높이 안).</summary>
        private const float ShelfProbeUp = 0.35f;
        private const float ShelfProbeDown = 0.6f;

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private InteractionConfigSO interactionConfig;

        [SerializeField]
        private Material ghostValidMaterial;

        [SerializeField]
        private Material ghostInvalidMaterial;

        public const string PlaceActionLabel = "배치";

        public bool IsPlacing { get; private set; }

        /// <summary>결과 화면 등 외부에서 배치 모드 진입을 막을 때 사용한다. 켜지면 진행 중인 배치도 끝낸다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>
        /// 배치 확정 좌클릭이 손을 비운 뒤 같은 입력으로 펀치가 나가지 않게 한다.
        /// </summary>
        public bool BlocksAttack => IsPlacing || suppressAttackUntilRelease;

        private bool suppressAttackUntilRelease;

        private PlayerInteractor interactor;
        private InputActionMap playerMap;
        private InputAction placementModeAction;
        private InputAction rotateAction;
        private InputAction scrollRotateAction;
        private InputAction confirmAction;
        private Transform cameraTransform;

        private GameObject ghost;
        private Renderer[] ghostRenderers;
        private Quaternion ghostRotation = Quaternion.identity;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[8];
        private bool isCurrentPoseValid;
        private Vector3 previewPosition;
        private Quaternion previewRotation;
        private Vector3 placementCenterOffset;
        private Vector3 placementHalfExtents;
        private InteractionPromptView promptView;
        private Sprite placeIcon;
        private bool? lastGhostValid;

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
            scrollRotateAction = playerMap.FindAction("AdjustHeight", throwIfNotFound: true);
            confirmAction = playerMap.FindAction("Attack", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            playerMap?.Enable();
        }

        private void OnDisable()
        {
            ExitPlacementMode();
        }

        private void OnDestroy()
        {
            if (promptView != null)
            {
                Destroy(promptView.gameObject);
            }
        }

        private void Update()
        {
            if (suppressAttackUntilRelease &&
                (confirmAction == null || !confirmAction.IsPressed()))
            {
                suppressAttackUntilRelease = false;
            }

            if (Cursor.lockState != CursorLockMode.Locked || IsInputLocked)
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
                suppressAttackUntilRelease = true;
                ConfirmPlacement();
            }
        }

        private void EnterPlacementMode()
        {
            IsPlacing = true;
            interactor.IsThrowSuppressed = true;

            // 시작 방향: 캐릭터가 보는 방향에 맞춰 세운 상태.
            ghostRotation = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up);
            CreateGhost(interactor.CarriedItem);
        }

        private void ExitPlacementMode()
        {
            if (!IsPlacing && ghost == null)
            {
                return;
            }

            IsPlacing = false;
            if (confirmAction != null && confirmAction.IsPressed())
            {
                suppressAttackUntilRelease = true;
            }

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

            lastGhostValid = null;
            promptView?.Hide();
        }

        private void ReadAdjustInput()
        {
            // 회전축은 항상 "내 시점 기준"으로 고정한다: 증분 회전을 누적 회전의 왼쪽에 곱하면
            // 물건이 어떤 자세든 축은 유지되고 물건만 돈다.
            // Q/E: 수직축(월드 위) 기준 좌우 회전
            var rotateInput = rotateAction.ReadValue<float>();
            if (Mathf.Abs(rotateInput) > 0.01f)
            {
                var deltaYaw = rotateInput * interactionConfig.PlacementRotateSpeedDegrees * Time.deltaTime;
                ghostRotation = Quaternion.AngleAxis(deltaYaw, Vector3.up) * ghostRotation;
            }

            // 스크롤: 내 시점의 좌우축 기준 앞뒤 기울이기 (한 칸에 일정 각도)
            var scroll = scrollRotateAction.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var tiltAxis = transform.right;
                tiltAxis.y = 0f;
                tiltAxis.Normalize();

                var deltaPitch = Mathf.Sign(scroll) * interactionConfig.PlacementScrollRotateStepDegrees;
                ghostRotation = Quaternion.AngleAxis(deltaPitch, tiltAxis) * ghostRotation;
            }
        }

        private void UpdatePreviewPose()
        {
            if (!TryEnsureCamera() || ghost == null)
            {
                promptView?.Hide();
                return;
            }

            // 크로스헤어가 가리키는 표면을 기준점으로 삼는다. 광선은 카메라에서 나가므로 카메라-플레이어 거리를 더한다.
            var cameraToPlayer = Vector3.Distance(cameraTransform.position, transform.position);
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            var maxRayDistance = interactionConfig.PlacementMaxDistance + cameraToPlayer;

            // 수직면(선반 앞면·상품 옆면)을 조준했으면 그 안쪽 윗면(선반 판)을 기준점으로 삼는다.
            var surfacePoint = TryFindNearestSurface(ray, maxRayDistance, out var aimed)
                ? ResolveSurfacePoint(ray, aimed)
                : ray.GetPoint(maxRayDistance);

            // 최대 배치 거리(수평)를 넘어가면 한계선 안쪽으로 끌어당긴다.
            // 홀로그램은 항상 "지금 놓을 수 있는 자리"를 보여준다.
            var flatOffset = surfacePoint - transform.position;
            var height = flatOffset.y;
            flatOffset.y = 0f;
            if (flatOffset.magnitude > interactionConfig.PlacementMaxDistance)
            {
                surfacePoint = transform.position
                    + flatOffset.normalized * interactionConfig.PlacementMaxDistance
                    + Vector3.up * height;
            }

            // 허공이라면 어차피 떨어질 것이므로 바로 아래 표면에 투영한다.
            if (TryFindNearestSurface(new Ray(surfacePoint + Vector3.up * 0.05f, Vector3.down), 20f, out var ground))
            {
                surfacePoint = ground.point;
            }

            previewRotation = ghostRotation;

            // 서버와 같은 콜라이더 부피를 사용해 바닥이 표면에 닿는 루트 위치를 계산한다.
            var xExtent = previewRotation * new Vector3(placementHalfExtents.x, 0f, 0f);
            var yExtent = previewRotation * new Vector3(0f, placementHalfExtents.y, 0f);
            var zExtent = previewRotation * new Vector3(0f, 0f, placementHalfExtents.z);
            var verticalExtent = Mathf.Abs(xExtent.y) +
                                 Mathf.Abs(yExtent.y) +
                                 Mathf.Abs(zExtent.y);
            var volumeCenter = surfacePoint + Vector3.up * verticalExtent;
            var rootPosition = volumeCenter -
                               (previewRotation * placementCenterOffset);
            ghost.transform.SetPositionAndRotation(rootPosition, previewRotation);

            // 장애물과 겹치면 얹힐 수 있는 높이까지 조금씩 올려 실제 놓일 자리를 예측한다.
            // (예: 장난감 자동차 위를 조준하면 그 위에 얹힌 모습으로 보정)
            var lifted = 0f;
            while (IsOverlapping() && lifted < AutoLiftMax)
            {
                ghost.transform.position += Vector3.up * AutoLiftStep;
                lifted += AutoLiftStep;
            }

            previewPosition = ghost.transform.position;

            // 보정 한도까지 올려도 겹치면 그때만 배치 불가(빨간색).
            isCurrentPoseValid = !IsOverlapping() && HasSupport();
            if (lastGhostValid != isCurrentPoseValid)
            {
                ApplyGhostMaterial(isCurrentPoseValid ? ghostValidMaterial : ghostInvalidMaterial);
                lastGhostValid = isCurrentPoseValid;
            }

            RefreshPlacementPrompt();
        }

        // 광선 경로에서 자기 몸(플레이어)을 제외한 가장 가까운 표면을 찾는다.
        private bool TryFindNearestSurface(Ray ray, float maxDistance, out RaycastHit nearest)
        {
            nearest = default;
            var hitCount = Physics.RaycastNonAlloc(ray, surfaceHits, maxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearestDistance = float.MaxValue;
            var found = false;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = surfaceHits[i];
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearest = hit;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// 조준한 면이 윗면이면 그 점을 쓰고, 수직면(선반 앞면·상품 옆면)이면 광선 방향으로 조금 안쪽에서
        /// 위아래로 윗면(선반 판·바닥)을 찾아 그 위를 기준점으로 삼는다. 못 찾으면 조준점 그대로(겹침으로 빨간색).
        /// </summary>
        private Vector3 ResolveSurfacePoint(Ray ray, RaycastHit aimed)
        {
            if (Mathf.Abs(aimed.normal.y) > VerticalFaceNormalLimit)
            {
                return aimed.point;
            }

            var inside = aimed.point + ray.direction * ShelfInsetDistance;
            var probe = new Ray(inside + Vector3.up * ShelfProbeUp, Vector3.down);
            if (TryFindNearestSurface(probe, ShelfProbeUp + ShelfProbeDown, out var top) &&
                top.normal.y > VerticalFaceNormalLimit)
            {
                return top.point;
            }

            return aimed.point;
        }

        // 고스트가 차지할 공간에 다른 물체가 있는지 검사한다.
        // 바닥에 붙여 놓는 경우 표면 자체에 닿는 것은 허용해야 하므로 검사 상자를 살짝 줄이고 띄운다.
        private bool IsOverlapping()
        {
            if (ghost == null)
            {
                return false;
            }

            var center = ghost.transform.position +
                         (ghost.transform.rotation * placementCenterOffset);
            var extents = placementHalfExtents -
                          (Vector3.one * PlacementSkinWidth);

            var overlaps = Physics.OverlapBox(
                center, extents, ghost.transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (var overlap in overlaps)
            {
                // 플레이어는 순간적으로 이동하므로 배치 지형으로 취급하지 않는다.
                if (overlap.GetComponentInParent<CharacterController>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasSupport()
        {
            var center = ghost.transform.position +
                         (ghost.transform.rotation * placementCenterOffset);
            // 서버(PhysicsPlacementValidator)와 같은 규칙: 기울인 상자는 실제 바닥까지 광선을 늘린다.
            var verticalExtent = PlacementVolumeMath.RotatedVerticalExtent(
                ghost.transform.rotation, placementHalfExtents);
            return Physics.Raycast(
                center,
                Vector3.down,
                verticalExtent + MaxSupportDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void ConfirmPlacement()
        {
            // Sending a network request does not mean authority accepted it.
            // Keep throw input suppressed until replicated state clears the hand.
            interactor.TryPlaceCarried(previewPosition, previewRotation);
        }

        // 들고 있는 물건의 겉모습만 복제해 홀로그램 고스트를 만든다.
        private void CreateGhost(CarryableItem item)
        {
            placementCenterOffset = item.PlacementCenterOffset;
            placementHalfExtents = item.PlacementHalfExtents;
            ghost = Instantiate(item.gameObject);
            ghost.name = "PlacementGhost";

            foreach (var component in ghost.GetComponentsInChildren<Collider>())
            {
                // Destroy is deferred until the end of the frame. Disable now
                // so the fresh preview cannot collide with its own collider.
                component.enabled = false;
                Destroy(component);
            }

            if (ghost.TryGetComponent<CarryableItem>(out var ghostItem))
            {
                Destroy(ghostItem);
            }

            if (ghost.TryGetComponent<Rigidbody>(out var ghostBody))
            {
                ghostBody.isKinematic = true;
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

        private void RefreshPlacementPrompt()
        {
            if (!IsPlacing ||
                !isCurrentPoseValid ||
                ghost == null ||
                interactor == null ||
                !PlayerInteractor.CanShowWorldPrompt(
                    interactor.HudVisible,
                    interactor.InteractionPromptsAllowed,
                    Cursor.lockState == CursorLockMode.Locked))
            {
                promptView?.Hide();
                return;
            }

            if (promptView != null && promptView.IsVisible)
            {
                return;
            }

            placeIcon ??= InteractionPromptView.LoadLeftClickIcon();
            PromptView.Show(
                string.Empty,
                PlaceActionLabel,
                ghost.transform,
                placeIcon);
        }

        private InteractionPromptView PromptView =>
            promptView != null ? promptView : promptView = InteractionPromptView.Create();

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
