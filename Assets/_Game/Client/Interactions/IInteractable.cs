namespace Game.Client.Interactions
{
    /// <summary>
    /// 크로스헤어로 조준하고 F키로 상호작용할 수 있는 모든 대상의 공통 규격.
    /// 물건 들기, 파쇄기 투입 등이 이 인터페이스를 구현한다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>HUD에 표시할 안내 문구. 예: "곰인형 들기 [F]"</summary>
        string InteractionPrompt { get; }

        bool CanInteract(PlayerInteractor interactor);

        void Interact(PlayerInteractor interactor);
    }
}
