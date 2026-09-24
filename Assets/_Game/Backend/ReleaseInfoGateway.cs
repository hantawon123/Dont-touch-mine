using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// Asks the release host which client build is on the download page
    /// (S15P21D205-1109).
    /// </summary>
    /// <remarks>
    /// Not through <see cref="BackendClient"/>: current.json is served by the
    /// release host behind nginx, not by the Spring backend, and it carries
    /// neither the session header nor the error body the client reads.
    /// </remarks>
    public sealed class ReleaseInfoGateway
    {
        public const string CurrentPath = "/play/current.json";
        public const string DownloadPath = "/play/";

        private readonly IHttpTransport transport;
        private readonly BackendEndpoint endpoint;

        public ReleaseInfoGateway(IHttpTransport transport, BackendEndpoint endpoint)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public string DownloadPageUrl => endpoint.Url(DownloadPath);

        /// <summary>
        /// The released revision, or null when the host could not say.
        /// </summary>
        /// <remarks>
        /// Null for every failure alike. The caller treats "could not ask" as
        /// "do not block", and nothing it would do differs by the reason.
        /// </remarks>
        public async UniTask<string> FetchReleasedRevisionAsync(CancellationToken cancellation)
        {
            var call = new HttpCall(
                HttpMethod.Get, endpoint.Url(CurrentPath), null, null, endpoint.TimeoutSeconds);
            var answer = await transport.SendAsync(call, cancellation);

            if (answer.Outcome != HttpOutcome.Completed || answer.StatusCode != 200)
            {
                return null;
            }

            try
            {
                var parsed = JsonUtility.FromJson<CurrentReleaseDto>(answer.Body);
                return string.IsNullOrWhiteSpace(parsed?.revision) ? null : parsed.revision.Trim();
            }
            catch (ArgumentException)
            {
                // JsonUtility throws on a body that is not JSON - an nginx error
                // page, say. That is the host not answering, not an answer.
                return null;
            }
        }

        [Serializable]
        private sealed class CurrentReleaseDto
        {
            public string revision;
            public string release;
        }
    }
}
