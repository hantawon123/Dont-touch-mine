using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 플레이어의 상호작용 담당: 카메라 중앙(크로스헤어)으로 조준한 대상을 감지하고
    /// F키 입력을 대상에 전달한다. 입력 의도만 다루며, 상태 확정은 각 대상이 수행한다.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaxAimHits = 8;

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private InteractionConfigSO interactionConfig;

        [SerializeField]
        private Transform holdPoint;

        public CarryableItem CarriedItem { get; private set; }

        public Transform HoldPoint => holdPoint;

        private InputActionMap playerMap;
        private InputAction interactAction;
        private Transform cameraTransform;
        private CarryableItem aimedItem;
        private readonly RaycastHit[] aimHits = new RaycastHit[MaxAimHits];

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

            if (holdPoint == null)
            {
                var holdPointObject = new GameObject("HoldPoint");
                holdPoint = holdPointObject.transform;
                holdPoint.SetParent(transform, false);
                holdPoint.localPosition = new Vector3(0f, 1.3f, 0.7f);
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

            if (interactAction.WasPressedThisFrame())
            {
                if (CarriedItem != null)
                {
                    DropCarried();
                }
                else if (aimedItem != null && aimedItem.CanInteract(this))
                {
                    aimedItem.Interact(this);
                }
            }
        }

        public bool TryPickUp(CarryableItem item)
        {
            if (CarriedItem != null || item == null || item.IsCarried)
            {
                return false;
            }

            // 로컬 즉시 확정. Photon 도입 시 서버 확정 응답을 받은 뒤 반영하도록 바뀐다.
            CarriedItem = item;
            item.OnPickedUp(holdPoint);
            return true;
        }

        private void DropCarried()
        {
            if (CarriedItem == null)
            {
                return;
            }

            var dropped = CarriedItem;
            CarriedItem = null;
            dropped.OnDropped();
        }

        private void UpdateAim()
        {
            var newAimedItem = FindAimedItem();
            if (newAimedItem == aimedItem)
            {
                return;
            }

            if (aimedItem != null)
            {
                aimedItem.SetAimed(false, 1f);
            }

            aimedItem = newAimedItem;

            if (aimedItem != null)
            {
                aimedItem.SetAimed(true, interactionConfig.AimedHighlightIntensity);
            }
        }

        private CarryableItem FindAimedItem()
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

            CarryableItem nearestItem = null;
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
                var item = hit.collider.GetComponentInParent<CarryableItem>();

                var withinReach = item != null
                    && Vector3.Distance(hit.point, transform.position) <= interactionConfig.InteractionDistance;
                nearestItem = withinReach ? item : null;
            }

            return nearestItem;
        }
    }
}
