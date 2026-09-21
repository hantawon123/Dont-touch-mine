using Game.Client.Tutorial;
using Game.Core.Settings;
using Game.Core.Tutorial;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The tutorial scene injects nothing, so the checklist has to read the
    /// applied language itself rather than wait to be handed a locale.
    /// </summary>
    public sealed class TutorialChecklistViewTests
    {
        [Test]
        public void Checklist_IsBuiltAndRedrawnInTheAppliedLanguage()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            using var locale = new UiLocale(new GeneralSettingsSystem(store));
            var canvas = new GameObject("TutorialHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = TutorialChecklistView.Create(canvas.transform);

                Assert.That(Row(canvas, "Title"), Is.EqualTo("Thief Basics"));
                Assert.That(Row(canvas, "Step0"), Does.Contain("Look around"));

                view.Show(TutorialStep.Prone, string.Empty);

                Assert.That(Row(canvas, "Step4"), Does.Contain("Crawl through"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Checklist_GrowsToTheLongestLine()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            using var locale = new UiLocale(new GeneralSettingsSystem(store));
            var canvas = new GameObject("TutorialHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = TutorialChecklistView.Create(canvas.transform);
                view.Show(TutorialStep.Drop, "Q/E to turn · scroll or click to place");
                var panel = (RectTransform)view.transform;
                var drop = canvas.transform.Find("TutorialChecklist/Step6").GetComponent<TMP_Text>();
                var hint = canvas.transform.Find("TutorialChecklist/CurrentAction").GetComponent<TMP_Text>();
                var needed = Mathf.Max(
                    drop.GetPreferredValues(drop.text).x,
                    hint.GetPreferredValues(hint.text).x)
                    + TutorialChecklistView.SidePadding * 2f;

                Assert.That(panel.sizeDelta.x, Is.GreaterThanOrEqualTo(needed));
                Assert.That(panel.sizeDelta.x, Is.GreaterThanOrEqualTo(TutorialChecklistView.MinWidth));
                Assert.That(panel.sizeDelta.y, Is.EqualTo(TutorialChecklistView.PanelHeight));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void Checklist_StaysKoreanWhenNoLanguageIsApplied()
        {
            var canvas = new GameObject("TutorialHud", typeof(RectTransform), typeof(Canvas));
            try
            {
                var view = TutorialChecklistView.Create(canvas.transform);
                view.Show(TutorialStep.MoveAndLook, string.Empty);

                Assert.That(Row(canvas, "Title"), Is.EqualTo("도둑의 기본 훈련"));
                Assert.That(Row(canvas, "Step0"), Does.Contain("주변 살피기"));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        private static string Row(GameObject canvas, string name) =>
            canvas.transform.Find("TutorialChecklist/" + name).GetComponent<TMP_Text>().text;
    }
}
