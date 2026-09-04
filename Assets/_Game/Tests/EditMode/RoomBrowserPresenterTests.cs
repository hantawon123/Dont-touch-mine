using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
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

        /// <summary>
        /// The screen has no other way of knowing a request is out. Losing this
        /// subscription still compiles and still runs; the refresh button simply
        /// stops saying it is working, and nothing fails until someone notices.
        /// </summary>
        [Test]
        public void Presenter_ReportsWhetherARequestIsRunning()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            var browser = new PendingRoomBrowser();
            var commands = new RoomUiCommands(browser, rooms);
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            Assert.That(view.IsBusy, Is.False);

            commands.RefreshAsync(CancellationToken.None).Forget();
            Assert.That(view.IsBusy, Is.True, "A running request must reach the view.");

            browser.CompleteRefresh(RoomEntryFailure.None);
            Assert.That(view.IsBusy, Is.False, "A finished request must release it.");
        }

        [Test]
        public void Presenter_ReportsWhyEnteringARoomFailed()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            var browser = new PendingRoomBrowser
            {
                EntryResult = RoomEntryResult.Failed(RoomEntryFailure.Full),
            };
            var commands = new RoomUiCommands(browser, rooms);
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            Assert.That(view.LastFailure, Is.EqualTo(RoomEntryFailure.None));

            commands.EnterAsync(new RoomId("room"), null, CancellationToken.None).Forget();

            Assert.That(view.LastFailure, Is.EqualTo(RoomEntryFailure.Full));
        }

        [Test]
        public void Presenter_Search_KeepsOnlyMatchingRooms()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            rooms.SetRooms(new[] { Room("숨바꼭질 방"), Room("술래잡기 방") });

            view.RaiseSearch("숨바꼭질");

            Assert.That(view.Rooms.Count, Is.EqualTo(1));
            Assert.That(view.Rooms[0].DisplayName, Is.EqualTo("숨바꼭질 방"));
            Assert.That(view.EmptyMessage, Is.Null);
        }

        /// <summary>
        /// The list arrives again on every refresh, and it must arrive filtered:
        /// a player who searched and then refreshed did not ask for the search
        /// to be dropped.
        /// </summary>
        [Test]
        public void Presenter_Search_SurvivesARefreshedList()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            rooms.SetRooms(new[] { Room("숨바꼭질 방"), Room("술래잡기 방") });
            view.RaiseSearch("숨바꼭질");

            rooms.SetRooms(new[]
            {
                Room("숨바꼭질 방"),
                Room("술래잡기 방"),
                Room("새로 열린 방"),
            });

            Assert.That(view.Rooms.Count, Is.EqualTo(1));
            Assert.That(view.Rooms[0].DisplayName, Is.EqualTo("숨바꼭질 방"));
        }

        [Test]
        public void Presenter_ClearingTheSearch_BringsEveryRoomBack()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            rooms.SetRooms(new[] { Room("숨바꼭질 방"), Room("술래잡기 방") });
            view.RaiseSearch("숨바꼭질");

            view.RaiseSearch(string.Empty);

            Assert.That(view.Rooms.Count, Is.EqualTo(2));
            Assert.That(view.EmptyMessage, Is.Null);
        }

        /// <summary>
        /// An empty list has two causes and the player can act on only one of
        /// them, so it is not enough to show nothing.
        /// </summary>
        [Test]
        public void Presenter_TellsAnEmptySearchApartFromAnEmptyLobby()
        {
            using var rooms = new RoomBrowserSystem();
            var view = new FakeRoomBrowserView();
            using var presenter = new RoomBrowserPresenter(
                view, rooms, new FakeHomeApplicationHost(), new AppFlowSystem());

            presenter.Start();
            Assert.That(view.EmptyMessage, Is.EqualTo(RoomBrowserPresenter.NoRooms));

            rooms.SetRooms(new[] { Room("숨바꼭질 방") });
            view.RaiseSearch("없는 이름");

            Assert.That(view.EmptyMessage, Is.EqualTo(RoomBrowserPresenter.NoSearchResults));
        }

        private static RoomSummary Room(string title)
        {
            return new RoomSummary(
                new RoomId(title),
                title,
                "playground",
                1,
                RoomSettings.MaxPlayerCount,
                isLocked: false,
                isOpen: true);
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

        /// <summary>
        /// Holds a refresh open so a test can look at the screen while a request
        /// is still out, and answers an entry however the test asks it to.
        /// </summary>
        private sealed class PendingRoomBrowser : IRoomBrowser
        {
            private UniTaskCompletionSource<RoomEntryFailure> pendingRefresh;

            public RoomEntryResult EntryResult = RoomEntryResult.Entered();

            public UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation)
            {
                pendingRefresh = new UniTaskCompletionSource<RoomEntryFailure>();
                return pendingRefresh.Task;
            }

            public void CompleteRefresh(RoomEntryFailure failure)
            {
                pendingRefresh?.TrySetResult(failure);
                pendingRefresh = null;
            }

            public UniTask<RoomEntryResult> CreateAsync(
                RoomCreateRequest request, CancellationToken cancellation) =>
                UniTask.FromResult(EntryResult);

            public UniTask<RoomEntryResult> EnterAsync(
                RoomId room, string password, CancellationToken cancellation) =>
                UniTask.FromResult(EntryResult);

            public UniTask<RoomEntryResult> EnterByCodeAsync(
                string roomCode, string password, CancellationToken cancellation) =>
                UniTask.FromResult(EntryResult);

            public UniTask LeaveAsync(CancellationToken cancellation) =>
                UniTask.CompletedTask;
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
            public bool IsBusy { get; private set; }

            public void SetRooms(IReadOnlyList<RoomSummary> rooms)
            {
                Rooms = rooms;
            }

            public void SetBusy(bool busy)
            {
                IsBusy = busy;
            }

            public string EmptyMessage { get; private set; }

            public void SetEmptyMessage(string message)
            {
                EmptyMessage = message;
            }

            public void RaiseSearch(string text) => SearchTextChanged?.Invoke(text);

            public RoomEntryFailure LastFailure { get; private set; }

            public void ShowEntryFailure(RoomEntryFailure failure)
            {
                LastFailure = failure;
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
