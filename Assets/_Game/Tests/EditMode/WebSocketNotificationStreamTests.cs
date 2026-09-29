using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Ports;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// Drives the realtime link through a fake socket, so the handshake, the
    /// parsing and the reconnecting are checked without a server.
    /// </summary>
    /// <remarks>
    /// Everything the fake does completes synchronously, and the stream's waits
    /// are replaced with ones that return at once. So after each action the
    /// stream has already reacted by the next line — no yielding, no sleeping,
    /// no chance of a test that passes on a fast machine and fails on a slow one.
    /// </remarks>
    public sealed class WebSocketNotificationStreamTests
    {
        private const string UserId = "user-1";
        private const string AccountToken = "signed-token-1";

        [Test]
        public void Run_SaysHelloFirst()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);

            var connection = transport.Connections[0];
            Assert.That(connection.Sent.Count, Is.EqualTo(1));
            Assert.That(connection.Sent[0], Does.Contain("\"type\":\"HELLO\""));
            Assert.That(connection.Sent[0], Does.Contain("\"userId\":\"" + UserId + "\""));

            // The id is public; the server's signature over it goes with it, the
            // same value the REST calls send as X-Account-Token.
            Assert.That(connection.Sent[0], Does.Contain("\"token\":\"" + AccountToken + "\""));
        }

        [Test]
        public void Url_SwapsTheSchemeAndKeepsTheHost()
        {
            var session = new BackendSession("device");
            session.Adopt(UserId);

            var secure = new WebSocketNotificationStream(
                new FakeWebSocketTransport(), new BackendEndpoint("https://j15d205.p.ssafy.io"), session);
            var local = new WebSocketNotificationStream(
                new FakeWebSocketTransport(), new BackendEndpoint("http://localhost:8080/"), session);

            Assert.That(secure.Url, Is.EqualTo("wss://j15d205.p.ssafy.io/ws/notifications"));
            Assert.That(local.Url, Is.EqualTo("ws://localhost:8080/ws/notifications"));
        }

        [Test]
        public void AnOpenSocket_IsOnlyConnectingUntilTheServerAnswers()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);

            // Open, HELLO sent, nothing back yet. The server does not know who
            // this is, so nothing it pushes would reach us.
            Assert.That(stream.State.CurrentValue, Is.EqualTo(NotificationLinkState.Connecting));

            transport.Connections[0].Deliver("{\"type\":\"HELLO_ACK\"}");

            Assert.That(stream.State.CurrentValue, Is.EqualTo(NotificationLinkState.Connected));
        }

        [Test]
        public void EveryKind_IsParsedWithSenderAndTime()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var received = new List<ServerNotification>();
            using var subscription = stream.Notifications.Subscribe(received.Add);
            var connection = transport.Connections[0];
            connection.Deliver("{\"type\":\"HELLO_ACK\"}");

            connection.Deliver(Frame("FRIEND_REQUEST_RECEIVED", null));
            connection.Deliver(Frame("FRIEND_REQUEST_ACCEPTED", null));
            connection.Deliver(Frame("FRIEND_REQUEST_REMOVED", null));
            connection.Deliver(Frame("FRIEND_REMOVED", null));
            connection.Deliver(Frame("ROOM_INVITE_RECEIVED", "7K2M9P"));

            Assert.That(received.Count, Is.EqualTo(5));
            Assert.That(received[0].Kind, Is.EqualTo(ServerNotificationKind.FriendRequestReceived));
            Assert.That(received[1].Kind, Is.EqualTo(ServerNotificationKind.FriendRequestAccepted));
            Assert.That(received[2].Kind, Is.EqualTo(ServerNotificationKind.FriendRequestRemoved));
            Assert.That(received[3].Kind, Is.EqualTo(ServerNotificationKind.FriendRemoved));
            Assert.That(received[4].Kind, Is.EqualTo(ServerNotificationKind.RoomInviteReceived));

            Assert.That(received[0].FromPlayerId, Is.EqualTo("host"));
            Assert.That(received[0].FromNickname, Is.EqualTo("방장"));
            Assert.That(received[0].RoomCode, Is.Null);
            Assert.That(received[4].RoomCode, Is.EqualTo("7K2M9P"));

            // 20260908123000 read as UTC, not as local time nine hours away.
            Assert.That(received[0].SentAtUtc, Is.EqualTo(new DateTime(2026, 9, 8, 12, 30, 0, DateTimeKind.Utc)));
            Assert.That(received[0].SentAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void AnUnknownType_IsIgnoredAndTheLinkStaysUp()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var received = new List<ServerNotification>();
            using var subscription = stream.Notifications.Subscribe(received.Add);
            var connection = transport.Connections[0];
            connection.Deliver("{\"type\":\"HELLO_ACK\"}");

            // A newer server with more to say. The guide says to ignore it, and
            // tearing the link down over it would lose everything else.
            connection.Deliver(Frame("SOMETHING_NEW", null));
            connection.Deliver("this is not json");

            Assert.That(received, Is.Empty);
            Assert.That(stream.State.CurrentValue, Is.EqualTo(NotificationLinkState.Connected));
            Assert.That(transport.Connections.Count, Is.EqualTo(1));
        }

        [Test]
        public void AFrameWithoutASender_IsDropped()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var received = new List<ServerNotification>();
            using var subscription = stream.Notifications.Subscribe(received.Add);
            transport.Connections[0].Deliver("{\"type\":\"HELLO_ACK\"}");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("without a sender"));
            transport.Connections[0].Deliver("{\"type\":\"FRIEND_REMOVED\",\"sentAt\":\"20260908123000\"}");

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void WhenTheSocketCloses_ItReconnectsAndSaysHelloAgain()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var states = new List<NotificationLinkState>();
            using var subscription = stream.State.Subscribe(states.Add);
            var first = transport.Connections[0];
            first.Deliver("{\"type\":\"HELLO_ACK\"}");

            first.Close();

            // The wait between attempts returns at once in this test, so the
            // second socket is already open and greeted.
            Assert.That(transport.Connections.Count, Is.EqualTo(2));
            Assert.That(transport.Connections[1].Sent[0], Does.Contain("\"type\":\"HELLO\""));
            Assert.That(states, Is.EqualTo(new[]
            {
                // 구독 시점의 현재 값이 재생됩니다. 그 시점은 소켓만 열린 상태이고
                // HELLO_ACK 은 바로 아래에서 오므로 Connecting 입니다 -
                // AnOpenSocket_IsOnlyConnectingUntilTheServerAnswers 와 같은 규칙입니다.
                NotificationLinkState.Connecting,
                NotificationLinkState.Connected,
                NotificationLinkState.Disconnected,
                // 두 번째 소켓도 인사만 보낸 상태라 아직 Connecting 입니다.
                NotificationLinkState.Connecting
            }));
        }

        [Test]
        public void ARefusedSocket_IsTriedAgain()
        {
            var transport = new FakeWebSocketTransport { RefuseNext = 2 };

            using var stream = Start(transport, out _);

            // Two refusals, then an open one. Each refusal took one wait.
            Assert.That(transport.OpenAttempts, Is.EqualTo(3));
            Assert.That(transport.Connections.Count, Is.EqualTo(1));
            Assert.That(stream.State.CurrentValue, Is.EqualTo(NotificationLinkState.Connecting));
        }

        [Test]
        public void EveryAcknowledgedConnection_RaisesConnectedOnce()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var connected = 0;
            using var subscription = stream.State
                .Where(state => state == NotificationLinkState.Connected)
                .Subscribe(_ => connected++);

            transport.Connections[0].Deliver("{\"type\":\"HELLO_ACK\"}");
            transport.Connections[0].Deliver("{\"type\":\"HELLO_ACK\"}");
            transport.Connections[0].Close();
            transport.Connections[1].Deliver("{\"type\":\"HELLO_ACK\"}");

            // This is the edge the screens re-read their lists on. Once per
            // acknowledged connection: a repeated ACK is not a new gap to cover.
            Assert.That(connected, Is.EqualTo(2));
        }

        [Test]
        public void Cancelling_StopsTheLoopAndClosesTheSocket()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out var cancellation);
            transport.Connections[0].Deliver("{\"type\":\"HELLO_ACK\"}");

            cancellation.Cancel();

            Assert.That(transport.Connections[0].CloseCalls, Is.EqualTo(1));
            Assert.That(transport.Connections.Count, Is.EqualTo(1));
            Assert.That(stream.State.CurrentValue, Is.EqualTo(NotificationLinkState.Disconnected));
        }

        [Test]
        public void TrySend_OnlyWhileConnected()
        {
            var transport = new FakeWebSocketTransport();
            using var stream = Start(transport, out _);
            var connection = transport.Connections[0];

            // Open but not yet acknowledged: the server would not know whose
            // frame this is.
            Assert.That(stream.TrySend("{\"type\":\"PRESENCE\"}"), Is.False);
            Assert.That(connection.Sent.Count, Is.EqualTo(1));

            connection.Deliver("{\"type\":\"HELLO_ACK\"}");

            Assert.That(stream.TrySend("{\"type\":\"PRESENCE\"}"), Is.True);
            Assert.That(connection.Sent.Count, Is.EqualTo(2));
            Assert.That(connection.Sent[1], Does.Contain("PRESENCE"));

            connection.Close();

            // Between connections there is nowhere to send. The caller uses REST.
            Assert.That(stream.TrySend("{\"type\":\"PRESENCE\"}"), Is.False);
        }

        [Test]
        public void Backoff_DoublesToTheCapAndResets()
        {
            // Midpoint randomness, so the jitter factor is exactly 1.
            var backoff = new ReconnectBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), () => 0.5);

            var waits = new List<double>();
            for (var attempt = 0; attempt < 7; attempt++)
            {
                waits.Add(backoff.Next().TotalSeconds);
            }

            Assert.That(waits, Is.EqualTo(new[] { 1.0, 2.0, 4.0, 8.0, 16.0, 30.0, 30.0 }));

            backoff.Reset();

            Assert.That(backoff.Next().TotalSeconds, Is.EqualTo(1.0));
        }

        [Test]
        public void Backoff_SpreadsEachWaitAround_ItsNominalLength()
        {
            var lowest = new ReconnectBackoff(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(30), () => 0.0);
            var highest = new ReconnectBackoff(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(30), () => 0.999);

            // 75% to just under 125%. Without this every client a restart cut off
            // would come back in the same instant, thirty seconds later.
            Assert.That(lowest.Next().TotalSeconds, Is.EqualTo(3.0).Within(0.001));
            Assert.That(highest.Next().TotalSeconds, Is.EqualTo(5.0).Within(0.01));
        }

        [Test]
        public void AConnectionThatWasAcknowledged_ResetsTheBackoff()
        {
            var transport = new FakeWebSocketTransport();
            var waits = new List<TimeSpan>();
            using var stream = Start(transport, out _, waits);

            // Two sockets that open but are never acknowledged — a server that
            // accepts the upgrade and then drops it — grow the wait. One that
            // was acknowledged brings it back down, however briefly it lived.
            transport.Connections[0].Close();                          // never acknowledged: 1s
            transport.Connections[1].Close();                          // never acknowledged: 2s
            transport.Connections[2].Deliver("{\"type\":\"HELLO_ACK\"}");
            transport.Connections[2].Close();                          // acknowledged: back to 1s

            Assert.That(waits.Count, Is.EqualTo(3));
            Assert.That(waits[0].TotalSeconds, Is.EqualTo(1.0));
            Assert.That(waits[1].TotalSeconds, Is.EqualTo(2.0));
            Assert.That(waits[2].TotalSeconds, Is.EqualTo(1.0));
        }

        /// <summary>
        /// A stream over the fake, already running and already holding one open
        /// socket that has been greeted.
        /// </summary>
        private static WebSocketNotificationStream Start(
            FakeWebSocketTransport transport,
            out CancellationTokenSource cancellation,
            List<TimeSpan> waits = null)
        {
            var session = new BackendSession("device");
            session.Adopt(UserId, AccountToken);

            var stream = new WebSocketNotificationStream(
                transport,
                new BackendEndpoint("https://example.test"),
                session,
                new ReconnectBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), () => 0.5),
                (duration, token) =>
                {
                    waits?.Add(duration);
                    return UniTask.CompletedTask;
                });

            cancellation = new CancellationTokenSource();
            stream.RunAsync(cancellation.Token).Forget();
            return stream;
        }

        private static string Frame(string type, string roomCode)
        {
            var room = roomCode == null ? "null" : "\"" + roomCode + "\"";
            return "{\"type\":\"" + type + "\",\"sentAt\":\"20260908123000\","
                + "\"from\":{\"userId\":\"host\",\"nickname\":\"방장\"},\"roomCode\":" + room + "}";
        }

        /// <summary>
        /// A transport whose sockets the test opens, feeds and closes by hand.
        /// Everything completes synchronously.
        /// </summary>
        private sealed class FakeWebSocketTransport : IWebSocketTransport
        {
            public readonly List<FakeConnection> Connections = new List<FakeConnection>();

            public int OpenAttempts { get; private set; }

            /// <summary>How many of the next opens fail.</summary>
            public int RefuseNext { get; set; }

            public UniTask<IWebSocketConnection> OpenAsync(string url, CancellationToken cancellation)
            {
                OpenAttempts++;
                if (RefuseNext > 0)
                {
                    RefuseNext--;
                    return UniTask.FromResult<IWebSocketConnection>(null);
                }

                var connection = new FakeConnection();
                Connections.Add(connection);
                return UniTask.FromResult<IWebSocketConnection>(connection);
            }
        }

        private sealed class FakeConnection : IWebSocketConnection
        {
            private readonly UniTaskCompletionSource closed = new UniTaskCompletionSource();

            public readonly List<string> Sent = new List<string>();

            public int CloseCalls { get; private set; }

            public event Action<string> TextReceived;

            public UniTask Closed => closed.Task;

            public UniTask SendTextAsync(string text)
            {
                Sent.Add(text);
                return UniTask.CompletedTask;
            }

            public UniTask CloseAsync()
            {
                CloseCalls++;
                closed.TrySetResult();
                return UniTask.CompletedTask;
            }

            /// <summary>The server said something.</summary>
            public void Deliver(string text) => TextReceived?.Invoke(text);

            /// <summary>The server, or the network, ended it.</summary>
            public void Close() => closed.TrySetResult();
        }
    }
}
