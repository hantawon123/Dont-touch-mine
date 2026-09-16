using Game.Client.Character;
using Game.Client.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class CharacterClosetViewLobbyChromeTests
    {
        [Test]
        public void ConfigureAsLobbyOverlay_UsesTheSettingsFrameAndInsetChrome()
        {
            var root = new GameObject("Lobby Character Closet");
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<CharacterClosetView>();
                view.ConfigureAsLobbyOverlay();
                root.SetActive(true);
                typeof(CharacterClosetView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                var frame = FindPanel(root, SettingsStyle.Frame.Size);
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.sizeDelta, Is.EqualTo(SettingsStyle.Frame.Size));
                Assert.That(
                    frame.localScale,
                    Is.EqualTo(Vector3.one * SettingsStyle.Frame.LobbyScale));
                Assert.That(
                    frame.anchoredPosition,
                    Is.EqualTo(
                        SettingsStyle.Frame.Position
                        + new Vector2(
                            SettingsStyle.Frame.Size.x * 0.5f,
                            -SettingsStyle.Frame.Size.y * 0.5f)));

                Assert.That(Find(root, "Background"), Is.Null);
                Assert.That(Find(root, "Glow"), Is.Null);

                var fill = frame.GetComponent<Image>();
                Assert.That(fill.color, Is.EqualTo(CharacterClosetStyle.Overlay.PanelFill));

                var stroke = Find(root, "Stroke").GetComponent<Image>();
                Assert.That(stroke.color, Is.EqualTo(CharacterClosetStyle.Overlay.Border));
                Assert.That(stroke.transform.parent, Is.EqualTo(frame));

                var rail = Find(root, "CategoryRail");
                Assert.That(rail, Is.Not.Null);
                Assert.That(rail.parent.name, Is.EqualTo("Panel"));

                var locker = Find(root, "Locker").GetComponent<RectTransform>();
                Assert.That(locker.parent.name, Is.EqualTo("Panel"));
                Assert.That(
                    locker.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -CharacterClosetStyle.Overlay.LockerMargin.x,
                        -CharacterClosetStyle.Overlay.LockerMargin.y)));

                Assert.That(Find(root, "BackButton"), Is.Null);

                var actionBar = Find(root, "ActionBar").GetComponent<RectTransform>();
                Assert.That(actionBar.parent.name, Is.EqualTo("Panel"));
                Assert.That(actionBar.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(actionBar.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(actionBar.pivot, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(
                    actionBar.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -CharacterClosetStyle.Overlay.ButtonsRightInset,
                        CharacterClosetStyle.Overlay.ButtonsBottom)));
                Assert.That(
                    actionBar.sizeDelta,
                    Is.EqualTo(CharacterClosetStyle.Overlay.ActionBarSize));

                var reset = Find(root, "ResetButton").GetComponent<RectTransform>();
                var apply = Find(root, "ApplyButton").GetComponent<RectTransform>();
                Assert.That(reset.parent, Is.EqualTo(actionBar));
                Assert.That(apply.parent, Is.EqualTo(actionBar));
                Assert.That(apply.anchoredPosition.x, Is.GreaterThan(reset.anchoredPosition.x));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HomeLayout_FillsTheScreenWithoutASettingsPanel()
        {
            var root = new GameObject("Home Closet");
            try
            {
                var view = root.AddComponent<CharacterClosetView>();
                typeof(CharacterClosetView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                Assert.That(FindPanel(root, SettingsStyle.Frame.Size), Is.Null);
                Assert.That(Find(root, "Glow"), Is.Null);
                Assert.That(Find(root, "Background"), Is.Not.Null);
                Assert.That(Find(root, "CategoryRail").parent.name, Is.EqualTo("ClosetCanvas"));
                Assert.That(Find(root, "BackButton"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform FindPanel(GameObject root, Vector2 size)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != "Panel")
                {
                    continue;
                }

                var rect = transform.GetComponent<RectTransform>();
                if (rect != null && rect.sizeDelta == size)
                {
                    return rect;
                }
            }

            return null;
        }

        private static Transform Find(GameObject root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform;
                }
            }

            return null;
        }
    }
}
