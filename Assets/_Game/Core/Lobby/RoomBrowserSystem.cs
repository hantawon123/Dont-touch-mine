using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Core.Rooms;
using R3;

namespace Game.Core.Lobby
{
    /// <summary>
    /// UI-facing room state. Network events update it through the sink ports,
    /// while presentation can only observe its R3 properties.
    /// </summary>
    public sealed class RoomBrowserSystem : IRoomListSink, IRoomSessionSink, IDisposable
    {
        private readonly ReactiveProperty<IReadOnlyList<RoomSummary>> rooms =
            new(Array.Empty<RoomSummary>());
        private readonly ReactiveProperty<bool> isBusy = new(false);
        private readonly ReactiveProperty<RoomEntryFailure> lastFailure =
            new(RoomEntryFailure.None);
        private readonly ReactiveProperty<string> roomCode = new(null);
        private readonly ReactiveProperty<int> playerCount = new(0);
        private readonly ReactiveProperty<int> maxPlayers = new(0);
        private readonly ReactiveProperty<RoomExitReason?> lastExit = new(null);

        private int activeOperations;

        public ReadOnlyReactiveProperty<IReadOnlyList<RoomSummary>> Rooms => rooms;
        public ReadOnlyReactiveProperty<bool> IsBusy => isBusy;
        public ReadOnlyReactiveProperty<RoomEntryFailure> LastFailure => lastFailure;
        public ReadOnlyReactiveProperty<string> RoomCode => roomCode;
        public ReadOnlyReactiveProperty<int> PlayerCount => playerCount;
        public ReadOnlyReactiveProperty<int> MaxPlayers => maxPlayers;
        public ReadOnlyReactiveProperty<RoomExitReason?> LastExit => lastExit;

        public void SetRooms(IReadOnlyList<RoomSummary> refreshedRooms)
        {
            if (refreshedRooms == null)
            {
                throw new ArgumentNullException(nameof(refreshedRooms));
            }

            var snapshot = new RoomSummary[refreshedRooms.Count];
            for (var index = 0; index < refreshedRooms.Count; index++)
            {
                snapshot[index] = refreshedRooms[index];
            }

            rooms.Value = snapshot;
        }

        public bool TryFindByCode(string candidate, out RoomSummary room)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var normalizedCode = candidate.Trim();
                var currentRooms = rooms.Value;

                for (var index = 0; index < currentRooms.Count; index++)
                {
                    if (string.Equals(
                            currentRooms[index].RoomId,
                            normalizedCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        room = currentRooms[index];
                        return true;
                    }
                }
            }

            room = default;
            return false;
        }

        public void PlayerCountChanged(int current, int max)
        {
            playerCount.Value = current;
            maxPlayers.Value = max;
            lastExit.Value = null;
        }

        public void RoomClosed(RoomExitReason reason)
        {
            playerCount.Value = 0;
            maxPlayers.Value = 0;
            roomCode.Value = null;
            lastExit.Value = reason;
        }

        internal void BeginOperation()
        {
            activeOperations++;
            isBusy.Value = true;
            lastFailure.Value = RoomEntryFailure.None;
        }

        internal void EndOperation()
        {
            activeOperations--;
            isBusy.Value = activeOperations > 0;
        }

        internal RoomEntryResult Record(RoomEntryResult result)
        {
            lastFailure.Value = result.Failure;
            roomCode.Value = result.Ok ? result.RoomCode : null;

            if (result.Ok)
            {
                lastExit.Value = null;
            }

            return result;
        }

        internal RoomEntryFailure Record(RoomEntryFailure failure)
        {
            lastFailure.Value = failure;
            return failure;
        }

        public void Dispose()
        {
            rooms.Dispose();
            isBusy.Dispose();
            lastFailure.Dispose();
            roomCode.Dispose();
            playerCount.Dispose();
            maxPlayers.Dispose();
            lastExit.Dispose();
        }
    }

    /// <summary>
    /// Commands a room adapter on behalf of UI without exposing Photon types.
    /// </summary>
    public sealed class RoomUiCommands
    {
        private readonly IRoomBrowser browser;
        private readonly RoomBrowserSystem state;

        public RoomUiCommands(IRoomBrowser browser, RoomBrowserSystem state)
        {
            this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public async UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation)
        {
            state.BeginOperation();

            try
            {
                return state.Record(await browser.RefreshAsync(cancellation));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                state.Record(RoomEntryFailure.ConnectionFailed);
                throw;
            }
            finally
            {
                state.EndOperation();
            }
        }

        public UniTask<RoomEntryResult> CreateAsync(
            RoomCreateRequest request,
            CancellationToken cancellation) =>
            TrackEntry(() => browser.CreateAsync(request, cancellation));

        public UniTask<RoomEntryResult> EnterAsync(
            RoomId room,
            string password,
            CancellationToken cancellation) =>
            TrackEntry(() => browser.EnterAsync(room, password, cancellation));

        public UniTask<RoomEntryResult> EnterByCodeAsync(
            string roomCode,
            string password,
            CancellationToken cancellation) =>
            TrackEntry(() => browser.EnterByCodeAsync(roomCode, password, cancellation));

        public async UniTask LeaveAsync(CancellationToken cancellation)
        {
            state.BeginOperation();

            try
            {
                await browser.LeaveAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                state.Record(RoomEntryFailure.ConnectionFailed);
                throw;
            }
            finally
            {
                state.EndOperation();
            }
        }

        private async UniTask<RoomEntryResult> TrackEntry(
            Func<UniTask<RoomEntryResult>> operation)
        {
            state.BeginOperation();

            try
            {
                return state.Record(await operation());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                state.Record(RoomEntryFailure.ConnectionFailed);
                throw;
            }
            finally
            {
                state.EndOperation();
            }
        }
    }
}
