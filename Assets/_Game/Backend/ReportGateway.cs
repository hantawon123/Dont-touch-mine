using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using Game.Core.Ports;

namespace Game.Backend
{
    /// <summary>
    /// <see cref="IReportGateway"/> against the backend's report endpoint.
    /// </summary>
    public sealed class ReportGateway : IReportGateway
    {
        private const string Reports = "/api/v1/reports";

        private readonly BackendClient client;

        public ReportGateway(BackendClient client)
        {
            this.client = client;
        }

        public UniTask<BackendResult> ReportAsync(
            string playerId, ReportReason reason, string note, CancellationToken cancellation)
        {
            var body = new SendReportRequestDto
            {
                userId = playerId,
                reason = Wire(reason),

                // JsonUtility writes a null string as "", and the server reads ""
                // and absent the same way, so nothing is lost by not omitting it.
                memo = note
            };

            return client.CallAsync(
                HttpMethod.Post, Reports, body, BackendAuth.UserId, cancellation);
        }

        /// <summary>
        /// The name the server expects.
        /// </summary>
        /// <remarks>
        /// Written out rather than taken from <c>ToString</c>, which would tie
        /// the wire format to C# member names: renaming <c>Abuse</c> would then
        /// silently start sending a value the server refuses, and nothing here
        /// would fail to compile.
        /// <para>
        /// An unrecognised value throws instead of falling back to
        /// <c>OTHER</c>. A fallback would file every report under the one reason
        /// that means "read the note", and the notes would not be there to read.
        /// </para>
        /// </remarks>
        private static string Wire(ReportReason reason)
        {
            switch (reason)
            {
                case ReportReason.Abuse:
                    return "ABUSE";

                case ReportReason.Cheating:
                    return "CHEATING";

                case ReportReason.Spam:
                    return "SPAM";

                case ReportReason.InappropriateName:
                    return "INAPPROPRIATE_NAME";

                case ReportReason.Other:
                    return "OTHER";

                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }
    }
}
