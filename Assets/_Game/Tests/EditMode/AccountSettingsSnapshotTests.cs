using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class AccountSettingsSnapshotTests
    {
        private GeneralSettingsSystem general;
        private ControlSettingsSystem control;
        private InterfaceSettingsSystem ui;
        private SoundSettingsSystem sound;
        private NotificationSettingsSystem notification;
        [SetUp] public void Setup()
        {
            general = new(new InMemoryGeneralSettingsStore());
            control = new(new InMemoryControlSettingsStore());
            ui = new(new InMemoryInterfaceSettingsStore());
            sound = new(new InMemorySoundSettingsStore());
            notification = new(new InMemoryNotificationSettingsStore());
        }
        private AccountSettingsSnapshot Capture() => AccountSettingsSnapshot.Capture(general, control, ui, sound, notification);
        [Test] public void JsonRoundTrip_RestoresAppliedValuesAndExplicitUnboundKey()
        {
            control.Apply(control.Current.With(ControlAction.Jump, ""));
            sound.Apply(sound.Current.With(SoundVolume.Master, 17));
            string json = JsonUtility.ToJson(Capture());
            control.Apply(control.Defaults); sound.Apply(sound.Defaults);
            JsonUtility.FromJson<AccountSettingsSnapshot>(json).Apply(general, control, ui, sound, notification);
            Assert.That(control.Current.Get(ControlAction.Jump), Is.Empty);
            Assert.That(sound.Current.Get(SoundVolume.Master), Is.EqualTo(17));
            Assert.That(json, Does.Not.Contain("resolution").And.Not.Contain("graphics"));
        }
        [Test] public void LateRemoteSnapshot_DoesNotOverwriteLocallyEditedSection()
        {
            var remote = Capture();
            remote.volumes[0] = 12;
            sound.Apply(sound.Current.With(SoundVolume.Master, 73));
            remote.Apply(general, control, ui, sound, notification, 8);
            Assert.That(sound.Current.Get(SoundVolume.Master), Is.EqualTo(73));
            remote.Apply(general, control, ui, sound, notification);
            Assert.That(sound.Current.Get(SoundVolume.Master), Is.EqualTo(12));
        }
        [Test] public void UnsupportedSchemaAndTruncatedArrays_DoNotChangeCurrentPreferences()
        {
            var remote = Capture(); remote.schemaVersion = 2; remote.volumes[0] = 0;
            remote.Apply(general, control, ui, sound, notification);
            Assert.That(sound.Current.Get(SoundVolume.Master), Is.EqualTo(50));
            remote.schemaVersion = 1; remote.bindings = new string[0];
            Assert.That(remote.IsValid, Is.False);
        }
    }
}
