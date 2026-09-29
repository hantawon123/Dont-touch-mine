using Game.Client.Home;
using Game.Client.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class LobbyPlayerCountViewTests
    {
        [Test]
        public void Create_PlacesTheLineBelowTheMatchCard()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyPlayerCountView.Create(canvas.transform);
                var rect = view.GetComponent<RectTransform>();

                Assert.That(view.name, Is.EqualTo(LobbyPlayerCountView.RootName));
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(
                    rect.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        LobbyMatchInfoView.MarginLeft,
                        -LobbyPlayerCountView.TopOffset)));
                Assert.That(LobbyPlayerCountView.GapBelowMatchInfo, Is.EqualTo(24f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Create_UsesBoldCaptionAndRegularCount()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyPlayerCountView.Create(canvas.transform);
                view.SetCount(4, 6);
                var caption = view.transform.Find("Caption").GetComponent<TMP_Text>();
                var count = view.transform.Find("Count").GetComponent<TMP_Text>();

                Assert.That(caption.text, Is.EqualTo(LobbyPlayerCountView.Caption));
                Assert.That(caption.fontSize, Is.EqualTo(LobbyPlayerCountView.FontSize));
                Assert.That(caption.font, Is.EqualTo(HomeUiFonts.ApplyBold()));
                Assert.That(count.text, Is.EqualTo("4/6"));
                Assert.That(count.fontSize, Is.EqualTo(LobbyPlayerCountView.FontSize));
                Assert.That(count.font, Is.EqualTo(HomeUiFonts.ApplyRegular()));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void LobbyHud_HidesTheOldListAndAttachesTheCount()
        {
            var canvas = new GameObject("LobbyHud", typeof(RectTransform), typeof(Canvas));
            var list = new GameObject("PlayerListRoot", typeof(RectTransform));
            list.transform.SetParent(canvas.transform, false);
            try
            {
                var hud = canvas.AddComponent<LobbyHudView>();
                var count = hud.EnsurePlayerCount();

                Assert.That(count, Is.Not.Null);
                Assert.That(list.activeSelf, Is.False);
                Assert.That(
                    canvas.transform.Find(LobbyPlayerCountView.RootName),
                    Is.SameAs(count.transform));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
