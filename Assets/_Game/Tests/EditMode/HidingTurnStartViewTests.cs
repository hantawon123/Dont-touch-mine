using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HidingTurnStartViewTests
    {
        [Test]
        public void FormatTimer_PadsMinutesAndSeconds()
        {
            Assert.That(HidingTurnStartView.VisibleSeconds, Is.EqualTo(1f));
            Assert.That(HidingTurnStartView.FormatTimer(30d), Is.EqualTo("00:30"));
            Assert.That(HidingTurnStartView.FormatTimer(29.1d), Is.EqualTo("00:30"));
            Assert.That(HidingTurnStartView.FormatTimer(0d), Is.EqualTo("00:00"));
        }

        [Test]
        public void Show_PlacesStopwatchAndBannerOverTheWorld()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingTurnStartView.Create(canvas.transform);
                view.Show(30d);

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("Background"), Is.Null);
                Assert.That(view.transform.Find("Card"), Is.Null);
                Assert.That(view.transform.Find("Content/Stopwatch/Timer"), Is.Not.Null);
                Assert.That(view.GetComponent<Canvas>().sortingOrder, Is.EqualTo(240));

                var timer = view.transform.Find("Content/Stopwatch/Timer").GetComponent<TMPro.TMP_Text>();
                Assert.That(timer.text, Is.EqualTo("00:30"));
                Assert.That(timer.fontSize, Is.EqualTo(HidingTurnStartView.TimerFontSize));
                Assert.That(HidingTurnStartView.TimerFontSize, Is.EqualTo(48f));

                var banner = view.transform.Find("Content/Banner/Label")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(banner, Is.Not.Null);
                Assert.That(banner.text, Is.EqualTo(HidingTurnStartView.BannerText));
                Assert.That(banner.fontSize, Is.EqualTo(HidingTurnStartView.BannerFontSize));
                Assert.That(HidingTurnStartView.BannerFontSize, Is.EqualTo(55f));
                Assert.That(view.transform.Find("Content/CompleteGuide"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
