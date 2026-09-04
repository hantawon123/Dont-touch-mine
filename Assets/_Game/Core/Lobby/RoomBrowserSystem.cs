using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Match;
using Game.Core.Ports;
using Game.Core.Rooms;
using R3;

namespace Game.Core.Lobby
{
    /// <summary>
    /// UI-facing room state. Network events update it through the sink ports,
    /// while presentation can only observe its R3 properties.
    /// </summary>
    public sealed class RoomBrowserSystem :
        IRoomListSink, IRoomSessionSink, IRoomParticipantSink, IMatchStartSink, IDisposable
    {
        private readonly ReactiveProperty<IReadOnlyList<RoomSummary>> rooms =
            new(Array.Empty<RoomSummary>());
        private readonly ReactiveProperty<bool> isBusy = new(false);
        private readonly ReactiveProperty<RoomEntryFailure> lastFailure =
            new(RoomEntryFailure.None);
        private readonly ReactiveProperty<string> roomCode = new(null);
        private readonly ReactiveProperty<bool> isInRoom = new(false);
        private readonly ReactiveProperty<int> playerCount = new(0);
        private readonly ReactiveProperty<int> maxPlayers = new(0);
        private readonly ReactiveProperty<RoomExitReason?> lastExit = new(null);
        private readonly ReactiveProperty<IReadOnlyList<RoomParticipant>> participants =
            new(Array.Empty<RoomParticipant>());
        private readonly ReactiveProperty<string> localPlayerId = new(null);
        private readonly ReactiveProperty<IReadOnlyList<MatchParticipant>> matchParticipants =
            new(Array.Empty<MatchParticipant>());
        private readonly ReactiveProperty<RoomStartResult?> lastStartRefusal = new(null);

        private int activeOperations;

        public ReadOnlyReactiveProperty<IReadOnlyList<RoomSummary>> Rooms => rooms;
        public ReadOnlyReactiveProperty<bool> IsBusy => isBusy;
        public ReadOnlyReactiveProperty<RoomEntryFailure> LastFailure => lastFailure;
        public ReadOnlyReactiveProperty<string> RoomCode => roomCode;
        public ReadOnlyReactiveProperty<bool> IsInRoom => isInRoom;
        public ReadOnlyReactiveProperty<int> PlayerCount => playerCount;
        public ReadOnlyReactiveProperty<int> MaxPlayers => maxPlayers;
        public ReadOnlyReactiveProperty<RoomExitReason?> LastExit => lastExit;

        /// <summary>Everyone in the room, ordered by seat.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<RoomParticipant>> Participants => participants;

        /// <summary>
        /// The id of the person at this screen. Compare it against a
        /// participant's id to find yourself; the room itself does not say,
        /// because the answer differs per screen.
        /// </summary>
        public ReadOnlyReactiveProperty<string> LocalPlayerId => localPlayerId;

        /// <summary>
        /// The confirmed line-up once a match has started, in play order. Empty
        /// while the room is still waiting.
        /// </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<MatchParticipant>> MatchParticipants =>
            matchParticipants;

        /// <summary>True once the authority has confirmed a match.</summary>
        public bool IsMatchStarted => matchParticipants.CurrentValue.Count > 0;

        /// <summary>
        /// Why the last start request from this peer was turned down. Null when
        /// none was refused.
        /// </summary>
        public ReadOnlyReactiveProperty<RoomStartResult?> LastStartRefusal => lastStartRefusal;

        /// <summary>
        /// Where the local player sits in the confirmed line-up, or -1 before a
        /// match starts. This is the index the match rules use, not a seat.
        /// </summary>
        public int LocalPlayerIndex
        {
            get
            {
                var id = localPlayerId.CurrentValue;
                var playing = matchParticipants.CurrentValue;

                for (var index = 0; index < playing.Count; index++)
                {
                    if (string.Equals(playing[index].PlayerId, id, StringComparison.Ordinal))
                    {
                        return index;
                    }
                }

                return -1;
            }
        }

        public void MatchStarted(IReadOnlyList<MatchParticipant> participants)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            // Copied because the caller reuses its buffer.
            var snapshot = new MatchParticipant[participants.Count];
            for (var index = 0; index < participants.Count; index++)
            {
                snapshot[index] = participants[index];
            }

            matchParticipants.Value = snapshot;

            if (snapshot.Length > 0)
            {
                lastStartRefusal.Value = null;
            }
        }

        public void MatchStartRefused(RoomStartResult reason)
        {
            lastStartRefusal.Value = reason;
        }

        public void SetParticipants(IReadOnlyList<RoomParticipant> refreshed)
        {
            if (refreshed == null)
            {
                throw new ArgumentNullException(nameof(refreshed));
            }

            // Copied because the caller reuses its buffer between rebuilds.
            var snapshot = new RoomParticipant[refreshed.Count];
            for (var index = 0; index < refreshed.Count; index++)
            {
                snapshot[index] = refreshed[index];
            }

            participants.Value = snapshot;
        }

        public void SetLocalPlayer(string playerId)
        {
            localPlayerId.Value = playerId;
        }

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

            Array.Sort(snapshot, CompareForListing);
            rooms.Value = snapshot;
        }

        /// <summary>
        /// Newest room first, and rooms opened in the same second by name, so a
        /// player watching the list sees it hold still except where it changed.
        /// </summary>
        /// <remarks>
        /// Matchmaking hands the rooms over in whatever order it happens to hold
        /// them, and that order moves between refreshes. Sorting here rather
        /// than in the view keeps every reader of the list — the browser, a
        /// search, a test — looking at the same order.
        /// </remarks>
        private static int CompareForListing(RoomSummary left, RoomSummary right)
        {
            var byAge = right.OpenedAt.CompareTo(left.OpenedAt);
            if (byAge != 0)
            {
                return byAge;
            }

            var byName = CompareNames(left.DisplayName, right.DisplayName);

            // Two rooms can share a name. Falling back to the id keeps the order
            // from depending on which one matchmaking listed first.
            return byName != 0
                ? byName
                : string.Compare(left.RoomId, right.RoomId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Korean first, then Latin, then digits, and alphabetically within each.
        /// </summary>
        /// <remarks>
        /// Ordinal order alone would run the other way round, because Unicode
        /// puts digits before Latin and Latin far before Hangul. Players read
        /// this list in Korean, so Korean names come first.
        /// </remarks>
        private static int CompareNames(string left, string right)
        {
            var byScript = ScriptRank(left).CompareTo(ScriptRank(right));
            if (byScript != 0)
            {
                return byScript;
            }

            // Case-insensitive so 'apple' and 'Apple' sit together, then ordinal
            // so the two of them still have a fixed order between refreshes.
            var byName = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            return byName != 0
                ? byName
                : string.Compare(left, right, StringComparison.Ordinal);
        }

        /// <summary>
        /// Which group a room name sorts into, read from its first character.
        /// Anything else — punctuation, an emoji — sorts last.
        /// </summary>
        private static int ScriptRank(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 4;
            }

            var first = name[0];

            if (first >= '가' && first <= '힣')
            {
                return 0;
            }

            // Standalone jamo live in their own block below the syllables, so
            // ordinal order alone would raise a name like ㅋㅋㅋ above 가나다.
            // They are Korean, but they come after whole syllables.
            if (first >= 'ㄱ' && first <= 'ㆎ')
            {
                return 1;
            }

            if ((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))
            {
                return 2;
            }

            return first >= '0' && first <= '9' ? 3 : 4;
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

        public bool TryFindById(string candidate, out RoomSummary room)
        {
            if (!string.IsNullOrEmpty(candidate))
            {
                var currentRooms = rooms.Value;

                for (var index = 0; index < currentRooms.Count; index++)
                {
                    if (string.Equals(
                            currentRooms[index].RoomId,
                            candidate,
                            StringComparison.Ordinal))
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
            isInRoom.Value = false;
            playerCount.Value = 0;
            maxPlayers.Value = 0;
            roomCode.Value = null;
            lastExit.Value = reason;
        }

        public void AcknowledgeExit() => lastExit.Value = null;

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
                isInRoom.Value = true;
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
            isInRoom.Dispose();
            playerCount.Dispose();
            maxPlayers.Dispose();
            lastExit.Dispose();
            participants.Dispose();
            localPlayerId.Dispose();
            matchParticipants.Dispose();
            lastStartRefusal.Dispose();
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
