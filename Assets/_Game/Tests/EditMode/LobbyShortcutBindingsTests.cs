using Game.Client.Lobby;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class LobbyShortcutBindingsTests
    {
        [Test]
        public void ReadPressed_PrefersCharacterThenPlayersThenSettings()
        {
            Assert.That(
                LobbyShortcutBindings.ReadPressed(true, true, true),
                Is.EqualTo(LobbyShortcutKind.Character));
            Assert.That(
                LobbyShortcutBindings.ReadPressed(false, true, true),
                Is.EqualTo(LobbyShortcutKind.Players));
            Assert.That(
                LobbyShortcutBindings.ReadPressed(false, false, true),
                Is.EqualTo(LobbyShortcutKind.Settings));
            Assert.That(
                LobbyShortcutBindings.ReadPressed(false, false, false),
                Is.EqualTo(LobbyShortcutKind.None));
        }

        [Test]
        public void CanHandle_IgnoresChatMenuAndForeignScreens()
        {
            Assert.That(LobbyShortcutBindings.CanHandle(false, false, false), Is.True);
            Assert.That(LobbyShortcutBindings.CanHandle(true, false, false), Is.False);
            Assert.That(LobbyShortcutBindings.CanHandle(false, true, false), Is.False);
            Assert.That(LobbyShortcutBindings.CanHandle(false, false, true), Is.False);
        }
    }
}
