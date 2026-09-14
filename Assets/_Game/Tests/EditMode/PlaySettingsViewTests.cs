using System.Reflection;
using Game.Client.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class PlaySettingsViewTests
    {
        [Test]
        public void TitleInput_LeavesRoomForTheCharacterCounter()
        {
            var root = CreateView(out var panel, out _);
            try
            {
                var input = Find(panel.transform, "TitleInput") as RectTransform;
                var underline = Find(panel.transform, "Underline") as RectTransform;
                var counter = Find(panel.transform, "TitleCounter") as RectTransform;
                Assert.That(input, Is.Not.Null);
                Assert.That(underline, Is.Not.Null);
                Assert.That(counter, Is.Not.Null);
                Assert.That(
                    input.offsetMax.x,
                    Is.EqualTo(-PlaySettingsStyle.Layout.TitleInputRightPadding));
                Assert.That(
                    underline.sizeDelta.x,
                    Is.EqualTo(-PlaySettingsStyle.Layout.TitleInputRightPadding));
                Assert.That(input.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(counter.anchorMin, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(counter.anchorMax, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(
                    counter.sizeDelta.x,
                    Is.EqualTo(PlaySettingsStyle.Layout.TitleCounterWidth));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CloseButton_SitsOnTheModalTopRightCorner()
        {
            var root = CreateView(out var panel, out var view);
            try
            {
                var close = panel.transform.Find("CloseButton") as RectTransform;
                Assert.That(close, Is.Not.Null);
                Assert.That(close.parent, Is.EqualTo(panel.transform));
                Assert.That(close.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(close.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(close.pivot, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(
                    close.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        -PlaySettingsStyle.Overlay.CloseOffset.x,
                        -PlaySettingsStyle.Overlay.CloseOffset.y)));
                Assert.That(
                    close.sizeDelta,
                    Is.EqualTo(new Vector2(
                        PlaySettingsStyle.Overlay.CloseSize,
                        PlaySettingsStyle.Overlay.CloseSize)));
                Assert.That(close.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(Find(root.transform, "BackButton"), Is.Null);

                var overlay = Find(root.transform, "PlaySettingsOverlay");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.Find("CloseButton"), Is.Null);

                var raised = 0;
                view.CloseRequested += () => raised++;
                close.GetComponent<Button>().onClick.Invoke();
                Assert.That(raised, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateView(out GameObject panel, out PlaySettingsView view)
        {
            var root = new GameObject("Settings layout", typeof(RectTransform), typeof(Canvas));
            panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            view = root.AddComponent<PlaySettingsView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            typeof(PlaySettingsView).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            return root;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
