using System.Collections.Generic;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// What the 사운드 rows allow, what they start on, and what applying them
    /// does.
    /// </summary>
    public sealed class SoundSettingsTests
    {
        [Test]
        public void Defaults_AreHalfVolume_TheMachinesMicrophone_AndPushToTalk()
        {
            var defaults = SoundCatalog.Defaults;

            Assert.That(defaults.Get(SoundVolume.Master), Is.EqualTo(50));
            Assert.That(defaults.Get(SoundVolume.Music), Is.EqualTo(50));
            Assert.That(defaults.Get(SoundVolume.Ambience), Is.EqualTo(50));
            Assert.That(defaults.Get(SoundVolume.Effects), Is.EqualTo(50));
            Assert.That(defaults.Get(SoundVolume.Microphone), Is.EqualTo(50));
            Assert.That(defaults.DeviceName, Is.EqualTo(SoundCatalog.DefaultDevice));
            Assert.That(defaults.InputMode, Is.EqualTo(SoundCatalog.PushToTalk));
        }

        [Test]
        public void Normalise_ClampsVolumes_AndFillsTheUnset()
        {
            var wild = SoundSettings.Empty
                .With(SoundVolume.Master, 140)
                .With(SoundVolume.Music, -20);

            var tidy = SoundCatalog.Normalise(wild);

            Assert.That(tidy.Get(SoundVolume.Master), Is.EqualTo(100));
            Assert.That(tidy.Get(SoundVolume.Music), Is.EqualTo(0));
            Assert.That(tidy.Get(SoundVolume.Effects), Is.EqualTo(50), "Never set, so the default.");
            Assert.That(tidy.InputMode, Is.EqualTo(SoundCatalog.PushToTalk));
        }

        [Test]
        public void Normalise_DropsAMicrophoneTheMachineNoLongerHas()
        {
            var saved = SoundCatalog.Defaults.WithDevice("Old Headset");

            var withIt = SoundCatalog.Normalise(saved, new[] { "Old Headset", "Webcam" });
            var withoutIt = SoundCatalog.Normalise(saved, new[] { "Webcam" });

            Assert.That(withIt.DeviceName, Is.EqualTo("Old Headset"));
            Assert.That(withoutIt.DeviceName, Is.EqualTo(SoundCatalog.DefaultDevice));
        }

        [Test]
        public void Normalise_KeepsTheDefaultDevice_WhateverTheMachineHas()
        {
            var tidy = SoundCatalog.Normalise(SoundCatalog.Defaults, new string[0]);

            Assert.That(tidy.DeviceName, Is.EqualTo(SoundCatalog.DefaultDevice));
        }

        [Test]
        public void Normalise_DropsAnInputModeWeDoNotRecognise()
        {
            var tidy = SoundCatalog.Normalise(SoundCatalog.Defaults.WithInputMode("shout"));

            Assert.That(tidy.InputMode, Is.EqualTo(SoundCatalog.PushToTalk));
        }

        [Test]
        public void DeviceChoices_ListTheDefaultFirst_ThenTheMachines()
        {
            var choices = SoundCatalog.DeviceChoices(new[] { "Headset", "Webcam" });

            Assert.That(choices.All.Count, Is.EqualTo(3));
            Assert.That(choices.Default.Code, Is.EqualTo(SoundCatalog.DefaultDevice));
            Assert.That(choices.Default.Label, Is.EqualTo("기본 장치"));
            Assert.That(choices.All[1].Label, Is.EqualTo("Headset"));
            Assert.That(choices.Step(SoundCatalog.DefaultDevice, 1).Code, Is.EqualTo("Headset"));
            Assert.That(choices.Step("Webcam", 1).Code, Is.EqualTo(SoundCatalog.DefaultDevice), "Wraps.");
        }

        /// <summary>
        /// Shown as the machine gives it, brackets and all: two microphones on
        /// one sound card differ only by what is inside them.
        /// </summary>
        [Test]
        public void DeviceLabels_AreTheMachinesOwnNames()
        {
            var choices = SoundCatalog.DeviceChoices(
                new[] { "헤드셋 마이크(Realtek(R) Audio)", "마이크 배열(Intel Smart Sound)" });

            Assert.That(choices.All[1].Label, Is.EqualTo("헤드셋 마이크(Realtek(R) Audio)"));
            Assert.That(choices.All[1].Code, Is.EqualTo("헤드셋 마이크(Realtek(R) Audio)"));
            Assert.That(choices.All[2].Label, Is.EqualTo("마이크 배열(Intel Smart Sound)"));
        }

        [Test]
        public void DeviceLabels_KeepTwoDevicesOnOneCardApart()
        {
            var choices = SoundCatalog.DeviceChoices(
                new[] { "마이크(Front Panel)", "마이크(Rear Panel)" });

            Assert.That(choices.All[1].Label, Is.Not.EqualTo(choices.All[2].Label));
        }

        [Test]
        public void DeviceChoices_WithNoMicrophones_OfferOnlyTheDefault_AndCannotStep()
        {
            var choices = SoundCatalog.DeviceChoices(new string[0]);

            Assert.That(choices.All.Count, Is.EqualTo(1));
            Assert.That(choices.CanStep, Is.False);
        }

        [Test]
        public void IsMicrophoneOff_IsOnlyTheOffInputMode()
        {
            Assert.That(SoundCatalog.IsMicrophoneOff(SoundCatalog.MicOff), Is.True);
            Assert.That(SoundCatalog.IsMicrophoneOff(SoundCatalog.PushToTalk), Is.False);
            Assert.That(SoundCatalog.IsMicrophoneOff(SoundCatalog.OpenMic), Is.False);
            Assert.That(SoundCatalog.IsMicrophoneOff(null), Is.False);
        }

        [Test]
        public void IsOpenMicrophone_IsOnlyTheOpenInputMode()
        {
            Assert.That(SoundCatalog.IsOpenMicrophone(SoundCatalog.OpenMic), Is.True);
            Assert.That(SoundCatalog.IsOpenMicrophone(SoundCatalog.PushToTalk), Is.False);
            Assert.That(SoundCatalog.IsOpenMicrophone(SoundCatalog.MicOff), Is.False);
            Assert.That(SoundCatalog.IsOpenMicrophone(null), Is.False);
        }

        [Test]
        public void InputModes_AreTheThreeTheDesignLists_InOrder()
        {
            var modes = SoundCatalog.InputModes;

            Assert.That(modes.All.Count, Is.EqualTo(3));
            Assert.That(modes.All[0].Label, Is.EqualTo("눌러서 말하기"));
            Assert.That(modes.All[1].Label, Is.EqualTo("오픈 마이크"));
            Assert.That(modes.All[2].Label, Is.EqualTo("끄기"));
        }

        [Test]
        public void Settings_AreEqualOnlyWhenEverythingMatches()
        {
            var a = SoundCatalog.Defaults;

            Assert.That(a, Is.EqualTo(SoundCatalog.Defaults));
            Assert.That(a.GetHashCode(), Is.EqualTo(SoundCatalog.Defaults.GetHashCode()));
            Assert.That(a.With(SoundVolume.Master, 51), Is.Not.EqualTo(a));
            Assert.That(a.WithDevice("Headset"), Is.Not.EqualTo(a));
            Assert.That(a.WithInputMode(SoundCatalog.OpenMic), Is.Not.EqualTo(a));
        }

        [Test]
        public void ChangingOneVolume_LeavesTheOthersAlone()
        {
            var moved = SoundCatalog.Defaults.With(SoundVolume.Music, 10);

            Assert.That(moved.Get(SoundVolume.Music), Is.EqualTo(10));
            Assert.That(moved.Get(SoundVolume.Master), Is.EqualTo(50));
            Assert.That(moved.DeviceName, Is.EqualTo(SoundCatalog.DefaultDevice));
        }

        [Test]
        public void Opening_WithNothingSaved_StartsFromTheDefaults_AndChangesNothingHeard()
        {
            var applier = new NullSoundSettingsApplier();

            var system = new SoundSettingsSystem(new InMemorySoundSettingsStore(), applier);

            Assert.That(system.Current, Is.EqualTo(SoundCatalog.Defaults));
            Assert.That(applier.ApplyCount, Is.EqualTo(0));
        }

        [Test]
        public void Opening_ChecksTheSavedMicrophoneAgainstTheMachine()
        {
            var store = new InMemorySoundSettingsStore();
            store.Save(SoundCatalog.Defaults.WithDevice("Gone"));

            var system = new SoundSettingsSystem(
                store, devices: new FixedMicrophoneDevices("Here"));

            Assert.That(system.Current.DeviceName, Is.EqualTo(SoundCatalog.DefaultDevice));
            Assert.That(system.DeviceChoices.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void Apply_Saves_ReachesTheAudio_AndTellsListeners()
        {
            var store = new InMemorySoundSettingsStore();
            var applier = new NullSoundSettingsApplier();
            var system = new SoundSettingsSystem(store, applier);
            var changes = new List<SoundSettings>();
            system.Changed += changes.Add;

            var wanted = system.Defaults.With(SoundVolume.Master, 80).WithInputMode(SoundCatalog.OpenMic);
            system.Apply(wanted);

            Assert.That(system.Current, Is.EqualTo(wanted));
            Assert.That(store.Saved, Is.EqualTo(wanted));
            Assert.That(applier.Applied, Is.EqualTo(wanted));
            Assert.That(changes, Is.EqualTo(new[] { wanted }));
        }

        [Test]
        public void Apply_WithTheSameValues_SaysNothing()
        {
            var store = new InMemorySoundSettingsStore();
            var applier = new NullSoundSettingsApplier();
            var system = new SoundSettingsSystem(store, applier);
            var changes = 0;
            system.Changed += _ => changes++;

            system.Apply(system.Current);

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(applier.ApplyCount, Is.EqualTo(0));
            Assert.That(store.Saved, Is.Null);
        }

        [Test]
        public void Apply_TidiesWhatItIsGiven()
        {
            var system = new SoundSettingsSystem(new InMemorySoundSettingsStore());

            system.Apply(system.Defaults.With(SoundVolume.Effects, 999));

            Assert.That(system.Current.Get(SoundVolume.Effects), Is.EqualTo(100));
        }
    }
}
