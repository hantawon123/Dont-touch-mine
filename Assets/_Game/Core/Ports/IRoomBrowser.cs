using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Rooms;

namespace Game.Core.Ports
{
    /// <summary>
    /// Opening and entering rooms. Implemented by whichever layer talks to
    /// matchmaking, called by presentation.
    /// </summary>
    /// <remarks>
    /// These are request-and-answer operations, so they return the answer rather
    /// than reporting it through a separate channel: the caller knows which
    /// request each outcome belongs to and can cancel a request it no longer
    /// wants. The room list is the exception and arrives through
    /// <see cref="IRoomListSink"/>, because it is pushed rather than requested.
    /// <para>
    /// No method takes or returns a matchmaking address. Rooms are picked by the
    /// opaque <see cref="RoomId"/> from the list, or by a code the player typed.
    /// A code identifies a room but grants nothing: entering a locked room needs
    /// its password either way, so a leaked code cannot be used to walk in.
    /// </para>
    /// </remarks>
    public interface IRoomBrowser
    {
        /// <summary>
        /// Asks for a fresh room list. Completes once the request settles, so a
        /// refresh control can show progress and report failure. Updated rooms
        /// arrive through <see cref="IRoomListSink"/>.
        /// </summary>
        UniTask<RoomEntryFailure> RefreshAsync(CancellationToken cancellation);

        /// <summary>
        /// Opens a new room and enters it as its authority. The issued code is
        /// returned so the host can share it.
        /// </summary>
        UniTask<RoomEntryResult> CreateAsync(
            RoomCreateRequest request, CancellationToken cancellation);

        /// <summary>
        /// Enters a room picked from the list. A locked room needs its password.
        /// </summary>
        UniTask<RoomEntryResult> EnterAsync(
            RoomId room, string password, CancellationToken cancellation);

        /// <summary>
        /// Enters a room by the code its host shared. A code only says which
        /// room to enter, so a locked room still needs its password.
        /// </summary>
        /// <param name="password">
        /// May be empty on a first attempt: an open room lets it through, and a
        /// locked one answers <see cref="RoomEntryFailure.WrongPassword"/>, which
        /// is the cue to ask the player for it.
        /// </param>
        UniTask<RoomEntryResult> EnterByCodeAsync(
            string roomCode, string password, CancellationToken cancellation);

        /// <summary>
        /// Leaves the current room. Does nothing when not in one.
        /// </summary>
        /// <remarks>
        /// Completing this only means the request went through. The room ending
        /// is reported through <see cref="IRoomSessionSink.RoomClosed"/> like any
        /// other exit, so presentation handles every departure in one place.
        /// </remarks>
        UniTask LeaveAsync(CancellationToken cancellation);
    }
}
