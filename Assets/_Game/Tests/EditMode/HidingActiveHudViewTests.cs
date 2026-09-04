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

                var action = view.transform.Find("KeyGuide/Row0/Action")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(action, Is.Not.Null);
                Assert.That(action.text, Is.EqualTo("공격"));
                Assert.That(action.fontSize, Is.EqualTo(HidingActiveHudView.ActionFontSize));

                var clickChip = view.transform.Find("KeyGuide/Row0/Key") as RectTransform;
                var clickIcon = view.transform.Find("KeyGuide/Row0/Key/Icon") as RectTransform;
                Assert.That(clickChip, Is.Not.Null);
                Assert.That(clickChip.sizeDelta, Is.EqualTo(new Vector2(
                    HidingActiveHudView.KeyChipWidth,
                    HidingActiveHudView.KeyChipHeight)));
                Assert.That(clickIcon, Is.Not.Null);
                Assert.That(clickIcon.sizeDelta, Is.EqualTo(new Vector2(
                    HidingActiveHudView.KeyIconSize,
                    HidingActiveHudView.KeyIconSize)));
                Assert.That(view.transform.Find("KeyGuide/Row0/Key/Label").gameObject.activeSelf, Is.False);

                var singleChip = view.transform.Find("KeyGuide/Row1/Key") as RectTransform;
                Assert.That(singleChip, Is.Not.Null);
                Assert.That(singleChip.sizeDelta, Is.EqualTo(new Vector2(
                    HidingActiveHudView.KeyChipWidth,
                    HidingActiveHudView.KeyChipHeight)));
                var chipImage = singleChip.GetComponent<UnityEngine.UI.Image>();
                Assert.That(chipImage.color, Is.EqualTo(HidingActiveHudView.KeyChipColor));
                Assert.That(chipImage.sprite.border.x, Is.EqualTo(HidingActiveHudView.KeyChipCornerRadius));

                var spaceLabel = view.transform.Find("KeyGuide/Row5/Key/Label")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(spaceLabel, Is.Not.Null);
                Assert.That(spaceLabel.fontSize, Is.EqualTo(HidingActiveHudView.KeyChipFontSize));
                var spaceChip = view.transform.Find("KeyGuide/Row5/Key") as RectTransform;
                Assert.That(spaceChip, Is.Not.Null);
                Assert.That(
                    spaceChip.sizeDelta.x,
                    Is.EqualTo(HidingActiveHudView.MeasureKeyChipWidth(
                        spaceLabel.text,
                        spaceLabel.preferredWidth)));
                Assert.That(view.transform.Find("KeyGuide").GetComponent<RectTransform>().anchorMin.x, Is.EqualTo(1f));
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
                Assert.That(view.transform.Find("KeyGuide").gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
