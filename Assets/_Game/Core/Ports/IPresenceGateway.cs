using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;

namespace Game.Core.Ports
{
    /// <summary>
    /// Tells the backend this player is here, so friends see them as online.
    /// </summary>
    /// <remarks>
    /// The backend does no realtime communication of its own — Photon does that
    /// — so a player who stops reporting simply looks offline to their friends.
    /// <para>
    /// This port reports once per call and schedules nothing. Deciding when to
    /// report belongs to whatever knows the session: a heartbeat every 30
    /// seconds so that missing two still fits inside the server's 90 second
    /// timeout, plus one report at each Photon transition so a state change is
    /// not up to 30 seconds late.
    /// </para>
    /// </remarks>
    public interface IPresenceGateway
    {
        /// <summary>
        /// Reports this player as online, or as in a game when
        /// <paramref name="sessionId"/> names the room they are in.
        /// </summary>
        /// <param name="sessionId">
        /// The Photon room, or null or empty when not in one. There is no
        /// separate status argument on purpose: with one, "in a game with no
        /// room" would be expressible and would need a rule to forbid it.
        /// </param>
        UniTask<BackendResult> ReportAsync(string sessionId, CancellationToken cancellation);

        /// <summary>
        /// Reports this player as gone, before quitting.
        /// </summary>
        /// <remarks>
        /// Without it the player keeps appearing online to friends until the
        /// server's timeout expires. A crash cannot send this, which is what the
        /// timeout is for.
        /// </remarks>
        UniTask<BackendResult> GoOfflineAsync(CancellationToken cancellation);
    }
}
