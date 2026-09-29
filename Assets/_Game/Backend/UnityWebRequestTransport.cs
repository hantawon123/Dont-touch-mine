using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace Game.Backend
{
    /// <summary>
    /// The only place in the project that touches UnityWebRequest.
    /// </summary>
    /// <remarks>
    /// Kept to transport concerns alone. It reports that an answer arrived and
    /// what its status was; it does not decide what a 404 means, because that
    /// depends on which endpoint was called and this class does not know.
    /// </remarks>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public async UniTask<HttpCallResult> SendAsync(
            HttpCall call, CancellationToken cancellation)
        {
            // Disposed on every path, which also aborts a request still in
            // flight when the caller cancelled.
            using var request = new UnityWebRequest(call.Url, Verb(call.Method));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = call.TimeoutSeconds;

            if (call.JsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(call.JsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Accept", "application/json");

            for (var index = 0; index < call.Headers.Count; index++)
            {
                var header = call.Headers[index];
                request.SetRequestHeader(header.Name, header.Value);
            }

            try
            {
                await request.SendWebRequest().WithCancellation(cancellation);
            }
            catch (OperationCanceledException)
            {
                return HttpCallResult.Failed(HttpOutcome.Cancelled);
            }
            catch (UnityWebRequestException)
            {
                // UniTask throws on any non-success result, including a 404 that
                // carries the error body we need. Swallowed here so that the one
                // judgement below decides, from the request itself, whether this
                // was an answer or a failure to get one.
            }

            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return HttpCallResult.Failed(TimedOut(request.error)
                        ? HttpOutcome.TimedOut
                        : HttpOutcome.ConnectionFailed);

                case UnityWebRequest.Result.DataProcessingError:
                    return HttpCallResult.Failed(HttpOutcome.ConnectionFailed);

                default:
                    // Success and ProtocolError both mean the server answered.
                    return HttpCallResult.Completed(
                        request.responseCode, request.downloadHandler?.text);
            }
        }

        /// <remarks>
        /// Unity reports a timeout as a connection error and only distinguishes
        /// it in the message, so this reads the message. Getting it wrong changes
        /// which sentence the player is shown and nothing else — both outcomes
        /// mean the request did not reach the server and can be retried.
        /// </remarks>
        private static bool TimedOut(string error)
        {
            return error != null
                && error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Verb(HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.Get: return UnityWebRequest.kHttpVerbGET;
                case HttpMethod.Post: return UnityWebRequest.kHttpVerbPOST;
                case HttpMethod.Put: return UnityWebRequest.kHttpVerbPUT;
                case HttpMethod.Delete: return UnityWebRequest.kHttpVerbDELETE;
                case HttpMethod.Patch: return "PATCH";
                default: throw new ArgumentOutOfRangeException(nameof(method));
            }
        }
    }
}
