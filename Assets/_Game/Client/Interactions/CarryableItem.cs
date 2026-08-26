using UnityEngine;

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

        public bool IsCarried { get; private set; }

        public string DisplayName => displayName;

        public string InteractionPrompt => $"{displayName} 들기 [F]";

        private Rigidbody body;
        private Collider[] colliders;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
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

        /// <summary>조준 하이라이트: 밝기를 살짝 올려 조준 중임을 표시한다.</summary>
        public void SetAimed(bool aimed, float intensity)
        {
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
            foreach (var itemCollider in colliders)
            {
                itemCollider.enabled = isEnabled;
            }
        }
    }
}
