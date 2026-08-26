using System;
using System.Collections.Generic;
using Game.Client.Home;
using Game.Client.Rooms;
using Game.Core.Flow;
using Game.Core.Lobby;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class RoomBrowserPresenterTests
    {
        [Test]
        public void Presenter_Back_ReturnsToHome()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();
            Assert.That(appFlow.TryTransitionTo(AppFlowState.RoomBrowser), Is.True);

            using (var presenter = new RoomBrowserPresenter(view, rooms, host, appFlow))
            {
                presenter.Start();
                view.RaiseBack();
            }

            Assert.That(appFlow.CurrentState, Is.EqualTo(AppFlowState.Home));
            Assert.That(host.HomeOpenCount, Is.EqualTo(1));
            Assert.That(host.RoomBrowserOpenCount, Is.Zero);
        }

        [Test]
        public void Presenter_RequiresDependencies()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            var host = new FakeHomeApplicationHost();
            var appFlow = new AppFlowSystem();

            Assert.That(
                () => new RoomBrowserPresenter(null, rooms, host, appFlow),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new RoomBrowserPresenter(view, null, host, appFlow),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new RoomBrowserPresenter(view, rooms, null, appFlow),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new RoomBrowserPresenter(view, rooms, host, null),
                Throws.TypeOf<ArgumentNullException>());
        }

#pragma warning disable CS0067
        private sealed class FakeRoomBrowserView : IRoomBrowserView
        {
            public event Action<string> SearchTextChanged;
            public event Action RefreshRequested;
            public event Action RoomCodeSearchRequested;
            public event Action CreateRoomRequested;
            public event Action BackRequested;
            public event Action<string> RoomSelected;

            public IReadOnlyList<RoomSummary> Rooms { get; private set; }

            public void SetRooms(IReadOnlyList<RoomSummary> rooms)
            {
                Rooms = rooms;
            }

            public void RaiseBack()
            {
                BackRequested?.Invoke();
            }
        }
#pragma warning restore CS0067

        private sealed class FakeHomeApplicationHost : IHomeApplicationHost
        {
            public int HomeOpenCount { get; private set; }

            public int RoomBrowserOpenCount { get; private set; }

            public void Quit()
            {
            }

            public void OpenHome()
            {
                HomeOpenCount++;
            }

            public void OpenRoomBrowser()
            {
                RoomBrowserOpenCount++;
            }

            public void OpenSettings()
            {
            }
        }
    }
}
