using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Backend
{
    /// <summary>
    /// Opens a socket and hands back the connection.
    /// </summary>
    /// <remarks>
    /// The seam between this assembly and the engine, the way
    /// <see cref="IHttpTransport"/> is for requests. Everything above it — the
    /// handshake, parsing, reconnecting — is ordinary C# that an EditMode test
    /// drives with a fake, which is the point: a reconnect loop that could only
    /// be watched against a live server would be checked by nobody.
    /// </remarks>
    public interface IWebSocketTransport
    {
        /// <summary>
        /// Opens a socket to <paramref name="url"/>. Completes once it is open and
        /// ready to send, or with null when it could not be opened — refused,
        /// timed out, or cancelled.
        /// </summary>
        /// <remarks>
        /// Null rather than a throw. Not being able to reach the server is the
        /// ordinary case this layer exists to ride out, and the only thing the
        /// caller does about it is wait and try again.
        /// </remarks>
        UniTask<IWebSocketConnection> OpenAsync(string url, CancellationToken cancellation);
    }

    /// <summary>
    /// One open socket.
    /// </summary>
    /// <remarks>
    /// Events and a task rather than an observable, so the seam has no
    /// dependency beyond UniTask and a fake is a few lines. The port above it
    /// is where R3 begins.
    /// </remarks>
    public interface IWebSocketConnection
    {
        /// <summary>A text frame arrived. Raised on the main thread.</summary>
        event Action<string> TextReceived;

        /// <summary>
        /// Completes when the connection has ended, whichever side ended it and
        /// for whatever reason. Never faults: an error is a reason to be closed,
        /// not a reason to throw at whoever is waiting.
        /// </summary>
        UniTask Closed { get; }

        UniTask SendTextAsync(string text);

        UniTask CloseAsync();
    }
}
