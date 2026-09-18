using Game.Client.Cameras;
using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HeldItemVisibilityTests
    {
        private GameObject itemObject;
        private GameObject otherItemObject;
        private CarryableItem item;
        private CarryableItem otherItem;
        private Renderer[] itemRenderers;

        [SetUp]
        public void SetUp()
        {
            itemObject = BuildItem("Vase", out item);
            otherItemObject = BuildItem("Book", out otherItem);
            itemRenderers = itemObject.GetComponentsInChildren<Renderer>(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(itemObject);
            if (otherItemObject != null) Object.DestroyImmediate(otherItemObject);
        }

        /// <summary>루트 메시 하나와 비활성 자식 메시 하나를 가진 들 수 있는 물건.</summary>
        private static GameObject BuildItem(string name, out CarryableItem carryable)
        {
            var root = new GameObject(name, typeof(Rigidbody), typeof(MeshRenderer));
            var child = new GameObject("Outline", typeof(MeshRenderer));
            child.transform.SetParent(root.transform, false);
            child.SetActive(false);
            carryable = root.AddComponent<CarryableItem>();
            return root;
        }

        private static bool AllForceOff(Renderer[] renderers, bool expected)
        {
            foreach (var renderer in renderers)
                if (renderer.forceRenderingOff != expected) return false;
            return true;
        }

        [Test]
        public void FirstPerson_HidesEveryRendererOfCarriedItem_IncludingInactiveChildren()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);

            Assert.That(itemRenderers.Length, Is.EqualTo(2));
            Assert.That(AllForceOff(itemRenderers, true), Is.True);
            Assert.That(view.HiddenItem, Is.SameAs(item));
        }

        [Test]
        public void ThirdPerson_LeavesCarriedItemVisible()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: false);

            Assert.That(AllForceOff(itemRenderers, false), Is.True);
            Assert.That(view.HiddenItem, Is.Null);
        }

        [Test]
        public void SwitchingToThirdPerson_RestoresRenderers()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);
            view.Apply(item, hide: false);

            Assert.That(AllForceOff(itemRenderers, false), Is.True);
            Assert.That(view.HiddenItem, Is.Null);
        }

        [Test]
        public void DroppingItem_RestoresRenderersWhileStillFirstPerson()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);
            view.Apply(null, hide: true);

            Assert.That(AllForceOff(itemRenderers, false), Is.True);
        }

        [Test]
        public void SwitchingItem_RestoresPreviousAndHidesNew()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);
            view.Apply(otherItem, hide: true);

            Assert.That(AllForceOff(itemRenderers, false), Is.True);
            Assert.That(AllForceOff(otherItemObject.GetComponentsInChildren<Renderer>(true), true), Is.True);
        }

        [Test]
        public void Rescan_HidesRendererAddedAfterPickup()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);

            var late = new GameObject("FocusOutline", typeof(MeshRenderer));
            late.transform.SetParent(itemObject.transform, false);
            var lateRenderer = late.GetComponent<Renderer>();

            view.Apply(item, hide: true);
            Assert.That(lateRenderer.forceRenderingOff, Is.False, "재수집 전에는 새 렌더러를 모른다");

            view.Apply(item, hide: true, rescan: true);
            Assert.That(lateRenderer.forceRenderingOff, Is.True);

            view.Reveal();
            Assert.That(lateRenderer.forceRenderingOff, Is.False);
            Assert.That(AllForceOff(itemRenderers, false), Is.True);
        }

        [Test]
        public void ReHidesEveryFrame_WhenSomethingElseTurnedRenderingBackOn()
        {
            var view = new HeldItemVisibility();
            view.Apply(item, hide: true);
            itemRenderers[0].forceRenderingOff = false;

            view.Apply(item, hide: true);

            Assert.That(AllForceOff(itemRenderers, true), Is.True);
        }

        [Test]
        public void Reveal_AfterItemDestroyed_DoesNotThrow()
        {
            var view = new HeldItemVisibility();
            view.Apply(otherItem, hide: true);
            Object.DestroyImmediate(otherItemObject);
            otherItemObject = null;

            Assert.DoesNotThrow(() => view.Apply(null, hide: true));
            Assert.DoesNotThrow(view.Reveal);
            Assert.That(view.HiddenItem, Is.Null);
        }
    }
}
