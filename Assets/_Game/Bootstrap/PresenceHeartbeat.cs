using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Flow;
using Game.Core.Ports;
using Game.Core.Presence;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Tells the backend which room this player is in, when that changes.
    /// </summary>
    /// <remarks>
    /// There used to be a report every thirty seconds, because a report was the
    /// only way the server knew this player was still here. The realtime
    /// notification link now says that by existing — up means online, its last
    /// connection dropping means offline — so the timer is gone. What is left to
    /// say is the room: entering one, leaving it, and the lobby becoming a match,
    /// which the room code alone cannot show because lobby and match are one
    /// Photon room.
    /// <para>
    /// Still a loop, checking once a second, because nothing raises an event when
    /// a room session begins or ends. A check that finds nothing new sends
    /// nothing, so the loop costs a comparison a second and no traffic. The
    /// rules for what counts as new live in <see cref="PresenceReportPlanner"/>,
    /// where they are tested without this loop.
    /// </para>
    /// </remarks>
    public sealed class PresenceHeartbeat : IAsyncStartable, IDisposable
    {
        /// <summary>
        /// How often the room is checked. Short enough that entering a match shows
        /// up as in-game almost at once, and cheap because a check that finds
        /// nothing new sends nothing.
        /// </summary>
        private static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

        private readonly IPresenceGateway presence;
        private readonly BackendSignIn signIn;
        private readonly IRoomSessionProbe room;
        private readonly AppFlowSystem flow;
        private readonly INotificationStream link;
        private readonly PresenceReportPlanner planner = new PresenceReportPlanner();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        public PresenceHeartbeat(
            IPresenceGateway presence,
            BackendSignIn signIn,
            IRoomSessionProbe room,
            AppFlowSystem flow,
            INotificationStream link)
        {
            this.presence = presence ?? throw new ArgumentNullException(nameof(presence));
            this.signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.link = link ?? throw new ArgumentNullException(nameof(link));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (!await signIn.Ready)
            {
                // No account, so there is nobody to report as. Reporting anyway
                // would fail on every change for the rest of the session.
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, lifetime.Token);

            while (!linked.Token.IsCancellationRequested)
            {
                var current = PresenceReportPlanner.Current(room.HasRoomSession, room.RoomCode, flow.CurrentState);
                var connected = link.State.CurrentValue == NotificationLinkState.Connected;

                if (planner.ShouldReport(current, connected))
                {
                    await ReportAsync(current, linked.Token);
                }

                if (await UniTask.Delay(Poll, cancellationToken: linked.Token).SuppressCancellationThrow())
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Reports this player as gone on the way out, so friends do not watch a
        /// ghost for the moment before the socket's closure says the same.
        /// </summary>
        /// <remarks>
        /// Started but not waited for. A quit does not give the application a
        /// reliable window to finish a request in, and holding the quit open for
        /// one would trade a certain delay for an uncertain saving. When it does
        /// not land, the dropped notification socket takes this player offline
        /// anyway; this only makes it sooner.
        /// </remarks>
        public void Dispose()
        {
            lifetime.Cancel();

            if (planner.HasReported)
            {
                presence.GoOfflineAsync(CancellationToken.None).Forget();
            }

            lifetime.Dispose();
        }

        private async UniTask ReportAsync(PresenceReport report, CancellationToken cancellation)
        {
            var result = await presence.ReportAsync(report.SessionId, report.Kind, cancellation);

            if (result.Ok)
            {
                planner.Reported(report);
                return;
            }

            if (result.Failure == BackendFailure.Cancelled)
            {
                return;
            }

            // Logged at info, not warning. A player on a bad network misses these
            // and recovers on the next check, which asks for the same report
            // again until it lands. The only cost is friends seeing the previous
            // room for a while.
            Debug.Log($"[Presence] Report {report} did not land: {result.Failure}.");
        }
    }
}
