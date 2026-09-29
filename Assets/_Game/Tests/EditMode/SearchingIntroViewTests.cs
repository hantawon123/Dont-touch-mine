using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class SearchingIntroViewTests
    {
        [Test]
        public void FormatHint_UsesAssignedItemNameAndParticle()
        {
            Assert.That(
                SearchingIntroView.FormatHint("사과"),
                Is.EqualTo("마지막 순간에 사과를 꼭 손에 쥐고 계세요!"));
            Assert.That(
                SearchingIntroView.FormatHint("파인애플"),
                Is.EqualTo("마지막 순간에 파인애플을 꼭 손에 쥐고 계세요!"));
        }

        [Test]
        public void FormatHint_FallsBackWhenNameIsMissing()
        {
            Assert.That(
                SearchingIntroView.FormatHint("  "),
                Is.EqualTo("마지막 순간에 물건을 꼭 손에 쥐고 계세요!"));
        }

        [Test]
        public void FormatRichHint_HighlightsTheItemName()
        {
            Assert.That(
                SearchingIntroView.FormatRichHint("사과"),
                Is.EqualTo("마지막 순간에 <color=#F4A26B>사과</color>를 꼭 손에 쥐고 계세요!"));
        }

        [Test]
        public void Show_FillsTheScreenAndWritesThreeLines()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = SearchingIntroView.Create(canvas.transform);
                view.Show("햄버거");

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("Background"), Is.Not.Null);

                var background = view.transform.Find("Background").GetComponent<RectTransform>();
                Assert.That(background.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(background.anchorMax, Is.EqualTo(Vector2.one));

                var title = view.transform.Find("Content/Title")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(title, Is.Not.Null);
                Assert.That(title.text, Is.EqualTo(SearchingIntroView.TitleText));
                Assert.That(title.fontSize, Is.EqualTo(SearchingIntroView.FontSize));

                var body = view.transform.Find("Content/Body")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(body, Is.Not.Null);
                Assert.That(body.text, Is.EqualTo(SearchingIntroView.BodyText));
                Assert.That(body.fontSize, Is.EqualTo(SearchingIntroView.FontSize));

                var hint = view.transform.Find("Content/Hint")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.text, Is.EqualTo(SearchingIntroView.FormatRichHint("햄버거")));
                Assert.That(hint.fontSize, Is.EqualTo(SearchingIntroView.FontSize));
                Assert.That(view.transform.Find("Content/ItemPreview"), Is.Null);
                Assert.That(view.transform.Find("ItemPreview"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
