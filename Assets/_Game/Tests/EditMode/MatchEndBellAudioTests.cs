using Game.Client.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MatchEndBellAudioTests
    {
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void RingsOnceUntilReset(bool alreadyPlayed, bool expected)
        {
            Assert.That(MatchEndBellAudio.ShouldPlay(alreadyPlayed), Is.EqualTo(expected));
        }

        [Test]
        public void TheApprovedBellIsLoadable()
        {
            var clip = Resources.Load<AudioClip>(MatchEndBellAudio.BellResource);
            Assert.That(clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(
                "Assets/_Game/Content/Resources/Audio/MatchEndBell.wav"));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(1.42f).Within(.001f));
        }
    }
}
