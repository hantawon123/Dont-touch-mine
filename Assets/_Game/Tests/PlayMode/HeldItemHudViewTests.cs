using System.Collections;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Core.Settings;
using Game.Client.Voice;
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
            GameObject hudRoot = null;
            try
            {
                hud.Apply(owner.transform, false, item);
                Assert.That(owner.transform.childCount, Is.Zero);
                hud.Apply(owner.transform, true, null);
                Assert.That(owner.transform.childCount, Is.Zero);

                hud.Apply(owner.transform, true, item);
                hudRoot = GameObject.Find("HeldItemHud");
                Assert.That(hudRoot, Is.Not.Null);
                var root = hudRoot.transform;
                Assert.That(root.parent, Is.Null, "Overlay must not inherit camera motion.");
                Assert.That(hudRoot.scene, Is.EqualTo(owner.scene));
                var card = root.Find("Card").GetComponent<RectTransform>();
                var image = card.GetComponentInChildren<RawImage>();
                var caption = card.Find("Caption").GetComponent<TMP_Text>();
                Assert.That(root.gameObject.activeSelf, Is.True);
                Assert.That(card.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(card.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(card.anchoredPosition.y, Is.EqualTo(HeldItemHudView.BottomPadding));
                Assert.That(card.anchoredPosition.x, Is.EqualTo(-VoiceView.CornerMarginRight));
                Assert.That(card.GetComponent<Mask>(), Is.Null);
                Assert.That(image.rectTransform.sizeDelta.y, Is.GreaterThan(DestroyedItemsHudView.SlotSize));
                Assert.That(caption.text, Is.EqualTo("들고 있는 물건"));
                Assert.That(caption.alignment, Is.EqualTo(TextAlignmentOptions.Midline));
                general.Apply(new GeneralSettings("en"));
                hud.Apply(owner.transform, true, item);
                Assert.That(caption.text, Is.EqualTo("Held Item"));
                general.Apply(new GeneralSettings("ko"));
                hud.Apply(owner.transform, true, item);
                Assert.That(caption.text, Is.EqualTo("들고 있는 물건"));
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

                // Check before any canvas rebuild too: a late camera update must not
                // move the card, even for part of a frame.
                yield return null;
                Canvas.ForceUpdateCanvases();
                var before = new Vector3[4];
                var after = new Vector3[4];
                card.GetWorldCorners(before);
                for (var step = 0; step < 3; step++)
                {
                    owner.transform.SetPositionAndRotation(
                        new Vector3(100f + step, 2f + step, -40f),
                        Quaternion.Euler(25f * step, 80f + step * 15f, 0f));
                    owner.transform.localScale = Vector3.one * (1f + step * 0.1f);
                    hud.Apply(owner.transform, true, item);
                    card.GetWorldCorners(after);
                    for (var corner = 0; corner < 4; corner++)
                        Assert.That(Vector3.Distance(before[corner], after[corner]), Is.LessThan(0.01f));
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    card.GetWorldCorners(after);
                    for (var corner = 0; corner < 4; corner++)
                        Assert.That(Vector3.Distance(before[corner], after[corner]), Is.LessThan(0.01f));
                }

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
            Assert.That(hudRoot == null, Is.True, "Disposal must remove the detached canvas.");
        }
    }
}
