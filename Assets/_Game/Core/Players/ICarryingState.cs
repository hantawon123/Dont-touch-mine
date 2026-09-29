namespace Game.Core.Players
{
    /// <summary>
    /// 들고 있는지 여부. Network는 Client 타입을 참조하지 않고 이 계약만 본다.
    /// </summary>
    public interface ICarryingState
    {
        bool IsCarrying { get; }
    }
}
