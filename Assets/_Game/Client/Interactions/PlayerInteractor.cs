using Game.Client.Players;
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
        bool RequestRelease(Pose pose);
        bool RequestThrow(Pose pose, Vector3 initialVelocity);
        bool RequestHit(int targetPlayerIndex);
        bool RequestUseShredder();
    }

    /// <summary>
    /// 플레이어의 상호작용 담당: 카메라 중앙(크로스헤어)으로 조준한 대상을 감지하고
    /// F키 입력을 대상에 전달한다. 입력 의도만 다루며, 상태 확정은 각 대상이 수행한다.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour, ICarriedItemDropper
    {
        private const int MaxAimHits = 8;

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

        public CarryableItem CarriedItem { get; private set; }

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
        private PlayerMovement playerMovement;
        private bool wasCursorLocked;
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
        }

        private void LateUpdate()
        {
            // 손 위치가 자세(서기/앉기/엎드리기)의 눈높이를 따라가게 한다.
            if (playerMovement != null)
            {
                var target = new Vector3(
                    0f,
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
            playerMap?.Disable();
        }

        private void Update()
        {
            UpdateAim();

            // 커서가 풀린 상태(메뉴 조작 등)의 클릭은 게임 입력으로 취급하지 않는다.
            // 재잠금 클릭과 같은 프레임에 던져지지 않도록, 직전 프레임부터 잠겨 있던 경우만 허용한다.
            var isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            var acceptInput = isCursorLocked && wasCursorLocked && !IsInputLocked;
            wasCursorLocked = isCursorLocked;

            if (!acceptInput)
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

            CarriedItem = item;
            item.OnPickedUp(holdPoint);
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
                commands.RequestRelease(
                    new Pose(dropped.transform.position, dropped.transform.rotation));
                return;
            }

            CarriedItem = null;
            dropped.OnDropped();
        }

        // 벽에 붙어 놓거나 던질 때 손 위치가 벽 너머라면 시작점을 벽 앞으로 당긴다.
        private void EnsureSafeReleasePosition(CarryableItem item)
        {
            var chest = transform.position + Vector3.up * 1.3f;
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
            var center = new Rect(Screen.width * 0.5f - 4f, Screen.height * 0.5f - 12f, 20f, 20f);
            GUI.Label(center, aimedTarget != null ? "<color=yellow><b>+</b></color>" : "+",
                new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true });
        }

        private void UpdateAim()
        {
            var newAimedTarget = FindAimedTarget();
            if (newAimedTarget == aimedTarget)
            {
                return;
            }

            if (aimedTarget is CarryableItem previousItem)
            {
                previousItem.SetAimed(false, 1f);
            }

            aimedTarget = newAimedTarget;

            if (aimedTarget is CarryableItem currentItem)
            {
                currentItem.SetAimed(true, interactionConfig.AimedHighlightIntensity);
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
