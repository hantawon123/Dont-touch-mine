using System;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the 그래픽 rows offer, what they start on, and what applying them
    /// does.
    /// </summary>
    public sealed class GraphicsSettingsTests
    {
        private static readonly GraphicsOption[] AllOptions =
            (GraphicsOption[])Enum.GetValues(typeof(GraphicsOption));

        [Test]
        public void ShippedCatalogue_OffersEveryRowTheDesignLists()
        {
            var catalog = GraphicsCatalog.Shipped;

            var counts = new Dictionary<GraphicsOption, int>();
            foreach (var option in AllOptions)
            {
                counts[option] = catalog.For(option).All.Count;
            }

            Assert.That(counts[GraphicsOption.DisplayMode], Is.EqualTo(2));
            Assert.That(counts[GraphicsOption.Resolution], Is.EqualTo(5));
            Assert.That(counts[GraphicsOption.FpsLimit], Is.EqualTo(4));
            Assert.That(counts[GraphicsOption.TextureQuality], Is.EqualTo(3));

            foreach (var option in AllOptions)
            {
                Assert.That(
                    catalog.For(option).CanStep,
                    Is.True,
                    $"{option} has nothing to step between.");
            }
        }

        [Test]
        public void ShippedDefaults_AreTheValuesTheMockUpShows()
        {
            var defaults = GraphicsCatalog.Shipped.Defaults;

            Assert.That(defaults.Get(GraphicsOption.DisplayMode), Is.EqualTo("fullscreen"));
            Assert.That(defaults.Get(GraphicsOption.Resolution), Is.EqualTo("1920x1080"));
            Assert.That(defaults.Get(GraphicsOption.FpsLimit), Is.EqualTo("120"));
            Assert.That(defaults.Get(GraphicsOption.TextureQuality), Is.EqualTo("medium"));
        }

        [Test]
        public void Settings_AreEqualOnlyWhenEveryRowMatches()
        {
            var defaults = GraphicsCatalog.Shipped.Defaults;

            Assert.That(defaults, Is.EqualTo(GraphicsCatalog.Shipped.Defaults));
            Assert.That(defaults.GetHashCode(), Is.EqualTo(GraphicsCatalog.Shipped.Defaults.GetHashCode()));

            var moved = defaults.With(GraphicsOption.TextureQuality, "low");
            Assert.That(moved, Is.Not.EqualTo(defaults));
            Assert.That(
                moved.Get(GraphicsOption.Resolution),
                Is.EqualTo(defaults.Get(GraphicsOption.Resolution)),
                "Changing one row must leave the others alone.");
        }

        [Test]
        public void Stepping_WalksTheListAndWraps()
        {
            var resolutions = GraphicsCatalog.Shipped.For(GraphicsOption.Resolution);

            Assert.That(resolutions.Step("1920x1080", 1).Code, Is.EqualTo("2560x1440"));
            Assert.That(resolutions.Step("1920x1080", -1).Code, Is.EqualTo("1600x900"));
            Assert.That(resolutions.Step("1280x720", -1).Code, Is.EqualTo("3840x2160"));
            Assert.That(resolutions.Step("3840x2160", 1).Code, Is.EqualTo("1280x720"));
            Assert.That(
                resolutions.Step("800x600", 1).Code,
                Is.EqualTo("2560x1440"),
                "An unlisted code steps from the default.");
        }

        [Test]
        public void Opening_WithNothingSaved_StartsFromTheDefaults()
        {
            var system = new GraphicsSettingsSystem(new InMemoryGraphicsSettingsStore());

            Assert.That(system.Current, Is.EqualTo(GraphicsCatalog.Shipped.Defaults));
        }

        [Test]
        public void Opening_FillsInRowsTheSaveDoesNotCover()
        {
            var store = new InMemoryGraphicsSettingsStore();

            // What a save written before a row existed looks like.
            store.Save(GraphicsSettings.Empty.With(GraphicsOption.TextureQuality, "low"));

            var system = new GraphicsSettingsSystem(store);

            Assert.That(system.Current.Get(GraphicsOption.TextureQuality), Is.EqualTo("low"));
            Assert.That(
                system.Current.Get(GraphicsOption.Resolution),
                Is.EqualTo("1920x1080"),
                "A row with nothing saved takes its default.");
        }

        [Test]
        public void Opening_DropsACodeTheCatalogueNoLongerLists()
        {
            var store = new InMemoryGraphicsSettingsStore();
            store.Save(GraphicsCatalog.Shipped.Defaults.With(GraphicsOption.Resolution, "640x480"));

            var system = new GraphicsSettingsSystem(store);

            Assert.That(system.Current.Get(GraphicsOption.Resolution), Is.EqualTo("1920x1080"));
        }

        [Test]
        public void Opening_ChangesNothingAboutThePicture()
        {
            var applier = new NullGraphicsSettingsApplier();

            _ = new GraphicsSettingsSystem(new InMemoryGraphicsSettingsStore(), applier);

            Assert.That(
                applier.ApplyCount,
                Is.EqualTo(0),
                "Building a container must not resize anybody's window.");
        }

        [Test]
        public void Apply_Saves_ReachesTheRenderer_AndTellsListeners()
        {
            var store = new InMemoryGraphicsSettingsStore();
            var applier = new NullGraphicsSettingsApplier();
            var system = new GraphicsSettingsSystem(store, applier);
            var changes = new List<GraphicsSettings>();
            system.Changed += changes.Add;

            var wanted = system.Defaults.With(GraphicsOption.TextureQuality, "low");
            system.Apply(wanted);

            Assert.That(system.Current, Is.EqualTo(wanted));
            Assert.That(store.Saved, Is.EqualTo(wanted));
            Assert.That(applier.Applied, Is.EqualTo(wanted));
            Assert.That(changes, Is.EqualTo(new[] { wanted }));
        }

        [Test]
        public void Apply_WithTheSameValues_SaysNothing()
        {
            var store = new InMemoryGraphicsSettingsStore();
            var applier = new NullGraphicsSettingsApplier();
            var system = new GraphicsSettingsSystem(store, applier);
            var changes = 0;
            system.Changed += _ => changes++;

            system.Apply(system.Current);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(applier.ApplyCount, Is.EqualTo(0));
            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void ApplyToRenderer_CarriesWhatIsInForce_WithoutSaving()
        {
            var store = new InMemoryGraphicsSettingsStore();
            var applier = new NullGraphicsSettingsApplier();
            var system = new GraphicsSettingsSystem(store, applier);

            system.ApplyToRenderer();

            Assert.That(applier.Applied, Is.EqualTo(system.Current));
            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void EveryResolutionCode_ReadsAsAWidthAndHeight()
        {
            foreach (var choice in GraphicsCatalog.Shipped.For(GraphicsOption.Resolution).All)
            {
                Assert.That(
                    UnityGraphicsSettingsApplier.TryReadResolution(
                        choice.Code, out var width, out var height),
                    Is.True,
                    $"'{choice.Code}' is not a size the applier can read.");
                Assert.That(width, Is.GreaterThan(0));
                Assert.That(height, Is.GreaterThan(0));
            }
        }

        [Test]
        public void ANonsenseResolution_IsRefusedRatherThanGuessed()
        {
            Assert.That(
                UnityGraphicsSettingsApplier.TryReadResolution("wide", out _, out _), Is.False);
            Assert.That(
                UnityGraphicsSettingsApplier.TryReadResolution("1920", out _, out _), Is.False);
            Assert.That(
                UnityGraphicsSettingsApplier.TryReadResolution("0x0", out _, out _), Is.False);
            Assert.That(
                UnityGraphicsSettingsApplier.TryReadResolution(string.Empty, out _, out _), Is.False);
        }

        [Test]
        public void EveryFpsCode_ReadsAsAPositiveNumber()
        {
            foreach (var choice in GraphicsCatalog.Shipped.For(GraphicsOption.FpsLimit).All)
            {
                Assert.That(int.TryParse(choice.Code, out var fps), Is.True, choice.Code);
                Assert.That(fps, Is.GreaterThan(0));
            }
        }

        [Test]
        public void ACatalogueMissingARow_IsRefusedWhenItIsBuilt()
        {
            var incomplete = new Dictionary<GraphicsOption, OptionChoices>
            {
                [GraphicsOption.DisplayMode] = new OptionChoices(
                    new OptionChoice("fullscreen", "전체화면"))
            };

            Assert.That(
                () => new GraphicsCatalog(incomplete),
                Throws.TypeOf<ArgumentException>(),
                "A row with no choices would draw an empty picker.");
        }

        [Test]
        public void ADefaultThatIsNotOneOfTheChoices_IsRefused()
        {
            Assert.That(
                () => new OptionChoices("nope", new OptionChoice("high", "높음")),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
