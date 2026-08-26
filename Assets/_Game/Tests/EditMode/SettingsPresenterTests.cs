using System;
using Game.Client.Accessibility;
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
            var accessibility = new FakeAccessibilitySettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow, audio, accessibility))
            {
                presenter.Start();
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Graphics));
                Assert.That(view.BoundAudio, Is.SameAs(audio.Current));
                Assert.That(view.BoundAccessibility, Is.SameAs(accessibility.Current));

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
            var accessibility = new FakeAccessibilitySettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow, audio, accessibility))
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
        public void Presenter_ForwardsAccessibilityChangesToSettings()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            var accessibility = new FakeAccessibilitySettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow, audio, accessibility))
            {
                presenter.Start();
                view.RaiseUiScale(80);
                view.RaiseTextScale(20);
                view.RaiseHighContrast(true);
            }

            Assert.That(accessibility.Current.UiScale, Is.EqualTo(80));
            Assert.That(accessibility.Current.TextScale, Is.EqualTo(20));
            Assert.That(accessibility.Current.HighContrastEnabled, Is.True);
            Assert.That(view.BoundAccessibility.UiScale, Is.EqualTo(80));
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            var accessibility = new FakeAccessibilitySettings();

            Assert.That(
                () => new SettingsPresenter(null, host, appFlow, audio, accessibility),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, null, appFlow, audio, accessibility),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, null, audio, accessibility),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, null, accessibility),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, audio, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsTab ActiveTab { get; private set; }

            public AudioSettingsState BoundAudio { get; private set; }

            public AccessibilitySettingsState BoundAccessibility { get; private set; }

            public event Action BackRequested;

            public event Action<SettingsTab> TabSelected;

            public event Action<AudioChannel, int> AudioVolumeChanged;

            public event Action<bool> VoiceChatEnabledChanged;

            public event Action<int> UiScaleChanged;

            public event Action<int> TextScaleChanged;

            public event Action<bool> HighContrastChanged;

            public void SetActiveTab(SettingsTab tab)
            {
                ActiveTab = tab;
            }

            public void SetAudioSettings(AudioSettingsState settings)
            {
                BoundAudio = settings;
            }

            public void SetAccessibilitySettings(AccessibilitySettingsState settings)
            {
                BoundAccessibility = settings;
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

            public void RaiseUiScale(int percent)
            {
                UiScaleChanged?.Invoke(percent);
            }

            public void RaiseTextScale(int percent)
            {
                TextScaleChanged?.Invoke(percent);
            }

            public void RaiseHighContrast(bool enabled)
            {
                HighContrastChanged?.Invoke(enabled);
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

        private sealed class FakeAccessibilitySettings : IAccessibilitySettings
        {
            public AccessibilitySettingsState Current { get; } = new AccessibilitySettingsState();

            public event Action<AccessibilitySettingsState> Changed;

            public bool TrySetUiScale(int percent, out AccessibilitySettingsError error)
            {
                if (!Current.TrySetUiScale(percent, out error))
                {
                    return false;
                }

                Changed?.Invoke(Current);
                return true;
            }

            public bool TrySetTextScale(int percent, out AccessibilitySettingsError error)
            {
                if (!Current.TrySetTextScale(percent, out error))
                {
                    return false;
                }

                Changed?.Invoke(Current);
                return true;
            }

            public bool TrySetHighContrastEnabled(bool enabled, out AccessibilitySettingsError error)
            {
                if (!Current.TrySetHighContrastEnabled(enabled, out error))
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
