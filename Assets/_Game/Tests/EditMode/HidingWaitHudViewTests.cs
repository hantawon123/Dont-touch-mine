using Game.Client.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HidingWaitHudViewTests
    {
        [Test]
        public void Show_PlacesProgressStatusAndPlayerOrder()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = HidingWaitHudView.Create(canvas.transform);
                view.Show(
                    2,
                    6,
                    "민수",
                    new[]
                    {
                        new HidingWaitPlayer("방장", true, false),
                        new HidingWaitPlayer("하나", true, false),
                        new HidingWaitPlayer("민수", false, true),
                        new HidingWaitPlayer("지연", false, false)
                    },
                    true,
                    15d,
                    30d);

                var count = view.transform.Find("TopPrompt/Count")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(count, Is.Not.Null);
                Assert.That(count.text, Is.EqualTo("2 / 6"));
                Assert.That(count.fontSize, Is.EqualTo(HidingWaitHudView.CountFontSize));

                var status = view.transform.Find("TopPrompt/Status")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(status, Is.Not.Null);
                Assert.That(status.text, Is.EqualTo("민수님이 물건을 숨기는 중"));
                Assert.That(status.fontSize, Is.EqualTo(HidingWaitHudView.StatusFontSize));
                Assert.That(
                    view.transform.Find("TopPrompt").GetComponent<RectTransform>().anchoredPosition.y,
                    Is.EqualTo(-HidingWaitHudView.TopPadding));
                Assert.That(view.transform.Find("TopPrompt/Person"), Is.Not.Null);

                var nextTurn = view.transform.Find("TopPrompt/NextTurn")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(nextTurn, Is.Not.Null);
                Assert.That(nextTurn.text, Is.EqualTo(HidingWaitHudView.NextTurnText));
                Assert.That(nextTurn.fontSize, Is.EqualTo(HidingWaitHudView.NextTurnFontSize));
                Assert.That(nextTurn.color, Is.EqualTo(HidingWaitHudView.AccentColor));
                Assert.That(nextTurn.gameObject.activeSelf, Is.True);

                Assert.That(
                    (view.transform.Find("PlayerList") as RectTransform).sizeDelta,
                    Is.EqualTo(new Vector2(
                        HidingWaitHudView.ListWidth,
                        HidingWaitHudView.ListHeight)));
                var currentName = view.transform.Find("PlayerList/Row2/Name")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(currentName.text, Is.EqualTo("민수"));
                Assert.That(currentName.fontSize, Is.EqualTo(HidingWaitHudView.NameFontSize));
                Assert.That(currentName.color, Is.EqualTo(HidingWaitHudView.AccentColor));
                Assert.That(
                    (view.transform.Find("PlayerList/Row0/Avatar/Face") as RectTransform).sizeDelta,
                    Is.EqualTo(new Vector2(
                        HidingWaitHudView.AvatarSize,
                        HidingWaitHudView.AvatarSize)));
                Assert.That(
                    HidingWaitHudView.AvatarSize,
                    Is.EqualTo(54f * HidingWaitHudView.AvatarScale).Within(0.001f));
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/CheckIcon").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    (view.transform.Find("PlayerList/Row0/Avatar/CheckIcon") as RectTransform).sizeDelta.x,
                    Is.EqualTo(HidingWaitHudView.CheckIconWidth));
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Dim").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Face")
                        .GetComponent<Game.Client.Character.AvatarFaceSlot>(),
                    Is.Not.Null);
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Face").GetComponent<UnityEngine.UI.Image>().color,
                    Is.EqualTo(HidingWaitHudView.DoneAvatarColor));
                Assert.That(
                    view.transform.Find("PlayerList/Row2/Avatar/Ring").GetComponent<UnityEngine.UI.Image>().enabled,
                    Is.True);
                var currentRing = view.transform.Find("PlayerList/Row2/Avatar/Ring")
                    .GetComponent<UnityEngine.UI.Image>();
                Assert.That(currentRing.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
                Assert.That(currentRing.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(
                    (currentRing.transform as RectTransform).sizeDelta,
                    Is.EqualTo(new Vector2(
                        HidingWaitHudView.RingOuterSize,
                        HidingWaitHudView.RingOuterSize)));
                Assert.That(
                    (view.transform.Find("PlayerList/Row2/Avatar/Face") as RectTransform).sizeDelta.x
                    + (HidingWaitHudView.RingGap * 2f)
                    + (HidingWaitHudView.RingThickness * 2f),
                    Is.EqualTo(HidingWaitHudView.RingOuterSize));
                var track = view.transform.Find("PlayerList/Row2/Avatar/RingTrack")
                    .GetComponent<UnityEngine.UI.Image>();
                Assert.That(track.enabled, Is.True);
                Assert.That(track.color, Is.EqualTo(HidingWaitHudView.RingTrackColor));
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Ring").GetComponent<UnityEngine.UI.Image>().enabled,
                    Is.False);
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/RingTrack")
                        .GetComponent<UnityEngine.UI.Image>().enabled,
                    Is.False);
                var currentTimer = view.transform.Find("PlayerList/Row2/Avatar/Timer")
                    ?.GetComponent<TMPro.TMP_Text>();
                Assert.That(currentTimer, Is.Not.Null);
                Assert.That(currentTimer.gameObject.activeSelf, Is.True);
                Assert.That(currentTimer.text, Is.EqualTo("15"));
                Assert.That(currentTimer.fontSize, Is.EqualTo(HidingWaitHudView.AvatarTimerFontSize));
                Assert.That(currentTimer.color, Is.EqualTo(HidingWaitHudView.AccentColor));
                var currentVeil = view.transform.Find("PlayerList/Row2/Avatar/TimerVeil")
                    ?.GetComponent<UnityEngine.UI.Image>();
                Assert.That(currentVeil, Is.Not.Null);
                Assert.That(currentVeil.gameObject.activeSelf, Is.True);
                Assert.That(currentVeil.color, Is.EqualTo(HidingWaitHudView.AvatarTimerVeilColor));
                Assert.That(
                    (currentVeil.transform as RectTransform).sizeDelta,
                    Is.EqualTo(new Vector2(
                        HidingWaitHudView.AvatarSize,
                        HidingWaitHudView.AvatarSize)));
                Assert.That(
                    currentVeil.transform.GetSiblingIndex(),
                    Is.LessThan(currentTimer.transform.GetSiblingIndex()));
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Timer").gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/TimerVeil").gameObject.activeSelf,
                    Is.False);
                Assert.That(view.transform.Find("PlayerList/Row4").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void FormatCountAndStatus_MatchCopy()
        {
            Assert.That(HidingWaitHudView.FormatCount(3, 6), Is.EqualTo("3 / 6"));
            Assert.That(
                HidingWaitHudView.FormatStatus("금오산남냥이"),
                Is.EqualTo("금오산남냥이님이 물건을 숨기는 중"));
            Assert.That(HidingWaitHudView.RingFillAmount(30d, 30d), Is.EqualTo(0f));
            Assert.That(HidingWaitHudView.RingFillAmount(15d, 30d), Is.EqualTo(0.5f));
            Assert.That(HidingWaitHudView.RingFillAmount(0d, 30d), Is.EqualTo(1f));
            Assert.That(HidingWaitHudView.FormatAvatarTimer(15.2d), Is.EqualTo("16"));
            Assert.That(HidingWaitHudView.FormatAvatarTimer(0d), Is.EqualTo("0"));
            Assert.That(HidingWaitHudView.RingGap, Is.EqualTo(3f));
            Assert.That(HidingWaitHudView.NameFontSize, Is.EqualTo(24f));
            Assert.That(HidingWaitHudView.AvatarScale, Is.EqualTo(1.2f));
            Assert.That(HidingWaitHudView.AvatarSize, Is.EqualTo(54f * 1.2f).Within(0.001f));
        }
    }
}
