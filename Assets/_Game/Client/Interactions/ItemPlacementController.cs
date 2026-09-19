using Game.Client.Cameras;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 배치 모드(PLACE TO FIT 방식). 들고 있는 물건을 조준점(크로스헤어) 위 최대 거리에 실루엣으로 띄우고, 회전시켜, 좌클릭으로 놓는다.
    /// <para>
    /// 조작(1·3인칭 공통): 들고 있을 때 <b>우클릭</b> = 배치 모드 켜기. 모드에서 <b>우클릭 유지 + 마우스</b> = 회전(좌우 → 세계 수직축,
    /// 상하 → 카메라 좌우축, 앞면이 마우스를 따라옴), <b>Q/E</b> = 시선축 비틀기(E 시계), <b>R</b> = 똑바로 세우기,
    /// <b>좌클릭</b> = 배치 확정(초록일 때만). <b>F</b> = 기존대로 그냥 떨어뜨리기(겹친 빨간 상태에서는 막힘).
    /// 모드 밖에서는 기존처럼 들고 다니고 좌클릭 = 던지기. 우클릭 회전 중에는 시선이 멈춘다. 모드는 손이 비면 자동으로 꺼진다.
    /// </para>
    /// <para>
    /// 동작: 모드를 켜면 실제 물건을 HoldPoint에서 떼어 운동학 몸체로 조준선 위에 둔다(콜라이더 꺼짐 = 순수 미리보기, 다른 물건을
    /// 밀지 않음). 실제 물건은 내 화면에서 숨기고 배치 가능(초록)/불가(빨강) 실루엣 복제본만 보인다. 위치는 항상 조준선 위에서
    /// 정한다: 조준선이 벽·선반 같은 정적 지형에 닿는 지점까지만 가고, 거기서 카메라 쪽으로 물러나며 물건 모양이 정적 지형과
    /// 겹치지 않는 가장 먼 자리를 고른다. 다른 물건과 겹치면 빨강. 회전은 시선 좌우(요) 기준으로 저장해 몸을 돌리면 같이 돌고,
    /// 초기 자세는 잡기 직전에 놓여 있던 방향이다. 다른 클라이언트는 각자 자기 HoldPoint(머리 위)에 물건을 그리므로 영향이 없다.
    /// 배치 확정은 기존 놓기 경로(권위 있으면 RequestDrop)를 그대로 쓴다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public sealed class ItemPlacementController : MonoBehaviour
    {
        public const string PlaceActionLabel = "배치";
        public const string YawActionLabel = "회전";        // 우클릭 끌기
        public const string TwistActionLabel = "좌우 회전"; // Q/E 비틀기
        private const string TwistKeyLabel = "Q E";

        private const float MinDistance = 0.45f;            // 카메라에서 이보다 가깝게는 두지 않는다
        private const float RotateDegreesPerPixel = 0.3f;   // 우클릭 드래그 감도
        private const float DominantAxisRatio = 2.5f;       // 드래그를 한 축으로 정리하는 비율
        private const float BlockMargin = 0.02f;            // 정적 지형 앞에 남기는 틈(m)
        private const float OverlapSkin = 0.02f;            // 겹침 판정에서 상자를 줄이는 두께(m). 살짝 닿는 것은 겹침으로 안 본다
        private const float PullInStep = 0.04f;             // 조준선 위에서 빈 자리를 찾을 때 카메라 쪽으로 물러나는 간격(m)
        private const float PromptColumnPixels = 70f;       // 구 아래 두 안내를 구 중심에서 좌우로 벌리는 화면 간격

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private InteractionConfigSO interactionConfig;

        [SerializeField]
        private Material ghostValidMaterial;

        [SerializeField]
        private Material ghostInvalidMaterial;

        /// <summary>배치 가능 상태의 실루엣 머티리얼.</summary>
        public Material GhostValidMaterial => ghostValidMaterial;

        public Material ValidGhostMaterial => ghostValidMaterial;

        /// <summary>배치 불가(겹침) 상태의 실루엣 머티리얼.</summary>
        public Material GhostInvalidMaterial => ghostInvalidMaterial;

        /// <summary>배치 모드(실루엣 미리보기)가 켜져 있는지.</summary>
        public bool IsPlacing => placementMode && held != null;

        /// <summary>들고 있는 물건이 다른 물건·지형과 겹쳐 지금 자리에 놓을 수 없는 상태인지.</summary>
        public bool IsOverlapping { get; private set; }

        /// <summary>결과 화면 등 외부에서 배치 모드를 막을 때 사용한다. 켜지면 진행 중인 배치 모드도 끝낸다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>배치 확정 좌클릭이 손을 비운 뒤 같은 입력으로 펀치가 나가지 않게 한다. 전투·이동 입력이 이 값을 본다.</summary>
        public bool BlocksAttack => held != null || blockAttackUntilRelease;

        public Quaternion HoldRotation => holdRot;

        /// <summary>시선에 맞추는 기준점(배치 상자 중심). 피벗이 바닥에 있는 모델도 보이는 중심이 크로스헤어에 온다.</summary>
        public Vector3 HeldCenter => heldBody != null
            ? heldBody.position + heldBody.rotation * centerLocalOffset
            : (held != null ? held.transform.position : Vector3.zero);

        private PlayerInteractor interactor;
        private PlayerCameraController cameraRig;
        private Transform cam;
        private InputActionMap playerMap;
        private InputAction rotateAction;   // Q/E
        private InputAction rmbAction;      // 우클릭(PlacementMode 바인딩)
        private InputAction attackAction;   // 좌클릭(배치 확정)
        private Collider[] playerColliders = System.Array.Empty<Collider>();

        private bool placementMode;
        private CarryableItem held;
        private Rigidbody heldBody;
        private Collider[] heldColliders = System.Array.Empty<Collider>();
        private Quaternion holdRot = Quaternion.identity;      // 이번 스텝의 세계 기준 자세(= 시선 요 × holdRotLocal)
        private Quaternion holdRotLocal = Quaternion.identity; // 내 시선 좌우(요) 기준 자세. 돌아서면 물건도 같이 돈다
        private Vector3 centerLocalOffset;                     // 피벗 → 배치 상자 중심(로컬)
        private bool rotating;
        private bool blockAttackUntilRelease;
        private float dragYawDeg, dragPitchDeg;                // 이번 우클릭 드래그에서 누적한 회전(기즈모 표시용)
        private float twistDeg;                                // 이번 Q/E 누름에서 누적한 비틀기(도, 화면 기준 시계 +)
        private bool wasTwisting;
        private readonly RaycastHit[] aimHits = new RaycastHit[16];
        private readonly Collider[] overlapBuffer = new Collider[32];

        private PlacementRotationGizmo gizmo;
        private GameObject ghost;
        private Transform ghostTransform;
        private Renderer[] ghostRenderers = System.Array.Empty<Renderer>();
        private bool ghostShowsValid = true;
        private InteractionPromptView placePromptView;   // 구 위 '좌클릭 = 배치'
        private InteractionPromptView twistPromptView;   // 구 아래 왼쪽 'Q E = 좌우 회전'
        private InteractionPromptView yawPromptView;     // 구 아래 오른쪽 '우클릭 끌기 = 회전'
        private Transform abovePromptAnchor;
        private Transform belowPromptAnchor;
        private Sprite placeIcon;
        private Sprite dragIcon;
        private float nextPromptDiagnosticAt;

        private float MaxReach => interactionConfig != null ? interactionConfig.PlacementMaxDistance : 2.8f;
        private float TwistSpeedDegrees => interactionConfig != null ? interactionConfig.PlacementRotateSpeedDegrees : 90f;

        private void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            playerColliders = GetComponentsInChildren<Collider>(true);

            if (inputActions == null || interactionConfig == null
                || ghostValidMaterial == null || ghostInvalidMaterial == null)
            {
                Debug.LogError("ItemPlacementController: Inspector 참조(InputActions/Config/고스트 머티리얼 2개)가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            rmbAction = playerMap.FindAction("PlacementMode", throwIfNotFound: true);
            rotateAction = playerMap.FindAction("RotateObject", throwIfNotFound: true);
            attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
            gizmo = PlacementRotationGizmo.Create(transform);
        }

        private void OnEnable()
        {
            playerMap?.Enable();
            if (cameraRig == null) cameraRig = FindFirstObjectByType<PlayerCameraController>();
        }

        private void OnDisable()
        {
            ExitPlacementMode();
        }

        private void OnDestroy()
        {
            DestroyGhost();
            if (placePromptView != null) Destroy(placePromptView.gameObject);
            if (twistPromptView != null) Destroy(twistPromptView.gameObject);
            if (yawPromptView != null) Destroy(yawPromptView.gameObject);
            if (abovePromptAnchor != null) Destroy(abovePromptAnchor.gameObject);
            if (belowPromptAnchor != null) Destroy(belowPromptAnchor.gameObject);
            if (gizmo != null) Destroy(gizmo.gameObject);
        }

        private void Update()
        {
            var item = interactor.CarriedItem;
            if (item == null || IsInputLocked) placementMode = false; // 손이 비면(놓기·던지기·뺏김) 또는 잠기면 모드도 꺼진다

            var pointerLocked = Game.Client.Common.WebPointerInput.IsLocked;
            // 들고 있을 때 우클릭을 누르면 배치 모드로 들어간다(1·3인칭 공통). 모드 안에서는 우클릭 유지가 회전이다.
            if (item != null && !placementMode && !IsInputLocked && pointerLocked && rmbAction.WasPressedThisFrame())
            {
                placementMode = true;
            }

            // 카메라 리그는 씬 로드 뒤에 켜질 수 있어 매 프레임 다시 찾는다(시선 잠금·물건 숨김에 쓴다).
            if (cameraRig == null) cameraRig = FindFirstObjectByType<PlayerCameraController>();

            var wantPreview = item != null && placementMode;
            if (held != null && (item != held || !wantPreview))
            {
                EndHold(reattachToHoldPoint: item == held);
            }
            if (wantPreview && held == null && TryEnsureCamera())
            {
                BeginHold(item);
            }

            // 배치 확정 좌클릭 뒤 손을 뗄 때까지 펀치를 막는다
            if (blockAttackUntilRelease && !attackAction.IsPressed()) blockAttackUntilRelease = false;
            interactor.IsThrowSuppressed = held != null;                       // 배치 모드에서는 좌클릭이 던지기가 아니라 배치
            if (cameraRig != null) cameraRig.HideHeldItemOverride = held != null; // 모드 동안 실제 물건은 숨기고 실루엣만

            if (held == null || !TryEnsureCamera())
            {
                if (cameraRig != null) cameraRig.LookSuspended = false;
                gizmo?.Hide();
                return;
            }

            var wasRotating = rotating;
            rotating = pointerLocked && rmbAction.IsPressed();
            if (rotating && !wasRotating) { dragYawDeg = 0f; dragPitchDeg = 0f; } // 새 드래그 시작: 기즈모 채움을 0에서 다시
            if (cameraRig != null) cameraRig.LookSuspended = rotating;

            if (rotating && Mouse.current != null)
            {
                ApplyRotationDelta(Mouse.current.delta.ReadValue());
            }

            var twistInput = pointerLocked ? rotateAction.ReadValue<float>() : 0f;
            var twistingNow = Mathf.Abs(twistInput) > 0.01f;
            if (twistingNow && !wasTwisting) twistDeg = 0f; // 새로 누르기 시작: 채움을 0에서 다시
            wasTwisting = twistingNow;
            if (twistingNow)
            {
                // E(+) = 화면 기준 시계 방향. Unity에서 시선축 양의 회전은 반시계라 부호를 뒤집는다.
                var step = twistInput * TwistSpeedDegrees * Time.deltaTime;
                SetWorldRotation(Quaternion.AngleAxis(-step, cam.forward) * WorldRotation());
                twistDeg += step;
            }

            if (pointerLocked && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetOrientation();
            }

            // 좌클릭 = 배치 확정. 겹쳐서 빨간 상태면 무시한다.
            if (pointerLocked && attackAction.WasPressedThisFrame() && TryPlace())
            {
                return;
            }

            gizmo.Show(HeldCenter, cam, held.transform.rotation, rotating, twistingNow, dragYawDeg, dragPitchDeg, twistDeg);
        }

        private void FixedUpdate()
        {
            if (held == null || heldBody == null || cam == null) return;
            // 좌클릭 배치·F 놓기·던지기 직후: 인터랙터가 이미 손을 비웠으면(같은 프레임) 여기서 물건을 옮기지 않는다.
            // 옮기면 던진 속도가 지워지고, 다음 Update에서 EndHold가 정리한다.
            if (interactor.CarriedItem != held) return;

            // 회전: 시선 좌우 기준으로 저장한 자세를 세계 기준으로 바꿔 적용한다. 몸을 돌리면 물건도 같이 돈다.
            holdRot = WorldRotation();
            heldBody.MoveRotation(holdRot);
            var center = ResolveBlockedCenter(HoldTarget());
            // 배치 상자 중심을 조준점에 맞춘다(피벗을 맞추면 바닥 피벗 모델이 밑동을 축으로 돈다).
            var pivot = center - holdRot * centerLocalOffset;
            heldBody.MovePosition(pivot);
            heldBody.interpolation = RigidbodyInterpolation.Interpolate;
            IsOverlapping = CheckOverlap(pivot, holdRot);
            interactor.IsDropSuppressed = IsOverlapping;
        }

        private void LateUpdate()
        {
            if (ghostTransform == null || held == null || interactor.CarriedItem != held) return;
            // 물리 보간(Interpolate) 뒤의 렌더 자세를 그대로 복사한다
            ghostTransform.SetPositionAndRotation(held.transform.position, held.transform.rotation);
            // 겹치면 빨간 실루엣(배치 불가 머티리얼), 아니면 초록. 바뀔 때만 머티리얼을 갈아 끼운다.
            var showValid = !IsOverlapping;
            if (showValid != ghostShowsValid)
            {
                ghostShowsValid = showValid;
                ApplyGhostMaterial(ghostRenderers, showValid ? ghostValidMaterial : ghostInvalidMaterial);
            }
            RefreshPrompts();
        }

        /// <summary>
        /// 우클릭 드래그 회전. 마우스가 가는 쪽으로 물건 앞면이 끌려간다: 좌우는 세계 수직축, 상하는 카메라 좌우축.
        /// 증분을 누적 회전의 왼쪽에 곱해 축은 항상 내 시점 기준으로 고정한다.
        /// </summary>
        public void ApplyRotationDelta(Vector2 pixels)
        {
            if (cam == null || pixels.sqrMagnitude < 1e-6f) return;
            // 한 축으로 뚜렷하게 움직이면 그 축만 돌린다: 좌우로 끌 때 섞이는 손떨림이 앞뒤 기울기(와 기즈모 채움)에 새지 않게.
            pixels = PlacementAimMath.FilterDominantAxis(pixels, DominantAxisRatio);
            // Unity 왼손 좌표: 수직축 양의 회전은 위에서 봐 시계 방향이라, 앞면을 마우스 쪽으로 끌려면 부호를 뒤집는다.
            var yaw = Quaternion.AngleAxis(-pixels.x * RotateDegreesPerPixel, Vector3.up);
            // 카메라 좌우축 양의 회전은 윗면을 멀어지는 쪽으로 넘긴다 = 마우스를 올리면 앞면이 위로.
            var pitch = Quaternion.AngleAxis(pixels.y * RotateDegreesPerPixel, cam.right);
            SetWorldRotation(pitch * yaw * WorldRotation());
            dragYawDeg += -pixels.x * RotateDegreesPerPixel;   // 세계 수직축 기준 시계 +
            dragPitchDeg += pixels.y * RotateDegreesPerPixel;  // 위→앞으로 넘어가는 방향 +
        }

        /// <summary>R: 똑바로 세우고 내 정면을 향하게.</summary>
        public void ResetOrientation()
        {
            holdRotLocal = Quaternion.identity;
            holdRot = WorldRotation();
        }

        /// <summary>시선 좌우(요)만 뽑은 회전. 물건 자세는 이 프레임 기준으로 저장해 몸을 돌려도 화면에서 같은 자세로 보인다.</summary>
        private Quaternion ViewYaw => cam != null ? Quaternion.AngleAxis(cam.eulerAngles.y, Vector3.up) : Quaternion.identity;
        private Quaternion WorldRotation() => ViewYaw * holdRotLocal;
        private void SetWorldRotation(Quaternion world) { holdRotLocal = Quaternion.Inverse(ViewYaw) * world; holdRot = world; }

        /// <summary>배치 확정: 초록(겹침 없음)일 때만 지금 자리에서 물리로 넘긴다(권위가 있으면 놓기 요청). 빨강이면 무시.</summary>
        private bool TryPlace()
        {
            if (held == null || IsOverlapping) return false;
            blockAttackUntilRelease = true;
            placementMode = false;
            interactor.PlaceCarriedItem(); // 지금 자리에서 물리로 넘어간다. 다음 Update에서 EndHold가 정리한다
            return true;
        }

        private void ExitPlacementMode()
        {
            placementMode = false;
            if (held != null) EndHold(reattachToHoldPoint: interactor != null && interactor.CarriedItem == held);
            if (interactor != null) interactor.IsThrowSuppressed = false;
            if (cameraRig != null) { cameraRig.LookSuspended = false; cameraRig.HideHeldItemOverride = false; }
            gizmo?.Hide();
        }

        private void BeginHold(CarryableItem item)
        {
            held = item;
            heldBody = item.GetComponent<Rigidbody>();
            heldColliders = item.GetComponentsInChildren<Collider>(true);
            item.BeginPhysicalHold();
            // 조준점에 딱 붙어야 하므로 운동학으로 움직인다. 기준점은 배치 상자 중심(콜라이더가 꺼져 있어도 유효).
            centerLocalOffset = item.PlacementCenterOffset;
            heldBody.isKinematic = true;
            // 첫 스텝은 보간 없이 조준점에 바로 놓고(HoldPoint 자리에서 미끄러져 오는 잔상 방지), 이후 스텝부터 보간을 켠다
            heldBody.interpolation = RigidbodyInterpolation.None;
            foreach (var mine in playerColliders)
                foreach (var theirs in heldColliders)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, true);

            // 초기 자세 = 잡기 직전에 놓여 있던 자세. (잡히는 순간 HoldPoint에 붙으며 회전이 바뀌므로 현재 회전은 쓰지 않는다)
            var before = item.PoseBeforePickup;
            SetWorldRotation(before.rotation);
            // 위치는 모드를 켠 순간 조준점(최대 거리)에 바로 둔다.
            var startCenter = ResolveBlockedCenter(HoldTarget());
            heldBody.rotation = before.rotation;
            heldBody.position = startCenter - before.rotation * centerLocalOffset;
            item.transform.SetPositionAndRotation(heldBody.position, heldBody.rotation);
            CreateGhost(item);
        }

        /// <param name="reattachToHoldPoint">아직 들고 있는 경우 true: 기존 들기(HoldPoint에 붙임)로 되돌린다.</param>
        private void EndHold(bool reattachToHoldPoint)
        {
            if (held == null) return;
            foreach (var mine in playerColliders)
                foreach (var theirs in heldColliders)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, false);
            if (heldBody != null) heldBody.isKinematic = false;
            held.EndPhysicalHold();
            if (reattachToHoldPoint && interactor.HoldPoint != null)
            {
                // 기존 들기: HoldPoint 자식으로, 운동학·콜라이더 꺼짐. 인터랙터가 매 프레임 머리 위로 위치를 잡는다.
                held.OnPickedUp(interactor.HoldPoint);
            }
            DestroyGhost();
            IsOverlapping = false;
            interactor.IsDropSuppressed = false;
            interactor.IsThrowSuppressed = false;
            if (cameraRig != null) { cameraRig.HideHeldItemOverride = false; cameraRig.LookSuspended = false; }
            held = null;
            heldBody = null;
            heldColliders = System.Array.Empty<Collider>();
        }

        // ---- 위치 ----

        /// <summary>시선 방향으로 뻗을 수 있는 최대 손 거리: 발 위치에서 MaxReach 안에 들어오는 시선 위의 가장 먼 점.</summary>
        private float MaxDistanceAlongView() =>
            PlacementAimMath.MaxDistanceAlongView(cam.position, cam.forward, transform.position, MaxReach, MinDistance);

        private Vector3 HoldTarget() => cam.position + cam.forward * MaxDistanceAlongView();

        /// <summary>
        /// 물건 자리는 항상 조준선 위에서 정한다. 지금 물건이 어디 있는지는 보지 않으므로 끼여도 조준점을 그대로 따라 나온다.
        /// 1) 조준선이 벽·선반 같은 정적 지형에 닿는 지점보다 멀리는 두지 않는다(조준점 자체가 거기까지).
        /// 2) 그 한도에서 카메라 쪽으로 조금씩 물러나며 물건 모양이 정적 지형과 겹치지 않는 가장 먼 자리를 고른다.
        ///    동역학 소품은 겹쳐도 상관없다(빨간 실루엣으로 알린다).
        /// </summary>
        private Vector3 ResolveBlockedCenter(Vector3 desiredCenter)
        {
            var origin = cam.position;
            var offset = desiredCenter - origin;
            var distance = offset.magnitude;
            if (distance < 0.01f) return desiredCenter;
            var dir = offset / distance;

            var limit = distance;
            var count = Physics.RaycastNonAlloc(origin, dir, aimHits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = aimHits[i];
                if (hit.collider == null || hit.distance <= 0f) continue;
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(held.transform)) continue;
                var body = hit.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue; // 동역학 소품은 조준선을 막지 않는다
                limit = Mathf.Min(limit, hit.distance - BlockMargin);
            }
            limit = Mathf.Max(limit, MinDistance);

            for (var d = limit; d >= MinDistance; d -= PullInStep)
            {
                var center = origin + dir * d;
                if (!OverlapsStatic(center - holdRot * centerLocalOffset, holdRot)) return center;
            }
            return origin + dir * MinDistance;
        }

        /// <summary>물건 배치 상자가 정적 지형(또는 운동학 몸체)과 겹치는지. 동역학 소품·자기 자신·내 몸은 무시.</summary>
        private bool OverlapsStatic(Vector3 pivotPosition, Quaternion rotation)
        {
            var center = pivotPosition + rotation * held.PlacementCenterOffset;
            var extents = held.PlacementHalfExtents - Vector3.one * 0.01f;
            if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f) return false;
            var count = Physics.OverlapBoxNonAlloc(center, extents, overlapBuffer, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = overlapBuffer[i];
                if (other == null) continue;
                if (other.transform.IsChildOf(held.transform) || other.transform.IsChildOf(transform)) continue;
                var body = other.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 놓기 가능 판정: 물건의 배치 상자를 살짝 줄여 OverlapBox, 자기 자신·내 몸·다른 플레이어는 무시.
        /// 동역학 소품(다른 물건)과 겹쳐도 불가다. 그 상태를 빨강으로 알리고 좌클릭·F를 막는다.
        /// </summary>
        private bool CheckOverlap(Vector3 pivotPosition, Quaternion rotation)
        {
            var center = pivotPosition + rotation * held.PlacementCenterOffset;
            var extents = held.PlacementHalfExtents - Vector3.one * OverlapSkin;
            if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f) return false;
            var count = Physics.OverlapBoxNonAlloc(center, extents, overlapBuffer, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = overlapBuffer[i];
                if (other == null) continue;
                if (other.transform.IsChildOf(held.transform)) continue;   // 자기 자신
                if (other.transform.IsChildOf(transform)) continue;        // 내 몸
                if (other.GetComponentInParent<CharacterController>() != null) continue; // 다른 플레이어
                return true;
            }
            return false;
        }

        // ---- 실루엣·안내 ----

        private void CreateGhost(CarryableItem item)
        {
            DestroyGhost();
            ghost = CreateGhostObject(item, ghostValidMaterial, out ghostRenderers);
            ghostShowsValid = true;
            ghostTransform = ghost.transform;
            ghostTransform.SetPositionAndRotation(item.transform.position, item.transform.rotation);
        }

        private void DestroyGhost()
        {
            placePromptView?.Hide();
            twistPromptView?.Hide();
            yawPromptView?.Hide();
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ghostTransform = null;
            ghostRenderers = System.Array.Empty<Renderer>();
        }

        /// <summary>
        /// 물건의 겉모습만 복제한 실루엣을 만든다. 콜라이더·리지드바디·CarryableItem을 떼고 모든 렌더러를 한 머티리얼로 칠한다.
        /// </summary>
        public static GameObject CreateGhostObject(CarryableItem item, Material material, out Renderer[] renderers)
        {
            var ghost = Instantiate(item.gameObject);
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

            renderers = ghost.GetComponentsInChildren<Renderer>();
            foreach (var ghostRenderer in renderers)
            {
                // 원본은 화면에서 숨겨져(forceRenderingOff) 있을 수 있다. Instantiate가 이 플래그를 복사하지는 않지만,
                // 미리보기 복제본은 반드시 보여야 하므로 명시적으로 켠다.
                ghostRenderer.forceRenderingOff = false;
                ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ApplyGhostMaterial(renderers, material);
            return ghost;
        }

        public static void ApplyGhostMaterial(Renderer[] renderers, Material material)
        {
            if (renderers == null || material == null)
            {
                return;
            }

            foreach (var ghostRenderer in renderers)
            {
                var materials = ghostRenderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                ghostRenderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// 배치 모드 안내: 기즈모 구 바로 위 '좌클릭 배치'(초록일 때만), 구 바로 아래 한 줄에 왼쪽 'Q E 좌우 회전'·오른쪽 '우클릭 끌기 회전'.
        /// 모두 물건이 아니라 회전하지 않는 기준점을 따라가며, 화면 기준 위·아래(카메라의 위 방향)로 고정된다.
        /// </summary>
        private void RefreshPrompts()
        {
            var hudAllows = held != null && ghostTransform != null &&
                            PlayerInteractor.CanShowWorldPrompt(interactor.HudVisible, interactor.InteractionPromptsAllowed,
                                Game.Client.Common.WebPointerInput.IsLocked);
            if (!hudAllows)
            {
                placePromptView?.Hide();
                twistPromptView?.Hide();
                yawPromptView?.Hide();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 실제 매치 씬에서 안내가 안 보이는 원인을 찾기 위한 진단. 1초에 한 번만 남긴다.
                if (Time.unscaledTime >= nextPromptDiagnosticAt)
                {
                    nextPromptDiagnosticAt = Time.unscaledTime + 1f;
                    Debug.Log($"[Placement] 안내 숨김: ghost={(ghostTransform != null)} hudVisible={interactor.HudVisible} " +
                              $"(presentation={interactor.PresentationHudVisible}, loading={Game.Client.Common.LoadingView.IsAnyPresented}) " +
                              $"promptsAllowed={interactor.InteractionPromptsAllowed} cursorLocked={Game.Client.Common.WebPointerInput.IsLocked}", this);
                }
#endif
                return;
            }

            var center = HeldCenter;
            var radius = PlacementRotationGizmo.RadiusAt(Vector3.Distance(cam.position, center));
            abovePromptAnchor ??= CreateAnchor("PlacementPromptAbove");
            belowPromptAnchor ??= CreateAnchor("PlacementPromptBelow");
            var screenUp = cam.up;
            abovePromptAnchor.SetPositionAndRotation(center + screenUp * (radius + 0.04f), Quaternion.identity);
            belowPromptAnchor.SetPositionAndRotation(center - screenUp * (radius + 0.04f), Quaternion.identity);

            if (IsOverlapping)
            {
                placePromptView?.Hide();
            }
            else if (placePromptView == null || !placePromptView.IsVisible)
            {
                placePromptView ??= InteractionPromptView.Create();
                placeIcon ??= InteractionPromptView.LoadLeftClickIcon();
                placePromptView.Show(string.Empty, PlaceActionLabel, abovePromptAnchor, placeIcon,
                    worldAnchor: abovePromptAnchor.position);
            }

            if (twistPromptView == null || !twistPromptView.IsVisible)
            {
                if (twistPromptView == null)
                {
                    twistPromptView = InteractionPromptView.Create();
                    twistPromptView.SetHangsBelowAnchor(true);
                    twistPromptView.ScreenOffset = new Vector2(-PromptColumnPixels, 0f); // 왼쪽
                }
                twistPromptView.Show(TwistKeyLabel, TwistActionLabel, belowPromptAnchor, null,
                    worldAnchor: belowPromptAnchor.position);
            }

            if (yawPromptView == null || !yawPromptView.IsVisible)
            {
                if (yawPromptView == null)
                {
                    yawPromptView = InteractionPromptView.Create();
                    yawPromptView.SetHangsBelowAnchor(true);
                    yawPromptView.ScreenOffset = new Vector2(PromptColumnPixels, 0f); // 오른쪽
                    dragIcon = InteractionPromptView.LoadMouseDragIcon();
                }
                yawPromptView.Show(string.Empty, YawActionLabel, belowPromptAnchor, dragIcon,
                    worldAnchor: belowPromptAnchor.position);
            }
        }

        private Transform CreateAnchor(string name)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(transform, false);
            return anchor;
        }

        private bool TryEnsureCamera()
        {
            if (cam != null) return true;
            var main = Camera.main;
            if (main == null) return false;
            cam = main.transform;
            return true;
        }
    }
}
