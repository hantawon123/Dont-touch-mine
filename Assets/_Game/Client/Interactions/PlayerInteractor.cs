using Game.Client.Players;
using Game.Core.Players;
using Game.Core.Settings;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// Sends local interaction intent to the authority. A null implementation
    /// means the standalone scene keeps using its existing local behaviour.
    /// </summary>
    public interface IPlayerInteractionCommands
    {
        bool RequestHold(string objectId);
        bool RequestDrop(Pose pose);
        bool RequestRelease(Pose pose);
        bool RequestThrow(Pose pose, Vector3 initialVelocity);
        bool RequestHit(int targetPlayerIndex);
        bool RequestUseShredder();
    }

    /// <summary>
    /// 조준한 대상을 감지하고 물건 상호작용 키 입력을 대상에 전달한다.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour, ICarriedItemDropper, ICarryingState
    {
        private const int MaxAimHits = 8;
        private bool hudVisible = true;
        private bool interfaceHudVisible = true;
        public bool HudVisible => hudVisible && interfaceHudVisible && !Game.Client.Common.LoadingView.IsAnyPresented;
        public bool PresentationHudVisible => hudVisible;
        public void SetInterfaceHudVisible(bool visible)
        {
            if (interfaceHudVisible == visible) return;
            interfaceHudVisible = visible;
            RefreshInteractionCue();
        }

        private static ControlSettingsSystem sharedSettings;
        private bool interactionPromptVisible = true;

        public bool InteractionPromptsAllowed => interactionPromptVisible;

        /// <summary>
        /// World prompts (방 설정, 물건 잡기, 배치) stay off while a menu owns
        /// the mouse. A free cursor is that menu.
        /// </summary>
        public static bool CanShowWorldPrompt(bool hudVisible, bool promptEnabled, bool cursorLocked) =>
            hudVisible && promptEnabled && cursorLocked;

        /// <summary>
        /// The centre mark stays off while a lobby or match modal owns the
        /// mouse. A free cursor, or an OS pointer sitting on a locked
        /// lockState, is that modal.
        /// </summary>
        public static bool CanShowCrosshair(bool hudVisible, bool cursorLocked, bool osCursorVisible) =>
            hudVisible && cursorLocked && !osCursorVisible;

        public const float CrosshairSize = 40f;
        public const float CrosshairThickness = 4f;
        public const float CrosshairGap = 16f;
        public const float CrosshairDotSize = 4f;
        public static float CrosshairOutlineThickness => CrosshairThickness * 0.5f;
        public static readonly Color CrosshairFill = new Color(1f, 1f, 1f, 0.8f);

        /// <summary>
        /// Hands the 컨트롤 tab's applied 물건 상호작용 key to world prompts.
        /// Pass null to fall back to the shipped key, as tests do.
        /// </summary>
        public static void UseSettings(ControlSettingsSystem settings)
        {
            if (sharedSettings != null)
            {
                sharedSettings.Changed -= OnSharedSettingsChanged;
            }

            sharedSettings = settings;
            if (sharedSettings != null)
            {
                sharedSettings.Changed += OnSharedSettingsChanged;
            }

            RefreshBoundPrompts();
        }

        public static string InteractKeyLabel()
        {
            var code = sharedSettings != null
                ? sharedSettings.Current.Get(ControlAction.Interact)
                : ControlCatalog.Defaults.Get(ControlAction.Interact);
            return ControlCatalog.KeyLabel(code);
        }

        public void SetHudVisible(bool visible)
        {
            hudVisible = visible;
            RefreshInteractionCue();
        }

        public void SetInteractionPromptVisible(bool visible)
        {
            interactionPromptVisible = visible;
            if (!visible)
            {
                promptView?.Hide();
            }
        }

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private InteractionConfigSO interactionConfig;

        [SerializeField]
        private Transform holdPoint;

        [Header("3인칭 소지 물건 위치 — 모델(스케일 1) 기준 미터. Visual 스케일을 곱해 실제 위치가 된다")]
        [SerializeField, Min(0f), Tooltip("몸에서 앞으로. 머리 위에 드는 모션이라 거의 0. (0.65 스케일에서 0.16 m)")]
        private float holdForwardOffset = 0.25f;

        [SerializeField, Min(0f), Tooltip("서기 높이. 정수리(모델 1.96) 위 손 사이. (0.65 스케일에서 1.46 m)")]
        private float holdHeightStanding = 2.25f;

        [SerializeField, Min(0f), Tooltip("앉기 높이. 0.82 × 0.55 = 0.45 m(원래 값)")]
        private float holdHeightCrouching = 0.82f;

        [SerializeField, Min(0f), Tooltip("엎드리기 높이. 0.36 × 0.55 = 0.2 m(원래 값)")]
        private float holdHeightProne = 0.36f;

        [SerializeField, Tooltip("좌우 치우침 (+ 오른쪽)")]
        private float holdSideOffset = 0f;

        [SerializeField, Min(0f), Tooltip("머리 본에서 위로(머리 본의 위 방향). 정수리(머리 본 위 0.68)보다 조금 높게. 모델 단위, Visual 스케일을 곱한다")]
        private float holdAboveHeadOffset = 0.8f;

        private Transform headBone;
        private bool searchedHeadBone;

        private const float ReferenceVisualScale = 0.55f;
        private Transform visualForScale;

        public CarryableItem CarriedItem { get; private set; }

        public bool IsCarrying => CarriedItem != null;

        public Transform HoldPoint => holdPoint;

        private Transform firstPersonCamera;
        private Vector3 firstPersonHoldOffset;
        private Quaternion firstPersonHoldTilt = Quaternion.identity;
        private bool firstPersonHoldActive;
        private float firstPersonMaxScreenFraction;
        private CarryableItem measuredItem;
        private float measuredItemSize;

        /// <summary>
        /// 1인칭 동안 손 위치를 카메라 기준으로 둔다. 카메라 컨트롤러가 자기 위치를 정한 직후 매 프레임 부른다.
        /// 다른 클라이언트는 각자 자기 HoldPoint에 물건을 붙이므로 내 화면에만 영향이 있다.
        /// </summary>
        /// <param name="camera">1인칭 카메라(리그) 트랜스폼</param>
        /// <param name="offset">카메라 기준 위치(m): x=오른쪽, y=위, z=앞</param>
        /// <param name="tiltEuler">카메라 기준 물건 기울기(도)</param>
        /// <param name="maxScreenFraction">물건이 화면 높이에서 차지할 최대 비율. 넘으면 그만큼 앞으로 민다. 0이면 끔</param>
        public void SetFirstPersonHold(Transform camera, Vector3 offset, Vector3 tiltEuler, float maxScreenFraction = 0f)
        {
            firstPersonCamera = camera;
            firstPersonHoldOffset = offset;
            firstPersonHoldTilt = Quaternion.Euler(tiltEuler);
            firstPersonMaxScreenFraction = maxScreenFraction;
            firstPersonHoldActive = camera != null;
            RefreshHoldPoint();
        }

        private bool TryGetHeadBone(out Transform head)
        {
            if (!searchedHeadBone || headBone == null)
            {
                searchedHeadBone = true;
                headBone = null;
                var visual = transform.Find("Visual");
                if (visual != null)
                {
                    foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name != "Head") continue;
                        headBone = t;
                        break;
                    }
                }
            }

            head = headBone;
            return head != null;
        }


        /// <summary>
        /// Visual의 실제 스케일. 캐릭터 크기를 바꿔도 물건이 머리 위에 남도록 손 위치 값에 곱한다.
        /// 원래 값(0.55 스케일에서 1.05 m 등)은 필드 툴팁에 남겨 두었다.
        /// </summary>
        private float VisualScale()
        {
            if (visualForScale == null) visualForScale = transform.Find("Visual");
            return visualForScale != null ? visualForScale.localScale.y : ReferenceVisualScale;
        }

        /// <summary>큰 물건을 멀리 밀 수 있는 최대 배율. 이 이상 밀면 손에 든 게 아니라 바닥에 놓인 것처럼 보인다.</summary>
        private const float FirstPersonMaxPushFactor = 1.6f;

        /// <summary>물건 윗면을 크로스헤어보다 이만큼 아래(화면 반높이 대비 비율)에 둔다.</summary>
        private const float FirstPersonTopMargin = 0.12f;

        /// <summary>
        /// 큰 물건이 시야를 가리지 않게 한다. 거리는 조금만 늘리고(최대 1.6배), 그래도 크면 물건을 아래로 내려
        /// 윗면이 크로스헤어 아래에 오게 한다 — 큰 화분은 화면 아래쪽에 윗부분만 보이고, 작은 물건은 그대로다.
        /// </summary>
        private Vector3 FitFirstPersonOffset(Vector3 offset)
        {
            var item = CarriedItem;
            if (item == null || firstPersonMaxScreenFraction <= 0f || offset.z <= 0.01f) return offset;

            if (!ReferenceEquals(item, measuredItem))
            {
                measuredItem = item;
                measuredItemSize = MeasureItemSize(item);
            }

            var cam = Camera.main;
            var fov = cam != null ? cam.fieldOfView : 60f;
            var tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var neededDistance = measuredItemSize / (2f * tanHalf * firstPersonMaxScreenFraction);
            var distance = Mathf.Clamp(neededDistance, offset.z, offset.z * FirstPersonMaxPushFactor);
            var ratio = distance / offset.z;
            var fitted = new Vector3(offset.x * ratio, offset.y * ratio, distance);

            // 윗면이 크로스헤어 아래에 오도록 내린다(작은 물건은 원래 값이 더 낮아 그대로).
            var halfSize = measuredItemSize * 0.5f;
            var topBelowCenter = -(halfSize + FirstPersonTopMargin * distance * tanHalf);
            fitted.y = Mathf.Min(fitted.y, topBelowCenter);
            return fitted;
        }


        private static float MeasureItemSize(CarryableItem item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        /// <summary>3인칭으로 돌아오면 손 위치를 다시 몸 기준으로 둔다.</summary>
        public void ClearFirstPersonHold()
        {
            if (!firstPersonHoldActive) return;
            firstPersonHoldActive = false;
            firstPersonCamera = null;
            if (holdPoint != null) holdPoint.localRotation = Quaternion.identity;
        }

        public bool UsesAuthoritativeCommands => commands != null;

        /// <summary>배치 모드 등 좌클릭을 다른 용도로 쓰는 동안 던지기를 막는다.</summary>
        public bool IsThrowSuppressed { get; set; }

        /// <summary>배치 모드에서 물건이 다른 물건·지형과 겹쳐 놓을 수 없는 동안 F 놓기를 막는다.</summary>
        public bool IsDropSuppressed { get; set; }

        /// <summary>기절 등 외부에서 상호작용 입력을 잠글 때 사용한다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>기절 등 외부 요인으로 들고 있던 물건을 강제로 떨어뜨린다.</summary>
        public void DropCarriedItem()
        {
            CancelThrowAim();
            DropCarried();
        }

        /// <summary>
        /// 배치 모드 확정: 물건이 이미 조준선 위의 빈 자리에 있으므로, 벽 뒤 보정 없이 지금 자세 그대로 놓는다(권위가 있으면 놓기 요청).
        /// </summary>
        public void PlaceCarriedItem()
        {
            CancelThrowAim();
            DropCarried(adjustForWalls: false);
        }

        /// <summary>배치 확정 등 외부 시스템이 소지 물건을 가져갈 때 사용한다.</summary>
        public CarryableItem ReleaseCarriedItem()
        {
            var released = CarriedItem;
            CarriedItem = null;
            CancelThrowAim();
            ClearItemCues(released);
            return released;
        }

        private InputActionMap playerMap;
        private InputAction interactAction;
        private InputAction attackAction;
        private Transform cameraTransform;
        private Component aimedTarget;
        private CarryableItem highlightedItem;
        private InteractionPromptView promptView;
        private ItemPlacementController placementController;
        private PlayerMovement playerMovement;
        private bool isAimingThrow;
        private IPlayerInteractionCommands commands;
        private readonly RaycastHit[] aimHits = new RaycastHit[MaxAimHits];
        private CarryableItem putDownPlayedFor;
        private CarryableItem throwPlayedFor;

        public void BindCommands(IPlayerInteractionCommands interactionCommands)
        {
            commands = interactionCommands;
        }

        private void Awake()
        {
            if (inputActions == null || interactionConfig == null)
            {
                Debug.LogError("PlayerInteractor: InputActions 또는 InteractionConfig가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
            attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);

            if (holdPoint == null)
            {
                var holdPointObject = new GameObject("HoldPoint");
                holdPoint = holdPointObject.transform;
                holdPoint.SetParent(transform, false);
                holdPoint.localPosition = new Vector3(0f, 1.3f, 0.7f);
            }

            playerMovement = GetComponent<PlayerMovement>();
            placementController = GetComponent<ItemPlacementController>();
        }

        private void LateUpdate() => RefreshHoldPoint();

        public void RefreshHoldPoint()
        {
            if (firstPersonHoldActive)
            {
                if (firstPersonCamera == null)
                {
                    ClearFirstPersonHold();
                    return;
                }

                // 1인칭: 시선을 따라 화면의 같은 자리에 보이도록 카메라 기준으로 즉시 놓는다(지연 없음).
                holdPoint.SetPositionAndRotation(
                    firstPersonCamera.TransformPoint(FitFirstPersonOffset(firstPersonHoldOffset)),
                    firstPersonCamera.rotation * firstPersonHoldTilt);
                return;
            }

            // 3인칭(및 다른 플레이어): 머리 본 기준 정수리 위. 머리 본은 애니메이션(서기·앉기·엎드리기·걷기)에
            // 따라 움직이므로 물건이 항상 머리 위를 따라간다. Animator가 본을 쓴 뒤(LateUpdate)라 이 프레임 값이다.
            if (TryGetHeadBone(out var head))
            {
                var scale = VisualScale();
                var above = head.position + head.up * (holdAboveHeadOffset * scale);
                var forward = transform.forward;
                forward.y = 0f;
                var target = above + forward.normalized * (holdForwardOffset * scale)
                             + transform.right * (holdSideOffset * scale);
                holdPoint.SetPositionAndRotation(target, transform.rotation);
                return;
            }

            // 머리 본이 없는 모델: 자세별 고정 높이. 카메라 눈높이에는 묶지 않는다.
            if (playerMovement != null)
            {
                var scale = VisualScale();
                var height = playerMovement.Posture switch
                {
                    PlayerPosture.Crouching => holdHeightCrouching,
                    PlayerPosture.Prone => holdHeightProne,
                    _ => holdHeightStanding,
                };
                var target = new Vector3(holdSideOffset * scale, height * scale, holdForwardOffset * scale);
                holdPoint.localPosition = Vector3.Lerp(holdPoint.localPosition, target, 10f * Time.deltaTime);
            }
        }

        private void OnEnable()
        {
            playerMap?.Enable();
        }

        private void OnDisable()
        {
            ClearInteractionCue();
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
            UpdateAim();

            // 커서가 풀린 상태(메뉴 조작 등)의 클릭만 게임 입력에서 제외한다.
            if (!Game.Client.Common.WebPointerInput.IsLocked || IsInputLocked)
            {
                CancelThrowAim();
                return;
            }

            if (interactAction.WasPressedThisFrame())
            {
                var aimedInteractable = aimedTarget as IInteractable;
                if (CarriedItem != null)
                {
                    CancelThrowAim();
                    if (aimedTarget != null &&
                        !(aimedTarget is CarryableItem) &&
                        aimedInteractable.CanInteract(this))
                    {
                        aimedInteractable.Interact(this);
                    }
                    else if (!IsDropSuppressed)
                    {
                        DropCarried();
                    }
                }
                else if (aimedInteractable != null && aimedInteractable.CanInteract(this))
                {
                    aimedInteractable.Interact(this);
                }
            }

            HandleThrowInput();
        }

        // 좌클릭을 누르고 있다가 놓는 순간 던진다. (누르는 동안 조준을 다듬을 수 있다)
        // 빈손 좌클릭(공격)은 전투 시스템에서 처리한다.
        private void HandleThrowInput()
        {
            if (CarriedItem == null || IsThrowSuppressed)
            {
                isAimingThrow = false;
                return;
            }

            if (attackAction.WasPressedThisFrame())
            {
                isAimingThrow = true;
            }

            if (isAimingThrow && attackAction.WasReleasedThisFrame())
            {
                isAimingThrow = false;
                ThrowCarried();
            }
        }

        private void CancelThrowAim()
        {
            isAimingThrow = false;
        }

        /// <summary>크로스헤어가 맞는 곳이 없을 때 조준점으로 삼는 앞 거리(m).</summary>
        private const float ThrowAimFallbackDistance = 12f;
        private readonly RaycastHit[] throwAimHits = new RaycastHit[8];

        /// <summary>던질 때 물건을 놓는 자리: 머리 본 위(3인칭 들기 위치). 머리 본이 없으면 현재 위치 그대로.</summary>
        private Vector3 GetThrowReleasePosition(Vector3 fallback)
        {
            if (!TryGetHeadBone(out var head)) return fallback;
            var scale = VisualScale();
            var forward = transform.forward;
            forward.y = 0f;
            return head.position + head.up * (holdAboveHeadOffset * scale)
                   + forward.normalized * (holdForwardOffset * scale)
                   + transform.right * (holdSideOffset * scale);
        }

        /// <summary>
        /// 머리 위 놓는 자리에서 크로스헤어가 가리키는 지점을 **통과하도록** 던진다(탄도 계산).
        /// 단 출발점보다 위로 솟지는 않는다 — 머리가 최고점. 속도로 닿을 수 없는 먼 조준점이면 수평으로 최대한 멀리.
        /// 조준점이 머리보다 높으면(천장 등) 그쪽으로 곧장 던진다. 속도 크기는 서버 상한(ThrowSpeed) 이하.
        /// </summary>
        private Vector3 GetThrowVelocity(Vector3 releasePosition)
        {
            var speed = Mathf.Max(0.1f, interactionConfig.ThrowSpeed);
            var toTarget = FindThrowAimPoint() - releasePosition;
            var flat = new Vector3(toTarget.x, 0f, toTarget.z);
            var distance = flat.magnitude;
            var height = toTarget.y;

            if (distance < 0.05f || height > 0f)
            {
                // 바로 앞/아래를 겨누거나 머리보다 높은 곳을 겨누면 곧장 그쪽으로.
                var direct = toTarget.sqrMagnitude < 0.0001f ? cameraTransform.forward : toTarget;
                return direct.normalized * speed;
            }

            // 낮은 궤적 해: tanθ = (v² − √(v⁴ − g(g·d² + 2·h·v²))) / (g·d)
            var g = Physics.gravity.magnitude;
            var v2 = speed * speed;
            var discriminant = v2 * v2 - g * (g * distance * distance + 2f * height * v2);
            var angle = 0f;
            if (discriminant >= 0f)
            {
                angle = Mathf.Atan((v2 - Mathf.Sqrt(discriminant)) / (g * distance));
            }

            // 출발점(머리) 위로는 솟지 않는다. 닿을 수 없으면 수평으로 던져 최대한 멀리 보낸다.
            angle = Mathf.Min(angle, 0f);
            var direction = flat.normalized * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            return direction * speed;
        }

        private Vector3 FindThrowAimPoint()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            var count = Physics.RaycastNonAlloc(ray, throwAimHits, ThrowAimFallbackDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearest = float.MaxValue;
            var point = ray.GetPoint(ThrowAimFallbackDistance);
            for (var i = 0; i < count; i++)
            {
                var hit = throwAimHits[i];
                if (hit.distance >= nearest || hit.transform.IsChildOf(transform)) continue;
                if (CarriedItem != null && hit.transform.IsChildOf(CarriedItem.transform)) continue;
                nearest = hit.distance;
                point = hit.point;
            }

            return point;
        }

        private void ThrowCarried()
        {
            if (CarriedItem == null || cameraTransform == null)
            {
                return;
            }

            var thrown = CarriedItem;
            // 1인칭·3인칭 모두 머리 위(3인칭 들기 위치)에서 던진다. 머리가 궤적의 최고점이 되고,
            // 1인칭에서 손(눈 아래) 위치에서 던져 낮게 출발하던 것도 같아진다.
            thrown.transform.position = GetThrowReleasePosition(thrown.transform.position);
            EnsureSafeReleasePosition(thrown);
            var velocity = GetThrowVelocity(thrown.transform.position);

            if (commands != null)
            {
                commands.RequestThrow(
                    new Pose(thrown.transform.position, thrown.transform.rotation),
                    velocity);
                return;
            }

            PlayConfirmedThrow(thrown);
            CarriedItem = null;
            thrown.OnThrown(velocity);
        }

        public bool TryPickUp(CarryableItem item)
        {
            if (CarriedItem != null || item == null || item.IsCarried)
            {
                return false;
            }

            if (commands != null)
            {
                return commands.RequestHold(item.ObjectId);
            }

            CarriedItem = item;
            item.OnPickedUp(holdPoint);
            ClearItemCues(item);
            GetComponent<PlayerAnimationDriver>()?.PlayPickup();
            return true;
        }

        public bool TryPlaceCarried(Vector3 position, Quaternion rotation)
        {
            if (CarriedItem == null)
            {
                return false;
            }

            PlayPutDownCue(CarriedItem);
            if (commands != null)
            {
                return commands.RequestRelease(new Pose(position, rotation));
            }

            var item = ReleaseCarriedItem();
            item.OnPlaced(position, rotation);
            return true;
        }

        public bool TryRequestHit(int targetPlayerIndex)
        {
            return commands != null && commands.RequestHit(targetPlayerIndex);
        }

        public bool TryUseAuthoritativeShredder()
        {
            if (commands == null)
            {
                return false;
            }

            commands.RequestUseShredder();
            return true;
        }

        public bool ApplyConfirmedPickup(CarryableItem item)
        {
            if (item == null || (CarriedItem != null && CarriedItem != item))
            {
                return false;
            }

            var alreadyHeld = CarriedItem == item;
            CarriedItem = item;
            item.OnPickedUp(holdPoint);
            if (!alreadyHeld)
            {
                ClearItemCues(item);
                GetComponent<PlayerAnimationDriver>()?.PlayPickup();
            }

            return true;
        }

        public void ApplyConfirmedRelease(
            CarryableItem item,
            Pose pose,
            Vector3 initialVelocity)
        {
            if (item == null)
            {
                return;
            }

            if (CarriedItem == item)
            {
                CarriedItem = null;
            }

            item.OnReleased(pose, initialVelocity);
            ClearItemCues(item);
        }

        public void ForgetConfirmedItem(CarryableItem item)
        {
            if (CarriedItem == item)
            {
                CarriedItem = null;
            }
        }

        /// <summary>
        /// Confirmed throw from replicated object state. Skips if this player
        /// is not holding the item, or the throw cue already played for it.
        /// </summary>
        public void PlayConfirmedThrow(CarryableItem item)
        {
            if (item == null || CarriedItem != item)
            {
                return;
            }

            if (!ShouldPlayItemCue(throwPlayedFor, item))
            {
                return;
            }

            throwPlayedFor = item;
            GetComponent<PlayerAnimationDriver>()?.PlayThrow();
        }

        /// <summary>
        /// True when this item has not already used this cue during the current hold.
        /// </summary>
        internal static bool ShouldPlayItemCue(CarryableItem alreadyPlayedFor, CarryableItem item) =>
            item != null && alreadyPlayedFor != item;

        private void PlayPutDownCue(CarryableItem item)
        {
            if (!ShouldPlayItemCue(putDownPlayedFor, item))
            {
                return;
            }

            putDownPlayedFor = item;
            GetComponent<PlayerAnimationDriver>()?.PlayPutDown();
        }

        private void ClearItemCues(CarryableItem item)
        {
            if (item == null)
            {
                return;
            }

            if (putDownPlayedFor == item)
            {
                putDownPlayedFor = null;
            }

            if (throwPlayedFor == item)
            {
                throwPlayedFor = null;
            }
        }

        private void DropCarried(bool adjustForWalls = true)
        {
            if (CarriedItem == null)
            {
                return;
            }

            var dropped = CarriedItem;
            if (adjustForWalls) EnsureSafeReleasePosition(dropped);

            PlayPutDownCue(dropped);
            if (commands != null)
            {
                commands.RequestDrop(
                    new Pose(dropped.transform.position, dropped.transform.rotation));
                return;
            }

            CarriedItem = null;
            dropped.OnDropped();
            ClearItemCues(dropped);
        }

        // 벽에 붙어 놓거나 던질 때 손 위치가 벽 너머라면 시작점을 벽 앞으로 당긴다.
        private void EnsureSafeReleasePosition(CarryableItem item)
        {
            var chestHeight = playerMovement != null
                ? Mathf.Max(0.4f, playerMovement.CurrentEyeHeight - 0.2f)
                : 1.3f;
            var chest = transform.position + Vector3.up * chestHeight;
            var toHold = item.transform.position - chest;
            if (toHold.sqrMagnitude > 0.0001f
                && Physics.Raycast(chest, toHold.normalized, out var blocked, toHold.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                item.transform.position = blocked.point - toHold.normalized * 0.15f;
            }
        }

        // 임시 크로스헤어: HUD 파트에서 정식 크로스헤어가 나오기 전까지 화면 중앙을 표시한다.
        private void OnGUI()
        {
            if (!CanShowCrosshair(
                    HudVisible,
                    Cursor.lockState == CursorLockMode.Locked,
                    Cursor.visible))
            {
                return;
            }

            DrawCrosshair(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static Texture2D crosshairCircle;

        private static void DrawCrosshair(float centerX, float centerY)
        {
            var previous = GUI.color;
            var outline = CrosshairOutlineThickness;
            DrawCrosshairMark(
                centerX,
                centerY,
                CrosshairSize + outline * 2f,
                Mathf.Max(0f, CrosshairGap - outline * 2f),
                CrosshairThickness + outline * 2f,
                CrosshairDotSize + outline * 2f,
                Color.black);
            DrawCrosshairMark(
                centerX,
                centerY,
                CrosshairSize,
                CrosshairGap,
                CrosshairThickness,
                CrosshairDotSize,
                CrosshairFill);
            GUI.color = previous;
        }

        private static void DrawCrosshairMark(
            float centerX,
            float centerY,
            float size,
            float gap,
            float thickness,
            float dotSize,
            Color color)
        {
            GUI.color = color;
            var outer = size * 0.5f;
            var inner = gap * 0.5f;
            DrawVerticalBar(centerX, centerY - outer, centerY - inner, thickness);
            DrawVerticalBar(centerX, centerY + inner, centerY + outer, thickness);
            DrawHorizontalBar(centerY, centerX - outer, centerX - inner, thickness);
            DrawHorizontalBar(centerY, centerX + inner, centerX + outer, thickness);
            DrawCircle(centerX, centerY, dotSize);
        }

        private static void DrawVerticalBar(float x, float y0, float y1, float thickness)
        {
            var top = Mathf.Min(y0, y1);
            var height = Mathf.Abs(y1 - y0);
            if (height <= 0f || thickness <= 0f)
            {
                return;
            }

            var half = thickness * 0.5f;
            DrawCircle(x, top + half, thickness);
            DrawCircle(x, top + height - half, thickness);
            if (height > thickness)
            {
                GUI.DrawTexture(
                    new Rect(x - half, top + half, thickness, height - thickness),
                    Texture2D.whiteTexture);
            }
        }

        private static void DrawHorizontalBar(float y, float x0, float x1, float thickness)
        {
            var left = Mathf.Min(x0, x1);
            var width = Mathf.Abs(x1 - x0);
            if (width <= 0f || thickness <= 0f)
            {
                return;
            }

            var half = thickness * 0.5f;
            DrawCircle(left + half, y, thickness);
            DrawCircle(left + width - half, y, thickness);
            if (width > thickness)
            {
                GUI.DrawTexture(
                    new Rect(left + half, y - half, width - thickness, thickness),
                    Texture2D.whiteTexture);
            }
        }

        private static void DrawCircle(float centerX, float centerY, float size)
        {
            if (size <= 0f)
            {
                return;
            }

            GUI.DrawTexture(
                new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size),
                CircleTexture());
        }

        private static Texture2D CircleTexture()
        {
            if (crosshairCircle != null)
            {
                return crosshairCircle;
            }

            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[resolution * resolution];
            var radius = (resolution - 1) * 0.5f;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var dx = x - radius;
                    var dy = y - radius;
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            crosshairCircle = texture;
            return crosshairCircle;
        }

        private void UpdateAim()
        {
            aimedTarget = FindAimedTarget();
            RefreshInteractionCue();
        }

        private void RefreshInteractionCue()
        {
            // Scene unload can destroy targets before the next aim update.
            // Unity's null check also detects destroyed native objects; type patterns do not.
            if (aimedTarget == null) aimedTarget = null;
            if (highlightedItem == null) highlightedItem = null;
            if (CarriedItem == null) CarriedItem = null;

            var nextHighlight = CanShowWorldPrompt(
                                    HudVisible,
                                    interactionPromptVisible,
                                    Game.Client.Common.WebPointerInput.IsLocked) &&
                                aimedTarget is CarryableItem item &&
                                CarriedItem == null &&
                                item.CanInteract(this)
                ? item
                : null;

            if (highlightedItem != nextHighlight)
            {
                if (highlightedItem != null) highlightedItem.SetAimed(false, 1f);
                nextHighlight?.SetAimed(true, interactionConfig.AimedHighlightIntensity);
                highlightedItem = nextHighlight;
            }

            if (!TryGetPrompt(out var key, out var action, out var follow, out var actionColor, out var worldAnchor))
            {
                promptView?.Hide();
                return;
            }

            PromptView.Show(key, action, follow, icon: null, actionColor, worldAnchor);
        }

        private bool TryGetPrompt(
            out string key,
            out string action,
            out Transform follow,
            out Color actionColor,
            out Vector3? worldAnchor)
        {
            key = null;
            action = null;
            follow = null;
            actionColor = Color.white;
            worldAnchor = null;

            if (!CanShowWorldPrompt(
                    HudVisible,
                    interactionPromptVisible,
                    Game.Client.Common.WebPointerInput.IsLocked) ||
                placementController is { IsPlacing: true } ||
                aimedTarget is not IInteractable interactable ||
                !interactable.CanInteract(this) ||
                (CarriedItem != null && aimedTarget is CarryableItem))
            {
                return false;
            }

            key = InteractKeyLabel();
            action = interactable.InteractionPrompt;
            follow = aimedTarget.transform;
            actionColor = interactable.InteractionPromptColor;
            if (interactable.TryGetInteractionPromptWorldPosition(out var promptWorld))
            {
                worldAnchor = promptWorld;
            }

            return true;
        }

        private InteractionPromptView PromptView =>
            promptView != null ? promptView : promptView = InteractionPromptView.Create();

        private void ClearInteractionCue()
        {
            aimedTarget = null;
            if (highlightedItem != null) highlightedItem.SetAimed(false, 1f);
            highlightedItem = null;
            promptView?.Hide();
        }

        private static void OnSharedSettingsChanged(ControlSettings _) => RefreshBoundPrompts();

        private static void RefreshBoundPrompts()
        {
            var interactors = UnityEngine.Object.FindObjectsByType<PlayerInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < interactors.Length; index++)
            {
                if (interactors[index] != null)
                {
                    interactors[index].RefreshInteractionCue();
                }
            }
        }

        private Component FindAimedTarget()
        {
            if (cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return null;
                }

                cameraTransform = mainCamera.transform;
            }

            // 3인칭에서는 카메라가 캐릭터 뒤에 있으므로, 광선 길이에 카메라-캐릭터 거리를 더하고
            // 실제 닿는 지점이 캐릭터로부터 상호작용 거리 안인지 다시 검사한다.
            var cameraToPlayer = Vector3.Distance(cameraTransform.position, transform.position);
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            var maxDistance = interactionConfig.InteractionDistance + cameraToPlayer;

            var hitCount = Physics.RaycastNonAlloc(
                ray, aimHits, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            Component nearestTarget = null;
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = aimHits[i];

                // 자기 자신(플레이어)은 조준 대상에서 제외한다.
                if (hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                // 물건이 아닌 벽/가구가 더 가까이 있으면 그 뒤의 물건은 조준할 수 없다.
                nearestDistance = hit.distance;
                var target = hit.collider.GetComponentInParent(typeof(IInteractable));

                var withinReach = target != null
                    && Vector3.Distance(hit.point, transform.position) <= interactionConfig.InteractionDistance;
                nearestTarget = withinReach ? target : null;
            }

            return nearestTarget;
        }
    }
}
