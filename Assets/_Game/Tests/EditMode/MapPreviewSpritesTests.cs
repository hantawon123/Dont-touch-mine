using Game.Client.Lobby;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode
{
    public sealed class MapPreviewSpritesTests
    {
        [Test]
        public void ResourcePath_FollowsMapIdAndIsNullForRandom()
        {
            Assert.That(MapPreviewSprites.ResourcePath("supermarket"), Is.EqualTo("UI/Maps/MapPreview_supermarket"));
            Assert.That(MapPreviewSprites.ResourcePath(" playground "), Is.EqualTo("UI/Maps/MapPreview_playground"));
            Assert.That(MapPreviewSprites.ResourcePath(string.Empty), Is.Null);
            Assert.That(MapPreviewSprites.ResourcePath(null), Is.Null);
            Assert.That(MapPreviewSprites.For(string.Empty), Is.Null, "랜덤 선택은 사진이 없다.");
        }

        [Test]
        public void AttachPhoto_MasksWithFrameAndStaysHiddenUntilSpriteApplied()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                    .GetComponent<Image>();
                frame.transform.SetParent(canvas.transform, false);

                var photo = MapPreviewSprites.AttachPhoto(frame);
                Assert.That(frame.GetComponent<Mask>(), Is.Not.Null, "둥근 상자가 사진을 잘라내는 마스크가 된다.");
                Assert.That(frame.GetComponent<Mask>().showMaskGraphic, Is.True, "사진이 없을 때 상자 색이 보여야 한다.");
                Assert.That(photo.gameObject.activeSelf, Is.False);
                Assert.That(MapPreviewSprites.FindPhoto(frame), Is.SameAs(photo));

                var sprite = Sprite.Create(new Texture2D(4, 3), new Rect(0, 0, 4, 3), new Vector2(0.5f, 0.5f));
                MapPreviewSprites.Apply(photo, sprite, MapPreviewSprites.DimmedTint);
                Assert.That(photo.gameObject.activeSelf, Is.True);
                Assert.That(photo.sprite, Is.SameAs(sprite));
                Assert.That(photo.color, Is.EqualTo(MapPreviewSprites.DimmedTint));

                MapPreviewSprites.Apply(photo, null);
                Assert.That(photo.gameObject.activeSelf, Is.False, "사진이 없으면 숨겨 상자 색만 남긴다.");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyMatchInfoView_SetInfoWithSprite_ShowsPhotoInsideRoundedPreview()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyMatchInfoView.Create(canvas.transform);
                var preview = view.transform.Find("MapRow/MapPreview");
                var photo = preview.Find(MapPreviewSprites.PhotoChildName);
                Assert.That(photo, Is.Not.Null);
                Assert.That(view.MapPreviewSprite, Is.Null);

                var sprite = Sprite.Create(new Texture2D(4, 3), new Rect(0, 0, 4, 3), new Vector2(0.5f, 0.5f));
                view.SetInfo("랜덤", "supermarket", sprite);
                Assert.That(view.MapPreviewSprite, Is.SameAs(sprite));
                Assert.That(view.MapLabel, Is.EqualTo("supermarket"));

                view.SetInfo("랜덤", "랜덤");
                Assert.That(view.MapPreviewSprite, Is.Null, "사진 없이 호출하면 다시 단색 상자로 돌아간다.");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
