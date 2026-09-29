using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 크로스헤어로 조준하고 F키로 상호작용할 수 있는 모든 대상의 공통 규격.
    /// 물건 들기, 파쇄기 투입 등이 이 인터페이스를 구현한다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>조준 시 키 아래에 표시할 동작 문구. 예: "물건 잡기", "파괴하기"</summary>
        string InteractionPrompt { get; }

        Color InteractionPromptColor => Color.white;

        bool TryGetInteractionPromptWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            return false;
        }

        bool CanInteract(PlayerInteractor interactor);

        void Interact(PlayerInteractor interactor);
    }
}
