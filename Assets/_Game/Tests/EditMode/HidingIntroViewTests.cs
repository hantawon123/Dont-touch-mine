using System.Collections;
using System.Linq;
using System.Reflection;
using Game.SOAP.Config;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HidingIntroViewTests
    {
        [UnityTest]
        public IEnumerator Preview_RepeatedShowAndHideKeepsRenderingOffscreen()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            var slot = new GameObject("Preview target", typeof(RectTransform), typeof(RawImage));
            var image = slot.GetComponent<RawImage>();
            var preview = new HidingIntroItemPreview(image);
            try
            {
                var catalog = Resources.Load<ItemCatalogSO>(ItemCatalogSO.ResourcePath);
                var item = catalog.categories.SelectMany(c => c.items).First(i => i.prefab != null);
                preview.Show(item.id);
                var texture = image.texture;
                Assert.That(texture, Is.Not.Null);
                preview.Clear();
                Assert.That(image.enabled, Is.False);
                preview.Show(item.id);
                preview.Show(item.id); // Assignment delivery can refresh an already visible intro.
                var camera = (Camera)typeof(HidingIntroItemPreview).GetField("camera",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
                Assert.That(image.texture, Is.SameAs(texture));
                Assert.That(camera.targetTexture, Is.SameAs(texture));
                Assert.That(camera.enabled, Is.False, "Only explicit preview rendering should run.");
                Assert.That(camera.pixelWidth, Is.EqualTo(texture.width));
                yield return null;
                yield return null;
                Assert.That(camera.targetTexture, Is.SameAs(texture));
            }
            finally { preview.Dispose(); Object.DestroyImmediate(slot); }
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void FormatMessage_UsesAssignedItemName()
        {
            Assert.That(
                HidingIntroView.FormatMessage("탄산음료"),
                Is.EqualTo("당신이 훔친 물건은 탄산음료입니다."));
        }

        [Test]
        public void FormatMessage_FallsBackWhenNameIsMissing()
        {
            Assert.That(
                HidingIntroView.FormatMessage("  "),
                Is.EqualTo("당신이 훔친 물건은 물건입니다."));
        }

        [Test]
        public void FormatRichMessage_HighlightsTheItemName()
        {
            Assert.That(
                HidingIntroView.FormatRichMessage("사과"),
                Is.EqualTo("당신이 훔친 물건은 <color=#F4A26B>사과</color>입니다."));
        }

        [Test]
        public void Show_FillsTheScreenAndWritesBothLines()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingIntroView.Create(canvas.transform);
                view.Show("햄버거");

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("Card"), Is.Null);
                Assert.That(view.transform.Find("Background"), Is.Not.Null);

                var background = view.transform.Find("Background").GetComponent<RectTransform>();
                Assert.That(background.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(background.anchorMax, Is.EqualTo(Vector2.one));

                var message = view.transform.Find("Content/Message")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(message, Is.Not.Null);
                Assert.That(message.text, Is.EqualTo(HidingIntroView.FormatRichMessage("햄버거")));

                var hint = view.transform.Find("Content/Hint")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.text, Is.EqualTo(HidingIntroView.HintText));
                var content = view.transform.Find("Content") as RectTransform;
                Assert.That(content, Is.Not.Null);
                Assert.That(content.anchoredPosition.y, Is.EqualTo(HidingIntroView.ContentAnchoredY));
                Assert.That(message.rectTransform.anchoredPosition.y, Is.EqualTo(HidingIntroView.MessageAnchoredY));
                Assert.That(hint.rectTransform.anchoredPosition.y, Is.EqualTo(HidingIntroView.HintAnchoredY));
                Assert.That(view.transform.Find("Content/ItemPreview"), Is.Null);
                var preview = view.transform.Find("ItemPreview")
                    ?.GetComponent<UnityEngine.UI.RawImage>();
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(
                    preview.rectTransform.anchoredPosition.y,
                    Is.EqualTo(HidingIntroItemPreview.IntroCenterOffsetY));
                Assert.That(preview.rectTransform.sizeDelta, Is.EqualTo(new Vector2(360f, 360f)));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
