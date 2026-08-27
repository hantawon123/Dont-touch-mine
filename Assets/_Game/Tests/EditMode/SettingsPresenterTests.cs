using System;
using Game.Client.Accessibility;
using Game.Client.Audio;
using Game.Client.Graphics;
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
            var graphics = new FakeGraphicsSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(
                view, host, appFlow, audio, accessibility, graphics))
            {
                presenter.Start();
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Graphics));
                Assert.That(view.BoundAudio, Is.SameAs(audio.Current));
                Assert.That(view.BoundAccessibility, Is.SameAs(accessibility.Current));
                Assert.That(view.BoundGraphics, Is.SameAs(graphics.Current));

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
            var graphics = new FakeGraphicsSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(
                view, host, appFlow, audio, accessibility, graphics))
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
            var graphics = new FakeGraphicsSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(
                view, host, appFlow, audio, accessibility, graphics))
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
        public void Presenter_ForwardsGraphicsChangesToSettings()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            var accessibility = new FakeAccessibilitySettings();
            var graphics = new FakeGraphicsSettings();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(
                view, host, appFlow, audio, accessibility, graphics))
            {
                presenter.Start();
                view.RaiseGraphics(GraphicsSetting.Quality, (int)GraphicsQualityPreset.Low);
                view.RaiseGraphics(GraphicsSetting.DisplayMode, (int)DisplayMode.Windowed);
                view.RaiseBrightness(20);
            }

            Assert.That(graphics.Current.Quality, Is.EqualTo(GraphicsQualityPreset.Low));
            Assert.That(graphics.Current.DisplayMode, Is.EqualTo(DisplayMode.Windowed));
            Assert.That(graphics.Current.Brightness, Is.EqualTo(20));
            Assert.That(view.BoundGraphics.Quality, Is.EqualTo(GraphicsQualityPreset.Low));
            Assert.That(view.BoundGraphics.Brightness, Is.EqualTo(20));
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            var audio = new FakeAudioSettings();
            var accessibility = new FakeAccessibilitySettings();
            var graphics = new FakeGraphicsSettings();

            Assert.That(
                () => new SettingsPresenter(null, host, appFlow, audio, accessibility, graphics),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, null, appFlow, audio, accessibility, graphics),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, null, audio, accessibility, graphics),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, null, accessibility, graphics),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, audio, null, graphics),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, appFlow, audio, accessibility, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsTab ActiveTab { get; private set; }

            public AudioSettingsState BoundAudio { get; private set; }

            public AccessibilitySettingsState BoundAccessibility { get; private set; }

            public GraphicsSettingsState BoundGraphics { get; private set; }

            public event Action BackRequested;

            public event Action<SettingsTab> TabSelected;

            public event Action<AudioChannel, int> AudioVolumeChanged;

            public event Action<bool> VoiceChatEnabledChanged;

            public event Action<int> UiScaleChanged;

            public event Action<int> TextScaleChanged;

            public event Action<bool> HighContrastChanged;

            public event Action<GraphicsSetting, int> GraphicsSettingChanged;

            public event Action<int> BrightnessChanged;

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

            public void SetGraphicsSettings(GraphicsSettingsState settings)
            {
                BoundGraphics = settings;
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

            public void RaiseGraphics(GraphicsSetting setting, int index)
            {
                GraphicsSettingChanged?.Invoke(setting, index);
            }

            public void RaiseBrightness(int percent)
            {
                BrightnessChanged?.Invoke(percent);
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

        private sealed class FakeGraphicsSettings : IGraphicsSettings
        {
            public GraphicsSettingsState Current { get; } = new GraphicsSettingsState();

            public event Action<GraphicsSettingsState> Changed;

            public bool TrySetQuality(GraphicsQualityPreset quality, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetQuality(quality, out error));
            }

            public bool TrySetResolution(int index, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetResolution(index, out error));
            }

            public bool TrySetDisplayMode(DisplayMode mode, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetDisplayMode(mode, out error));
            }

            public bool TrySetFrameCap(int index, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetFrameCap(index, out error));
            }

            public bool TrySetShadows(ShadowQualityLevel shadows, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetShadows(shadows, out error));
            }

            public bool TrySetEffects(EffectsQualityLevel effects, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetEffects(effects, out error));
            }

            public bool TrySetAntiAliasing(AntiAliasingMode antiAliasing, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetAntiAliasing(antiAliasing, out error));
            }

            public bool TrySetBrightness(int percent, out GraphicsSettingsError error)
            {
                return Persist(Current.TrySetBrightness(percent, out error));
            }

            private bool Persist(bool succeeded)
            {
                if (succeeded)
                {
                    Changed?.Invoke(Current);
                }

                return succeeded;
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
