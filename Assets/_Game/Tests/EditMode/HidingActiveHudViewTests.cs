using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HidingActiveHudViewTests
    {
        [Test]
        public void Show_PlacesTimerHintCompleteGuideAndKeys()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingActiveHudView.Create(canvas.transform);
                view.Show(30d, true, true);

                var timer = view.transform.Find("TopPrompt/Timer")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(timer, Is.Not.Null);
                Assert.That(timer.text, Is.EqualTo("00:30"));
                Assert.That(timer.fontSize, Is.EqualTo(HidingActiveHudView.TimerFontSize));
                Assert.That(HidingActiveHudView.TimerFontSize, Is.EqualTo(58.5f));
                Assert.That(
                    timer.rectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(
                        HidingActiveHudView.TimerWidth,
                        HidingActiveHudView.TimerHeight)));

                var hint = view.transform.Find("TopPrompt/Hint")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.text, Is.EqualTo(HidingActiveHudView.HintText));
                Assert.That(hint.fontSize, Is.EqualTo(HidingActiveHudView.HintFontSize));
                Assert.That(
                    view.transform.Find("TopPrompt").GetComponent<RectTransform>().anchoredPosition.y,
                    Is.EqualTo(-HidingActiveHudView.TopPadding));

                var complete = view.transform.Find("CompleteGuide/Caption")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(complete, Is.Not.Null);
                Assert.That(complete.text, Is.EqualTo(HidingActiveHudView.CompleteText));
                var completeKey = view.transform.Find("CompleteGuide/Key") as RectTransform;
                var completeKeyLabel = view.transform.Find("CompleteGuide/Key/Label")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(completeKeyLabel?.text, Is.EqualTo(HidingActiveHudView.CompleteKey));
                Assert.That(completeKeyLabel.fontSize, Is.EqualTo(HidingActiveHudView.KeyChipFontSize));
                Assert.That(completeKey.sizeDelta, Is.EqualTo(new Vector2(
                    HidingActiveHudView.KeyChipWidth,
                    HidingActiveHudView.KeyChipHeight)));
                Assert.That(
                    completeKey.GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(HidingActiveHudView.KeyChipColor));
                Assert.That(view.transform.Find("KeyGuide"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void MeasureKeyChipWidth_UsesFixedSizeForSingleLetterAndPaddingForLongKeys()
        {
            Assert.That(HidingActiveHudView.MeasureKeyChipWidth("C", 12f), Is.EqualTo(35f));
            Assert.That(HidingActiveHudView.MeasureKeyChipWidth("Space", 46f), Is.EqualTo(66f));
        }

        [Test]
        public void Show_UsesWarningCopyAndColorInLastTenSeconds()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingActiveHudView.Create(canvas.transform);
                view.Show(11d, true, true);

                var timer = view.transform.Find("TopPrompt/Timer")?.GetComponent<TMPro.TMP_Text>();
                var hint = view.transform.Find("TopPrompt/Hint")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(hint.text, Is.EqualTo(HidingActiveHudView.HintText));
                Assert.That(timer.color, Is.EqualTo(Color.white));
                Assert.That(HidingActiveHudView.IsWarning(11d), Is.False);

                view.Show(10d, true, true);
                Assert.That(timer.text, Is.EqualTo("00:10"));
                Assert.That(hint.text, Is.EqualTo(HidingActiveHudView.WarningHintText));
                Assert.That(timer.color, Is.EqualTo(HidingActiveHudView.WarningColor));
                Assert.That(hint.color, Is.EqualTo(HidingActiveHudView.WarningColor));
                Assert.That(HidingActiveHudView.IsWarning(10d), Is.True);
                Assert.That(HidingActiveHudView.HeartbeatScale(0f), Is.GreaterThan(1f));
                Assert.That(HidingActiveHudView.HeartbeatScale(0.45f), Is.LessThan(1.02f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_CanHideTopPromptAndKeepKeys()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingActiveHudView.Create(canvas.transform);
                view.Show(29d, false, false);

                Assert.That(view.transform.Find("TopPrompt").gameObject.activeSelf, Is.False);
                Assert.That(view.transform.Find("CompleteGuide").gameObject.activeSelf, Is.False);
                Assert.That(view.transform.Find("KeyGuide"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
