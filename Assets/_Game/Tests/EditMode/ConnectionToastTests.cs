using Game.Client.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class ConnectionToastTests
    {
        [Test]
        public void CheckIcon_IsAvailableThroughPlayerResources()
        {
            var sprite = Resources.Load<Sprite>(ConnectionToast.CheckIconResource);
            Assert.That(sprite, Is.Not.Null, "Player builds must load the check icon through Resources.");
            Assert.That(ConnectionToast.LoadCheckIcon(), Is.EqualTo(sprite));
        }

        [Test]
        public void Success_UsesGreenAndShowsCheckToTheLeftOfTheTitle()
        {
            var canvas = new GameObject("Toast canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var toast = ConnectionToast.AttachTo(canvas.GetComponent<RectTransform>());
                toast.Show("피드백 보내기", "보냈습니다. 고맙습니다", success: true);

                var root = canvas.transform.Find("ConnectionToast");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.gameObject.activeSelf, Is.True);

                var tint = root.Find("Tint").GetComponent<Image>();
                var title = root.Find("Title").GetComponent<TMP_Text>();
                var icon = root.Find(ConnectionToast.CheckIconName).GetComponent<Image>();

                Assert.That(tint.color, Is.EqualTo(ConnectionToast.Style.SuccessTint));
                Assert.That(title.color, Is.EqualTo(ConnectionToast.Style.SuccessTitle));
                Assert.That(title.text, Is.EqualTo("피드백 보내기"));
                Assert.That(icon.gameObject.activeSelf, Is.True);
                Assert.That(icon.rectTransform.sizeDelta.x, Is.EqualTo(ConnectionToast.Style.CheckIconSize));
                Assert.That(
                    icon.rectTransform.anchoredPosition.x,
                    Is.LessThan(title.rectTransform.anchoredPosition.x),
                    "the check sits to the left of the title.");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Failure_KeepsTheWarmPlateAndHidesTheCheck()
        {
            var canvas = new GameObject("Toast canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var toast = ConnectionToast.AttachTo(canvas.GetComponent<RectTransform>());
                toast.Show("피드백 보내기", "서버에 연결할 수 없습니다");

                var root = canvas.transform.Find("ConnectionToast");
                var tint = root.Find("Tint").GetComponent<Image>();
                var title = root.Find("Title").GetComponent<TMP_Text>();
                var icon = root.Find(ConnectionToast.CheckIconName);

                Assert.That(tint.color, Is.EqualTo(ConnectionToast.Style.Tint));
                Assert.That(title.color, Is.EqualTo(ConnectionToast.Style.Title));
                Assert.That(icon.gameObject.activeSelf, Is.False);
                Assert.That(title.rectTransform.anchoredPosition, Is.EqualTo(
                    new Vector2(0f, ConnectionToast.Style.TitleOffsetY)));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
