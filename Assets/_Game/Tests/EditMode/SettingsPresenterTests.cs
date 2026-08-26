using System;
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
            Assert.That(appFlow.TryTransitionTo(AppFlowState.Settings), Is.True);

            using (var presenter = new SettingsPresenter(view, host, appFlow))
            {
                presenter.Start();
                Assert.That(view.ActiveTab, Is.EqualTo(SettingsTab.Graphics));

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
        public void Presenter_RequiresDependencies()
        {
            var view = new FakeSettingsView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();

            Assert.That(
                () => new SettingsPresenter(null, host, appFlow),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, null, appFlow),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new SettingsPresenter(view, host, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsTab ActiveTab { get; private set; }

            public event Action BackRequested;

            public event Action<SettingsTab> TabSelected;

            public void SetActiveTab(SettingsTab tab)
            {
                ActiveTab = tab;
            }

            public void RaiseBack()
            {
                BackRequested?.Invoke();
            }

            public void RaiseTab(SettingsTab tab)
            {
                TabSelected?.Invoke(tab);
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
