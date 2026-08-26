using System;
using Game.Client.Audio;
using Game.Client.Home;
using Game.Client.Settings;
using Game.Core.Flow;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SettingsPresenterTests
    {
        [Test]
        public void Presenter_StartsOnGraphicsTabAndBackReturnsHome()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow, audio))
            {
                presenter.Start();
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Graphics));
                Assert.That(view.BoundAudio, Is.SameAs(audio.Current));

                view.RaiseTab(SettingsTab.Audio);
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Audio));

                view.RaiseTab(SettingsTab.Graphics);
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Graphics));

                view.RaiseBack();
            }

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Home));
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Presenter_ForwardsAudioChangesToSettings()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow, audio))
            {
                presenter.Start();
                view.RaiseVolume(AudioChannel.Master, 80);
                view.RaiseVolume(AudioChannel.Bgm, 10);
                view.RaiseVoiceChat(false);
            }

            Assert.That(audio.Current.MasterVolume, Is.EqualTo(80));
            Assert.That(audio.Current.BgmVolume, Is.EqualTo(10));
            Assert.That(audio.Current.VoiceChatEnabled, Is.False);
            Assert.That(view.BoundAudio.MasterVolume, Is.EqualTo(80));
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();

            Assert.That(
                () => new SettingsPresenter(null, host, appFlow, audio),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, null, appFlow, audio),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, null, audio),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsTab ActiveTab { get; private set; }

            public AudioSettingsState BoundAudio { get; private set; }

            public event Action BackRequested;

            public event Action<SettingsTab> TabSelected;

            public event Action<AudioChannel, int> AudioVolumeChanged;

            public event Action<bool> VoiceChatEnabledChanged;

            public void SetActiveTab(SettingsTab tab)
            {
                ActiveTab = tab;
            }

            public void SetAudioSettings(AudioSettingsState settings)
            {
                BoundAudio = settings;
            }

            public void RaiseBack()
            {
                BackRequested?.Invoke();
            }

            public void RaiseTab(SettingsTab tab)
            {
                TabSelected?.Invoke(tab);
            }

            public void RaiseVolume(AudioChannel channel, int percent)
            {
                AudioVolumeChanged?.Invoke(channel, percent);
            }

            public void RaiseVoiceChat(bool enabled)
            {
                VoiceChatEnabledChanged?.Invoke(enabled);
            }
        }

        private sealed class FakeAudioSettings : IAudioSettings
        {
            public AudioSettingsState Current { get; } = new AudioSettingsState();

            public event Action<AudioSettingsState> Changed;

            public bool TrySetVolume(
                AudioChannel channel,
                int percent,
                out AudioSettingsError error)
            {
                if (!Current.TrySetVolume(channel, percent, out error))
                {
                    return false;
                }

                Changed?.Invoke(Current);
                return true;
            }

            public bool TrySetVoiceChatEnabled(bool enabled, out AudioSettingsError error)
            {
                if (!Current.TrySetVoiceChatEnabled(enabled, out error))
                {
                    return false;
                }

                Changed?.Invoke(Current);
                return true;
            }
        }

        private sealed class FakeHomeApplicationHost : IHomeApplicationHost
        {
            public int HomeOpenCount { get; private set; }

            public void Quit()
            {
            }

            public void OpenHome()
            {
                HomeOpenCount++;
            }

            public void OpenRoomBrowser()
            {
            }

            public void OpenSettings()
            {
            }
        }
    }
}
