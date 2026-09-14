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

        [SerializeField, Min(0f), Tooltip("소지 물건이 몸에서 앞으로 떨어진 거리")]
        private float holdForwardOffset = 0.45f;

        [SerializeField, Min(0f), Tooltip("소지 물건이 눈높이에서 아래로 내려간 거리")]
        private float holdHeightBelowEyes = 0.55f;

        [SerializeField, Tooltip("소지 물건의 좌우 치우침 (+ 오른쪽)")]
        private float holdSideOffset = 0f;

        public CarryableItem CarriedItem { get; private set; }

        public bool IsCarrying => CarriedItem != null;

        public Transform HoldPoint => holdPoint;

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
            // 손 위치가 자세(서기/앉기/엎드리기)의 눈높이를 따라가게 한다.
            if (playerMovement != null)
            {
                var target = new Vector3(
                    holdSideOffset,
                    Mathf.Max(0.2f, playerMovement.CurrentEyeHeight - holdHeightBelowEyes),
                    holdForwardOffset);
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
            if (Cursor.lockState != CursorLockMode.Locked || IsInputLocked)
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

        private Vector3 GetThrowVelocity()
        {
            var direction = (cameraTransform.forward + Vector3.up * interactionConfig.ThrowUpwardBias).normalized;
            return direction * interactionConfig.ThrowSpeed;
        }

        private void ThrowCarried()
        {
            if (CarriedItem == null || cameraTransform == null)
            {
                return;
            }

            var thrown = CarriedItem;
            EnsureSafeReleasePosition(thrown);
            var velocity = GetThrowVelocity();

            GetComponent<PlayerAnimationDriver>()?.PlayThrow();
            if (commands != null)
            {
                commands.RequestThrow(
                    new Pose(thrown.transform.position, thrown.transform.rotation),
                    velocity);
                return;
            }

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
                                    Cursor.lockState == CursorLockMode.Locked) &&
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
                    Cursor.lockState == CursorLockMode.Locked) ||
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
