using System.Reflection;
using Game.Client.Match;
using Game.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class MatchUrgencyBorderViewTests
    {
        [Test]
        public void Create_BuildsFourEdgeGradientsHiddenByDefault()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchUrgencyBorderView.Create(canvas.transform);

                var topTransform = Child(view.transform, "Top");
                var bottomTransform = Child(view.transform, "Bottom");
                var leftTransform = Child(view.transform, "Left");
                var rightTransform = Child(view.transform, "Right");
                Assert.That(topTransform, Is.Not.Null);
                Assert.That(bottomTransform, Is.Not.Null);
                Assert.That(leftTransform, Is.Not.Null);
                Assert.That(rightTransform, Is.Not.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);

                var top = topTransform.GetComponent<Image>();
                Assert.That(top.raycastTarget, Is.False);
                Assert.That(top.sprite, Is.Not.Null);
                Assert.That(top.color.r, Is.GreaterThan(0.5f));
                Assert.That(top.color.g, Is.LessThan(0.2f));
                Assert.That(MatchUrgencyBorderView.EdgeColor.a, Is.EqualTo(0.5f));
                Assert.That(MatchUrgencyBorderView.MaxThickness, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_PulsesThicknessBetweenMinAndMax()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = MatchUrgencyBorderView.Create(canvas.transform);
                view.Show();
                Assert.That(view.gameObject.activeSelf, Is.True);

                InvokeLateUpdate(view);
                var top = view.transform.Find("Top") as RectTransform;
                Assert.That(top.sizeDelta.y, Is.EqualTo(MatchUrgencyBorderView.ThicknessAt(Time.unscaledTime)).Within(0.01f));
                Assert.That(top.sizeDelta.y, Is.GreaterThanOrEqualTo(MatchUrgencyBorderView.MinThickness));
                Assert.That(top.sizeDelta.y, Is.LessThanOrEqualTo(MatchUrgencyBorderView.MaxThickness));

                Assert.That(MatchUrgencyBorderView.PulseAmount(0f), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    MatchUrgencyBorderView.ThicknessAt(0f),
                    Is.Not.EqualTo(MatchUrgencyBorderView.ThicknessAt(MatchUrgencyBorderView.PulsePeriod * 0.25f)).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SearchingLastThirtySeconds_ShowsBorderAndHidesOutsideWarning()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                hud.SetPhase(MatchPhase.Searching, string.Empty);
                hud.SetRemainingSeconds(31d);

                var border = hud.GetComponentInChildren<MatchUrgencyBorderView>(true);
                Assert.That(border, Is.Not.Null);
                Assert.That(border.gameObject.activeSelf, Is.False);

                hud.SetRemainingSeconds(30d);
                Assert.That(border.gameObject.activeSelf, Is.True);

                hud.SetRemainingSeconds(1d);
                Assert.That(border.gameObject.activeSelf, Is.True);

                hud.SetPhase(MatchPhase.Hiding, string.Empty);
                hud.SetRemainingSeconds(20d);
                Assert.That(border.gameObject.activeSelf, Is.False);

                hud.SetPhase(MatchPhase.Searching, string.Empty);
                hud.SetRemainingSeconds(12d);
                Assert.That(border.gameObject.activeSelf, Is.True);

                hud.SetEndResult(MatchTimerView.WinHeadline, MatchTimerView.WinSubtitle);
                Assert.That(border.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        private static Transform Child(Transform root, string name)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void InvokeLateUpdate(MatchUrgencyBorderView view)
        {
            typeof(MatchUrgencyBorderView)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);
        }
    }
}
