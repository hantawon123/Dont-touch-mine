namespace Game.Core.Bots
{
    /// <summary>
    /// 봇의 시야가 물건에게 묻는 한 가지 질문: "네 이름표 번호는 무엇인가".
    /// 물건 코드(Client)가 이 질문에 답하고, 봇 코드(BotRuntime)는 물건 코드를
    /// 직접 참조하지 않은 채 번호만 받는다. 위치나 Transform은 여기로 흘러가지 않는다.
    /// </summary>
    public interface IBotSightTarget
    {
        /// <summary>모든 피어에서 같은, 물건 하나를 가리키는 안정된 문자열 ID.</summary>
        string SightTargetId { get; }
    }
}
