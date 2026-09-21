using System.Linq;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// 결과 화면과 하이라이트가 손에 붙이는 <b>겉모습 복제본</b>을 검사한다 (S15P21D205-1087, 1097).
    /// </summary>
    public sealed class ItemDisplayCopyTests
    {
        /// <summary>
        /// 외곽선 껍데기가 복제본에 따라오면 안 된다.
        ///
        /// <para>
        /// 껍데기는 런타임에 만든 재질을 원본과 공유한다. 복제본은 원본이 파괴되기 직전에
        /// 만들어지므로, 원본이 사라지면 그 재질이 파괴되고 껍데기가 자홍색으로 그려진다.
        /// 껍데기가 물건보다 크기 때문에 물건 전체가 자홍색 덩어리로 보인다.
        /// </para>
        /// </summary>
        [Test]
        public void Create_LeavesNoOutlineShellBehind()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();
            var hand = new GameObject("hand");

            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                item.SetAssignedHighlight(true);
                Assert.That(
                    Generated(itemObject),
                    Is.GreaterThan(0),
                    "원본에 껍데기가 있어야 이 검사가 뜻이 있다.");

                var copy = ItemDisplayCopy.Create(itemObject, hand.transform, "item (Ending)");

                Assert.That(copy, Is.Not.Null);
                Assert.That(Generated(copy), Is.Zero, "복제본에는 껍데기가 없어야 한다.");
                // 물건의 제 모습은 그대로 남는다. 껍데기만 지우는 것이지 렌더러를 다 지우는 것이 아니다.
                Assert.That(
                    copy.GetComponentsInChildren<Renderer>(true).Count(r => r != null),
                    Is.GreaterThan(0),
                    "복제본은 물건 자신의 렌더러를 그대로 갖는다.");
                Assert.That(
                    copy.GetComponentsInChildren<Renderer>(true)
                        .Where(r => r != null)
                        .All(r => r.sharedMaterial != null),
                    Is.True,
                    "복제본의 렌더러는 모두 재질이 있어야 한다. 없으면 자홍색으로 그려진다.");
            }
            finally
            {
                Object.DestroyImmediate(hand);
                Object.DestroyImmediate(itemObject);
            }
        }

        /// <summary>복제본은 보여 주는 것만 한다. 스크립트·콜라이더·리지드바디는 지운다.</summary>
        [Test]
        public void Create_KeepsOnlyTheLook()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();
            var hand = new GameObject("hand");

            try
            {
                itemObject.AddComponent<CarryableItem>();
                var copy = ItemDisplayCopy.Create(itemObject, hand.transform, "item (Archived)");

                Assert.That(copy, Is.Not.Null);
                Assert.That(copy.transform.parent, Is.EqualTo(hand.transform));
                Assert.That(copy.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(copy.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                Object.DestroyImmediate(hand);
                Object.DestroyImmediate(itemObject);
            }
        }

        private static int Generated(GameObject target) =>
            target.GetComponentsInChildren<Renderer>(true)
                .Count(r => r != null && ItemOutlineRenderers.IsGenerated(r));
    }
}
