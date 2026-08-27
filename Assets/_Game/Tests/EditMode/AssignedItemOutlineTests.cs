using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class AssignedItemOutlineTests
    {
        [Test]
        public void Carryable_ShowsOutlineOnlyWhileLocallyAssigned()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();

            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                item.SetAssignedHighlight(true);

                var outline = item.GetComponent<AssignedItemOutline>();
                Assert.That(outline, Is.Not.Null);
                Assert.That(outline.IsVisible, Is.True);

                item.SetAssignedHighlight(false);
                Assert.That(outline.IsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }
    }
}
