using System;
using Game.Client.Audio;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Settings;
using VContainer.Unity;

namespace Game.Client.Settings
{
    public sealed class SettingsPresenter : IStartable, IDisposable
    {
        private readonly ISettingsView view;
        private readonly IHomeApplicationHost applicationHost;
        private readonly AppFlowSystem appFlow;
        private readonly IAudioSettings audioSettings;

        public SettingsPresenter(
            ISettingsView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow,
            IAudioSettings audioSettings)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
            this.audioSettings = audioSettings
                ?? throw new ArgumentNullException(nameof(audioSettings));
        }

        public void Start()
        {
            view.BackRequested += OnBackRequested;
            view.TabSelected += OnTabSelected;
            view.AudioVolumeChanged += OnAudioVolumeChanged;
            view.VoiceChatEnabledChanged += OnVoiceChatEnabledChanged;
            audioSettings.Changed += OnAudioSettingsChanged;
            view.SetActiveTab(SettingsTab.Graphics);
            view.SetAudioSettings(audioSettings.Current);
        }

        public void Dispose()
        {
            view.BackRequested -= OnBackRequested;
            view.TabSelected -= OnTabSelected;
            view.AudioVolumeChanged -= OnAudioVolumeChanged;
            view.VoiceChatEnabledChanged -= OnVoiceChatEnabledChanged;
            audioSettings.Changed -= OnAudioSettingsChanged;
        }

        private void OnTabSelected(SettingsTab tab)
        {
            view.SetActiveTab(tab);
        }

        private void OnAudioVolumeChanged(AudioChannel channel, int percent)
        {
            audioSettings.TrySetVolume(channel, percent, out _);
        }

        private void OnVoiceChatEnabledChanged(bool enabled)
        {
            audioSettings.TrySetVoiceChatEnabled(enabled, out _);
        }

        private void OnAudioSettingsChanged(AudioSettingsState settings)
        {
            view.SetAudioSettings(settings);
        }

        private void OnBackRequested()
        {
            if (appFlow.CurrentState != AppFlowState.Home &&
                !appFlow.TryTransitionTo(AppFlowState.Home))
            {
                return;
            }

            applicationHost.OpenHome();
        }
    }
}
