using Game.Client.Match;
using Game.Core.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class DestroyedItemsHudViewTests
    {
        [Test]
        public void Show_KeepsEmptyCirclesBesideLocalSlotBeforeAnyDestruction()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = DestroyedItemsHudView.Create(canvas.transform);
                view.Show(
                    6,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", false),
                    },
                    "Soda_01",
                    System.Array.Empty<string>());

                var panel = view.transform.Find("Panel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Assert.That(DestroyedItemsHudView.SlotSize, Is.EqualTo(100f));
                Assert.That(DestroyedItemsHudView.OwnMarkerSize, Is.EqualTo(100f));
                Assert.That(DestroyedItemsHudView.PreviewTextureSize, Is.EqualTo(256));
                Assert.That(DestroyedItemsHudView.QuestionFontSize, Is.EqualTo(30f));

                var slot = panel.Find("Slot0");
                Assert.That(slot, Is.Not.Null);
                var fill = slot.Find(DestroyedItemsHudView.FillName)?.GetComponent<Image>();
                Assert.That(fill, Is.Not.Null);
                Assert.That(fill.color, Is.EqualTo(DestroyedItemsHudView.SlotColor));
                Assert.That(slot.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(100f));
                Assert.That(slot.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(100f));
                Assert.That(
                    slot.Find(DestroyedItemsHudView.OwnMarkerName).gameObject.activeSelf,
                    Is.True);
                var empty = panel.Find("Slot1");
                Assert.That(empty, Is.Not.Null);
                Assert.That(
                    empty.Find(DestroyedItemsHudView.OwnMarkerName).gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    empty.Find($"{DestroyedItemsHudView.FillName}/Question")
                        .gameObject.activeSelf,
                    Is.True);
                Assert.That(panel.Find("Slot5"), Is.Not.Null);
                Assert.That(panel.Find("Slot6"), Is.Null);

                view.SetCategory("과일");
                var category = view.transform.Find(DestroyedItemsHudView.CategoryName)
                    ?.GetComponent<TMP_Text>();
                Assert.That(category, Is.Not.Null);
                Assert.That(category.gameObject.activeSelf, Is.True);
                Assert.That(category.text, Is.EqualTo("과일"));
                Assert.That(category.fontSize, Is.EqualTo(DestroyedItemsHudView.CategoryFontSize));
                Assert.That(category.alignment, Is.EqualTo(TextAlignmentOptions.TopLeft));
                Assert.That(
                    category.rectTransform.anchoredPosition,
                    Is.EqualTo(DestroyedItemsHudView.CategoryAnchoredPosition));
                Assert.That(
                    category.rectTransform.anchorMin,
                    Is.EqualTo(new Vector2(0f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_KeepsPlayerCountCirclesWhenSomeItemsAreDestroyed()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = DestroyedItemsHudView.Create(canvas.transform);
                view.Show(
                    3,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", true),
                        new PlayerItemStatusSnapshot("Burger_01", false),
                        new PlayerItemStatusSnapshot("Pineapple_01", false),
                    });

                var panel = view.transform.Find("Panel");
                Assert.That(panel.Find("Slot0"), Is.Not.Null);
                Assert.That(panel.Find("Slot1"), Is.Not.Null);
                Assert.That(panel.Find("Slot2"), Is.Not.Null);
                Assert.That(panel.Find("Slot3"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_KeepsLocalSlotLeftmostAfterMoreItemsAreDestroyed()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = DestroyedItemsHudView.Create(canvas.transform);
                view.Show(
                    4,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", false),
                    },
                    "Soda_01",
                    System.Array.Empty<string>());

                view.Show(
                    4,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", true),
                        new PlayerItemStatusSnapshot("Pineapple_01", true),
                    },
                    "Soda_01",
                    new[] { "Burger_01", "Pineapple_01" });

                var panel = view.transform.Find("Panel");
                var slot0 = panel.Find("Slot0");
                var slot1 = panel.Find("Slot1");
                var slot2 = panel.Find("Slot2");
                Assert.That(slot0.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(slot1.GetSiblingIndex(), Is.EqualTo(1));
                Assert.That(slot2.GetSiblingIndex(), Is.EqualTo(2));
                Assert.That(
                    slot0.Find(DestroyedItemsHudView.OwnMarkerName).gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    slot1.Find(DestroyedItemsHudView.OwnMarkerName).gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    slot2.Find(DestroyedItemsHudView.OwnMarkerName).gameObject.activeSelf,
                    Is.False);
                Assert.That(view.transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_MarksLocalSlotWithOwnItemIcon()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = DestroyedItemsHudView.Create(canvas.transform);
                view.Show(
                    3,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", true),
                        new PlayerItemStatusSnapshot("Pineapple_01", false),
                    },
                    "Soda_01",
                    new[] { "Burger_01" });

                var ownMarker = view.transform.Find(
                    $"Panel/Slot0/{DestroyedItemsHudView.OwnMarkerName}")
                    ?.GetComponent<Image>();
                Assert.That(ownMarker, Is.Not.Null);
                Assert.That(ownMarker.gameObject.activeSelf, Is.True);
                Assert.That(ownMarker.sprite, Is.EqualTo(DestroyedItemsHudView.OwnItemSprite));
                Assert.That(ownMarker.preserveAspect, Is.False);
                var markerRect = (RectTransform)ownMarker.transform;
                Assert.That(markerRect.sizeDelta, Is.EqualTo(new Vector2(
                    DestroyedItemsHudView.OwnMarkerSize,
                    DestroyedItemsHudView.OwnMarkerSize)));
                var ownFill = view.transform.Find(
                    $"Panel/Slot0/{DestroyedItemsHudView.FillName}")
                    ?.GetComponent<Image>();
                var otherFill = view.transform.Find(
                    $"Panel/Slot1/{DestroyedItemsHudView.FillName}")
                    ?.GetComponent<Image>();
                Assert.That(ownFill.color, Is.EqualTo(DestroyedItemsHudView.SlotColor));
                Assert.That(otherFill.color, Is.EqualTo(DestroyedItemsHudView.SlotColor));
                Assert.That(((RectTransform)ownFill.transform).offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(
                    view.transform.Find($"Panel/Slot1/{DestroyedItemsHudView.OwnMarkerName}")
                        .gameObject.activeSelf,
                    Is.False);
                Assert.That(view.transform.Find("Panel/Slot2"), Is.Not.Null);
                Assert.That(
                    view.transform.Find($"Panel/Slot2/{DestroyedItemsHudView.FillName}/Question")
                        .gameObject.activeSelf,
                    Is.True);
                Assert.That(view.transform.Find("Panel/Slot3"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Show_KeepsDefaultPreviewMaterialWhenLocalItemIsDestroyed()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = DestroyedItemsHudView.Create(canvas.transform);
                view.Show(
                    2,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", true),
                        new PlayerItemStatusSnapshot("Burger_01", true),
                    },
                    "Soda_01",
                    new[] { "Burger_01", "Soda_01" });

                var ownPreview = view.transform.Find(
                    $"Panel/Slot0/{DestroyedItemsHudView.FillName}/Preview")
                    ?.GetComponent<RawImage>();
                var otherPreview = view.transform.Find(
                    $"Panel/Slot1/{DestroyedItemsHudView.FillName}/Preview")
                    ?.GetComponent<RawImage>();
                Assert.That(ownPreview, Is.Not.Null);
                Assert.That(otherPreview, Is.Not.Null);
                Assert.That(ownPreview.material, Is.EqualTo(ownPreview.defaultMaterial));
                Assert.That(otherPreview.material, Is.EqualTo(otherPreview.defaultMaterial));
                var ownMarker = view.transform.Find(
                    $"Panel/Slot0/{DestroyedItemsHudView.OwnMarkerName}")
                    ?.GetComponent<Image>();
                Assert.That(ownMarker, Is.Not.Null);
                Assert.That(ownMarker.gameObject.activeSelf, Is.True);
                Assert.That(ownMarker.sprite, Is.EqualTo(DestroyedItemsHudView.OwnDestroyedItemSprite));
                Assert.That(
                    ((RectTransform)ownMarker.transform).sizeDelta,
                    Is.EqualTo(new Vector2(
                        DestroyedItemsHudView.OwnMarkerSize,
                        DestroyedItemsHudView.OwnMarkerSize)));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void NetworkHud_KeepsDestroyedItemsVisibleWhenPlayerStatusIsHidden()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                hud.SetPhase(MatchPhase.Searching, string.Empty);
                hud.SetDestroyedItems(
                    2,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", false),
                    },
                    "Soda_01",
                    System.Array.Empty<string>());
                hud.SetPlayerStatusVisible(false);

                var panel = hud.transform.Find("DestroyedItems/Panel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Assert.That(
                    panel.Find($"Slot0/{DestroyedItemsHudView.OwnMarkerName}")
                        .gameObject.activeSelf,
                    Is.True);
                Assert.That(panel.Find("Slot1"), Is.Not.Null);
                Assert.That(
                    panel.Find($"Slot1/{DestroyedItemsHudView.FillName}/Question")
                        .gameObject.activeSelf,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [TestCase(MatchPhase.Hiding)]
        [TestCase(MatchPhase.Highlight)]
        [TestCase(MatchPhase.Result)]
        public void NetworkHud_HidesDestroyedItemsOutsideSearching(MatchPhase phase)
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hud = canvas.AddComponent<NetworkMatchHudView>();
                hud.SetPhase(MatchPhase.Searching, string.Empty);
                hud.SetDestroyedItems(
                    2,
                    new[]
                    {
                        new PlayerItemStatusSnapshot("Soda_01", false),
                        new PlayerItemStatusSnapshot("Burger_01", true),
                    },
                    "Soda_01",
                    new[] { "Burger_01" });

                var panel = hud.transform.Find("DestroyedItems/Panel");
                Assert.That(panel.gameObject.activeSelf, Is.True);

                hud.SetPhase(phase, string.Empty);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                var category = hud.transform.Find("DestroyedItems/" + DestroyedItemsHudView.CategoryName);
                Assert.That(category == null || !category.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
