using System.Collections.Generic;
using Game.Client.Audio;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class AudioSettingsServiceTests
    {
        [Test]
        public void Construction_LoadsStoreAndAppliesImmediately()
        {
            var stored = new AudioSettingsState(20, 30, 40, 50, false, 60, 70);
            var store = new MemoryAudioSettingsStore { NextLoad = stored };
            var applier = new RecordingAudioSettingsApplier();

            var service = new AudioSettingsService(store, applier);

            Assert.That(service.Current.MasterVolume, Is.EqualTo(20));
            Assert.That(service.Current.VoiceChatEnabled, Is.False);
            Assert.That(applier.Applied.Count, Is.EqualTo(1));
            Assert.That(applier.Applied[0].MasterVolume, Is.EqualTo(20));
            Assert.That(AudioSettingsOutput.Current, Is.SameAs(service.Current));
        }

        [Test]
        public void TrySetVolume_PersistsAndReapplies()
        {
            var store = new MemoryAudioSettingsStore();
            var applier = new RecordingAudioSettingsApplier();
            var service = new AudioSettingsService(store, applier);
            var changedCount = 0;
            service.Changed += _ => changedCount++;

            Assert.That(
                service.TrySetVolume(AudioChannel.Sfx, 15, out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(AudioSettingsError.None));
            Assert.That(service.Current.SfxVolume, Is.EqualTo(15));
            Assert.That(store.Saved.SfxVolume, Is.EqualTo(15));
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));

            Assert.That(service.TrySetVolume(AudioChannel.Sfx, 15, out error), Is.True);
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void TrySetVoiceChatEnabled_KeepsStoredVoiceVolumes()
        {
            var store = new MemoryAudioSettingsStore();
            var applier = new RecordingAudioSettingsApplier();
            var service = new AudioSettingsService(store, applier);

            Assert.That(service.TrySetVolume(AudioChannel.Voice, 90, out _), Is.True);
            Assert.That(service.TrySetVoiceChatEnabled(false, out _), Is.True);
            Assert.That(service.Current.VoiceChatEnabled, Is.False);
            Assert.That(service.Current.VoiceVolume, Is.EqualTo(90));
            Assert.That(service.Current.GetSourceVolume(AudioChannel.Voice), Is.EqualTo(0f));
            Assert.That(store.Saved.VoiceChatEnabled, Is.False);
        }

        [Test]
        public void UnityApplier_SetsAudioListenerFromMaster()
        {
            var previous = AudioListener.volume;
            try
            {
                var applier = new UnityAudioSettingsApplier();
                applier.Apply(new AudioSettingsState(25, 50, 50, 50, true, 50, 50));
                Assert.That(AudioListener.volume, Is.EqualTo(0.25f).Within(0.0001f));
            }
            finally
            {
                AudioListener.volume = previous;
            }
        }

        private sealed class MemoryAudioSettingsStore : IAudioSettingsStore
        {
            public AudioSettingsState NextLoad { get; set; }

            public AudioSettingsState Saved { get; private set; }

            public AudioSettingsState LoadOrDefault()
            {
                return NextLoad ?? new AudioSettingsState();
            }

            public void Save(AudioSettingsState settings)
            {
                Saved = settings;
            }
        }

        private sealed class RecordingAudioSettingsApplier : IAudioSettingsApplier
        {
            public List<AudioSettingsState> Applied { get; } = new List<AudioSettingsState>();

            public void Apply(AudioSettingsState settings)
            {
                Applied.Add(settings);
            }
        }
    }
}
