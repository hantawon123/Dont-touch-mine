using Game.Core.Lobby;
using Game.Network.Session;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class SessionPropertyMapperTests
    {
        [Test]
        public void JoinRequest_DoesNotWriteSessionProperties()
        {
            var request = SessionRequest.Join("ROOM01", "secret");

            var properties = SessionPropertyMapper.BuildForStart(request, "player");

            Assert.That(properties, Is.Null);
        }

        [Test]
        public void CreateRequest_WritesPublicSettingsWithoutPassword()
        {
            var request = SessionRequest.Create(
                "ROOM01",
                "테스트 방",
                "Playground",
                6,
                "secret");

            var properties = SessionPropertyMapper.BuildForStart(request, "태원");

            Assert.That((string)properties[SessionPropertyKeys.DisplayName],
                Is.EqualTo("테스트 방"));
            Assert.That((string)properties[SessionPropertyKeys.MapId],
                Is.EqualTo("Playground"));
            Assert.That((int)properties[SessionPropertyKeys.MaxPlayers], Is.EqualTo(6));
            Assert.That(
                (int)properties[SessionPropertyKeys.DestructionLimit],
                Is.EqualTo(PlaySettingsDraft.DefaultDestructionLimit));
            Assert.That((string)properties[SessionPropertyKeys.HostNickname],
                Is.EqualTo("태원"));
            Assert.That((bool)properties[SessionPropertyKeys.Locked], Is.True);
            foreach (var property in properties.Values)
            {
                if (property.IsString)
                {
                    Assert.That((string)property, Is.Not.EqualTo(request.Password));
                }
            }
        }

        [Test]
        public void LobbySettings_TrimMapIdAndPreserveLimits()
        {
            var properties = SessionPropertyMapper.BuildLobbySettings(4, 3, " Playground ");

            Assert.That((int)properties[SessionPropertyKeys.MaxPlayers], Is.EqualTo(4));
            Assert.That((int)properties[SessionPropertyKeys.DestructionLimit], Is.EqualTo(3));
            Assert.That((string)properties[SessionPropertyKeys.MapId],
                Is.EqualTo("Playground"));
        }
    }
}
