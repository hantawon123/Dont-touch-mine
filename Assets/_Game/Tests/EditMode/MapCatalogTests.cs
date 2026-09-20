using Game.Client.Lobby;
using Game.Core.Maps;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MapCatalogTests
    {
        [Test]
        public void Catalog_KeepsPlaygroundAndOffersSupermarketAndMansionInLobby()
        {
            Assert.That(MapCatalog.MapIds, Is.EqualTo(new[] { MapCatalog.PlaygroundId, MapCatalog.SupermarketId, MapCatalog.MansionId }));
            Assert.That(MapCatalog.LobbyMapIds, Is.EqualTo(new[] { MapCatalog.SupermarketId, MapCatalog.MansionId }));
            Assert.That(MapCatalog.DefaultMapId, Is.EqualTo(MapCatalog.SupermarketId), "기본 맵은 마트 그대로.");
            Assert.That(MapCatalog.Contains(" playground "), Is.True);
            Assert.That(MapCatalog.Contains("supermarket"), Is.True);
            Assert.That(MapCatalog.Contains("mansion"), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("supermarket"), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("mansion"), Is.True);
            Assert.That(MapCatalog.Contains("unknown"), Is.False);
            Assert.That(MapCatalog.Contains(""), Is.False);
            Assert.That(MapCatalog.IsRandom(null), Is.True);
            Assert.That(MapCatalog.IsRandom(""), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice(""), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("playground"), Is.True);
            Assert.That(MapCatalog.IsLobbyChoice("unknown"), Is.False);
            Assert.That(MapCatalog.NormalizeLobbyMapId("", "playground"), Is.EqualTo(string.Empty));
            Assert.That(MapCatalog.NormalizeLobbyMapId(" playground ", ""), Is.EqualTo("playground"));
            Assert.That(MapCatalog.NormalizeLobbyMapId(" mansion ", ""), Is.EqualTo("mansion"));
            Assert.That(MapCatalog.NormalizeLobbyMapId("unknown", "playground"), Is.EqualTo("playground"));
            Assert.That(LobbyMapCatalog.Maps.Count, Is.EqualTo(2));
            Assert.That(LobbyMapCatalog.Maps[0].Id, Is.EqualTo(MapCatalog.SupermarketId));
            Assert.That(LobbyMapCatalog.Maps[1].Id, Is.EqualTo(MapCatalog.MansionId));
            Assert.That(PlaySettingsMapCatalog.All.Count, Is.EqualTo(3));
            Assert.That(PlaySettingsMapCatalog.All[0].IsRandom, Is.True);
            Assert.That(PlaySettingsMapCatalog.All[0].Label, Is.EqualTo(PlaySettingsMapCatalog.RandomLabel));
            Assert.That(PlaySettingsMapCatalog.Default.IsRandom, Is.True);
            Assert.That(PlaySettingsMapCatalog.DefaultIndex, Is.EqualTo(0));
            Assert.That(PlaySettingsMapCatalog.Contains(""), Is.True);
            Assert.That(PlaySettingsMapCatalog.Contains(MapCatalog.PlaygroundId), Is.False);
            Assert.That(PlaySettingsMapCatalog.Contains(MapCatalog.SupermarketId), Is.True);
            Assert.That(PlaySettingsMapCatalog.Contains(MapCatalog.MansionId), Is.True);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                Assert.That(MapCatalog.LobbyMapIds, Has.Member(MapCatalog.PickRandom()),
                    "A random pick must stay on the lobby map list.");
            }
        }
    }
}
