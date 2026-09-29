using Game.Client.Match;
using Game.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchUrgencyAudioTests
    {
        [TestCase(MatchPhase.Searching, 30d, true)]
        [TestCase(MatchPhase.Searching, 29.4d, true)]
        [TestCase(MatchPhase.Searching, 0d, true)]
        [TestCase(MatchPhase.Searching, 30.1d, false)]
        [TestCase(MatchPhase.Searching, 90d, false)]
        [TestCase(MatchPhase.Hiding, 10d, false)]
        [TestCase(MatchPhase.Waiting, 10d, false)]
        [TestCase(MatchPhase.Highlight, 10d, false)]
        [TestCase(MatchPhase.Result, 10d, false)]
        public void OnlyTheLastThirtySecondsOfSearchingPlay(
            MatchPhase phase, double remainingSeconds, bool expected)
        {
            Assert.That(MatchUrgencyAudio.ShouldPlay(phase, remainingSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void TheBorderAndTheSoundAgreeOnWhenItIsUrgent()
        {
            // 붉은 테두리와 소리가 어긋나면 연출이 깨지므로 같은 판정을 쓴다.
            for (var remaining = 0d; remaining <= 60d; remaining += .5d)
            {
                Assert.That(
                    MatchUrgencyAudio.ShouldPlay(MatchPhase.Searching, remaining),
                    Is.EqualTo(MatchTimerView.IsWarning(remaining)),
                    $"remaining={remaining}");
            }
        }

        [Test]
        public void TheBedHoldsWhileUrgentAndFadesOutAfterwards()
        {
            Assert.That(MatchUrgencyAudio.AdvanceFade(0f, true, .016f), Is.EqualTo(1f));
            Assert.That(MatchUrgencyAudio.AdvanceFade(1f, true, 10f), Is.EqualTo(1f));

            var gain = 1f;
            // 절반쯤 지나면 절반쯤 남아 있어야 한다.
            gain = MatchUrgencyAudio.AdvanceFade(gain, false, MatchUrgencyAudio.FadeOutSeconds / 2f);
            Assert.That(gain, Is.EqualTo(.5f).Within(.001f));

            gain = MatchUrgencyAudio.AdvanceFade(gain, false, MatchUrgencyAudio.FadeOutSeconds);
            Assert.That(gain, Is.EqualTo(0f));
            Assert.That(MatchUrgencyAudio.AdvanceFade(gain, false, 1f), Is.EqualTo(0f),
                "페이드가 끝난 뒤 음수로 내려가면 안 된다");
        }

        [Test]
        public void APausedFrameDoesNotMoveTheFade()
        {
            Assert.That(MatchUrgencyAudio.AdvanceFade(.4f, false, 0f), Is.EqualTo(.4f).Within(.001f));
        }

        [Test]
        public void TheApprovedWarningChimeIsLoadable()
        {
            var clip = Resources.Load<AudioClip>(MatchUrgencyAudio.ChimeResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/WarningChime.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(.95f).Within(.001f));
        }

        [Test]
        public void TheApprovedTensionBedIsLoadableAndLoopLength()
        {
            var clip = Resources.Load<AudioClip>(MatchUrgencyAudio.LoopResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/TensionLoop.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            // 120 BPM 8박 정확히. 길이가 틀어지면 루프 이음매가 들린다.
            Assert.That(clip.length, Is.EqualTo(4f).Within(.0005f));
        }
    }
}
