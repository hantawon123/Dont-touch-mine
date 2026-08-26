using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Lobby;
using Game.Core.Ports;
using Game.Core.Rooms;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pins the sequence a client goes through to enter the first listed room:
    /// refresh, read the list, enter what it found.
    /// </summary>
    /// <remarks>
    /// Written because a client stopped entering rooms and the logs could not say
    /// whether the fault was in this logic or in the environment around it. The
    /// steps here mirror <c>SessionAutoConnect.EnterFirstListed</c>; that method
    /// is private and needs Fusion to construct its owner, so the collaborators
    /// it drives are exercised directly instead.
    /// <para>
    /// No Photon and no play mode. The room list arrives through
    /// <see cref="IRoomListSink"/> exactly as the network pushes it, so the timing
    /// that matters can be reproduced without two peers.
    /// </para>
    /// </remarks>
    public sealed class RoomEntryFlowTests
    {
        private static RoomSummary AnyRoom(string code = "ABC123", string hostNickname = null)
        {
            return new RoomSummary(
                new RoomId(code),
                "hoons rooms",
                "market-01",
                1,
                6,
                false,
                true,
                RoomStatus.Waiting,
                hostNickname);
        }

        [Test]
        public void ListedRoom_IsVisibleThroughTheSystem()
        {
            var state = new RoomBrowserSystem();

            ((IRoomListSink)state).SetRooms(new[] { AnyRoom() });

            Assert.That(state.Rooms.CurrentValue.Count, Is.EqualTo(1));
        }

        [Test]
        public void RefreshSucceeding_LeavesTheRoomListAlone()
        {
            var state = new RoomBrowserSystem();
            var browser = new FakeBrowser();
            var commands = new RoomUiCommands(browser, state);

            // The list arrives while the refresh is still in flight, which is the
            // order the logs showed: the lobby answers, the listing follows, and
            // only then does the caller get to look. Bookkeeping around the
            // refresh must not discard what arrived during it.
            browser.DuringRefresh = () => ((IRoomListSink)state).SetRooms(new[] { AnyRoom() });

            var failure = commands.RefreshAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(failure, Is.EqualTo(RoomEntryFailure.None));
            Assert.That(state.Rooms.CurrentValue.Count, Is.EqualTo(1));
        }

        [Test]
        public void FirstListedRoom_IsTheOneEntered()
        {
            var state = new RoomBrowserSystem();
            var browser = new FakeBrowser();
            var commands = new RoomUiCommands(browser, state);

            browser.DuringRefresh = () => ((IRoomListSink)state).SetRooms(
                new[] { AnyRoom("FIRST1"), AnyRoom("SECOND") });

            // The three steps EnterFirstListed takes, in its order.
            commands.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            var target = state.Rooms.CurrentValue[0];
            var result = commands.EnterAsync(target.Id, "1111", CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(result.Ok, Is.True);
            Assert.That(browser.EnteredRoom.ToString(), Is.EqualTo(target.Id.ToString()));
            Assert.That(browser.EnteredPassword, Is.EqualTo("1111"));
        }

        [Test]
        public void RefreshFailing_IsReportedAndLeavesNothingToEnter()
        {
            var state = new RoomBrowserSystem();
            var browser = new FakeBrowser { RefreshFailure = RoomEntryFailure.ConnectionFailed };
            var commands = new RoomUiCommands(browser, state);

            var failure = commands.RefreshAsync(CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(failure, Is.EqualTo(RoomEntryFailure.ConnectionFailed));
            Assert.That(state.Rooms.CurrentValue.Count, Is.EqualTo(0));
            Assert.That(browser.EnterCalls, Is.EqualTo(0));
        }

        /// <summary>
        /// The host's name has to survive the listing, because that is the only
        /// place a player sees whose room they are about to enter.
        /// </summary>
        [Test]
        public void HostNickname_SurvivesTheListing()
        {
            var state = new RoomBrowserSystem();

            ((IRoomListSink)state).SetRooms(new[] { AnyRoom(hostNickname: "심재훈") });

            Assert.That(state.Rooms.CurrentValue[0].HostNickname, Is.EqualTo("심재훈"));
        }

        private sealed class FakeBrowser : IRoomBrowser
        {
            public RoomEntryFailure RefreshFailure = RoomEntryFailure.None;

            /// <summary>
            /// Runs while a refresh is in flight, so a test can deliver the room
            /// list at the moment the network would.
            /// </summary>
            public Action DuringRefresh;

            public RoomId EnteredRoom;
            public string EnteredPassword;
            public int EnterCalls;

            public UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation)
            {
                DuringRefresh?.Invoke();
                return UniTask.FromResult(RefreshFailure);
            }

            public UniTask<RoomEntryResult> EnterAsync(
                RoomId room, string password, CancellationToken cancellation)
            {
                EnterCalls++;
                EnteredRoom = room;
                EnteredPassword = password;
                return UniTask.FromResult(RoomEntryResult.Entered());
            }

            public UniTask<RoomEntryResult> CreateAsync(
                RoomCreateRequest request, CancellationToken cancellation)
            {
                return UniTask.FromResult(RoomEntryResult.Opened("ABC123"));
            }

            public UniTask<RoomEntryResult> EnterByCodeAsync(
                string roomCode, string password, CancellationToken cancellation)
            {
                return UniTask.FromResult(RoomEntryResult.Entered());
            }

            public UniTask LeaveAsync(CancellationToken cancellation)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
