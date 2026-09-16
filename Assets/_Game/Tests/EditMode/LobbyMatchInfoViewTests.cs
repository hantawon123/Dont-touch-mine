using Game.Client.Home;
using Game.Client.Lobby;
using Game.Client.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class LobbyMatchInfoViewTests
    {
        [Test]
        public void Create_PlacesTheCardInTheTopLeft_LinedUpWithChat()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyMatchInfoView.Create(canvas.transform);
                var rect = view.GetComponent<RectTransform>();

                Assert.That(view.name, Is.EqualTo(LobbyMatchInfoView.RootName));
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(
                    new Vector2(LobbyMatchInfoView.MarginLeft, -LobbyMatchInfoView.MarginTop)));
                Assert.That(LobbyMatchInfoView.MarginLeft, Is.EqualTo(MatchChatView.Margin));
                Assert.That(rect.sizeDelta.x, Is.EqualTo(LobbyMatchInfoView.Width));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Create_UsesSpecifiedTypeAndMapPreviewSize()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyMatchInfoView.Create(canvas.transform);
                var innerCard = view.transform.Find("CategoryRow/CategoryValueCard") as RectTransform;
                var category = view.transform.Find("CategoryRow/CategoryValueCard/CategoryValue")
                    .GetComponent<TMP_Text>();
                var mapName = view.transform.Find("MapRow/MapName").GetComponent<TMP_Text>();
                var preview = view.transform.Find("MapRow/MapPreview") as RectTransform;
                var mapRow = view.transform.Find("MapRow") as RectTransform;
                var categoryRow = view.transform.Find("CategoryRow") as RectTransform;

                Assert.That(view.transform.Find("CategoryRow/CategoryCaption"), Is.Null);
                Assert.That(view.transform.Find("CategoryRow/CategoryPreview"), Is.Null);
                Assert.That(view.transform.Find("CategoryRow/Divider"), Is.Null);
                Assert.That(innerCard, Is.Not.Null);
                Assert.That(
                    innerCard.GetComponent<Image>().color,
                    Is.EqualTo(Color.white));
                Assert.That(LobbyMatchInfoView.CategoryInnerFill, Is.EqualTo(Color.white));
                Assert.That(category.fontSize, Is.EqualTo(LobbyMatchInfoView.FontSize));
                Assert.That(category.color, Is.EqualTo(Color.black));
                Assert.That(category.font, Is.EqualTo(HomeUiFonts.Apply()));
                Assert.That(category.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                Assert.That(mapName.fontSize, Is.EqualTo(LobbyMatchInfoView.FontSize));
                Assert.That(mapName.font, Is.EqualTo(HomeUiFonts.Apply()));
                Assert.That(mapName.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                Assert.That(
                    mapRow.GetComponent<Image>().color,
                    Is.EqualTo(LobbyMatchInfoView.MapRowFill));
                Assert.That(
                    categoryRow.GetComponent<Image>().color,
                    Is.EqualTo(LobbyMatchInfoView.MapRowFill));
                Assert.That(LobbyMatchInfoView.MapRowFill, Is.EqualTo(Color.black));
                Assert.That(LobbyMatchInfoView.MapRowFill.a, Is.EqualTo(1f));
                Assert.That(preview.sizeDelta, Is.EqualTo(LobbyMatchInfoView.MapPreviewSize));
                Assert.That(
                    LobbyMatchInfoView.MapPreviewSize.x,
                    Is.EqualTo(LobbyMatchInfoView.Width - (LobbyMatchInfoView.MapRowPadding * 2f)));
                Assert.That(
                    mapRow.GetSiblingIndex(),
                    Is.LessThan(categoryRow.GetSiblingIndex()));
                Assert.That(categoryRow.GetComponent<Image>(), Is.Not.Null);
                Assert.That(mapRow.GetComponent<Image>(), Is.Not.Null);
                Assert.That(
                    view.GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(LobbyMatchInfoView.Width, LobbyMatchInfoView.PanelHeight)));
                Assert.That(LobbyMatchInfoView.Width, Is.EqualTo(320f * 1.8f));
                Assert.That(LobbyMatchInfoView.FontSize, Is.EqualTo(32f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Ensure_RebuildsTheOldCaptionRowIntoABoxedCategory()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var root = new GameObject(LobbyMatchInfoView.RootName, typeof(RectTransform));
                root.transform.SetParent(canvas.transform, false);
                var caption = new GameObject("CategoryRow", typeof(RectTransform));
                caption.transform.SetParent(root.transform, false);
                new GameObject("CategoryCaption", typeof(RectTransform), typeof(TextMeshProUGUI))
                    .transform.SetParent(caption.transform, false);
                new GameObject("CategoryValue", typeof(RectTransform), typeof(TextMeshProUGUI))
                    .transform.SetParent(caption.transform, false);
                var mapRow = new GameObject("MapRow", typeof(RectTransform));
                mapRow.transform.SetParent(root.transform, false);
                new GameObject("MapName", typeof(RectTransform), typeof(TextMeshProUGUI))
                    .transform.SetParent(mapRow.transform, false);

                var view = LobbyMatchInfoView.Ensure(canvas.transform);

                Assert.That(view.transform.Find("CategoryRow/CategoryCaption"), Is.Null);
                Assert.That(view.transform.Find("CategoryRow/Divider"), Is.Null);
                Assert.That(view.transform.Find("CategoryRow/CategoryValueCard"), Is.Not.Null);
                Assert.That(view.transform.Find("CategoryRow/CategoryPreview"), Is.Null);
                Assert.That(
                    view.transform.Find("CategoryRow/CategoryValueCard/CategoryValue"),
                    Is.Not.Null);
                Assert.That(view.transform.Find("MapRow/MapPreview"), Is.Not.Null);
                Assert.That(
                    (view.transform.Find("MapRow/MapPreview") as RectTransform).sizeDelta,
                    Is.EqualTo(LobbyMatchInfoView.MapPreviewSize));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SetInfo_ReplacesCategoryAndMapLabels()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyMatchInfoView.Create(canvas.transform);
                view.SetInfo("과일", "playground");

                Assert.That(view.CategoryLabel, Is.EqualTo("과일"));
                Assert.That(view.MapLabel, Is.EqualTo("playground"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyHud_AttachesTheCard()
        {
            var canvas = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                var info = hud.EnsureMatchInfo();

                Assert.That(info, Is.Not.Null);
                Assert.That(
                    canvas.transform.Find(LobbyMatchInfoView.RootName),
                    Is.SameAs(info.transform));
                Assert.That(info.GetComponent<Image>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
