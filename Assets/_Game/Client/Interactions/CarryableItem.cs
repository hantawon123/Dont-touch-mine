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

        public string InteractionPrompt => "물건 잡기";

        private Rigidbody cachedBody;
        private Rigidbody body => cachedBody != null ? cachedBody : cachedBody = GetComponent<Rigidbody>();
        private bool remoteDriven;
        private Pose remoteFrom, remoteTo;
        private float remoteProgress;

        public void OnNetworkPose(Pose pose)
        {
            var snap = !remoteDriven;
            remoteFrom = new Pose(body.position, body.rotation);
            remoteTo = pose;
            remoteProgress = 0f;
            remoteDriven = true;
            gameObject.SetActive(true);
            transform.SetParent(null, true);
            RestoreOwningScene();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            SetCollidersEnabled(true);
            IsCarried = false;
            if (snap)
            {
                body.position = pose.position;
                body.rotation = pose.rotation;
                remoteFrom = pose;
            }
        }

        private void FixedUpdate()
        {
            if (!remoteDriven || IsCarried || remoteProgress >= 1f) return;
            remoteProgress = Mathf.Min(1f, remoteProgress + Time.fixedDeltaTime / 0.1f);
            body.MovePosition(Vector3.Lerp(remoteFrom.position, remoteTo.position, remoteProgress));
            body.MoveRotation(Quaternion.Slerp(remoteFrom.rotation, remoteTo.rotation, remoteProgress));
        }

        public bool TryGetPhysicsPose(out Pose pose, out Vector3 velocity, out bool moving)
        {
            pose = new Pose(body.position, body.rotation);
            velocity = body.linearVelocity;
            moving = !body.IsSleeping();
            return gameObject.activeInHierarchy && !IsCarried && !body.isKinematic;
        }

        private void WakeNeighbours()
        {
            EnsurePlacementVolume();
            var center = transform.position + transform.rotation * placementCenterOffset;
            foreach (var hit in Physics.OverlapBox(center, placementHalfExtents + Vector3.one * 0.05f,
                         transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                var other = hit.attachedRigidbody;
                if (other != null && other != body && !other.isKinematic) other.WakeUp();
            }
        }

        private Collider[] colliders;
        private AssignedItemOutline assignedOutline;
        private InteractableFocusOutline focusOutline;
        private bool assignedHighlightVisible;
        private string resolvedObjectId;
        private Scene owningScene;
        private Vector3 placementCenterOffset;
        private Vector3 placementHalfExtents;

        private void Awake()
        {
            if (!owningScene.IsValid()) owningScene = gameObject.scene;
            _ = ObjectId;
            cachedBody = GetComponent<Rigidbody>();

            // 빠르게 던져진 작은 물체가 얇은 벽을 프레임 사이에 통과(터널링)하지 않도록
            // 이동 경로 전체를 검사하는 연속 충돌 감지를 사용한다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ApplyCarryableLayer();
        }

        /// <summary>
        /// 3인칭 카메라 장애물 회피가 Default만 보므로, 물건을 Carryable 레이어에 두면
        /// 드롭 직후 카메라가 물건에 붙어 확대되는 현상을 막는다.
        /// </summary>
        private void ApplyCarryableLayer()
        {
            var layer = LayerMask.NameToLayer("Carryable");
            if (layer < 0)
            {
                return;
            }

            SetLayerRecursively(transform, layer);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
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
            if (!owningScene.IsValid()) owningScene = gameObject.scene;
            WakeNeighbours();
            remoteDriven = false;
            gameObject.SetActive(true);
            IsCarried = true;
            SetAimed(false, 1f);

            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            SetCollidersEnabled(false);

            transform.SetParent(holdPoint, worldPositionStays: false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        public void OnDropped()
        {
            remoteDriven = false;
            transform.SetParent(null, worldPositionStays: true);
            RestoreOwningScene();

            SetCollidersEnabled(true);
            body.isKinematic = false;

            IsCarried = false;
        }

        public void OnStored(Pose pose)
        {
            remoteDriven = false;
            WakeNeighbours();
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
            remoteDriven = false;
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
            remoteDriven = false;
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

                var itemBounds = CaptureColliderBounds(itemCollider);
                if (!combined.HasValue)
                {
                    combined = itemBounds;
                    continue;
                }

                var bounds = combined.Value;
                bounds.Encapsulate(itemBounds);
                combined = bounds;
            }

            var captured = combined ?? new Bounds(Vector3.zero, Vector3.one * 0.04f);
            placementCenterOffset = captured.center;
            placementHalfExtents = new Vector3(
                Mathf.Max(captured.extents.x, 0.02f),
                Mathf.Max(captured.extents.y, 0.02f),
                Mathf.Max(captured.extents.z, 0.02f));
        }

        private Bounds CaptureColliderBounds(Collider itemCollider)
        {
            // Collider.bounds is empty while an assignment is inactive or held.
            // Read the shape itself, in the item's rotated frame but at world scale.
            var inverseRotation = Quaternion.Inverse(transform.rotation);
            var colliderTransform = itemCollider.transform;
            var scale = colliderTransform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (itemCollider is SphereCollider sphere)
            {
                var center = inverseRotation * (colliderTransform.TransformPoint(sphere.center) - transform.position);
                var radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                return new Bounds(center, Vector3.one * (radius * 2f));
            }

            if (itemCollider is CapsuleCollider capsule)
            {
                var center = inverseRotation * (colliderTransform.TransformPoint(capsule.center) - transform.position);
                var direction = capsule.direction;
                var radius = capsule.radius * Mathf.Max(scale[(direction + 1) % 3], scale[(direction + 2) % 3]);
                var halfSegment = Mathf.Max(0f, capsule.height * scale[direction] * .5f - radius);
                var axis = Vector3.zero;
                axis[direction] = 1f;
                axis = inverseRotation * colliderTransform.TransformDirection(axis);
                var extents = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)) * halfSegment + Vector3.one * radius;
                return new Bounds(center, extents * 2f);
            }

            var localBounds = itemCollider switch
            {
                BoxCollider box => new Bounds(box.center, box.size),
                MeshCollider mesh when mesh.sharedMesh != null => mesh.sharedMesh.bounds,
                _ => itemCollider.bounds
            };
            var localShape = itemCollider is BoxCollider || itemCollider is MeshCollider;
            Bounds? result = null;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                if (localShape) point = colliderTransform.TransformPoint(point);
                point = inverseRotation * (point - transform.position);
                if (result.HasValue)
                {
                    var bounds = result.Value;
                    bounds.Encapsulate(point);
                    result = bounds;
                }
                else result = new Bounds(point, Vector3.zero);
            }
            return result.Value;
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
            assignedHighlightVisible = visible;
            if (visible && assignedOutline == null)
            {
                assignedOutline = GetComponent<AssignedItemOutline>() ??
                                  gameObject.AddComponent<AssignedItemOutline>();
            }

            assignedOutline?.SetVisible(visible && focusOutline is not { IsVisible: true });
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

        /// <summary>조준 하이라이트: 집을 수 있는 물건에 주황 2px 테두리를 켠다.</summary>
        /// <remarks>
        /// 진열대처럼 옆 물건·선반 판에 딱 붙어 있어도 보이도록, 새로 붙이는 실루엣은 가려진 부분까지 그리는
        /// <see cref="InteractableFocusOutline.SeeThrough"/> 모드로 만든다. 프리팹에 미리 넣어 둔 실루엣 설정은 그대로 둔다.
        /// </remarks>
        public void SetAimed(bool aimed, float intensity)
        {
            _ = intensity;
            if (aimed)
            {
                if (focusOutline == null)
                {
                    focusOutline = GetComponent<InteractableFocusOutline>();
                    if (focusOutline == null)
                    {
                        focusOutline = gameObject.AddComponent<InteractableFocusOutline>();
                        focusOutline.SeeThrough = true;
                    }
                }

                focusOutline.SetVisible(true);
                assignedOutline?.SetVisible(false);
                return;
            }

            focusOutline?.SetVisible(false);
            assignedOutline?.SetVisible(assignedHighlightVisible);
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

            // Unity returns a new managed string for name; read it once per object,
            // not once per catalog entry for every prop in a large map.
            var itemName = name;
            foreach (var definition in ItemCatalog.Definitions)
            {
                if (itemName.StartsWith(definition.ItemId, StringComparison.Ordinal))
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
