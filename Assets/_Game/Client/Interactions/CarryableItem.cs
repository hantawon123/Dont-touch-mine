using System;
using Game.Core.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 들고 다닐 수 있는 물건. 잡동사니(일반 물건)와 플레이어 고유 물건 모두 이 컴포넌트를 사용한다.
    /// 들리는 동안은 물리와 충돌을 끄고 플레이어의 HoldPoint에 붙는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarryableItem : MonoBehaviour, IInteractable
    {
        [SerializeField]
        private string displayName = "물건";

        [SerializeField]
        [Tooltip("네트워크에서 사용하는 고유 ID. 비우면 씬 계층에서 안정적으로 생성합니다.")]
        private string objectId;

        [SerializeField]
        private bool isPlayerItem;

        [SerializeField]
        private int ownerPlayerIndex = -1;

        public bool IsCarried { get; private set; }

        public string DisplayName => displayName;

        public string ObjectId => resolvedObjectId ??= ResolveObjectId();

        public bool HasExplicitObjectId => !string.IsNullOrWhiteSpace(objectId);

        public bool IsPlayerItem => isPlayerItem;

        public int OwnerPlayerIndex => isPlayerItem ? ownerPlayerIndex : -1;

        public Vector3 PlacementCenterOffset
        {
            get
            {
                EnsurePlacementVolume();
                return placementCenterOffset;
            }
        }

        public Vector3 PlacementHalfExtents
        {
            get
            {
                EnsurePlacementVolume();
                return placementHalfExtents;
            }
        }

        public string InteractionPrompt => $"{displayName} 들기 [F]";

        private Rigidbody body;
        private Collider[] colliders;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private AssignedItemOutline assignedOutline;
        private string resolvedObjectId;
        private Scene owningScene;
        private Vector3 placementCenterOffset;
        private Vector3 placementHalfExtents;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            owningScene = gameObject.scene;
            _ = ObjectId;
            body = GetComponent<Rigidbody>();

            // 빠르게 던져진 작은 물체가 얇은 벽을 프레임 사이에 통과(터널링)하지 않도록
            // 이동 경로 전체를 검사하는 연속 충돌 감지를 사용한다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return !IsCarried;
        }

        public void Interact(PlayerInteractor interactor)
        {
            interactor.TryPickUp(this);
        }

        // 아래 두 메서드는 로컬에서 즉시 상태를 확정한다.
        // Photon 도입 시 서버 확정 결과를 받아 호출하는 구조로 바뀐다.
        public void OnPickedUp(Transform holdPoint)
        {
            gameObject.SetActive(true);
            IsCarried = true;
            SetAimed(false, 1f);

            body.isKinematic = true;
            SetCollidersEnabled(false);

            transform.SetParent(holdPoint, worldPositionStays: false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void OnDropped()
        {
            transform.SetParent(null, worldPositionStays: true);
            RestoreOwningScene();

            SetCollidersEnabled(true);
            body.isKinematic = false;

            IsCarried = false;
        }

        public void OnStored(Pose pose)
        {
            transform.SetParent(null, worldPositionStays: true);
            RestoreOwningScene();
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            body.linearVelocity = default;
            body.angularVelocity = default;
            body.isKinematic = true;
            SetCollidersEnabled(false);
            IsCarried = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 정밀 배치 확정: 미리보기 위치·회전으로 옮긴 뒤 물리를 되살린다.
        /// 놓인 뒤에는 일반 물리 규칙을 따른다. (불안정한 자리면 자연스럽게 굴러떨어진다)
        /// </summary>
        public void OnPlaced(Vector3 position, Quaternion rotation)
        {
            gameObject.SetActive(true);
            transform.SetParent(null, worldPositionStays: true);
            RestoreOwningScene();
            transform.SetPositionAndRotation(position, rotation);

            SetCollidersEnabled(true);
            body.isKinematic = false;

            IsCarried = false;
        }

        /// <summary>
        /// 던지기: 놓기와 같지만 조준 방향으로 초기 속도를 준다.
        /// 기획서 규칙 — 던진 물건은 플레이어를 맞혀도 피해가 없다(난장판용).
        /// 전투 시스템은 IsThrown 여부와 무관하게 물건 충돌을 피해로 취급하지 않는다.
        /// </summary>
        public void OnThrown(Vector3 initialVelocity)
        {
            OnDropped();
            body.linearVelocity = initialVelocity;
        }

        public void OnReleased(Pose pose, Vector3 initialVelocity)
        {
            OnPlaced(pose.position, pose.rotation);
            body.angularVelocity = default;
            body.linearVelocity = initialVelocity;
            body.WakeUp();
        }

        public void OnSettled(Pose pose, bool keepDynamic)
        {
            transform.SetParent(null, worldPositionStays: true);
            RestoreOwningScene();
            transform.SetPositionAndRotation(pose.position, pose.rotation);

            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.linearVelocity = default;
            body.angularVelocity = default;
            IsCarried = false;

            if (keepDynamic)
            {
                body.Sleep();
            }
            else
            {
                body.isKinematic = true;
            }
        }

        private void RestoreOwningScene()
        {
            if (owningScene.IsValid() && owningScene.isLoaded &&
                gameObject.scene.handle != owningScene.handle)
            {
                SceneManager.MoveGameObjectToScene(gameObject, owningScene);
            }
        }

        private void CapturePlacementVolume()
        {
            colliders ??= GetComponentsInChildren<Collider>(includeInactive: true);
            Bounds? combined = null;
            foreach (var itemCollider in colliders)
            {
                if (itemCollider.isTrigger)
                {
                    continue;
                }

                if (!combined.HasValue)
                {
                    combined = itemCollider.bounds;
                    continue;
                }

                var bounds = combined.Value;
                bounds.Encapsulate(itemCollider.bounds);
                combined = bounds;
            }

            var captured = combined ?? new Bounds(transform.position, Vector3.one * 0.04f);
            placementCenterOffset = Quaternion.Inverse(transform.rotation) *
                                    (captured.center - transform.position);
            placementHalfExtents = new Vector3(
                Mathf.Max(captured.extents.x, 0.02f),
                Mathf.Max(captured.extents.y, 0.02f),
                Mathf.Max(captured.extents.z, 0.02f));
        }

        private void EnsurePlacementVolume()
        {
            if (placementHalfExtents.x <= 0f ||
                placementHalfExtents.y <= 0f ||
                placementHalfExtents.z <= 0f)
            {
                CapturePlacementVolume();
            }
        }

        public bool TryGetSettledPose(out Pose pose)
        {
            if (!IsCarried && !body.isKinematic && body.IsSleeping())
            {
                pose = new Pose(transform.position, transform.rotation);
                return true;
            }

            pose = default;
            return false;
        }

        public void AssignToPlayer(int playerIndex)
        {
            isPlayerItem = true;
            ownerPlayerIndex = playerIndex;
        }

        public void SetAssignedHighlight(bool visible)
        {
            if (visible && assignedOutline == null)
            {
                assignedOutline = GetComponent<AssignedItemOutline>() ??
                                  gameObject.AddComponent<AssignedItemOutline>();
            }

            assignedOutline?.SetVisible(visible);
        }

        /// <summary>
        /// 같은 카탈로그 물건이 씬에 여러 개 있을 때 추가 인스턴스에
        /// 씬 계층 기반의 결정적인 ID를 부여한다.
        /// </summary>
        public void UseSceneInstanceObjectId()
        {
            resolvedObjectId = ResolveSceneInstanceObjectId();
        }

        /// <summary>
        /// Runtime-created assignment copies need an id that is different from
        /// the original prop which remains in the map.
        /// </summary>
        public void UseObjectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Object id is required.", nameof(value));
            }

            objectId = value.Trim();
            resolvedObjectId = objectId;
        }

        /// <summary>조준 하이라이트: 밝기를 살짝 올려 조준 중임을 표시한다.</summary>
        public void SetAimed(bool aimed, float intensity)
        {
            renderers ??= GetComponentsInChildren<Renderer>();
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var itemRenderer in renderers)
            {
                var baseColor = itemRenderer.sharedMaterial != null
                    && itemRenderer.sharedMaterial.HasProperty(BaseColorId)
                    ? itemRenderer.sharedMaterial.GetColor(BaseColorId)
                    : Color.white;

                if (aimed)
                {
                    propertyBlock.SetColor(BaseColorId, baseColor * intensity);
                    itemRenderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    itemRenderer.SetPropertyBlock(null);
                }
            }
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            colliders ??= GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var itemCollider in colliders)
            {
                itemCollider.enabled = isEnabled;
            }
        }

        private string ResolveObjectId()
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                return objectId.Trim();
            }

            foreach (var definition in ItemCatalog.Definitions)
            {
                if (name.StartsWith(definition.ItemId, StringComparison.Ordinal))
                {
                    return definition.ItemId;
                }
            }

            return ResolveSceneInstanceObjectId();
        }

        private string ResolveSceneInstanceObjectId()
        {
            // Scene hierarchy is identical on every peer. The compact hash is
            // enough to identify scene props and keeps the replicated match
            // state inside Fusion's per-NetworkObject size limit.
            var hash = 2166136261u;
            var current = transform;
            while (current != null)
            {
                Hash(current.name, ref hash);
                hash = (hash ^ (uint)current.GetSiblingIndex()) * 16777619u;
                current = current.parent;
            }

            return $"{hash:X8}";
        }

        private static void Hash(string value, ref uint hash)
        {
            for (var index = 0; index < value.Length; index++)
            {
                hash = (hash ^ value[index]) * 16777619u;
            }
        }
    }
}
