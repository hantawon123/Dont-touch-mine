using System.Collections;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Settings;
using Game.Client.Common;
using Game.Client.Settings;
using NUnit.Framework;
using TMPro;
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
            var general = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore());
            using var locale = new UiLocale(general);
            try
            {
                hud.Apply(owner.transform, false, item);
                Assert.That(owner.transform.childCount, Is.Zero);
                hud.Apply(owner.transform, true, null);
                Assert.That(owner.transform.childCount, Is.Zero);

                hud.Apply(owner.transform, true, item);
                var root = owner.transform.Find("HeldItemHud");
                var card = root.Find("Card").GetComponent<RectTransform>();
                var image = card.GetComponentInChildren<RawImage>();
                var caption = card.Find("Caption").GetComponent<TMP_Text>();
                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(card.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(card.anchoredPosition.y, Is.EqualTo(MatchVitalsHudView.BottomPadding));
                Assert.That(card.anchoredPosition.x, Is.LessThan(-MatchVitalsHudView.PanelWidth * 0.5f));
                Assert.That(card.GetComponent<Mask>(), Is.Null);
                Assert.That(image.rectTransform.sizeDelta.y, Is.GreaterThan(DestroyedItemsHudView.SlotSize));
                Assert.That(caption.text, Is.EqualTo("들고 있는 물건"));
                general.Apply(new GeneralSettings("en"));
                hud.Apply(owner.transform, true, item);
                Assert.That(caption.text, Is.EqualTo("Held Item"));
                general.Apply(new GeneralSettings("ko"));
                hud.Apply(owner.transform, true, item);
                Assert.That(caption.text, Is.EqualTo("들고 있는 물건"));
                Assert.That(image.raycastTarget, Is.False);

                // At every shipped UI scale in a 16:9 viewport, the card clears
                // the existing Y guide's right edge (48 + 360) without moving it.
                foreach (var size in new[] { InterfaceCatalog.Small, InterfaceCatalog.Medium, InterfaceCatalog.Large })
                {
                    var logicalWidth = HudScreenScale.ScaledReference.x / InterfaceHudView.HudScale(size);
                    var cardLeft = logicalWidth * 0.5f + card.anchoredPosition.x - card.sizeDelta.x;
                    Assert.That(cardLeft, Is.GreaterThan(48f + 360f), size);
                }
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
