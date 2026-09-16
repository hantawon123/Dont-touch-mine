using Game.Client.Lobby;
using Game.Core.Maps;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MapCatalogTests
    {
        [Test]
        public void Catalog_KeepsPlaygroundAndOffersOnlySupermarketInLobby()
        {
            Assert.That(MapCatalog.MapIds, Is.EqualTo(new[] { MapCatalog.PlaygroundId, MapCatalog.SupermarketId }));
            Assert.That(MapCatalog.LobbyMapIds, Is.EqualTo(new[] { MapCatalog.SupermarketId }));
            Assert.That(MapCatalog.DefaultMapId, Is.EqualTo(MapCatalog.SupermarketId));
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
            Assert.That(MapCatalog.NormalizeLobbyMapId("", "playground"), Is.EqualTo(MapCatalog.SupermarketId));
            Assert.That(MapCatalog.NormalizeLobbyMapId(" playground ", ""), Is.EqualTo("playground"));
            Assert.That(MapCatalog.NormalizeLobbyMapId("unknown", "playground"), Is.EqualTo("playground"));
            Assert.That(LobbyMapCatalog.Maps.Count, Is.EqualTo(1));
            Assert.That(LobbyMapCatalog.Maps[0].Id, Is.EqualTo(MapCatalog.SupermarketId));
            Assert.That(PlaySettingsMapCatalog.All.Count, Is.EqualTo(1));
            Assert.That(PlaySettingsMapCatalog.Default.Id, Is.EqualTo(MapCatalog.SupermarketId));
            Assert.That(PlaySettingsMapCatalog.Contains(""), Is.False);
            Assert.That(PlaySettingsMapCatalog.Contains(MapCatalog.PlaygroundId), Is.False);
            Assert.That(PlaySettingsMapCatalog.Contains(MapCatalog.SupermarketId), Is.True);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Assert.That(MapCatalog.PickRandom(), Is.EqualTo(MapCatalog.SupermarketId),
                    "A random pick must stay on the lobby map list.");
            }
        }
    }
}
