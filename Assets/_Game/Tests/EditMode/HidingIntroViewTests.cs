using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HidingIntroViewTests
    {
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
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
