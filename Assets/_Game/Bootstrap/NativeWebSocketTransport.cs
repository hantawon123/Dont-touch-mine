// Compiled only when the NativeWebSocket package is present. The define comes
// from Game.Bootstrap.asmdef's versionDefines, which Unity sets when
// com.endel.nativewebsocket resolves. Without the package the rest of the
// assembly still builds — including under dotnet build, which never has it —
// and ProjectLifetimeScope registers a silent stream instead.
#if NATIVEWEBSOCKET_PRESENT
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using NativeWebSocket;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// The only place in the project that touches NativeWebSocket.
    /// </summary>
    /// <remarks>
    /// Kept to transport concerns alone, the way
    /// <see cref="UnityWebRequestTransport"/> is for requests: it opens sockets
    /// and reports frames and closure. What a frame means is decided above.
    /// <para>
    /// Native Connect runs the receive loop until closure, while WebGL Connect
    /// returns as soon as it starts the handshake. OnOpen and OnClose are the
    /// lifecycle signals shared by both platforms.
    /// </para>
    /// <para>
    /// Version 2 of the package delivers its events through the main thread's
    /// <c>SynchronizationContext</c>, so the per-frame dispatch this class does
    /// is normally a no-op. It stays because the package falls back to an
    /// internal queue whenever that context is missing, and a queue nobody
    /// drains is a socket that appears dead.
    /// </para>
    /// </remarks>
    public sealed class NativeWebSocketTransport : IWebSocketTransport, ITickable
    {
        private readonly TimeSpan openTimeout;
        private readonly List<Connection> live = new List<Connection>();

        /// <param name="openTimeoutSeconds">
        /// How long a handshake may take. The package has no timeout of its own,
        /// and a server that accepts TCP but never finishes the upgrade would
        /// otherwise hold the loop forever.
        /// </param>
        public NativeWebSocketTransport(int openTimeoutSeconds)
        {
            if (openTimeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(openTimeoutSeconds));
            }

            openTimeout = TimeSpan.FromSeconds(openTimeoutSeconds);
        }

        public async UniTask<IWebSocketConnection> OpenAsync(string url, CancellationToken cancellation)
        {
            var connection = new Connection(new WebSocket(url));
            connection.Begin();

            var timeout = UniTask.Delay(openTimeout, cancellationToken: cancellation)
                .SuppressCancellationThrow();

            var (winner, opened, _) = await UniTask.WhenAny(connection.Opened, timeout);

            if (winner != 0 || !opened)
            {
                var why = winner == 0 ? "refused"
                    : cancellation.IsCancellationRequested ? "cancelled"
                    : "timed out";
                Debug.LogWarning($"[Notifications] Socket to {url} {why}.");
                connection.CloseAsync().Forget();
                return null;
            }

            live.Add(connection);
            connection.Closed.ContinueWith(() => live.Remove(connection)).Forget();
            return connection;
        }

        public void Tick()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Backwards, because a dispatched close removes the connection.
            for (var index = live.Count - 1; index >= 0; index--)
            {
                live[index].Dispatch();
            }
#endif
        }

        private sealed class Connection : IWebSocketConnection
        {
            private readonly WebSocket socket;
            private readonly UniTaskCompletionSource<bool> opened = new UniTaskCompletionSource<bool>();
            private readonly UniTaskCompletionSource closed = new UniTaskCompletionSource();

            public Connection(WebSocket socket)
            {
                this.socket = socket;

                socket.OnOpen += () => opened.TrySetResult(true);
                socket.OnMessage += bytes => TextReceived?.Invoke(Encoding.UTF8.GetString(bytes));
                socket.OnError += message =>
                {
                    Debug.LogWarning($"[Notifications] Socket error: {message}");

                    // An error before the handshake finished is a failed open. One
                    // after it is followed by OnClose, and this does nothing then.
                    opened.TrySetResult(false);
                };
                socket.OnClose += _ =>
                {
                    opened.TrySetResult(false);
                    closed.TrySetResult();
                };
            }

            public event Action<string> TextReceived;

            public UniTask<bool> Opened => opened.Task;

            public UniTask Closed => closed.Task;

            /// <summary>
            /// Starts the socket. Not awaited by the caller, for the reason in the
            /// class remarks: the task it starts lives as long as the connection.
            /// </summary>
            public void Begin()
            {
                LiveAsync().Forget();
            }

            public async UniTask SendTextAsync(string text)
            {
                await socket.SendText(text);
            }

            public async UniTask CloseAsync()
            {
                if (socket.State != WebSocketState.Open && socket.State != WebSocketState.Connecting)
                {
                    return;
                }

                try
                {
                    await socket.Close();
                }
                catch (Exception exception)
                {
                    // Closing a socket that is already going away. Nothing to do.
                    Debug.Log($"[Notifications] Close reported: {exception.Message}");
                }
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            public void Dispatch()
            {
                socket.DispatchMessageQueue();
            }
#endif

            private async UniTaskVoid LiveAsync()
            {
                try
                {
                    await socket.Connect();
                    // WebGL Connect only initiates the handshake and returns immediately.
                    // OnClose owns the lifetime on both native and browser transports.
                    await closed.Task;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Notifications] Socket ended: {exception.Message}");
                }
                finally
                {
                    // Whatever happened, the connection is over. Both sources are
                    // settled so that nobody waits on a socket that is gone.
                    opened.TrySetResult(false);
                    closed.TrySetResult();
                }
            }
        }
    }
}
#endif
