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
            Assert.That(MapCatalog.MapIds, Is.EqualTo(new[] { MapCatalog.PlaygroundId, MapCatalog.SupermarketId }));
            Assert.That(MapCatalog.DefaultMapId, Is.EqualTo(MapCatalog.PlaygroundId));
            Assert.That(MapCatalog.Contains(" playground "), Is.True);
            Assert.That(MapCatalog.Contains("supermarket"), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("supermarket"), Is.True);
            Assert.That(MapCatalog.Contains("unknown"), Is.False);
            Assert.That(MapCatalog.Contains(""), Is.False);
            Assert.That(MapCatalog.IsRandom(null), Is.True);
            Assert.That(MapCatalog.IsRandom(""), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice(""), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("playground"), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("unknown"), Is.False);
            Assert.That(MapCatalog.NormalizeLobbyMapId("", "playground"), Is.EqualTo(string.Empty));
            Assert.That(MapCatalog.NormalizeLobbyMapId(" playground ", ""), Is.EqualTo("playground"));
            Assert.That(MapCatalog.NormalizeLobbyMapId("unknown", "playground"), Is.EqualTo("playground"));
            Assert.That(LobbyMapCatalog.Maps.Count, Is.EqualTo(MapCatalog.MapIds.Count));
            Assert.That(LobbyMapCatalog.Maps[0].Id, Is.EqualTo(MapCatalog.PlaygroundId));
            Assert.That(LobbyMapCatalog.Maps[1].Id, Is.EqualTo(MapCatalog.SupermarketId));
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Assert.That(MapCatalog.MapIds, Does.Contain(MapCatalog.PickRandom()),
                    "A random pick must always be one of the playable maps.");
            }
        }
    }
}
