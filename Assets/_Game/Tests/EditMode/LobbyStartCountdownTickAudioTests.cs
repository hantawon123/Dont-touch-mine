using Game.Client.Lobby;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyStartCountdownTickAudioTests
    {
        [TestCase(10d, true)]
        [TestCase(9.4d, true)]
        [TestCase(.1d, true)]
        [TestCase(0d, false)]
        [TestCase(-1d, false)]
        [TestCase(10.1d, false)]
        [TestCase(30d, false)]
        public void TicksForTheWholeLobbyCountdown(double remainingSeconds, bool expected)
        {
            Assert.That(LobbyStartCountdownTickAudio.ShouldTick(remainingSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void TheTickAgreesWithTheCountdownLabelWhileTimeRemains()
        {
            // 화면에 뜬 "N초 뒤"와 째각이 어긋나면 연출이 깨지므로 같은 초 경계를 쓴다.
            for (var remaining = .1d; remaining <= 20d; remaining += .5d)
            {
                var labelVisible = remaining > 0d &&
                    System.Math.Ceiling(remaining) <= LobbyStartCountdownTickAudio.DurationSeconds;
                Assert.That(
                    LobbyStartCountdownTickAudio.ShouldTick(remaining),
                    Is.EqualTo(labelVisible),
                    $"remaining={remaining}");
            }
        }

        [Test]
        public void TheApprovedTickIsLoadable()
        {
            var clip = Resources.Load<AudioClip>(LobbyStartCountdownTickAudio.TickResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/LobbyStartCountdownTick.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(.12f).Within(.001f));
        }
    }
}
