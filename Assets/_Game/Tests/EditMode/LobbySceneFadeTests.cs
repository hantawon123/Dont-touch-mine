using Game.Client.Lobby;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    public sealed class LobbySceneFadeTests
    {
        [Test]
        public void FadeIn_StartsCoveredAndClearsOverDuration()
        {
            Assert.That(LobbySceneFade.FadeInOpacity(0f), Is.EqualTo(1f));
            Assert.That(
                LobbySceneFade.FadeInOpacity(LobbySceneFade.DurationSeconds * 0.5f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(LobbySceneFade.FadeInOpacity(LobbySceneFade.DurationSeconds), Is.Zero);
            Assert.That(LobbySceneFade.IsComplete(LobbySceneFade.DurationSeconds), Is.True);
        }

        [Test]
        public void FadeOut_CoversOverDurationFromCurrentOpacity()
        {
            Assert.That(LobbySceneFade.FadeOutOpacity(0f), Is.Zero);
            Assert.That(
                LobbySceneFade.FadeOutOpacity(LobbySceneFade.DurationSeconds * 0.5f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(LobbySceneFade.FadeOutOpacity(LobbySceneFade.DurationSeconds), Is.EqualTo(1f));
            Assert.That(
                LobbySceneFade.Lerp(0.2f, 1f, LobbySceneFade.DurationSeconds * 0.5f),
                Is.EqualTo(0.6f).Within(0.001f));
        }
    }
}
