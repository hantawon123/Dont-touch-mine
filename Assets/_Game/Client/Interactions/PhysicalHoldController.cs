using Game.Client.Cameras;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// [테스트, Playground 전용] PLACE TO FIT 방식 물리 들기.
    /// 1인칭에서 물건을 잡으면 손(HoldPoint)에 붙이는 대신 조준점(크로스헤어) 위 최대 거리에 딱 붙여 든다. 몸체는 운동학
    /// (kinematic)으로 움직여서 조준점에서 처지지 않는다. 들고 있는 동안은 콜라이더를 꺼 두어 다른 물건을 밀거나 부딪히지
    /// 않는 순수 미리보기다. 벽·탁자·선반 같은 정적 지형은 조준선 광선·상자 검사로 미리 막혀 그 앞까지만 나가고, 다른 물건과
    /// 겹치면 빨간 실루엣으로 놓기를 막는다. 회전은 내 시선 기준으로 고정된다. 놓으면(F) 그 자리에서 물리로 넘어간다.
    /// 3인칭에서는 기존처럼 실제 물건을 머리 위에 든다.
    /// <para>
    /// 조작(1·3인칭 공통): F 잡기(기존) → 들고 있을 때 우클릭 = 배치 모드 켜기. 배치 모드에서 우클릭 유지 + 마우스 = 회전
    /// (좌우 → 세계 수직축, 상하 → 카메라 좌우축, 앞면이 마우스를 따라옴), Q/E = 시선축 비틀기(E 시계), R = 똑바로 세우기,
    /// 좌클릭 = 배치 확정(초록일 때만). F = 기존대로 그냥 떨어뜨리기(모드 안·밖 모두, 겹친 빨간 상태에서는 막힘).
    /// 배치 모드 밖에서는 기존처럼 들고 다니고 좌클릭 = 던지기. 우클릭 회전 중에는 시선이 멈춘다.
    /// 배치 모드는 1인칭·3인칭 같은 로직이다(조준선은 현재 카메라 기준). 모드 동안 실제 물건은 숨기고 실루엣만 보인다.
    /// 손 거리는 조절하지 않고 항상 시선 위 최대 거리(발에서 MaxReach 안에 드는 가장 먼 점)에 둔다. 걸어 다녀도,
    /// 시선을 내려도 그 기준이 매 프레임 다시 계산되어 유지된다.
    /// </para>
    /// <para>
    /// 웹 시뮬레이터(physical-placement-sim)에서 확정한 수식을 그대로 옮겼다. 기존 배치 모드(ItemPlacementController)는
    /// 이 컴포넌트가 붙은 캐릭터에서 꺼지되, 그 배치 가능 실루엣 머티리얼은 그대로 쓴다: 실제 물건은 1인칭에서 숨기고
    /// (기존 규칙), 물리 몸체의 자세를 따라가는 실루엣 고스트만 보인다. 네트워크 권위 연동 전 단독 씬 검증용이다.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public sealed class PhysicalHoldController : MonoBehaviour
    {
        // ---- 시뮬레이터와 같은 값 ----
        private const float MinDistance = 0.45f;
        private const float MaxReach = 2.8f;            // 발 기준 최대 손 거리(정면 시선에서 앞으로 약 2.4 m). 네트워크 연동 시 호스트 놓기 한도(현재 2 m)도 이에 맞춰 올려야 한다
        private const float TwistSpeedDegrees = 90f;    // 짧게 눌렀을 때 덜 돌게 120에서 낮춤. 한 바퀴 4초
        private const float RotateDegreesPerPixel = 0.3f;
        private const float CenterTime = 0.25f;         // (예비) 조준점·최대 거리로 모이는 시간. 지금은 모드를 켤 때 바로 조준점에 둔다
        private const float BlockMargin = 0.02f;        // 정적 지형 앞에 남기는 틈(m)
        private const float OverlapSkin = 0.02f;        // 겹침 판정에서 상자를 줄이는 두께(m). 살짝 닿는 것은 겹침으로 안 본다
        private const float PullInStep = 0.04f;         // 조준선 위에서 빈 자리를 찾을 때 카메라 쪽으로 물러나는 간격(m)
        private readonly RaycastHit[] aimHits = new RaycastHit[16];

        private PlayerInteractor interactor;
        private ItemPlacementController placement;
        private PlayerCameraController cameraRig;
        private Transform cam;
        private InputAction rotateAction;   // Q/E
        private InputAction rmbAction;      // 우클릭(기존 PlacementMode 바인딩)
        private Collider[] playerColliders;

        private CarryableItem held;
        private Rigidbody heldBody;
        private Collider[] heldColliders = System.Array.Empty<Collider>();
        private Quaternion holdRot = Quaternion.identity;      // 이번 스텝의 세계 기준 자세(= 시선 요 × holdRotLocal)
        private Quaternion holdRotLocal = Quaternion.identity; // 내 시선 좌우(요) 기준 자세. 돌아서면 물건도 같이 돈다
        private float holdDistance = 1.1f;
        private Vector3 holdLateral;
        private Vector3 comLocalOffset;     // 피벗 → 배치 상자 중심(로컬). 조준점에 맞추는 기준. 질량 중심은 콜라이더가 꺼지면 피벗으로 되돌아가 쓸 수 없다
        private bool rotating;
        private float twistInput;
        private bool placementMode;              // F로 켜고 끈다. 켜진 동안만 실루엣·회전·좌클릭 배치가 동작
        private bool blockAttackUntilRelease;    // 배치 확정 좌클릭이 손을 비운 뒤 같은 입력으로 펀치가 나가지 않게
        private InputAction attackAction;        // 좌클릭(배치 확정)

        /// <summary>배치 모드(실루엣 미리보기)가 켜져 있는지.</summary>
        public bool IsPlacementMode => placementMode && held != null;
        private float dragYawDeg, dragPitchDeg; // 이번 우클릭 드래그에서 누적한 회전(기즈모 표시용)
        private float twistDeg;                 // 이번 Q/E 누름에서 누적한 비틀기(도, 화면 기준 시계 +)
        private bool wasTwisting;
        private PhysicalHoldGizmo gizmo;
        private GameObject ghost;
        private Transform ghostTransform;
        private Renderer[] ghostRenderers = System.Array.Empty<Renderer>();
        private bool ghostShowsValid = true;
        private InteractionPromptView promptView; // '좌클릭 = 배치' 안내(기존 배치 모드와 같은 뷰)
        private Sprite placeIcon;
        private Transform promptAnchor;           // 안내 위치: 기즈모 구 바로 위. 물건 회전과 무관하게 세계 위 방향으로 고정
        private readonly Collider[] overlapBuffer = new Collider[32];

        /// <summary>들고 있는 물건이 다른 물건·지형과 겹쳐 지금 자리에 놓을 수 없는 상태인지.</summary>
        public bool IsOverlapping { get; private set; }

        public bool IsHolding => held != null;
        public CarryableItem Held => held;
        public Quaternion HoldRotation => holdRot;

        /// <summary>시선 좌우(요)만 뽑은 회전. 물건 자세는 이 프레임 기준으로 저장해 몸을 돌려도 화면에서 같은 자세로 보인다.</summary>
        private Quaternion ViewYaw => cam != null ? Quaternion.AngleAxis(cam.eulerAngles.y, Vector3.up) : Quaternion.identity;
        private Quaternion WorldRotation() => ViewYaw * holdRotLocal;
        private void SetWorldRotation(Quaternion world) { holdRotLocal = Quaternion.Inverse(ViewYaw) * world; holdRot = world; }
        public float HoldDistance => EffectiveHoldDistance();
        /// <summary>시선에 맞추는 기준점(질량 중심). 피벗이 바닥에 있는 모델도 보이는 중심이 크로스헤어에 온다.</summary>
        public Vector3 HeldCenter => heldBody != null ? heldBody.position + heldBody.rotation * comLocalOffset : (held != null ? held.transform.position : Vector3.zero);

        private void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            placement = GetComponent<ItemPlacementController>();
            playerColliders = GetComponentsInChildren<Collider>(true);
            var actions = interactor.InputActions;
            if (actions != null)
            {
                var map = actions.FindActionMap("Player", throwIfNotFound: false);
                rotateAction = map?.FindAction("RotateObject", throwIfNotFound: false);
                rmbAction = map?.FindAction("PlacementMode", throwIfNotFound: false);
                attackAction = map?.FindAction("Attack", throwIfNotFound: false);
            }

            // 기존 고스트 배치 모드는 우클릭을 쓰므로 이 테스트가 붙은 캐릭터에서는 끈다.
            if (placement != null) placement.enabled = false;
            gizmo = PhysicalHoldGizmo.Create(transform);
        }

        private void OnEnable()
        {
            if (cameraRig == null) cameraRig = FindFirstObjectByType<PlayerCameraController>();
        }

        private void OnDisable()
        {
            if (cameraRig != null) { cameraRig.LookSuspended = false; cameraRig.HideHeldItemOverride = false; }
            EndHold();
            gizmo?.Hide();
        }

        /// <summary>배치 확정: 초록(겹침 없음)일 때만 지금 자리에서 물리로 넘긴다. 빨강이면 무시.</summary>
        private bool TryPlace()
        {
            if (held == null || IsOverlapping) return false;
            blockAttackUntilRelease = true;
            placementMode = false;
            interactor.DropCarriedItem(); // 지금 자리에서 물리로 넘어간다. 다음 Update에서 EndHold가 정리한다
            return true;
        }

        private void OnDestroy()
        {
            if (placement != null) placement.ExternalAttackBlock = false;
            DestroyGhost();
            if (promptView != null) Destroy(promptView.gameObject);
            if (promptAnchor != null) Destroy(promptAnchor.gameObject);
            if (placement != null) placement.enabled = true;
            if (gizmo != null) Destroy(gizmo.gameObject);
        }

        private void Update()
        {
            var item = interactor.CarriedItem;
            if (item == null) placementMode = false; // 손이 비면(놓기·던지기·뺏김) 모드도 꺼진다
            // 들고 있을 때 우클릭을 누르면 배치 모드로 들어간다(1·3인칭 공통). 모드 안에서는 우클릭 유지가 회전이다.
            if (item != null && !placementMode && Game.Client.Common.WebPointerInput.IsLocked &&
                rmbAction != null && rmbAction.WasPressedThisFrame())
            {
                placementMode = true;
            }
            // 카메라 리그는 씬 로드 뒤에 켜질 수 있어 매 프레임 다시 찾는다(시선 잠금·물건 숨김에 쓴다).
            if (cameraRig == null) cameraRig = FindFirstObjectByType<PlayerCameraController>();
            // 실루엣 미리보기는 배치 모드일 때(1·3인칭 공통). 모드를 끄면 기존 들기(HoldPoint)로 되돌린다.
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
            if (blockAttackUntilRelease && (attackAction == null || !attackAction.IsPressed())) blockAttackUntilRelease = false;
            if (placement != null) placement.ExternalAttackBlock = held != null || blockAttackUntilRelease;
            interactor.IsThrowSuppressed = held != null; // 배치 모드에서는 좌클릭이 던지기가 아니라 배치
            if (cameraRig != null) cameraRig.HideHeldItemOverride = held != null; // 모드 동안 실제 물건은 숨기고 실루엣만

            if (held == null || !TryEnsureCamera())
            {
                if (cameraRig != null) cameraRig.LookSuspended = false;
                gizmo.Hide();
                return;
            }

            var pointerLocked = Game.Client.Common.WebPointerInput.IsLocked;
            var wasRotating = rotating;
            rotating = pointerLocked && rmbAction != null && rmbAction.IsPressed();
            if (rotating && !wasRotating) { dragYawDeg = 0f; dragPitchDeg = 0f; } // 새 드래그 시작: 호를 0에서 다시 채운다
            if (cameraRig != null) cameraRig.LookSuspended = rotating;

            if (rotating && Mouse.current != null)
            {
                ApplyRotationDelta(Mouse.current.delta.ReadValue());
            }

            twistInput = pointerLocked && rotateAction != null ? rotateAction.ReadValue<float>() : 0f;
            var twistingNow = Mathf.Abs(twistInput) > 0.01f;
            if (twistingNow && !wasTwisting) twistDeg = 0f; // 새로 누르기 시작: 호를 0에서 다시 채운다
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

            // 좌클릭 = 배치 확정(F도 같음). 겹쳐서 빨간 상태면 무시한다.
            if (pointerLocked && attackAction != null && attackAction.WasPressedThisFrame() && TryPlace())
            {
                return;
            }

            gizmo.Show(HeldCenter, cam, held.transform.rotation,
                rotating, Mathf.Abs(twistInput) > 0.01f, dragYawDeg, dragPitchDeg, twistDeg);
        }

        /// <summary>
        /// 우클릭 드래그 회전. 마우스가 가는 쪽으로 물건 앞면이 끌려간다: 좌우는 세계 수직축, 상하는 카메라 좌우축.
        /// 증분을 누적 회전의 왼쪽에 곱해 축은 항상 내 시점 기준으로 고정한다.
        /// </summary>
        public void ApplyRotationDelta(Vector2 pixels)
        {
            if (cam == null || pixels.sqrMagnitude < 1e-6f) return;
            // 한 축으로 뚜렷하게 움직이면 그 축만 돌린다. 좌우로 끌 때 섞이는 손떨림만큼의 세로 움직임이
            // 앞뒤 기울기(와 파란 채움)에 새어 들어가는 것을 막는다. 대각선 의도(비율 2.5 안)는 두 축 모두 돈다.
            const float dominantRatio = 2.5f;
            if (Mathf.Abs(pixels.x) > Mathf.Abs(pixels.y) * dominantRatio) pixels.y = 0f;
            else if (Mathf.Abs(pixels.y) > Mathf.Abs(pixels.x) * dominantRatio) pixels.x = 0f;
            // Unity 왼손 좌표: 수직축 양의 회전은 위에서 봐 시계 방향이라, 앞면을 마우스 쪽으로 끌려면 부호를 뒤집는다.
            var yaw = Quaternion.AngleAxis(-pixels.x * RotateDegreesPerPixel, Vector3.up);
            // 카메라 좌우축 양의 회전은 윗면을 멀어지는 쪽으로 넘긴다 = 마우스를 올리면 앞면이 위로.
            var pitch = Quaternion.AngleAxis(pixels.y * RotateDegreesPerPixel, cam.right);
            SetWorldRotation(pitch * yaw * WorldRotation());
            dragYawDeg += -pixels.x * RotateDegreesPerPixel;   // 세계 수직축 기준 시계 +
            dragPitchDeg += pixels.y * RotateDegreesPerPixel;  // 위→앞으로 넘어가는 방향 +
        }

        /// <summary>R: 똑바로 세우고 내 정면을 향하게(기존 배치 모드의 초기 자세와 같음).</summary>
        public void ResetOrientation()
        {
            // 똑바로 세워 내 정면을 향하게 = 시선 기준 자세는 항등
            holdRotLocal = Quaternion.identity;
            holdRot = WorldRotation();
        }

        private void BeginHold(CarryableItem item)
        {
            if (!TryEnsureCamera()) return;
            held = item;
            heldBody = item.GetComponent<Rigidbody>();
            heldColliders = item.GetComponentsInChildren<Collider>(true);
            item.BeginPhysicalHold();
            // 조준점에 딱 붙어야 하므로 운동학으로 움직인다.
            // 기준점은 배치 상자 중심(콜라이더 모양에서 계산, 콜라이더가 꺼져 있어도 유효). 피벗이 밑면에 있는 소품도 보이는 중심이 크로스헤어에 온다.
            comLocalOffset = item.PlacementCenterOffset;
            heldBody.isKinematic = true;
            // 첫 스텝은 보간 없이 조준점에 바로 놓고(HoldPoint 자리에서 미끄러져 오는 잔상 방지), 이후 스텝부터 보간을 켠다
            heldBody.interpolation = RigidbodyInterpolation.None;
            foreach (var mine in playerColliders)
                foreach (var theirs in heldColliders)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, true);

            // 초기 자세 = 잡기 직전에 놓여 있던 자세. (잡히는 순간 HoldPoint에 붙으며 회전이 바뀌므로 현재 회전은 쓰지 않는다)
            var before = item.PoseBeforePickup;
            SetWorldRotation(before.rotation);
            // 위치는 모드를 켠 순간 조준점(최대 거리)에 바로 둔다. 예전 자리에서 날아오는 연출은 몸 뒤에서 오는 듯 보여 뺐다.
            holdDistance = MaxDistanceAlongView();
            holdLateral = Vector3.zero;
            var startCenter = ResolveBlockedCenter(HoldTarget());
            heldBody.rotation = before.rotation;
            heldBody.position = startCenter - before.rotation * comLocalOffset;
            item.transform.SetPositionAndRotation(heldBody.position, heldBody.rotation);
            CreateGhost(item);
        }

        /// <param name="reattachToHoldPoint">3인칭 전환처럼 아직 들고 있는 경우 true: 기존 방식(HoldPoint에 붙임)으로 되돌린다.</param>
        private void EndHold(bool reattachToHoldPoint = false)
        {
            if (held == null) return;
            foreach (var mine in playerColliders)
                foreach (var theirs in heldColliders)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, false);
            heldBody.isKinematic = false;
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
            if (cameraRig != null) cameraRig.HideHeldItemOverride = false;
            held = null;
            heldBody = null;
            heldColliders = System.Array.Empty<Collider>();
            if (cameraRig != null) cameraRig.LookSuspended = false;
        }

        /// <summary>
        /// 실루엣 고스트: 배치 모드의 '배치 가능' 머티리얼로 칠한 복제본. 실제 물건은 1인칭에서 숨겨지므로(카메라 컨트롤러의
        /// 기존 규칙) 플레이어에게 보이는 것은 이 고스트다. 물리 몸체의 자세를 매 프레임 그대로 따라간다.
        /// </summary>
        private void CreateGhost(CarryableItem item)
        {
            DestroyGhost();
            var material = placement != null ? placement.GhostValidMaterial : null;
            if (material == null) return; // 머티리얼이 없으면 실제 물건이 보이는 채로 진행
            ghost = ItemPlacementController.CreateGhostObject(item, material, out ghostRenderers);
            ghostShowsValid = true;
            ghost.name = "PhysicalHoldGhost";
            ghostTransform = ghost.transform;
            ghostTransform.SetPositionAndRotation(item.transform.position, item.transform.rotation);
        }

        private void DestroyGhost()
        {
            promptView?.Hide();
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ghostTransform = null;
            ghostRenderers = System.Array.Empty<Renderer>();
        }

        private void LateUpdate()
        {
            if (ghostTransform == null || held == null || interactor.CarriedItem != held) return;
            // 물리 보간(Interpolate) 뒤의 렌더 자세를 그대로 복사한다
            ghostTransform.SetPositionAndRotation(held.transform.position, held.transform.rotation);
            // 겹치면 빨간 실루엣(배치 불가 머티리얼), 아니면 초록. 바뀔 때만 머티리얼을 갈아 끼운다.
            var showValid = !IsOverlapping;
            if (showValid != ghostShowsValid && placement != null)
            {
                ghostShowsValid = showValid;
                ItemPlacementController.ApplyGhostMaterial(ghostRenderers,
                    showValid ? placement.GhostValidMaterial : placement.GhostInvalidMaterial);
            }
            RefreshPlacementPrompt();
        }

        /// <summary>기존 배치 모드와 같은 안내: 놓을 수 있는(초록) 동안 실루엣 위에 '좌클릭 배치'를 띄운다.</summary>
        private void RefreshPlacementPrompt()
        {
            var canShow = held != null && ghostTransform != null && !IsOverlapping &&
                          PlayerInteractor.CanShowWorldPrompt(interactor.HudVisible, interactor.InteractionPromptsAllowed,
                              Game.Client.Common.WebPointerInput.IsLocked);
            if (!canShow)
            {
                promptView?.Hide();
                return;
            }
            // 물건이 아니라 회전하지 않는 별도 기준점을 따라가게 한다: 기즈모 구 바로 위, 항상 세계 위 방향
            if (promptAnchor == null)
            {
                promptAnchor = new GameObject("PlacementPromptAnchor").transform;
                promptAnchor.SetParent(transform, false);
            }
            var center = HeldCenter;
            var radius = PhysicalHoldGizmo.RadiusAt(Vector3.Distance(cam.position, center));
            promptAnchor.SetPositionAndRotation(center + Vector3.up * (radius + 0.04f), Quaternion.identity);
            if (promptView != null && promptView.IsVisible) return;
            promptView ??= InteractionPromptView.Create();
            placeIcon ??= InteractionPromptView.LoadLeftClickIcon();
            promptView.Show(string.Empty, ItemPlacementController.PlaceActionLabel, promptAnchor, placeIcon,
                worldAnchor: promptAnchor.position);
        }

        /// <summary>
        /// 배치 모드와 같은 규칙으로 겹침을 본다: 물건의 배치 상자를 살짝 줄여 OverlapBox, 자기 자신과 플레이어는 무시.
        /// 운동학 몸체는 밀리지 않으므로 소품이 밀리지 못하면(벽에 끼임·무거움) 그대로 파고든다. 그 상태를 빨강으로 알린다.
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

        private void FixedUpdate()
        {
            if (held == null || heldBody == null || cam == null) return;
            // 좌클릭 던지기·F 놓기 직후: 인터랙터가 이미 손을 비웠으면(같은 프레임) 여기서 물건을 옮기지 않는다.
            // 옮기면 던진 속도가 지워지고, 다음 Update에서 EndHold가 정리한다.
            if (interactor.CarriedItem != held) return;
            var dt = Time.fixedDeltaTime;
            var blend = 1f - Mathf.Exp(-dt / CenterTime);
            holdLateral *= 1f - blend;
            // 잡은 자리의 거리에서 시작해 잠깐 사이에 최대 거리로 붙는다. 이후에는 매 스텝 최대 거리 그대로(이동·시선 변화 반영).
            holdDistance = Mathf.Lerp(holdDistance, MaxDistanceAlongView(), blend);
            // 회전: 시선 좌우 기준으로 저장한 자세를 세계 기준으로 바꿔 적용한다. 몸을 돌리면 물건도 같이 돈다.
            holdRot = WorldRotation();
            heldBody.MoveRotation(holdRot);
            var center = ResolveBlockedCenter(HoldTarget());
            // 질량 중심을 조준점에 맞춘다(피벗을 맞추면 바닥 피벗 모델이 밑동을 축으로 돈다).
            var pivot = center - holdRot * comLocalOffset;
            heldBody.MovePosition(pivot);
            heldBody.interpolation = RigidbodyInterpolation.Interpolate;
            IsOverlapping = CheckOverlap(pivot, holdRot);
            interactor.IsDropSuppressed = IsOverlapping;
        }

        /// <summary>
        /// 물건 자리는 항상 조준선 위에서 정한다. 지금 물건이 어디 있는지는 보지 않으므로 끼여도 조준점을 그대로 따라 나온다.
        /// 1) 조준선이 벽·선반 같은 정적 지형에 닿는 지점보다 멀리는 두지 않는다(조준점 자체가 거기까지).
        /// 2) 그 한도에서 카메라 쪽으로 조금씩 물러나며 물건 모양이 정적 지형과 겹치지 않는 가장 먼 자리를 고른다.
        ///    동역학 소품은 겹쳐도 상관없다(밀어내거나, 밀리지 못하면 빨간 실루엣으로 알린다).
        /// </summary>
        private Vector3 ResolveBlockedCenter(Vector3 desiredCenter)
        {
            var origin = cam.position;
            var offset = desiredCenter - origin;
            var distance = offset.magnitude;
            if (distance < 0.01f) return desiredCenter;
            var dir = offset / distance;

            // 1) 조준선이 정적 지형에 닿는 지점
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

            // 2) 한도에서 카메라 쪽으로 물러나며 정적 지형과 겹치지 않는 첫 자리
            for (var d = limit; d >= MinDistance; d -= PullInStep)
            {
                var center = origin + dir * d;
                if (!OverlapsStatic(center - holdRot * comLocalOffset, holdRot)) return center;
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

        /// <summary>시선 방향으로 뻗을 수 있는 최대 손 거리: 발 위치에서 MaxReach 안에 들어오는 시선 위의 가장 먼 점.</summary>
        private float MaxDistanceAlongView()
        {
            var f = cam.forward;
            var e = cam.position - transform.position;
            var b = Vector3.Dot(e, f);
            var c = e.sqrMagnitude - MaxReach * MaxReach;
            var disc = b * b - c;
            return disc > 0f ? -b + Mathf.Sqrt(disc) : MinDistance;
        }

        /// <summary>지금 쓰는 손 거리. 잡은 직후 잠깐을 빼면 시선 위 최대 거리와 같다.</summary>
        private float EffectiveHoldDistance() =>
            cam == null ? holdDistance : Mathf.Max(MinDistance, Mathf.Min(holdDistance, MaxDistanceAlongView()));

        private Vector3 HoldTarget()
        {
            var lateral = cam.right * holdLateral.x + Vector3.up * holdLateral.y;
            // 바닥·지형과의 겹침은 ResolveBlockedCenter가 조준선 위에서 물러나며 풀기 때문에 여기서 높이를 보정하지 않는다.
            // (예전 바닥 보정은 목표점을 위로 밀어 조준선에서 벗어나게 했다.)
            return cam.position + cam.forward * EffectiveHoldDistance() + lateral;
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
