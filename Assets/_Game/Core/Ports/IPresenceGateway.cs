using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>
    /// Which kind of room this player is in.
    /// </summary>
    /// <remarks>
    /// The lobby and the match are the same Photon room: people gather, the
    /// host starts, and that room moves to the match scene. Its name does not
    /// change, so the server cannot tell the two apart from the session id and
    /// this client has to say. Friends see the difference as "can be called
    /// over now" against "busy until the match ends".
    /// </remarks>
    public enum RoomSessionKind
    {
        /// <summary>Waiting for people. The server shows IN_LOBBY.</summary>
        Lobby,

        /// <summary>The match has started. The server shows IN_GAME.</summary>
        Match
    }

    /// <summary>
    /// Tells the backend where this player is, so friends see them right.
    /// </summary>
    /// <remarks>
    /// Being online is no longer something this port reports. The realtime
    /// notification link says that by existing: while it is up the server counts
    /// this player as online, and when its last connection drops they go
    /// offline at once. What remains to say is the room — entering one, leaving
    /// it, and the lobby turning into a match — and this port says only that.
    /// <para>
    /// Reports once per call and schedules nothing. Deciding when to report
    /// belongs to whatever knows the session, and the answer now is "when it
    /// changes", never on a timer.
    /// </para>
    /// </remarks>
    public interface IPresenceGateway
    {
        /// <summary>
        /// Reports this player as out of any room, or as in the room named by
        /// <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="sessionId">
        /// The Photon room, or null or empty when not in one. There is no
        /// separate status argument on purpose: with one, "in a game with no
        /// room" would be expressible and would need a rule to forbid it.
        /// </param>
        /// <param name="kind">
        /// Lobby or match. Ignored when <paramref name="sessionId"/> is empty,
        /// because out of a room there is nothing to be either of.
        /// </param>
        UniTask<BackendResult> ReportAsync(
            string sessionId, RoomSessionKind kind, CancellationToken cancellation);

        /// <summary>
        /// Reports this player as gone, before quitting.
        /// </summary>
        /// <remarks>
        /// The dropped socket would say the same thing a moment later. Saying it
        /// first is cheap and spares friends the moment.
        /// </remarks>
        UniTask<BackendResult> GoOfflineAsync(CancellationToken cancellation);
    }
}
