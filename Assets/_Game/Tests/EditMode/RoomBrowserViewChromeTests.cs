using Game.Client.Rooms;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class RoomBrowserViewChromeTests
    {
        [Test]
        public void ShowChrome_RedrawsEnterByCodeInTheAppliedLanguage()
        {
            var root = new GameObject("Room Browser");
            try
            {
                var view = root.AddComponent<RoomBrowserView>();
                typeof(RoomBrowserView).GetMethod(
                        "Awake",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);

                var store = new InMemoryGeneralSettingsStore();
                store.Save(new GeneralSettings("en"));
                var general = new GeneralSettingsSystem(store);
                using var locale = new UiLocale(general);
                view.ShowChrome(locale);

                var codeTitle = Find(root, "CodePanel")?.Find("Title")?.GetComponent<TMPro.TMP_Text>();
                Assert.That(codeTitle, Is.Not.Null);
                Assert.That(codeTitle.text, Is.EqualTo("Join by Code"));

                view.ShowDisconnection("gone");
                var toastTitle = Find(root, "ConnectionToast")?.Find("Title")
                    ?.GetComponent<TMPro.TMP_Text>();
                Assert.That(toastTitle, Is.Not.Null);
                Assert.That(toastTitle.text, Is.EqualTo("Connection Error"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
