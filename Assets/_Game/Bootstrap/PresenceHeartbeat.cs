using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;
using Game.Network.Session;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Tells the backend this player is here, and which room they are in.
    /// </summary>
    /// <remarks>
    /// The backend does no realtime communication, so a player who stops
    /// reporting simply looks offline to their friends. This is the only thing
    /// that keeps them looking online.
    /// <para>
    /// Two cadences in one loop. Every 30 seconds so that missing two reports
    /// still fits inside the server's 90 second timeout, and immediately
    /// whenever the room changes, because a periodic report alone would leave a
    /// player looking like they are in the lobby for up to 30 seconds after they
    /// entered a match.
    /// </para>
    /// </remarks>
    public sealed class PresenceHeartbeat : IAsyncStartable, IDisposable
    {
        /// <summary>Matches what the client guide asks of every client.</summary>
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How often the room is checked for a change. Short enough that
        /// entering a match shows up as in-game almost at once, and cheap
        /// because a check that finds nothing new sends nothing.
        /// </summary>
        private static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

        private readonly IPresenceGateway presence;
        private readonly BackendSignIn signIn;
        private readonly NetworkRunnerService network;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        private string reportedSessionId;
        private bool hasReported;

        public PresenceHeartbeat(
            IPresenceGateway presence,
            BackendSignIn signIn,
            NetworkRunnerService network)
        {
            this.presence = presence ?? throw new ArgumentNullException(nameof(presence));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            this.network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (!await signIn.Ready)
            {
                // No account, so there is nobody to report as. Reporting anyway
                // would fail every 30 seconds for the rest of the session.
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, lifetime.Token);

            var sinceLastReport = TimeSpan.Zero;

            while (!linked.Token.IsCancellationRequested)
            {
                var current = CurrentSessionId();
                var roomChanged = hasReported
                    && !string.Equals(current, reportedSessionId, StringComparison.Ordinal);

                if (!hasReported || roomChanged || sinceLastReport >= Interval)
                {
                    await ReportAsync(current, linked.Token);
                    sinceLastReport = TimeSpan.Zero;
                }

                if (await UniTask.Delay(Poll, cancellationToken: linked.Token)
                        .SuppressCancellationThrow())
                {
                    return;
                }

                sinceLastReport += Poll;
            }
        }

        /// <summary>
        /// Reports this player as gone on the way out, so friends do not watch a
        /// ghost until the timeout expires.
        /// </summary>
        /// <remarks>
        /// Started but not waited for. A quit does not give the application a
        /// reliable window to finish a request in, and holding the quit open for
        /// one would trade a certain delay for an uncertain saving. When it does
        /// not land — here, or in a crash, which cannot send anything at all —
        /// the server's 90 second timeout is what covers it.
        /// </remarks>
        public void Dispose()
        {
            lifetime.Cancel();

            if (hasReported)
            {
                presence.GoOfflineAsync(CancellationToken.None).Forget();
            }

            lifetime.Dispose();
        }

        /// <remarks>
        /// The room's Photon session name, which is what identifies a room to
        /// anyone else. Null while not in one, which the server reads as being
        /// online rather than in a game.
        /// </remarks>
        private string CurrentSessionId()
        {
            return network.HasRoomSession ? network.RoomCode : null;
        }

        private async UniTask ReportAsync(string sessionId, CancellationToken cancellation)
        {
            var result = await presence.ReportAsync(sessionId, cancellation);

            if (result.Ok)
            {
                reportedSessionId = sessionId;
                hasReported = true;
                return;
            }

            if (result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            // Logged at info, not warning. A player on a bad network misses
            // these routinely and recovers on the next one; the loop keeps
            // going, and the only cost of a miss is looking offline for a while.
            Debug.Log($"[Presence] Heartbeat did not land: {result.Failure}.");
        }
    }
}
