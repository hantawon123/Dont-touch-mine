using System.Collections.Generic;
using Game.Core.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class LobbyHostSessionTests
    {
        [Test]
        public void RequestKick_EmitsOnlyWhenLocalHostAndNotSelf()
        {
            var session = CreateSession(true);
            var kicked = new List<string>();
            session.KickRequested += id => kicked.Add(id);

            session.RequestKick("host-1");
            session.RequestKick("player-2");
            session.SetLocalHost(false);
            session.RequestKick("player-3");

            Assert.That(kicked, Is.EqualTo(new[] { "player-2" }));
        }

        [Test]
        public void RequestApplySettings_UpdatesSettingsWhenHost()
        {
            var session = CreateSession(true);
            var draft = new PlaySettingsDraft(
                "새 방",
                "ABC123",
                true,
                "1234",
                4,
                3,
                "map-2");

            session.RequestApplySettings(draft);

            Assert.That(session.Settings.CurrentValue.Title, Is.EqualTo("새 방"));
            Assert.That(session.Settings.CurrentValue.MaxPlayers, Is.EqualTo(4));
        }

        private static LobbyHostSession CreateSession(bool isHost)
        {
            return new LobbyHostSession(
                "host-1",
                isHost,
                new PlaySettingsDraft("방", "CODE", false, string.Empty, 6, 5, "map"));
        }
    }
}
