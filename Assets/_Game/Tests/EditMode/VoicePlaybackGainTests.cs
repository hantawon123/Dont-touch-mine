using System.Reflection;
using Game.Bootstrap;
using Game.Core.Settings;
using Game.Core.Voice;
using Game.Network.Voice;
using NUnit.Framework;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class VoicePlaybackGainTests
    {
        [Test]
        public void IncomingVolumeIsIndependentAndOldSettingsKeepUnityGain()
        {
            var before = SoundCatalog.Defaults.With(SoundVolume.Microphone, 73);
            var old = before.With(SoundVolume.Voice, SoundCatalog.Unset);
            Assert.That(VoiceCaptureGain.From(SoundCatalog.Normalise(old).Get(SoundVolume.Voice)), Is.EqualTo(1f));
            var master = AudioListener.volume;
            try
            {
                var applier = new UnitySoundSettingsApplier();
                applier.Apply(before);
                var effects = Game.Client.Players.PlayerFootstepAudio.EffectsVolume;
                var music = Game.Client.Match.MatchUrgencyAudio.MusicVolume;
                var appliedMaster = AudioListener.volume;
                applier.Apply(before.With(SoundVolume.Voice, 100));
                Assert.That(VoicePlaybackGain.Gain, Is.EqualTo(2f));
                Assert.That(VoiceCaptureGain.From(before), Is.EqualTo(1.46f).Within(.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(appliedMaster));
                Assert.That(Game.Client.Players.PlayerFootstepAudio.EffectsVolume, Is.EqualTo(effects));
                Assert.That(Game.Client.Match.MatchUrgencyAudio.MusicVolume, Is.EqualTo(music));
            }
            finally { VoicePlaybackGain.Gain = 1f; AudioListener.volume = master; }
        }

        [Test]
        public void IncomingVolumePersistsWithoutOverwritingOtherSavedVolumes()
        {
            const string key = "game.settings.sound.Voice";
            const string micKey = "game.settings.sound.Microphone";
            var hadValue = PlayerPrefs.HasKey(key);
            var previous = PlayerPrefs.GetInt(key);
            var mic = PlayerPrefs.GetInt(micKey, -1);
            try
            {
                var store = new PlayerPrefsSoundSettingsStore();
                store.Save(SoundSettings.Empty.With(SoundVolume.Voice, 81));
                Assert.That(store.TryLoad(out var saved), Is.True);
                Assert.That(saved.Get(SoundVolume.Voice), Is.EqualTo(81));
                Assert.That(PlayerPrefs.GetInt(micKey, -1), Is.EqualTo(mic));
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetInt(key, previous);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void PerPlayerVolumeOnlyChangesThatAccountAndCombinesWithGlobalGain()
        {
            var preferences = new VoicePreferences();
            var first = new GameObject("First remote speaker");
            var second = new GameObject("Second remote speaker");
            var replacement = new GameObject("Respawned speaker");
            try
            {
                var a = first.AddComponent<VoicePlaybackGain>();
                var b = second.AddComponent<VoicePlaybackGain>();
                a.BindPlayer(preferences, "P1", "account-a");
                b.BindPlayer(preferences, "P2", "account-b");
                var process = typeof(VoicePlaybackGain).GetMethod("OnAudioFilterRead", BindingFlags.Instance | BindingFlags.NonPublic);
                preferences.SetPlayerVolume("P1", "account-a", 0);
                VoicePlaybackGain.Gain = 2f;
                var muted = new[] { .1f, -.1f };
                var heard = new[] { .1f, -.1f };
                process.Invoke(a, new object[] { muted, 1 });
                process.Invoke(b, new object[] { heard, 1 });
                Assert.That(muted, Is.All.Zero);
                Assert.That(heard[0], Is.EqualTo(.2f).Within(.00001f));
                var respawn = replacement.AddComponent<VoicePlaybackGain>();
                respawn.BindPlayer(preferences, "P3", "account-a");
                Assert.That(preferences.GetPlayerVolume("P1", "different-account"), Is.EqualTo(50));
                var respawnAudio = new[] { .1f };
                process.Invoke(respawn, new object[] { respawnAudio, 1 });
                Assert.That(respawnAudio, Is.All.Zero);
                preferences.SetPlayerVolume("P3", "account-a", 999);
                Assert.That(preferences.GetPlayerVolume("P3", "account-a"), Is.EqualTo(100));
                var loud = new[] { 1f, -.5f };
                process.Invoke(respawn, new object[] { loud, 1 });
                Assert.That(loud[0], Is.EqualTo(.98f).Within(.00001f));
                Assert.That(loud[1], Is.EqualTo(-.49f).Within(.00001f));
                preferences.ResetPlayerVolumes();
                Assert.That(preferences.GetPlayerVolume("P1", "account-a"), Is.EqualTo(50));
            }
            finally
            {
                VoicePlaybackGain.Gain = 1f;
                Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void VoiceOnlyFilterBoundsBoostAndPreservesSourceDistanceAndMute()
        {
            var host = new GameObject("Voice output test");
            var other = new GameObject("Music output test");
            try
            {
                other.AddComponent<AudioSource>();
                var source = host.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.minDistance = 2f;
                source.maxDistance = 15f;
                source.mute = true;
                var speaker = host.AddComponent<Speaker>();
                VoicePlaybackGain.Attach(speaker);
                VoicePlaybackGain.Attach(speaker);
                Assert.That(host.GetComponents<VoicePlaybackGain>().Length, Is.EqualTo(1));
                var filter = host.GetComponent<VoicePlaybackGain>();
                var process = typeof(VoicePlaybackGain).GetMethod("OnAudioFilterRead", BindingFlags.Instance | BindingFlags.NonPublic);
                VoicePlaybackGain.Gain = 2f;
                var data = new[] { .9f, -.9f, .45f, -.45f };
                process.Invoke(filter, new object[] { data, 2 });
                Assert.That(data[0], Is.EqualTo(.98f).Within(.00001f));
                Assert.That(data[2], Is.EqualTo(.49f).Within(.00001f));
                VoicePlaybackGain.Gain = 0f;
                process.Invoke(filter, new object[] { data, 2 });
                Assert.That(data, Is.All.Zero);
                Assert.That(source.mute, Is.True);
                Assert.That(source.spatialBlend, Is.EqualTo(1f));
                Assert.That(source.minDistance, Is.EqualTo(2f));
                Assert.That(source.maxDistance, Is.EqualTo(15f));
                Assert.That(other.GetComponent<VoicePlaybackGain>(), Is.Null);
            }
            finally { VoicePlaybackGain.Gain = 1f; Object.DestroyImmediate(host); Object.DestroyImmediate(other); }
        }
    }
}
