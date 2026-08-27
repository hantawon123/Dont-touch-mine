using Game.Core.Maps;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MapCatalogTests
    {
        [Test]
        public void Catalog_UsesPlaygroundAsAvailableDefaultMap()
        {
            Assert.That(MapCatalog.MapIds, Is.EqualTo(new[] { MapCatalog.PlaygroundId }));
            Assert.That(MapCatalog.DefaultMapId, Is.EqualTo(MapCatalog.PlaygroundId));
            Assert.That(MapCatalog.Contains(" playground "), Is.True);
            Assert.That(MapCatalog.Contains("unknown"), Is.False);
            Assert.That(LobbyMapCatalog.Maps.Count, Is.EqualTo(MapCatalog.MapIds.Count));
            Assert.That(LobbyMapCatalog.Maps[0].Id, Is.EqualTo(MapCatalog.PlaygroundId));
        }
    }
}
