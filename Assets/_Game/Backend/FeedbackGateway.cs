using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IFeedbackGateway"/> against the backend's feedback endpoint.
    /// </summary>
    public sealed class FeedbackGateway : IFeedbackGateway
    {
        private const string Feedback = "/api/v1/feedback";

        /// <summary>Server column widths. Longer values are cut rather than refused.</summary>
        private const int BuildVersionLimit = 32;
        private const int PlatformLimit = 16;

        private readonly BackendClient client;

        public FeedbackGateway(BackendClient client)
        {
            this.client = client;
        }

        public UniTask<BackendResult> SendAsync(string message, CancellationToken cancellation)
        {
            var body = new SendFeedbackRequestDto
            {
                message = message,

                // Filled here rather than asked of the caller. This is the layer
                // that knows them, and a screen that forgot would leave whoever
                // reads "튕깁니다" with no build to reproduce it on.
                buildVer = Cut(Application.version, BuildVersionLimit),
                platform = Cut(Application.platform.ToString(), PlatformLimit)
            };

            return client.CallAsync(
                HttpMethod.Post, Feedback, body, BackendAuth.UserId, cancellation);
        }

        /// <summary>
        /// Keeps a value inside the server's column.
        /// </summary>
        /// <remarks>
        /// Cutting rather than sending as-is: these two are context, and losing
        /// the player's words to a 400 because a platform name grew would be a
        /// bad trade. The values in use today are far shorter than the limits —
        /// this is here so that a new platform name cannot break sending.
        /// </remarks>
        private static string Cut(string value, int limit)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return value.Length <= limit ? value : value.Substring(0, limit);
        }
    }
}
