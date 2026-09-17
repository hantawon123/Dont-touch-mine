using Game.Client.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HidingTimerTickAudioTests
    {
        [TestCase(10d, true)]
        [TestCase(9.4d, true)]
        [TestCase(.1d, true)]
        [TestCase(0d, false)]
        [TestCase(-1d, false)]
        [TestCase(10.1d, false)]
        [TestCase(30d, false)]
        public void OnlyTheLastTenSecondsOfHidingTick(double remainingSeconds, bool expected)
        {
            Assert.That(HidingTimerTickAudio.ShouldTick(remainingSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void TheTickAgreesWithTheHudsWarningWindowWhileTimeRemains()
        {
            // 경고 색상/펄스와 째각 소리가 어긋나면 연출이 깨지므로 같은 판정을 쓴다.
            for (var remaining = .1d; remaining <= 60d; remaining += .5d)
            {
                Assert.That(
                    HidingTimerTickAudio.ShouldTick(remaining),
                    Is.EqualTo(HidingActiveHudView.IsWarning(remaining)),
                    $"remaining={remaining}");
            }
        }

        [Test]
        public void TheApprovedTickIsLoadable()
        {
            var clip = Resources.Load<AudioClip>(HidingTimerTickAudio.TickResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/HidingTimerTick.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(.08f).Within(.001f));
        }
    }
}
