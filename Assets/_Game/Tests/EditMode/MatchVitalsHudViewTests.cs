using System.Reflection;
using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class MatchVitalsHudViewTests
    {
        [Test]
        public void Show_BuildsBottomPanelWithStaminaAndThreeHitSegments()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(
                    MatchVitalsHudView.DefaultStamina,
                    MatchVitalsHudView.DefaultStamina,
                    MatchVitalsHudView.DefaultHits,
                    MatchVitalsHudView.DefaultHits);

                var panel = view.transform.Find("Panel")?.GetComponent<Image>();
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.color, Is.EqualTo(MatchVitalsHudView.PanelColor));
                Assert.That(panel.rectTransform.anchorMin.x, Is.EqualTo(0.5f));
                Assert.That(panel.rectTransform.anchorMax.x, Is.EqualTo(0.5f));
                Assert.That(panel.rectTransform.sizeDelta.x, Is.EqualTo(MatchVitalsHudView.PanelWidth));
                Assert.That(MatchVitalsHudView.PanelWidth, Is.EqualTo(380f));
                Assert.That(panel.rectTransform.anchoredPosition.y, Is.EqualTo(MatchChatView.Margin));
                Assert.That(MatchVitalsHudView.BottomPadding, Is.EqualTo(MatchChatView.Margin));

                var staminaBar = view.transform.Find("Panel/Stamina/Bar")?.GetComponent<Image>();
                Assert.That(staminaBar, Is.Not.Null);
                Assert.That(staminaBar.color, Is.EqualTo(MatchVitalsHudView.StaminaColor));
                Assert.That(view.transform.Find("Panel/Stamina/Value"), Is.Null);
                Assert.That(view.transform.Find("Panel/Health/Value"), Is.Null);

                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment0"), Is.Not.Null);
                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment1"), Is.Not.Null);
                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment2"), Is.Not.Null);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment0").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.HealthStartColor));
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment2").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.HealthEndColor));
                Assert.That(view.transform.Find("Panel/Health/Value"), Is.Null);

                var staminaRect = view.transform.Find("Panel/Stamina/Bar") as RectTransform;
                var track = view.transform.Find("Panel/Health/BarTrack") as RectTransform;
                Assert.That(MatchVitalsHudView.BarWidth, Is.EqualTo(
                    (MatchVitalsHudView.SegmentWidth * MatchVitalsHudView.DefaultHits) +
                    (MatchVitalsHudView.SegmentGap * (MatchVitalsHudView.DefaultHits - 1))));
                Assert.That(staminaRect.sizeDelta.x, Is.EqualTo(MatchVitalsHudView.BarWidth));
                Assert.That(track.sizeDelta.x, Is.EqualTo(MatchVitalsHudView.BarWidth));
                Assert.That(staminaRect.anchoredPosition.x, Is.EqualTo(MatchVitalsHudView.BarStart));
                Assert.That(track.anchoredPosition.x, Is.EqualTo(MatchVitalsHudView.BarStart));
                var group = track.GetComponent<HorizontalLayoutGroup>();
                Assert.That(group.spacing, Is.EqualTo(MatchVitalsHudView.SegmentGap));
                Assert.That(group.childForceExpandWidth, Is.False);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment0")
                        .GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(MatchVitalsHudView.SegmentWidth));
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment1")
                        .GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(MatchVitalsHudView.SegmentWidth));
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment2")
                        .GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(MatchVitalsHudView.SegmentWidth));
                Assert.That(staminaBar.GetComponent<ParallelogramShear>(), Is.Not.Null);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment0")
                        .GetComponent<ParallelogramShear>(),
                    Is.Not.Null);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment1")
                        .GetComponent<ParallelogramShear>(),
                    Is.Not.Null);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment2")
                        .GetComponent<ParallelogramShear>(),
                    Is.Not.Null);
                Assert.That(ParallelogramShear.AngleDegrees, Is.EqualTo(60f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ParallelogramShear_UsesSixtyDegreeSlant()
        {
            Assert.That(ParallelogramShear.AngleDegrees, Is.EqualTo(60f));
            Assert.That(
                ParallelogramShear.SlantForHeight(Mathf.Sqrt(3f)),
                Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SetValues_HidesLostHitSegments()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(5, 5, 3, 3);
                view.SetValues(2, 5, 1, 3);

                Assert.That(view.transform.Find("Panel/Stamina/Value"), Is.Null);
                Assert.That(view.transform.Find("Panel/Health/Value"), Is.Null);
                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment0").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment1").gameObject.activeSelf, Is.False);
                Assert.That(view.transform.Find("Panel/Health/BarTrack/Segment2").gameObject.activeSelf, Is.False);
                Assert.That(
                    view.transform.Find("Panel/Health/BarTrack/Segment0")
                        .GetComponent<LayoutElement>().preferredWidth,
                    Is.EqualTo(MatchVitalsHudView.SegmentWidth));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetValues_FillsStaminaBarToCurrentRatio()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(100, 100, 3, 3);
                view.SetValues(40, 100, 3, 3);

                Assert.That(view.transform.Find("Panel/Stamina/Value"), Is.Null);
                Assert.That(
                    view.transform.Find("Panel/Stamina/Bar").GetComponent<Image>().fillAmount,
                    Is.EqualTo(0.4f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetValues_UsesDisabledColorWhileExhausted()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(0, 100, 3, 3, true);
                view.SetValues(35, 100, 3, 3, true);

                Assert.That(
                    view.transform.Find("Panel/Stamina/Bar").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaDisabledColor));
                Assert.That(
                    view.transform.Find("Panel/Stamina/Icon").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaDisabledColor));

                view.SetValues(100, 100, 3, 3, false);
                Assert.That(
                    view.transform.Find("Panel/Stamina/Bar").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaColor));
                Assert.That(
                    view.transform.Find("Panel/Stamina/Icon").GetComponent<Image>().color,
                    Is.EqualTo(Color.white));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetValues_UsesLowColorAndStartsShakeWhenStaminaIsTwentyOrBelow()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(20, 100, 3, 3);

                Assert.That(
                    view.transform.Find("Panel/Stamina/Bar").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaLowColor));
                Assert.That(
                    view.transform.Find("Panel/Stamina/Icon").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaLowColor));
                Assert.That(view.transform.Find("Panel/Stamina/Value"), Is.Null);

                view.SetValues(21, 100, 3, 3);
                Assert.That(
                    view.transform.Find("Panel/Stamina/Bar").GetComponent<Image>().color,
                    Is.EqualTo(MatchVitalsHudView.StaminaColor));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetValues_StopsShakeWhileStaminaIsRefilling()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchVitalsHudView.Create(canvas.transform);
                view.Show(25, 100, 3, 3);
                view.SetValues(18, 100, 3, 3);

                var row = view.transform.Find("Panel/Stamina") as RectTransform;
                var rest = row.anchoredPosition;
                InvokeLateUpdate(view);
                Assert.That(row.anchoredPosition, Is.Not.EqualTo(rest));

                view.SetValues(19, 100, 3, 3);
                InvokeLateUpdate(view);
                Assert.That(row.anchoredPosition, Is.EqualTo(rest));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void FormatStamina_ShowsCurrentValueOnly()
        {
            Assert.That(MatchVitalsHudView.FormatStamina(49.6f), Is.EqualTo("50"));
            Assert.That(MatchVitalsHudView.FillAmount(25f, 100f), Is.EqualTo(0.25f));
            Assert.That(MatchVitalsHudView.FillAmount(10f, 0f), Is.EqualTo(0f));
            Assert.That(MatchVitalsHudView.IsLowStamina(20f), Is.True);
            Assert.That(MatchVitalsHudView.IsLowStamina(21f), Is.False);
            Assert.That(MatchVitalsHudView.ShouldShakeStamina(20f, 21f, false), Is.True);
            Assert.That(MatchVitalsHudView.ShouldShakeStamina(19f, 20f, false), Is.True);
            Assert.That(MatchVitalsHudView.ShouldShakeStamina(16f, 15f, false), Is.False);
            Assert.That(MatchVitalsHudView.ShouldShakeStamina(10f, 9f, true), Is.False);
            Assert.That(MatchVitalsHudView.ShouldShakeStamina(20f, float.NaN, false), Is.False);
            Assert.That(
                MatchVitalsHudView.StaminaColorFor(true),
                Is.EqualTo(MatchVitalsHudView.StaminaDisabledColor));
            Assert.That(
                MatchVitalsHudView.StaminaColorFor(12f, false),
                Is.EqualTo(MatchVitalsHudView.StaminaLowColor));
            Assert.That(
                MatchVitalsHudView.StaminaColorFor(12f, true),
                Is.EqualTo(MatchVitalsHudView.StaminaDisabledColor));
            Assert.That(MatchVitalsHudView.ShakeOffset(0.03f).sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(MatchVitalsHudView.RemainingHits(0, 3), Is.EqualTo(3));
            Assert.That(MatchVitalsHudView.RemainingHits(1, 3), Is.EqualTo(2));
            Assert.That(MatchVitalsHudView.RemainingHits(3, 3), Is.Zero);
        }

        private static void InvokeLateUpdate(MatchVitalsHudView view)
        {
            typeof(MatchVitalsHudView)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);
        }
    }
}
