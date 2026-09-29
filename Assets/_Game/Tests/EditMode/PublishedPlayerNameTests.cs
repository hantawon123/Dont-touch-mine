using Game.Core.Home;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// The name that leaves the room: the room browser's host line and the
    /// name sent to the other players have to be the same one.
    /// </summary>
    public sealed class PublishedPlayerNameTests
    {
        private static PublishedPlayerName Make(out InterfaceSettingsSystem settings)
        {
            settings = new InterfaceSettingsSystem(new InMemoryInterfaceSettingsStore());
            return new PublishedPlayerName(settings, new PlayerProfile("진짜닉네임"));
        }

        [Test]
        public void WithStreamerModeOff_ItIsTheirOwnName()
        {
            var name = Make(out _);

            Assert.That(name.IsPseudonymous, Is.False, "It ships off.");
            Assert.That(name.Current, Is.EqualTo("진짜닉네임"));
        }

        [Test]
        public void WithStreamerModeOn_ItIsAPseudonym()
        {
            var name = Make(out var settings);
            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));

            Assert.That(name.IsPseudonymous, Is.True);
            Assert.That(name.Current, Is.Not.EqualTo("진짜닉네임"));
            Assert.That(name.Current, Does.Match(Pseudonym.Shape), "It looks like any default nickname.");
        }

        /// <summary>
        /// The room list and the room ask separately, and a host who was one
        /// name in the browser and another inside would read as two people.
        /// </summary>
        [Test]
        public void EveryAsk_DuringOneVisit_GetsTheSameName()
        {
            var name = Make(out var settings);
            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));

            var first = name.Pseudonym;

            Assert.That(name.Pseudonym, Is.EqualTo(first));
            Assert.That(name.Current, Is.EqualTo(first));
        }

        /// <summary>
        /// Turning the mode off and on again inside one visit keeps the name.
        /// Somebody who is being talked to should not become a stranger because
        /// they looked at the settings screen.
        /// </summary>
        [Test]
        public void TogglingTheMode_DoesNotChangeTheName()
        {
            var name = Make(out var settings);
            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));
            var first = name.Pseudonym;

            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.Off));
            Assert.That(name.Current, Is.EqualTo("진짜닉네임"));

            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));
            Assert.That(name.Current, Is.EqualTo(first));
        }

        [Test]
        public void TheNextVisit_IsSomebodyElse()
        {
            var name = Make(out var settings);
            settings.Apply(settings.Current.With(InterfaceOption.StreamerMode, InterfaceCatalog.On));
            var first = name.Pseudonym;

            name.ForgetPseudonym();

            Assert.That(name.Pseudonym, Is.Not.EqualTo(first));
        }
    }
}
