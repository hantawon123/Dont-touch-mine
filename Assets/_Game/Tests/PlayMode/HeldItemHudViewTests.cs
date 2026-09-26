using System.Collections;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class HeldItemHudViewTests
    {
        [UnityTest]
        public IEnumerator PortraitFollowsCarryingAndPerspective_WithoutChangingTheProp()
        {
            var owner = new GameObject("Camera owner");
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var item = prop.AddComponent<CarryableItem>();
            var renderer = prop.GetComponent<Renderer>();
            renderer.forceRenderingOff = true; // First-person world visibility must not hide the portrait.
            var hud = new HeldItemHudView();
            try
            {
                hud.Apply(owner.transform, false, item);
                Assert.That(owner.transform.childCount, Is.Zero);
                hud.Apply(owner.transform, true, null);
                Assert.That(owner.transform.childCount, Is.Zero);

                hud.Apply(owner.transform, true, item);
                var root = owner.transform.Find("HeldItemHud");
                var circle = root.Find("Circle").GetComponent<RectTransform>();
                var image = circle.GetComponentInChildren<RawImage>();
                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(circle.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(circle.GetComponent<Mask>(), Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Assert.That(image.texture, Is.Not.Null);
                    Assert.That(image.enabled, Is.True);
                    var texture = image.texture;
                    hud.Apply(owner.transform, true, item);
                    Assert.That(image.texture, Is.SameAs(texture));
                }
                Assert.That(renderer.forceRenderingOff, Is.True);

                hud.Apply(owner.transform, false, item);
                Assert.That(root.gameObject.activeSelf, Is.False);
                Assert.That(image.texture, Is.Null);
                hud.Apply(owner.transform, true, item);
                Assert.That(root.gameObject.activeSelf, Is.True);
                hud.Apply(owner.transform, true, null);
                Assert.That(root.gameObject.activeSelf, Is.False);

                var settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
                settings.Apply(settings.Current.With(InterfaceOption.InGameUi, InterfaceCatalog.Off));
                hud.Apply(owner.transform, true, item, settings);
                Assert.That(root.gameObject.activeSelf, Is.False);
                settings.Apply(settings.Current.With(InterfaceOption.InGameUi, InterfaceCatalog.On));
                hud.Apply(owner.transform, true, item, settings);
                Assert.That(root.gameObject.activeSelf, Is.True);

                hud.Apply(owner.transform, true, item);
                Object.Destroy(prop);
                yield return null;
                hud.Apply(owner.transform, true, item);
                Assert.That(root.gameObject.activeSelf, Is.False);
            }
            finally
            {
                hud.Dispose();
                Object.Destroy(owner);
                if (prop != null) Object.Destroy(prop);
            }
            yield return null;
        }
    }
}
