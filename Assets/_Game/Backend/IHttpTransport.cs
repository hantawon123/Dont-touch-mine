using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Backend
{
    /// <summary>
    /// Sends one request and reports what came back.
    /// </summary>
    /// <remarks>
    /// The seam between this assembly and the engine. Everything above it —
    /// addressing, headers, JSON, error codes — is ordinary C# that an EditMode
    /// test can drive by handing it a fake, which is the point: a wrapper that
    /// only ran against a live server would be checked by nobody.
    /// </remarks>
    public interface IHttpTransport
    {
        UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation);
    }
}
