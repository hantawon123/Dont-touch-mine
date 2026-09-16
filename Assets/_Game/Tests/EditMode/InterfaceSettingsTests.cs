using System;
using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the 인터페이스 rows offer, what they start on, and what the shared
    /// row-keeping underneath them promises.
    /// </summary>
    public sealed class InterfaceSettingsTests
    {
        private static readonly InterfaceOption[] AllOptions =
            (InterfaceOption[])Enum.GetValues(typeof(InterfaceOption));

        [Test]
        public void ShippedCatalogue_OffersEveryRowTheDesignLists()
        {
            var catalog = InterfaceCatalog.Shipped;
            var counts = new Dictionary<InterfaceOption, int>();
            foreach (var option in AllOptions)
            {
                counts[option] = catalog.For(option).All.Count;
            }

            Assert.That(counts[InterfaceOption.UiScale], Is.EqualTo(3));
            Assert.That(counts[InterfaceOption.FontScale], Is.EqualTo(3));
            Assert.That(counts[InterfaceOption.InGameUi], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.FpsCounter], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.PingCounter], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.PlayerNames], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.StreamerMode], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.BeginnerGuide], Is.EqualTo(2));
            Assert.That(counts[InterfaceOption.ChatScope], Is.EqualTo(2));
        }

        [Test]
        public void ScaleRows_WalkFromSmallToLarge()
        {
            var scale = InterfaceCatalog.Shipped.For(InterfaceOption.UiScale);
            Assert.That(scale.Step(InterfaceCatalog.Small, 1).Code, Is.EqualTo(InterfaceCatalog.Medium));
            Assert.That(scale.Step(InterfaceCatalog.Medium, 1).Code, Is.EqualTo(InterfaceCatalog.Large));
            Assert.That(scale.Step(InterfaceCatalog.Medium, -1).Code, Is.EqualTo(InterfaceCatalog.Small));
            Assert.That(
                InterfaceCatalog.Shipped.For(InterfaceOption.FontScale).All,
                Is.EqualTo(scale.All));
        }

        [Test]
        public void ShippedDefaults_ShowEverythingAtMiddleAndUnrestricted()
        {
            var defaults = InterfaceCatalog.Shipped.Defaults;

            Assert.That(defaults.Get(InterfaceOption.UiScale), Is.EqualTo("medium"));
            Assert.That(defaults.Get(InterfaceOption.FontScale), Is.EqualTo("medium"));
            Assert.That(defaults.IsOn(InterfaceOption.InGameUi), Is.True);
            Assert.That(defaults.IsOn(InterfaceOption.FpsCounter), Is.True);
            Assert.That(defaults.IsOn(InterfaceOption.PingCounter), Is.True);
            Assert.That(defaults.IsOn(InterfaceOption.PlayerNames), Is.True);
            Assert.That(defaults.IsOn(InterfaceOption.BeginnerGuide), Is.True);

            Assert.That(
                defaults.IsOn(InterfaceOption.StreamerMode),
                Is.False,
                "It changes what everybody else sees, so it is not done to somebody who never asked.");

            Assert.That(
                defaults.Get(InterfaceOption.ChatScope),
                Is.EqualTo("off"),
                "The chat row is a restriction, so off is everybody.");
        }

        /// <summary>
        /// The one row whose words could be read backwards, so the labels
        /// carry who it means as well as whether it is on.
        /// </summary>
        [Test]
        public void ChatScope_SaysWhoItMeansOnBothChoices()
        {
            var catalog = InterfaceCatalog.Shipped;

            Assert.That(catalog.Label(InterfaceOption.ChatScope, "off"), Does.Contain("모두"));
            Assert.That(catalog.Label(InterfaceOption.ChatScope, "on"), Does.Contain("친구만"));
        }

        /// <summary>
        /// Both name rows are simply on or off now. 친구만 was a third answer
        /// on each, and the friend list is only the chat row's business.
        /// </summary>
        [Test]
        public void NameRows_AreOnOrOff_AndStepBetweenTheTwo()
        {
            foreach (var option in new[] { InterfaceOption.PlayerNames, InterfaceOption.StreamerMode })
            {
                var names = InterfaceCatalog.Shipped.For(option);

                Assert.That(names.All.Count, Is.EqualTo(2), $"{option} offers more than on and off.");
                Assert.That(names.Step("on", 1).Code, Is.EqualTo("off"));
                Assert.That(names.Step("off", 1).Code, Is.EqualTo("on"), "And it wraps.");
            }
        }

        [Test]
        public void Opening_WithNothingSaved_StartsFromTheDefaults()
        {
            var system = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());

            Assert.That(system.Current, Is.EqualTo(InterfaceCatalog.Shipped.Defaults));
        }

        [Test]
        public void Opening_FillsInRowsTheSaveDoesNotCover()
        {
            var store = new InMemoryInterfaceSettingsStore();
            store.Save(InterfaceSettings.Empty.With(InterfaceOption.FpsCounter, "off"));

            var system = new InterfaceSettingsSystem(store);

            Assert.That(system.Current.Get(InterfaceOption.FpsCounter), Is.EqualTo("off"));
            Assert.That(system.Current.Get(InterfaceOption.UiScale), Is.EqualTo("medium"));
        }

        [Test]
        public void Opening_DropsACodeTheCatalogueNoLongerLists()
        {
            var store = new InMemoryInterfaceSettingsStore();
            store.Save(InterfaceCatalog.Shipped.Defaults.With(InterfaceOption.UiScale, "huge"));

            var system = new InterfaceSettingsSystem(store);

            Assert.That(system.Current.Get(InterfaceOption.UiScale), Is.EqualTo("medium"));
        }

        [Test]
        public void Apply_Saves_AndTellsListeners()
        {
            var store = new InMemoryInterfaceSettingsStore();
            var system = new InterfaceSettingsSystem(store);
            var changes = new List<InterfaceSettings>();
            system.Changed += changes.Add;

            var wanted = system.Defaults.With(InterfaceOption.PingCounter, "off");
            system.Apply(wanted);

            Assert.That(system.Current, Is.EqualTo(wanted));
            Assert.That(store.Saved, Is.EqualTo(wanted));
            Assert.That(changes, Is.EqualTo(new[] { wanted }));
        }

        [Test]
        public void Apply_WithTheSameValues_SaysNothing()
        {
            var store = new InMemoryInterfaceSettingsStore();
            var system = new InterfaceSettingsSystem(store);
            var changes = 0;
            system.Changed += _ => changes++;

            system.Apply(system.Current);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(store.Saved, Is.Null);
        }

        /// <summary>
        /// Two tabs' values are different types, so one cannot be handed where
        /// the other is wanted even though both are rows of codes underneath.
        /// </summary>
        [Test]
        public void SettingsOfDifferentTabs_AreDifferentTypes()
        {
            Assert.That(typeof(InterfaceSettings), Is.Not.EqualTo(typeof(GraphicsSettings)));
        }

        [Test]
        public void RowKeeping_TreatsAnUnwrittenRowAsEmpty_AndComparesTheSame()
        {
            var narrow = OptionValues.Empty.With(0, "a");
            var grown = OptionValues.Empty.With(4, string.Empty).With(0, "a");

            Assert.That(grown.Get(2), Is.EqualTo(string.Empty));
            Assert.That(
                grown,
                Is.EqualTo(narrow),
                "A set grown by rows nobody has chosen is the set it grew from.");
            Assert.That(grown.GetHashCode(), Is.EqualTo(narrow.GetHashCode()));
        }

        [Test]
        public void RowKeeping_CopiesRatherThanSharing()
        {
            var first = OptionValues.Empty.With(0, "a");
            var second = first.With(1, "b");

            Assert.That(first.Get(1), Is.EqualTo(string.Empty));
            Assert.That(second.Get(0), Is.EqualTo("a"));
            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
