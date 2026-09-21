using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the locale reads from the applied language, and when it tells
    /// listeners that the words have moved.
    /// </summary>
    public sealed class UiLocaleTests
    {
        private static readonly LanguageCatalog TwoLanguages = new LanguageCatalog(
            new Language("ko", "한국어"),
            new Language("en", "English"));

        private static readonly UiTextCatalog TwoLines = new UiTextCatalog(
            new UiTextLine("greeting", "안녕하세요", "Hello"),
            new UiTextLine("farewell", "안녕히 가세요"));

        [Test]
        public void Opening_WithNothingSaved_ReadsKorean()
        {
            var general = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore(), TwoLanguages);
            using var locale = new UiLocale(general, TwoLines);

            Assert.That(locale.LanguageCode, Is.EqualTo("ko"));
            Assert.That(locale.Get("greeting"), Is.EqualTo("안녕하세요"));
        }

        [Test]
        public void Opening_ReadsTheLanguageThatWasSaved()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            var general = new GeneralSettingsSystem(store, TwoLanguages);
            using var locale = new UiLocale(general, TwoLines);

            Assert.That(locale.LanguageCode, Is.EqualTo("en"));
            Assert.That(locale.Get("greeting"), Is.EqualTo("Hello"));
        }

        [Test]
        public void Get_WithoutATranslation_FallsBackToKorean()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            var general = new GeneralSettingsSystem(store, TwoLanguages);
            using var locale = new UiLocale(general, TwoLines);

            Assert.That(locale.Get("farewell"), Is.EqualTo("안녕히 가세요"));
        }

        [Test]
        public void Apply_ToEnglish_MovesTheWords_AndTellsListeners()
        {
            var general = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore(), TwoLanguages);
            using var locale = new UiLocale(general, TwoLines);
            var changes = 0;
            locale.Changed += () => changes++;

            general.Apply(new GeneralSettings("en"));

            Assert.That(locale.LanguageCode, Is.EqualTo("en"));
            Assert.That(locale.Get("greeting"), Is.EqualTo("Hello"));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Apply_WithTheSameLanguage_SaysNothing()
        {
            var general = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore(), TwoLanguages);
            using var locale = new UiLocale(general, TwoLines);
            var changes = 0;
            locale.Changed += () => changes++;

            general.Apply(general.Current);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(locale.Get("greeting"), Is.EqualTo("안녕하세요"));
        }

        [Test]
        public void Opening_DoesNotTellListeners()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            var general = new GeneralSettingsSystem(store, TwoLanguages);
            var changes = 0;
            using var locale = new UiLocale(general, TwoLines);
            locale.Changed += () => changes++;

            Assert.That(changes, Is.EqualTo(0), "Nothing is applied after construction.");
            Assert.That(locale.Get("greeting"), Is.EqualTo("Hello"));
        }
    }
}
