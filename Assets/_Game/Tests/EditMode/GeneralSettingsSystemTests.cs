using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the general settings open as, and what applying them does.
    /// </summary>
    public sealed class GeneralSettingsSystemTests
    {
        private static readonly LanguageCatalog TwoLanguages = new LanguageCatalog(
            new Language("ko", "한국어"),
            new Language("en", "English"));

        [Test]
        public void Opening_WithNothingSaved_StartsFromTheDefaults()
        {
            var system = new GeneralSettingsSystem(new InMemoryGeneralSettingsStore(), TwoLanguages);

            Assert.That(system.Current, Is.EqualTo(system.Defaults));
            Assert.That(system.Current.LanguageCode, Is.EqualTo("ko"));
        }

        [Test]
        public void Opening_ReadsWhatWasSaved()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));

            var system = new GeneralSettingsSystem(store, TwoLanguages);

            Assert.That(system.Current.LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void Opening_DropsALanguageTheCatalogueNoLongerLists()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("fr"));

            var system = new GeneralSettingsSystem(store, TwoLanguages);

            Assert.That(system.Current.LanguageCode, Is.EqualTo("ko"));
        }

        [Test]
        public void Apply_Saves_AndTellsListeners()
        {
            var store = new InMemoryGeneralSettingsStore();
            var system = new GeneralSettingsSystem(store, TwoLanguages);
            var changes = new List<GeneralSettings>();
            system.Changed += changes.Add;

            system.Apply(new GeneralSettings("en"));

            Assert.That(system.Current.LanguageCode, Is.EqualTo("en"));
            Assert.That(store.Saved, Is.EqualTo(new GeneralSettings("en")));
            Assert.That(changes, Is.EqualTo(new[] { new GeneralSettings("en") }));
        }

        [Test]
        public void Apply_WithTheSameValues_SaysNothing()
        {
            var store = new InMemoryGeneralSettingsStore();
            var system = new GeneralSettingsSystem(store, TwoLanguages);
            var changes = 0;
            system.Changed += _ => changes++;

            system.Apply(system.Current);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(store.Saved, Is.Null, "Nothing changed, so nothing is written.");
        }

        [Test]
        public void Apply_WithAnUnlistedLanguage_FallsBackToTheDefault()
        {
            var store = new InMemoryGeneralSettingsStore();
            store.Save(new GeneralSettings("en"));
            var system = new GeneralSettingsSystem(store, TwoLanguages);

            system.Apply(new GeneralSettings("fr"));

            Assert.That(system.Current.LanguageCode, Is.EqualTo("ko"));
        }

        [Test]
        public void Catalogue_StepsAndWraps()
        {
            Assert.That(TwoLanguages.Step("ko", 1).Code, Is.EqualTo("en"));
            Assert.That(TwoLanguages.Step("en", 1).Code, Is.EqualTo("ko"));
            Assert.That(TwoLanguages.Step("ko", -1).Code, Is.EqualTo("en"));
            Assert.That(TwoLanguages.Step("unknown", 1).Code, Is.EqualTo("en"),
                "An unlisted code steps from the default.");
            Assert.That(TwoLanguages.CanStep, Is.True);
        }

        [Test]
        public void ShippedCatalogue_DefaultsToKorean_AndCanStep()
        {
            var shipped = LanguageCatalog.Shipped;

            Assert.That(shipped.Default.Code, Is.EqualTo("ko"));
            Assert.That(shipped.CanStep, Is.True);
            Assert.That(shipped.Step("ko", 1).Code, Is.EqualTo("en"));
            Assert.That(shipped.Step("en", 1).Code, Is.EqualTo("ko"));
        }
    }
}
