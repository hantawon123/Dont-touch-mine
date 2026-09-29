using Game.Client.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class ResultViewTests
    {
        [Test]
        public void SetOutcome_PlacesHeadlineAndSubtitleLikeTheInGameTimer()
        {
            var root = new GameObject("Result", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ResultView>();
                view.Initialize();
                view.SetOutcome(MatchTimerView.WinHeadline, MatchTimerView.WinSubtitle);

                var headline = root.transform.Find("Result Canvas/Result Headline")
                    ?.GetComponent<TMP_Text>();
                var subtitle = root.transform.Find("Result Canvas/Result Subtitle")
                    ?.GetComponent<TMP_Text>();
                Assert.That(headline, Is.Not.Null);
                Assert.That(subtitle, Is.Not.Null);

                Assert.That(headline.text, Is.EqualTo(MatchTimerView.WinHeadline));
                Assert.That(headline.fontSize, Is.EqualTo(MatchTimerView.TimerFontSize));
                Assert.That(headline.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(headline.rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(
                    headline.rectTransform.anchoredPosition,
                    Is.EqualTo(new Vector2(0f, -HidingActiveHudView.TopPadding)));
                Assert.That(
                    headline.rectTransform.sizeDelta.y,
                    Is.EqualTo(MatchTimerView.TimerHeight));

                Assert.That(subtitle.text, Is.EqualTo(MatchTimerView.WinSubtitle));
                Assert.That(subtitle.fontSize, Is.EqualTo(MatchTimerView.HintFontSize));
                Assert.That(subtitle.color, Is.EqualTo(MatchTimerView.ResultSubtitleColor));
                Assert.That(subtitle.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(
                    subtitle.rectTransform.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        0f,
                        -(HidingActiveHudView.TopPadding + MatchTimerView.TimerHeight))));
                Assert.That(
                    subtitle.rectTransform.sizeDelta.y,
                    Is.EqualTo(MatchTimerView.HintHeight));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
