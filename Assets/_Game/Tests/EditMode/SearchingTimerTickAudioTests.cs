using Game.Client.Match;
using Game.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class SearchingTimerTickAudioTests
    {
        [TestCase(MatchPhase.Searching, 30d, true)]
        [TestCase(MatchPhase.Searching, 29.4d, true)]
        [TestCase(MatchPhase.Searching, .1d, true)]
        [TestCase(MatchPhase.Searching, 0d, false)]
        [TestCase(MatchPhase.Searching, -1d, false)]
        [TestCase(MatchPhase.Searching, 30.1d, false)]
        [TestCase(MatchPhase.Searching, 90d, false)]
        [TestCase(MatchPhase.Hiding, 10d, false)]
        [TestCase(MatchPhase.Waiting, 10d, false)]
        [TestCase(MatchPhase.Highlight, 10d, false)]
        [TestCase(MatchPhase.Result, 10d, false)]
        public void OnlyTheLastThirtySecondsOfSearchingTick(
            MatchPhase phase, double remainingSeconds, bool expected)
        {
            Assert.That(SearchingTimerTickAudio.ShouldTick(phase, remainingSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void TheTickAgreesWithTheUrgencyBorderAndChimeWhileTimeRemains()
        {
            // 붉은 테두리, 긴장음, 째각 소리가 어긋나면 연출이 깨지므로 같은 판정을 쓴다.
            for (var remaining = .1d; remaining <= 60d; remaining += .5d)
            {
                Assert.That(
                    SearchingTimerTickAudio.ShouldTick(MatchPhase.Searching, remaining),
                    Is.EqualTo(MatchUrgencyAudio.ShouldPlay(MatchPhase.Searching, remaining)),
                    $"remaining={remaining}");
            }
        }

        [Test]
        public void TheApprovedTickIsLoadable()
        {
            var clip = Resources.Load<AudioClip>(SearchingTimerTickAudio.TickResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/SearchingTimerTick.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(.08f).Within(.001f));
        }
    }
}
