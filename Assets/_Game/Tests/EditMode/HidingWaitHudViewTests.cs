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
                    });

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

                var currentName = view.transform.Find("PlayerList/Row2/Name")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(currentName.text, Is.EqualTo("민수"));
                Assert.That(currentName.color, Is.EqualTo(HidingWaitHudView.AccentColor));
                Assert.That(
                    view.transform.Find("PlayerList/Row0/Avatar/Check").gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    view.transform.Find("PlayerList/Row2/Avatar/Ring").GetComponent<UnityEngine.UI.Image>().enabled,
                    Is.True);
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
        }
    }
}
