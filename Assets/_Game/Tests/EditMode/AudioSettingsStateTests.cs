using System;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class AudioSettingsStateTests
    {
        [Test]
        public void Defaults_AreMidVolumeWithVoiceChatOn()
        {
            var settings = new AudioSettingsState();

            Assert.That(settings.MasterVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.BgmVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.SfxVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.UiVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.VoiceVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.MicVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));
            Assert.That(settings.VoiceChatEnabled, Is.True);
            Assert.That(settings.GetListenerVolume(), Is.EqualTo(0.5f));
        }

        [Test]
        public void Constructor_ClampsOutOfRangeVolumes()
        {
            var settings = new AudioSettingsState(-10, 140, 0, 100, true, 200, -1);

            Assert.That(settings.MasterVolume, Is.EqualTo(0));
            Assert.That(settings.BgmVolume, Is.EqualTo(100));
            Assert.That(settings.SfxVolume, Is.EqualTo(0));
            Assert.That(settings.UiVolume, Is.EqualTo(100));
            Assert.That(settings.VoiceVolume, Is.EqualTo(100));
            Assert.That(settings.MicVolume, Is.EqualTo(0));
        }

        [Test]
        public void TrySetVolume_RejectsOutOfRangeAndUnknownChannel()
        {
            var settings = new AudioSettingsState();

            Assert.That(
                settings.TrySetVolume(AudioChannel.Master, -1, out var error),
                Is.False);
            Assert.That(error, Is.EqualTo(AudioSettingsError.InvalidVolume));
            Assert.That(settings.MasterVolume, Is.EqualTo(AudioSettingsState.DefaultVolume));

            Assert.That(
                settings.TrySetVolume(AudioChannel.Master, 101, out error),
                Is.False);
            Assert.That(error, Is.EqualTo(AudioSettingsError.InvalidVolume));

            Assert.That(
                settings.TrySetVolume((AudioChannel)999, 40, out error),
                Is.False);
            Assert.That(error, Is.EqualTo(AudioSettingsError.InvalidChannel));
        }

        [Test]
        public void SourceVolumes_ScaleIndependentlyAndMuteVoiceWhenDisabled()
        {
            var settings = new AudioSettingsState(80, 50, 25, 100, true, 40, 70);

            Assert.That(settings.GetListenerVolume(), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Master), Is.EqualTo(1f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Bgm), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Sfx), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Ui), Is.EqualTo(1f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Voice), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Mic), Is.EqualTo(0.7f).Within(0.0001f));

            Assert.That(settings.TrySetVoiceChatEnabled(false, out var error), Is.True);
            Assert.That(error, Is.EqualTo(AudioSettingsError.None));
            Assert.That(settings.VoiceVolume, Is.EqualTo(40));
            Assert.That(settings.MicVolume, Is.EqualTo(70));
            Assert.That(settings.GetSourceVolume(AudioChannel.Voice), Is.EqualTo(0f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Mic), Is.EqualTo(0f));
            Assert.That(settings.GetSourceVolume(AudioChannel.Bgm), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void GetSourceVolume_RejectsUnknownChannel()
        {
            var settings = new AudioSettingsState();
            Assert.That(
                () => settings.GetSourceVolume((AudioChannel)999),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
