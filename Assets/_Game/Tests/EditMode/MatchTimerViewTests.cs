using System.Reflection;
using Game.Client.Home;
using Game.Client.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class MatchTimerViewTests
    {
        [Test]
        public void NetworkHud_CreatesTimerWhenTheSceneDidNotWireOne()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                hud.SetRemainingSeconds(90d);
                var timer = hud.GetComponentInChildren<MatchTimerView>(true);
                Assert.That(timer, Is.Not.Null);
                Assert.That(timer.GetComponent<TMP_Text>().text, Is.EqualTo("01:30"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetRemainingSeconds_KeepsHidingTimerLookBeforeThirtySeconds()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = CreateView(canvas.transform);
                view.SetRemainingSeconds(185d);

                var timer = view.GetComponent<TMP_Text>();
                Assert.That(timer.text, Is.EqualTo("03:05"));
                Assert.That(timer.fontSize, Is.EqualTo(HidingActiveHudView.TimerFontSize));
                Assert.That(HidingActiveHudView.TimerFontSize, Is.EqualTo(58.5f));
                Assert.That(MatchTimerView.TimerFontSize, Is.EqualTo(83.2f));
                Assert.That(MatchTimerView.TimerHeight, Is.EqualTo(104f));
                Assert.That(timer.color, Is.EqualTo(Color.white));

                var hint = view.transform.Find("Hint");
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetRemainingSeconds_UsesOrangeBlackWeightAndHintFromThirtySeconds()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = CreateView(canvas.transform);

                view.SetRemainingSeconds(31d);
                var timer = view.GetComponent<TMP_Text>();
                Assert.That(timer.text, Is.EqualTo("00:31"));
                Assert.That(timer.fontSize, Is.EqualTo(HidingActiveHudView.TimerFontSize));
                Assert.That(timer.color, Is.EqualTo(Color.white));
                Assert.That(timer.font, Is.EqualTo(HomeUiFonts.Apply()));
                Assert.That(view.transform.Find("Hint").gameObject.activeSelf, Is.False);
                Assert.That(MatchTimerView.IsWarning(31d), Is.False);
                InvokeUpdate(view);
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));

                view.SetRemainingSeconds(30d);
                Assert.That(timer.text, Is.EqualTo("00:30"));
                Assert.That(timer.fontSize, Is.EqualTo(MatchTimerView.TimerFontSize));
                Assert.That(timer.color, Is.EqualTo(MatchTimerView.TimerColor));
                Assert.That(timer.color, Is.EqualTo(MatchTimerView.WarningColor));
                Assert.That(timer.font, Is.EqualTo(HomeUiFonts.ApplyBlack()));

                var hint = view.transform.Find("Hint")?.GetComponent<TMP_Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.gameObject.activeSelf, Is.True);
                Assert.That(hint.text, Is.EqualTo(MatchTimerView.HintText));
                Assert.That(hint.fontSize, Is.EqualTo(MatchTimerView.HintFontSize));
                Assert.That(hint.color, Is.EqualTo(MatchTimerView.WarningColor));
                Assert.That(MatchTimerView.IsWarning(30d), Is.True);
                InvokeUpdate(view);
                Assert.That(
                    view.transform.localScale.x,
                    Is.EqualTo(HidingActiveHudView.HeartbeatScale(Time.unscaledTime)).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetResult_ReplacesTimerWithWhiteSubtitle()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = CreateView(canvas.transform);
                view.SetRemainingSeconds(30d);
                view.SetResult(MatchTimerView.WinHeadline, MatchTimerView.WinSubtitle);

                var timer = view.GetComponent<TMP_Text>();
                Assert.That(timer.text, Is.EqualTo(MatchTimerView.WinHeadline));
                Assert.That(timer.fontSize, Is.EqualTo(MatchTimerView.TimerFontSize));
                Assert.That(timer.color, Is.EqualTo(MatchTimerView.TimerColor));
                Assert.That(timer.font, Is.EqualTo(HomeUiFonts.ApplyBlack()));

                var hint = view.transform.Find("Hint")?.GetComponent<TMP_Text>();
                Assert.That(hint, Is.Not.Null);
                Assert.That(hint.gameObject.activeSelf, Is.True);
                Assert.That(hint.text, Is.EqualTo(MatchTimerView.WinSubtitle));
                Assert.That(hint.color, Is.EqualTo(MatchTimerView.ResultSubtitleColor));

                view.SetRemainingSeconds(12d);
                Assert.That(timer.text, Is.EqualTo(MatchTimerView.WinHeadline));
                Assert.That(hint.text, Is.EqualTo(MatchTimerView.WinSubtitle));
                InvokeUpdate(view);
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));

                view.SetResult(MatchTimerView.LoseHeadline, MatchTimerView.LoseSubtitle);
                Assert.That(timer.text, Is.EqualTo(MatchTimerView.LoseHeadline));
                Assert.That(hint.text, Is.EqualTo(MatchTimerView.LoseSubtitle));
                Assert.That(hint.color, Is.EqualTo(Color.white));

                view.ClearResult();
                view.SetRemainingSeconds(90d);
                Assert.That(timer.text, Is.EqualTo("01:30"));
                Assert.That(hint.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetHintVisible_HidesThePromptEvenInTheLastThirtySeconds()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = CreateView(canvas.transform);
                view.SetRemainingSeconds(20d);
                view.SetHintVisible(false);

                Assert.That(view.transform.Find("Hint").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void NetworkHud_RoundsTheShredderMarkerCorners()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            canvas.SetActive(false);
            var marker = new GameObject(
                "ShredderMarker",
                typeof(RectTransform),
                typeof(Image));
            marker.transform.SetParent(canvas.transform, false);
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                typeof(NetworkMatchHudView)
                    .GetField("shredderMarker", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hud, marker.GetComponent<RectTransform>());
                canvas.SetActive(true);
                hud.ApplyShredderMarkerChrome();

                var image = marker.GetComponent<Image>();
                Assert.That(image.sprite, Is.EqualTo(HomeUiFonts.Rounded(NetworkMatchHudView.ShredderMarkerCornerRadius)));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void NetworkHud_MovesDestructionUsesOntoTheShredderMarker()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            canvas.SetActive(false);
            var marker = new GameObject(
                "ShredderMarker",
                typeof(RectTransform),
                typeof(Image));
            marker.transform.SetParent(canvas.transform, false);
            var labelObject = new GameObject(
                NetworkMatchHudView.ShredderMarkerLabelName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(marker.transform, false);
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                typeof(NetworkMatchHudView)
                    .GetField("shredderMarker", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hud, marker.GetComponent<RectTransform>());
                canvas.SetActive(true);
                hud.ApplyShredderMarkerChrome();

                hud.SetRemainingDestructionUses(4);
                Assert.That(
                    labelObject.GetComponent<TMP_Text>().text,
                    Is.EqualTo("파쇄기 (4/5)"));
                Assert.That(
                    marker.GetComponent<RectTransform>().sizeDelta.x,
                    Is.EqualTo(NetworkMatchHudView.ShredderMarkerWidth));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        private static MatchTimerView CreateView(Transform parent)
        {
            var textObject = new GameObject(
                "TimerText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.AddComponent<MatchTimerView>();
        }

        private static void InvokeUpdate(MatchTimerView view)
        {
            typeof(MatchTimerView)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(view, null);
        }
    }
}
