using Game.Client.Lobby;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class LobbyShortcutOverlayViewTests
    {
        [Test]
        public void Show_SetsThePlaceholderTitle()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutOverlayView.Ensure(canvas.transform);
                view.Show(LobbyShortcutKind.Character);

                Assert.That(view.IsOpen, Is.True);
                Assert.That(view.OpenKind, Is.EqualTo(LobbyShortcutKind.Character));
                Assert.That(
                    canvas.transform.Find("ShortcutOverlay/Title").GetComponent<TMP_Text>().text,
                    Is.EqualTo("캐릭터 설정"));

                view.Show(LobbyShortcutKind.Settings);
                Assert.That(
                    canvas.transform.Find("ShortcutOverlay/Title").GetComponent<TMP_Text>().text,
                    Is.EqualTo("환경설정"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ShowPlayers_RevealsTheBoundParticipantList()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var listGo = new GameObject("PlayerListRoot", typeof(RectTransform));
            listGo.transform.SetParent(canvas.transform, false);
            try
            {
                var list = listGo.AddComponent<LobbyPlayerListView>();
                var view = LobbyShortcutOverlayView.Ensure(canvas.transform);
                view.BindPlayerList(list);

                Assert.That(listGo.activeSelf, Is.False);
                view.Show(LobbyShortcutKind.Players);

                Assert.That(listGo.activeSelf, Is.True);
                Assert.That(listGo.transform.parent.name, Is.EqualTo(LobbyShortcutOverlayView.RootName));
                Assert.That(
                    listGo.GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(LobbyPlayerListView.ModalSize));
                Assert.That(
                    canvas.transform.Find("ShortcutOverlay/Title").gameObject.activeSelf,
                    Is.False);

                view.Hide();
                Assert.That(listGo.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RequestClose_HidesAndRaisesOnce()
        {
            var canvas = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = LobbyShortcutOverlayView.Ensure(canvas.transform);
                var closes = 0;
                view.CloseRequested += () => closes++;
                view.Show(LobbyShortcutKind.Players);
                view.RequestClose();
                view.RequestClose();

                Assert.That(view.IsOpen, Is.False);
                Assert.That(closes, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
