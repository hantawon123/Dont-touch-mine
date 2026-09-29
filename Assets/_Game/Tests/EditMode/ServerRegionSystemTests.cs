using Game.Core.Home;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Which region the game connects to, and remembering it between runs.
    /// </summary>
    /// <remarks>
    /// The consequences of getting this wrong are quiet ones: a room list from
    /// the wrong side of the world, or a connection dropped for no reason. Both
    /// look like the network being flaky rather than like a bug here.
    /// </remarks>
    public sealed class ServerRegionSystemTests
    {
        [Test]
        public void FirstRun_StartsOnTheRegionTheBuildShipsWith()
        {
            var system = new ServerRegionSystem(new InMemoryServerRegionStore(), "eu");

            Assert.That(system.Current.Code, Is.EqualTo("eu"));
        }

        [Test]
        public void FirstRun_WithNoShippedRegion_StartsOnTheCatalogueDefault()
        {
            var system = new ServerRegionSystem(new InMemoryServerRegionStore());

            Assert.That(system.Current.Code, Is.EqualTo(ServerRegionCatalog.Default.Code));
        }

        [Test]
        public void AShippedRegionWeDoNotOffer_FallsBackToTheDefault()
        {
            var system = new ServerRegionSystem(new InMemoryServerRegionStore(), "mars");

            Assert.That(system.Current.Code, Is.EqualTo(ServerRegionCatalog.Default.Code));
        }

        [Test]
        public void WhatThePlayerChose_BeatsWhatTheBuildShipsWith()
        {
            var store = new InMemoryServerRegionStore();
            store.Save("au");

            var system = new ServerRegionSystem(store, "eu");

            Assert.That(system.Current.Code, Is.EqualTo("au"));
        }

        [Test]
        public void ChoosingAnotherRegion_IsRememberedAndAnnouncedOnce()
        {
            var store = new InMemoryServerRegionStore();
            var system = new ServerRegionSystem(store, "kr");
            var announced = 0;
            system.Changed += _ => announced++;

            Assert.That(system.TrySelect("eu"), Is.True);

            Assert.That(system.Current.Code, Is.EqualTo("eu"));
            Assert.That(announced, Is.EqualTo(1));
            Assert.That(store.TryLoad(out var saved), Is.True);
            Assert.That(saved, Is.EqualTo("eu"));
        }

        [Test]
        public void ChoosingTheRegionAlreadyInUse_ChangesNothing()
        {
            var store = new InMemoryServerRegionStore();
            var system = new ServerRegionSystem(store, "kr");
            var announced = 0;
            system.Changed += _ => announced++;

            Assert.That(system.TrySelect("kr"), Is.True, "누른 것 자체는 실패가 아니다.");

            // Every announcement drops the lobby connection and opens it again.
            // Pressing the region you are already on must not cost that.
            Assert.That(announced, Is.Zero);
            Assert.That(store.TryLoad(out _), Is.False, "바뀐 게 없으면 저장할 것도 없다.");
        }

        [Test]
        public void ARegionWeDoNotOffer_IsRefusedAndChangesNothing()
        {
            var store = new InMemoryServerRegionStore();
            var system = new ServerRegionSystem(store, "kr");
            var announced = 0;
            system.Changed += _ => announced++;

            Assert.That(system.TrySelect("mars"), Is.False);

            Assert.That(system.Current.Code, Is.EqualTo("kr"));
            Assert.That(announced, Is.Zero);
            Assert.That(store.TryLoad(out _), Is.False);
        }

        [Test]
        public void EveryRegionTheCatalogueOffers_CanBeChosen()
        {
            var system = new ServerRegionSystem(new InMemoryServerRegionStore());

            foreach (var region in ServerRegionCatalog.All)
            {
                Assert.That(system.TrySelect(region.Code), Is.True, region.DisplayName);
                Assert.That(system.Current.Code, Is.EqualTo(region.Code));
            }
        }
    }
}
