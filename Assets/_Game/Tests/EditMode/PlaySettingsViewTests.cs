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
            var root = new GameObject("Settings title layout", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("PlaySettingsPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<PlaySettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("panel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                typeof(PlaySettingsView).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);

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
