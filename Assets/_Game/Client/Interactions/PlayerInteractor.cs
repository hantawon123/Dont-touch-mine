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

        /// <summary>
        /// 큰 물건은 화면을 가리므로, 화면 높이의 일정 비율을 넘는 만큼 카메라에서 멀리 민다.
        /// 옆·아래 오프셋도 같은 비율로 키워 화면상의 자리는 그대로 둔다.
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
            var visibleHeightPerMeter = 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var neededDistance = measuredItemSize / (visibleHeightPerMeter * firstPersonMaxScreenFraction);
            if (neededDistance <= offset.z) return offset;
            return offset * (neededDistance / offset.z);
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

        /// <summary>기절 등 외부에서 상호작용 입력을 잠글 때 사용한다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>기절 등 외부 요인으로 들고 있던 물건을 강제로 떨어뜨린다.</summary>
        public void DropCarriedItem()
        {
            CancelThrowAim();
            DropCarried();
        }

        /// <summary>배치 확정 등 외부 시스템이 소지 물건을 가져갈 때 사용한다.</summary>
        public CarryableItem ReleaseCarriedItem()
        {
            var released = CarriedItem;
            CarriedItem = null;
            CancelThrowAim();
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
                    else
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

        /// <summary>던질 때 물건을 눈높이 앞으로 옮기는 거리(m). 여기가 궤적의 최고점이 된다.</summary>
        private const float ThrowReleaseForward = 0.35f;

        /// <summary>
        /// 던지는 순간 물건을 눈높이(머리)에서 시선 방향으로 조금 앞에 옮긴다. 손은 시선보다 아래라
        /// 손에서 던지면 조준점과 어긋나므로, 눈에서 시선 그대로 나가게 한다. 정면을 보고 던지면 눈높이가
        /// 최고점이고 거기서 떨어지기만 하며, 위를 보고 던지면 그대로 위로 날아간다.
        /// 눈과 놓는 점 사이에 벽이 있으면 벽 앞에서 놓는다.
        /// </summary>
        private void MoveToThrowRelease(CarryableItem item)
        {
            var eyeHeight = playerMovement != null ? playerMovement.CurrentEyeHeight : 1.3f;
            var eye = transform.position + Vector3.up * eyeHeight;
            var forward = cameraTransform.forward;
            var release = eye + forward * ThrowReleaseForward;
            if (Physics.Raycast(eye, forward, out var blocked, ThrowReleaseForward,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                release = blocked.point - forward * 0.1f;
            }

            item.transform.position = release;
        }

        /// <summary>
        /// 시선(크로스헤어) 방향 그대로 던진다. 인위적인 위쪽 보정은 없다 — 중력만 작용하므로 정면 던지기는
        /// 놓는 점이 최고점이 되고, 천장을 보고 던지면 위로 날아간다. 속도 크기는 서버 상한(ThrowSpeed) 이하.
        /// </summary>
        private Vector3 GetThrowVelocity(Vector3 releasePosition)
        {
            var speed = Mathf.Max(0.1f, interactionConfig.ThrowSpeed);
            var direction = FindThrowAimPoint() - releasePosition;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = cameraTransform.forward;
            }

            return direction.normalized * speed;
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
            MoveToThrowRelease(thrown);
            var velocity = GetThrowVelocity(thrown.transform.position);

            if (commands != null)
            {
                commands.RequestThrow(
                    new Pose(thrown.transform.position, thrown.transform.rotation),
                    velocity);
                return;
            }

            GetComponent<PlayerAnimationDriver>()?.PlayThrow();
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
            GetComponent<PlayerAnimationDriver>()?.PlayPickup();
            return true;
        }

        public bool TryPlaceCarried(Vector3 position, Quaternion rotation)
        {
            if (CarriedItem == null)
            {
                return false;
            }

            if (commands != null)
            {
                GetComponent<PlayerAnimationDriver>()?.PlayPutDown();
                return commands.RequestRelease(new Pose(position, rotation));
            }

            GetComponent<PlayerAnimationDriver>()?.PlayPutDown();
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

            CarriedItem = item;
            item.OnPickedUp(holdPoint);
            GetComponent<PlayerAnimationDriver>()?.PlayPickup();
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
        }

        public void ForgetConfirmedItem(CarryableItem item)
        {
            if (CarriedItem == item)
            {
                CarriedItem = null;
            }
        }

        private void DropCarried()
        {
            if (CarriedItem == null)
            {
                return;
            }

            var dropped = CarriedItem;
            EnsureSafeReleasePosition(dropped);

            if (commands != null)
            {
                GetComponent<PlayerAnimationDriver>()?.PlayPutDown();
                commands.RequestDrop(
                    new Pose(dropped.transform.position, dropped.transform.rotation));
                return;
            }

            GetComponent<PlayerAnimationDriver>()?.PlayPutDown();
            CarriedItem = null;
            dropped.OnDropped();
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

            var center = new Rect(Screen.width * 0.5f - 4f, Screen.height * 0.5f - 12f, 20f, 20f);
            GUI.Label(center, aimedTarget != null ? "<color=yellow><b>+</b></color>" : "+",
                new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true });
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
