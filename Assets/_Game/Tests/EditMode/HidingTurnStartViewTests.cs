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
                Assert.That(
                    view.transform.Find("Content/Stopwatch").GetSiblingIndex(),
                    Is.LessThan(view.transform.Find("Content/Banner").GetSiblingIndex()));

                var timer = view.transform.Find("Content/Stopwatch/Timer").GetComponent<TMPro.TMP_Text>();
                Assert.That(timer.text, Is.EqualTo("00:30"));
                Assert.That(timer.fontSize, Is.EqualTo(HidingTurnStartView.TimerFontSize));
                Assert.That(HidingTurnStartView.TimerFontSize, Is.EqualTo(64f));

                var banner = view.transform.Find("Content/Banner") as RectTransform;
                Assert.That(banner, Is.Not.Null);
                Assert.That(HidingTurnStartView.BannerWidthPercent, Is.EqualTo(0.7f));
                Assert.That(banner.anchorMin.x, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(banner.anchorMax.x, Is.EqualTo(0.85f).Within(0.0001f));
                Assert.That(banner.sizeDelta.y, Is.EqualTo(HidingTurnStartView.BannerHeight));
                Assert.That(
                    (view.transform.Find("Content/Stopwatch") as RectTransform).sizeDelta,
                    Is.EqualTo(HidingTurnStartView.StopwatchSize));
                Assert.That(timer.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(timer.rectTransform.sizeDelta, Is.EqualTo(HidingTurnStartView.StopwatchSize));
                Assert.That(timer.alignment, Is.EqualTo(TMPro.TextAlignmentOptions.Midline));
                var bannerLabel = view.transform.Find("Content/Banner/Label")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(bannerLabel, Is.Not.Null);
                Assert.That(bannerLabel.text, Is.EqualTo(HidingTurnStartView.BannerText));
                Assert.That(bannerLabel.fontSize, Is.EqualTo(HidingTurnStartView.BannerFontSize));
                Assert.That(HidingTurnStartView.BannerFontSize, Is.EqualTo(55f));
                Assert.That(bannerLabel.font.name, Does.Contain("Paperlogy").IgnoreCase);
                Assert.That(bannerLabel.font.name, Does.Contain("SemiBold").IgnoreCase);
                Assert.That(view.transform.Find("Content/CompleteGuide"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_CanReuseTheSameOverlayWithAFinalWarningBanner()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingTurnStartView.Create(canvas.transform);
                view.Show(30d, HidingTurnStartView.FinalWarningBannerText);

                var timer = view.transform.Find("Content/Stopwatch/Timer").GetComponent<TMPro.TMP_Text>();
                Assert.That(timer.text, Is.EqualTo("00:30"));

                var banner = view.transform.Find("Content/Banner/Label")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(banner, Is.Not.Null);
                Assert.That(banner.text, Is.EqualTo(HidingTurnStartView.FinalWarningBannerText));
                Assert.That(banner.fontSize, Is.EqualTo(HidingTurnStartView.BannerFontSize));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
