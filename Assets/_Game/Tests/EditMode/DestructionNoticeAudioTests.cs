using Game.Bootstrap;
using Game.Client.Match;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class DestructionNoticeAudioTests
    {
        [Test]
        public void TheApprovedNoticeClipIsLoadableThroughPlayerResources()
        {
            var clip = Resources.Load<AudioClip>(DestructionNoticeAudio.ClipResource);
            Assert.That(clip, Is.Not.Null, "Player builds must load the notice clip through Resources.");
            Assert.That(clip.name, Is.EqualTo("SFX_FastUiSoundMusical01"));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<AudioClip>(DestructionNoticeAudio.ClipAssetPath),
                Is.Not.Null);
        }

        [Test]
        public void RepeatDelay_WaitsForTheClipToFinish()
        {
            Assert.That(DestructionNoticeAudio.RepeatCount, Is.EqualTo(3));
            Assert.That(DestructionNoticeAudio.NextPlayAt(1f, 0.4f), Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(DestructionNoticeAudio.NextPlayAt(1f, 0f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EffectsVolumeFollowsSoundSettings()
        {
            var previous = DestructionNoticeAudio.EffectsVolume;
            try
            {
                new UnitySoundSettingsApplier().Apply(
                    SoundCatalog.Defaults.With(SoundVolume.Effects, 50));
                Assert.That(DestructionNoticeAudio.EffectsVolume, Is.EqualTo(.5f).Within(.001f));
            }
            finally
            {
                DestructionNoticeAudio.EffectsVolume = previous;
            }
        }
    }
}
