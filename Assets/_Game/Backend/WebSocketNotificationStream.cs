using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using R3;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// Keeps a socket to the backend's notification endpoint open, says who this
    /// client is, and turns what arrives into <see cref="ServerNotification"/>s.
    /// </summary>
    /// <remarks>
    /// One loop: connect, HELLO, listen until it closes, wait, again. It never
    /// gives up on its own; only cancellation stops it. The wait between attempts
    /// comes from <see cref="ReconnectBackoff"/> and is reset by any connection
    /// the server acknowledged, so a link that drops after an hour retries in a
    /// second rather than thirty.
    /// <para>
    /// <see cref="State"/> reads <see cref="NotificationLinkState.Connected"/>
    /// only after <c>HELLO_ACK</c>. An open socket the server has not yet bound
    /// to a user receives nothing, so treating it as connected would make the
    /// screens re-read their lists a moment before the gap they were re-reading
    /// to cover.
    /// </para>
    /// <para>
    /// Frames the client does not recognise are dropped, not treated as errors.
    /// The server is allowed to add kinds, and an old client must not tear its
    /// link down every time one arrives.
    /// </para>
    /// </remarks>
    public sealed class WebSocketNotificationStream :
        INotificationStream, INotificationFrameSender, IDisposable
    {
        public const string Path = "/ws/notifications";

        private const string HelloType = "HELLO";
        private const string HelloAckType = "HELLO_ACK";

        /// <summary>The server's time format for every timestamp it sends.</summary>
        private const string TimeFormat = "yyyyMMddHHmmss";

        private readonly IWebSocketTransport transport;
        private readonly BackendEndpoint endpoint;
        private readonly BackendSession session;
        private readonly ReconnectBackoff backoff;
        private readonly Func<TimeSpan, CancellationToken, UniTask> wait;

        private readonly Subject<ServerNotification> notifications = new Subject<ServerNotification>();
        private readonly ReactiveProperty<NotificationLinkState> state =
            new ReactiveProperty<NotificationLinkState>(NotificationLinkState.Disconnected);

        private IWebSocketConnection current;
        private bool disposed;

        public WebSocketNotificationStream(
            IWebSocketTransport transport,
            BackendEndpoint endpoint,
            BackendSession session)
            : this(
                transport,
                endpoint,
                session,
                ReconnectBackoff.Default(),
                (duration, cancellation) => UniTask.Delay(duration, cancellationToken: cancellation))
        {
        }

        /// <param name="wait">
        /// How to pass time between attempts. Tests hand in one that returns at
        /// once, so a reconnection is checked without waiting for it.
        /// </param>
        public WebSocketNotificationStream(
            IWebSocketTransport transport,
            BackendEndpoint endpoint,
            BackendSession session,
            ReconnectBackoff backoff,
            Func<TimeSpan, CancellationToken, UniTask> wait)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.backoff = backoff ?? throw new ArgumentNullException(nameof(backoff));
            this.wait = wait ?? throw new ArgumentNullException(nameof(wait));
        }

        public Observable<ServerNotification> Notifications => notifications;

        public ReadOnlyReactiveProperty<NotificationLinkState> State => state;

        /// <summary>
        /// The socket address: the REST base with its scheme swapped, plus the
        /// notification path.
        /// </summary>
        /// <remarks>
        /// Derived rather than configured separately so that pointing the
        /// application at a local server moves both at once. Two fields would be
        /// two fields to forget.
        /// </remarks>
        public string Url
        {
            get
            {
                var baseUrl = endpoint.BaseUrl;
                if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return "wss://" + baseUrl.Substring("https://".Length) + Path;
                }

                if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    return "ws://" + baseUrl.Substring("http://".Length) + Path;
                }

                return baseUrl + Path;
            }
        }

        /// <summary>
        /// Runs the link until <paramref name="cancellation"/> is cancelled.
        /// </summary>
        /// <remarks>
        /// Needs an account before it is called. Without one there is no HELLO to
        /// send, and the server closes a socket that stays silent.
        /// </remarks>
        public async UniTask RunAsync(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested && !disposed)
            {
                if (!session.SignedIn)
                {
                    // The account went away underneath us — deleted on this
                    // machine. A HELLO with no id is closed as UNKNOWN_USER, and
                    // reconnecting into that would spin. Wait as if refused.
                    SetState(NotificationLinkState.Disconnected);
                    await Pause(cancellation);
                    continue;
                }

                SetState(NotificationLinkState.Connecting);
                var connection = await transport.OpenAsync(Url, cancellation);

                if (cancellation.IsCancellationRequested)
                {
                    if (connection != null)
                    {
                        await connection.CloseAsync();
                    }

                    break;
                }

                if (connection == null)
                {
                    SetState(NotificationLinkState.Disconnected);
                    await Pause(cancellation);
                    continue;
                }

                current = connection;
                connection.TextReceived += OnText;
                try
                {
                    await connection.SendTextAsync(Hello());
                    await connection.Closed.AttachExternalCancellation(cancellation);
                }
                catch (OperationCanceledException)
                {
                    // Shutting down. Handled below by the cancellation check.
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Notifications] Link failed: {exception.Message}");
                }
                finally
                {
                    connection.TextReceived -= OnText;
                    current = null;
                }

                var acknowledged = state.Value == NotificationLinkState.Connected;
                SetState(NotificationLinkState.Disconnected);

                if (cancellation.IsCancellationRequested || disposed)
                {
                    await connection.CloseAsync();
                    break;
                }

                if (acknowledged)
                {
                    backoff.Reset();
                }

                await Pause(cancellation);
            }

            SetState(NotificationLinkState.Disconnected);
        }

        public bool TrySend(string json)
        {
            var connection = current;
            if (connection == null || state.Value != NotificationLinkState.Connected)
            {
                return false;
            }

            // Not awaited. There is nothing to learn from the send completing:
            // the server acknowledges no frame on this channel, and a send that
            // fails ends the connection, which the loop sees on its own.
            connection.SendTextAsync(json).Forget();
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current?.CloseAsync().Forget();
            notifications.Dispose();
            state.Dispose();
        }

        private async UniTask Pause(CancellationToken cancellation)
        {
            try
            {
                await wait(backoff.Next(), cancellation);
            }
            catch (OperationCanceledException)
            {
                // The loop condition ends it.
            }
        }

        private void SetState(NotificationLinkState next)
        {
            // Dispose can run while the loop is between awaits. A disposed
            // property throws on write, and there is nobody left to tell.
            if (!disposed)
            {
                state.Value = next;
            }
        }

        private string Hello()
        {
            return JsonUtility.ToJson(new HelloFrameDto
            {
                type = HelloType,
                userId = session.UserId,
                token = session.AccountToken ?? string.Empty
            });
        }

        private void OnText(string text)
        {
            if (disposed)
            {
                return;
            }

            NotificationFrameDto frame;
            try
            {
                frame = JsonUtility.FromJson<NotificationFrameDto>(text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Notifications] Unreadable frame: {exception.Message}");
                return;
            }

            if (frame == null || string.IsNullOrEmpty(frame.type))
            {
                return;
            }

            if (frame.type == HelloAckType)
            {
                SetState(NotificationLinkState.Connected);
                return;
            }

            if (!TryKind(frame.type, out var kind))
            {
                // The guide says to ignore these. A newer server has more to
                // say than this client knows how to hear, and that is fine.
                Debug.Log($"[Notifications] Ignoring unknown frame type {frame.type}.");
                return;
            }

            if (frame.from == null || string.IsNullOrWhiteSpace(frame.from.userId))
            {
                Debug.LogWarning($"[Notifications] {frame.type} arrived without a sender. Dropped.");
                return;
            }

            notifications.OnNext(new ServerNotification(
                kind,
                frame.from.userId,
                frame.from.nickname,
                frame.roomCode,
                SentAt(frame.sentAt)));
        }

        private static bool TryKind(string type, out ServerNotificationKind kind)
        {
            switch (type)
            {
                case "FRIEND_REQUEST_RECEIVED": kind = ServerNotificationKind.FriendRequestReceived; return true;
                case "FRIEND_REQUEST_ACCEPTED": kind = ServerNotificationKind.FriendRequestAccepted; return true;
                case "FRIEND_REQUEST_REMOVED": kind = ServerNotificationKind.FriendRequestRemoved; return true;
                case "FRIEND_REMOVED": kind = ServerNotificationKind.FriendRemoved; return true;
                case "ROOM_INVITE_RECEIVED": kind = ServerNotificationKind.RoomInviteReceived; return true;
                default: kind = default; return false;
            }
        }

        /// <remarks>
        /// Parsed as UTC explicitly, as <c>FriendGateway</c> does for the same
        /// format. Read in the machine's zone it would land nine hours out here
        /// and look plausible while doing it.
        /// </remarks>
        private static DateTime SentAt(string value)
        {
            if (DateTime.TryParseExact(
                    value,
                    TimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            // The notification is still delivered. Its time is the least
            // important thing about it; who and what are what the screen acts on.
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }
    }
}
