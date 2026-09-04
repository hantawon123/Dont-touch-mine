using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Backend;
using UnityEngine;

namespace Game.Backend
{
    /// <summary>Which credentials a call carries.</summary>
    public enum BackendAuth
    {
        /// <summary>
        /// Nothing. Only issuing an account, which is the call that answers who
        /// this machine is.
        /// </summary>
        None,

        /// <summary>
        /// Identifies the caller. Almost every call.
        /// </summary>
        /// <remarks>
        /// Identification, not authentication: the value is public and anyone
        /// who knows another player's id can act as them. That is accepted for
        /// now, and it is why nothing destructive settles for it.
        /// </remarks>
        UserId,

        /// <summary>
        /// Identifies the caller and proves it. Only for what cannot be undone.
        /// </summary>
        UserIdAndDevice
    }

    /// <summary>
    /// One place that turns a call into a request and an answer into a result.
    /// </summary>
    /// <remarks>
    /// Addressing, credentials, JSON and error codes live here so the gateways
    /// above hold nothing but which endpoint means what. Without it every
    /// gateway would attach its own headers, and a header attached in seven
    /// places is a header forgotten in one.
    /// </remarks>
    public sealed class BackendClient
    {
        private const string UserIdHeader = "X-User-Id";

        private const string DeviceIdHeader = "X-Device-Id";

        private readonly IHttpTransport transport;
        private readonly BackendEndpoint endpoint;
        private readonly BackendSession session;

        public BackendClient(
            IHttpTransport transport,
            BackendEndpoint endpoint,
            BackendSession session)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public BackendSession Session => session;

        /// <summary>Makes a call whose answer carries a body.</summary>
        public async UniTask<BackendResult<TResponse>> CallAsync<TResponse>(
            HttpMethod method,
            string path,
            object body,
            BackendAuth auth,
            CancellationToken cancellation)
            where TResponse : class
        {
            var answer = await ExchangeAsync(method, path, body, auth, cancellation);
            if (answer.Failure != BackendFailure.None)
            {
                return BackendResult<TResponse>.Failed(answer.Failure);
            }

            if (string.IsNullOrWhiteSpace(answer.Body))
            {
                Debug.LogWarning($"[Backend] {method} {path} answered with no body.");
                return BackendResult<TResponse>.Failed(BackendFailure.Unknown);
            }

            TResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<TResponse>(answer.Body);
            }
            catch (Exception exception)
            {
                // A body that will not parse means the server and this client
                // disagree about a shape. Reported rather than thrown: the caller
                // can only retry or give up either way.
                Debug.LogWarning(
                    $"[Backend] {method} {path} answered unreadably: {exception.Message}");
                return BackendResult<TResponse>.Failed(BackendFailure.Unknown);
            }

            if (parsed == null)
            {
                Debug.LogWarning($"[Backend] {method} {path} answered with nothing usable.");
                return BackendResult<TResponse>.Failed(BackendFailure.Unknown);
            }

            return BackendResult<TResponse>.Success(parsed);
        }

        /// <summary>Makes a call whose answer is only whether it worked.</summary>
        public async UniTask<BackendResult> CallAsync(
            HttpMethod method,
            string path,
            object body,
            BackendAuth auth,
            CancellationToken cancellation)
        {
            var answer = await ExchangeAsync(method, path, body, auth, cancellation);
            return answer.Failure == BackendFailure.None
                ? BackendResult.Success()
                : BackendResult.Failed(answer.Failure);
        }

        private async UniTask<Answer> ExchangeAsync(
            HttpMethod method,
            string path,
            object body,
            BackendAuth auth,
            CancellationToken cancellation)
        {
            if (auth != BackendAuth.None && !session.SignedIn)
            {
                // Refused before it is sent. The server would answer
                // MISSING_HEADER, which reads as a bug in building headers rather
                // than as the truth, which is that nobody has signed in yet.
                Debug.LogWarning($"[Backend] {method} {path} needs an account and there is none.");
                return Answer.Failed(BackendFailure.NotSignedIn);
            }

            var call = new HttpCall(
                method,
                endpoint.Url(path),
                body == null ? null : JsonUtility.ToJson(body),
                Headers(auth),
                endpoint.TimeoutSeconds);

            var answer = await transport.SendAsync(call, cancellation);

            switch (answer.Outcome)
            {
                case HttpOutcome.Cancelled:
                    return Answer.Failed(BackendFailure.Cancelled);

                case HttpOutcome.TimedOut:
                    return Answer.Failed(BackendFailure.Timeout);

                case HttpOutcome.ConnectionFailed:
                    return Answer.Failed(BackendFailure.Offline);
            }

            if (answer.StatusCode >= 200 && answer.StatusCode < 300)
            {
                return Answer.Received(answer.Body);
            }

            var failure = Classify(answer.StatusCode, answer.Body);

            // The request body is never logged. The only credential this client
            // sends travels in a header, and a log line is one of the two ways
            // it would escape.
            Debug.LogWarning($"[Backend] {method} {path} failed: {answer.StatusCode} {failure}");
            return Answer.Failed(failure);
        }

        /// <remarks>
        /// The code decides and the status code only fills in when there is no
        /// code to read. Branching on status first would collapse distinctions
        /// the server took care to make: ACCOUNT_NOT_FOUND and TARGET_NOT_FOUND
        /// are both 404 and mean opposite things to the caller.
        /// </remarks>
        private static BackendFailure Classify(long statusCode, string body)
        {
            var code = ReadCode(body);
            if (code != null)
            {
                return BackendErrorCodes.ToFailure(code);
            }

            // No code, so this did not come from the server's own error handler
            // — a proxy page, or a path that does not exist.
            return statusCode >= 500 ? BackendFailure.ServerError : BackendFailure.Unknown;
        }

        private static string ReadCode(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                var error = JsonUtility.FromJson<ErrorDto>(body);
                return string.IsNullOrWhiteSpace(error?.code) ? null : error.code;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <remarks>
        /// The device identifier goes in a header, never in the path or the
        /// query: nginx writes query strings to its access log, and this value is
        /// the account's password.
        /// </remarks>
        private IReadOnlyList<HttpHeader> Headers(BackendAuth auth)
        {
            switch (auth)
            {
                case BackendAuth.UserId:
                    return new[] { new HttpHeader(UserIdHeader, session.UserId) };

                case BackendAuth.UserIdAndDevice:
                    return new[]
                    {
                        new HttpHeader(UserIdHeader, session.UserId),
                        new HttpHeader(DeviceIdHeader, session.DeviceId)
                    };

                default:
                    return Array.Empty<HttpHeader>();
            }
        }

        /// <summary>An exchange that either failed or came back with a body.</summary>
        private readonly struct Answer
        {
            public readonly BackendFailure Failure;

            public readonly string Body;

            private Answer(BackendFailure failure, string body)
            {
                Failure = failure;
                Body = body;
            }

            public static Answer Failed(BackendFailure failure) =>
                new Answer(failure, null);

            public static Answer Received(string body) =>
                new Answer(BackendFailure.None, body);
        }
    }
}
