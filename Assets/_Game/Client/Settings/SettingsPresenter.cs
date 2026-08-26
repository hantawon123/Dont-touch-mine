using System;
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

        public SettingsPresenter(
            ISettingsView view,
            IHomeApplicationHost applicationHost,
            AppFlowSystem appFlow)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.applicationHost = applicationHost
                ?? throw new ArgumentNullException(nameof(applicationHost));
            this.appFlow = appFlow ?? throw new ArgumentNullException(nameof(appFlow));
        }

        public void Start()
        {
            view.BackRequested += OnBackRequested;
            view.TabSelected += OnTabSelected;
            view.SetActiveTab(SettingsTab.Graphics);
        }

        public void Dispose()
        {
            view.BackRequested -= OnBackRequested;
            view.TabSelected -= OnTabSelected;
        }

        private void OnTabSelected(SettingsTab tab)
        {
            view.SetActiveTab(tab);
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
