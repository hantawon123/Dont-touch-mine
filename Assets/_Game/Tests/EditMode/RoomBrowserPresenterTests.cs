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
        public void Presenter_KickHasItsOwnMessageAndCanBeAcknowledged()
        {
            using var rooms = new RoomBrowserSystem();
            rooms.RoomClosed(RoomExitReason.Kicked);
            var view = new FakeRoomBrowserView();
            using var presenter = new RoomBrowserPresenter(view, rooms,
                new FakeHomeApplicationHost(), new AppFlowSystem());
            presenter.Start();
            Assert.That(view.DisconnectionMessage, Is.EqualTo("방장에 의해 강퇴되었습니다"));
            view.AcknowledgeDisconnection();
            Assert.That(rooms.LastExit.CurrentValue, Is.Null);
        }

        [Test]
        public void Presenter_HostDisconnectIsShownOnceUntilAcknowledged_AndNotForVoluntaryExit()
        {
            using var rooms = new RoomBrowserSystem();
            var host = new FakeHomeApplicationHost();
            var flow = new AppFlowSystem();
            rooms.RoomClosed(RoomExitReason.HostClosed); // Arrives before the browser scene exists.
            var view = new FakeRoomBrowserView();
            using (var presenter = new RoomBrowserPresenter(view, rooms, host, flow))
            {
                presenter.Start();
                Assert.That(view.DisconnectionMessage, Is.EqualTo("호스트의 연결이 끊어졌습니다"));
                Assert.That(view.DisconnectionCount, Is.EqualTo(1));
                rooms.RoomClosed(RoomExitReason.HostClosed); // Duplicate network callback.
                Assert.That(view.DisconnectionCount, Is.EqualTo(1));
                view.AcknowledgeDisconnection();
                Assert.That(rooms.LastExit.CurrentValue, Is.Null);
            }
            var reopened = new FakeRoomBrowserView();
            using var next = new RoomBrowserPresenter(reopened, rooms, host, flow);
            next.Start();
            Assert.That(reopened.DisconnectionCount, Is.Zero);
            rooms.RoomClosed(RoomExitReason.Left);
            Assert.That(reopened.DisconnectionCount, Is.Zero);
            rooms.RoomClosed(RoomExitReason.HostClosed); // A subsequent room can also close.
            Assert.That(reopened.DisconnectionCount, Is.EqualTo(1));
        }

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
            public event Action DisconnectionAcknowledged;
            public string DisconnectionMessage { get; private set; }
            public int DisconnectionCount { get; private set; }
            public void ShowDisconnection(string message)
            {
                DisconnectionMessage = message;
                DisconnectionCount++;
            }
            public void AcknowledgeDisconnection() => DisconnectionAcknowledged?.Invoke();

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

            public int LobbyOpenCount { get; private set; }

            public void OpenLobby()
            {
                LobbyOpenCount++;
            }

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
        }
    }
}
