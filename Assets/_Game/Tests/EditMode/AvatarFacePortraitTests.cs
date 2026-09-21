using Game.Client.Character;
using Game.Core.Players;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Architecture.Tests
{
    public sealed class AvatarFacePortraitTests
    {
        [TearDown]
        public void TearDown() => AvatarAppearanceBoard.Clear();

        [Test]
        public void OrthographicSizeForHead_UsesLargestExtentAndPadding()
        {
            Assert.That(
                AvatarFacePortrait.OrthographicSizeForHead(new Vector3(0.5f, 0.2f, 0.1f)),
                Is.EqualTo(0.5f * AvatarFacePortrait.FramePadding).Within(0.0001f));
            Assert.That(
                AvatarFacePortrait.OrthographicSizeForHead(new Vector3(0.01f, 0.02f, 0.01f)),
                Is.EqualTo(0.08f * AvatarFacePortrait.FramePadding).Within(0.0001f));
        }

        [Test]
        public void Bind_AddsCircularMaskAndStretchPortrait()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var circle = new GameObject(
                    "Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            try
            {
                circle.SetParent(canvas.transform, false);
                circle.sizeDelta = new Vector2(54f, 54f);
                var image = circle.GetComponent<Image>();
                image.sprite = Game.Client.Home.HomeUiFonts.CircleSprite;
                image.color = new Color(0.62f, 0.62f, 0.62f, 1f);

                AvatarFacePortrait.Bind(circle, new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a"));

                var mask = circle.GetComponent<Mask>();
                Assert.That(mask, Is.Not.Null);
                Assert.That(mask.showMaskGraphic, Is.True);

                var portrait = circle.Find(AvatarFacePortrait.PortraitName) as RectTransform;
                Assert.That(portrait, Is.Not.Null);
                Assert.That(portrait.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(portrait.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(portrait.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(portrait.offsetMax, Is.EqualTo(Vector2.zero));
                var raw = portrait.GetComponent<RawImage>();
                Assert.That(raw, Is.Not.Null);
                Assert.That(raw.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Follow_LooksUpBoardByPlayerId()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var circle = new GameObject(
                    "Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                .GetComponent<RectTransform>();
            try
            {
                circle.SetParent(canvas.transform, false);
                var worn = new AvatarAppearance("body_a", "hood_a", "shoes_a", "face_a");
                AvatarAppearanceBoard.Replace(new[] { ("p1", "u1", worn) });

                AvatarFaceSlot.Attach(circle).Follow("p1", "u1");

                Assert.That(circle.GetComponent<Mask>(), Is.Not.Null);
                Assert.That(circle.Find(AvatarFacePortrait.PortraitName), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
