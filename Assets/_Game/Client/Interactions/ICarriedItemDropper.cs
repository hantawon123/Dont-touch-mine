namespace Game.Client.Interactions
{
    /// <summary>기절 등 외부 요인으로 들고 있던 물건을 강제로 떨어뜨릴 수 있는 대상.</summary>
    public interface ICarriedItemDropper
    {
        void DropCarriedItem();
    }
}
