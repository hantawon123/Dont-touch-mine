using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 전투 테스트용 더미가 물건을 들고 있게 하는 간이 소지 컴포넌트.
    /// 씬에 있는 CarryableItem을 연결하면 시작 시 손에 쥔다.
    /// </summary>
    public sealed class DummyItemHolder : MonoBehaviour, ICarriedItemDropper
    {
        [SerializeField]
        private CarryableItem startItem;

        private Transform holdPoint;
        private CarryableItem carriedItem;

        private void Start()
        {
            if (startItem == null)
            {
                return;
            }

            var holdPointObject = new GameObject("HoldPoint");
            holdPoint = holdPointObject.transform;
            holdPoint.SetParent(transform, false);
            holdPoint.localPosition = new Vector3(0f, 1.1f, 0.5f);

            carriedItem = startItem;
            carriedItem.OnPickedUp(holdPoint);
        }

        public void DropCarriedItem()
        {
            if (carriedItem == null)
            {
                return;
            }

            var dropped = carriedItem;
            carriedItem = null;
            dropped.OnDropped();
        }
    }
}
