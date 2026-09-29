using System;
using System.Collections.Generic;

namespace Game.Backend
{
    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Patch,
        Delete
    }

    /// <summary>How far a call got, before anything about the answer is read.</summary>
    public enum HttpOutcome
    {
        /// <summary>
        /// An answer arrived. This includes 4xx and 5xx: the server was reached
        /// and said something, and what it said is
        /// <see cref="BackendClient"/>'s to interpret.
        /// </summary>
        Completed,

        /// <summary>The server could not be reached.</summary>
        ConnectionFailed,

        /// <summary>Nothing came back before the deadline.</summary>
        TimedOut,

        /// <summary>The caller cancelled.</summary>
        Cancelled
    }

    public readonly struct HttpHeader
    {
        public HttpHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Header name is required.", nameof(name));
            }

            Name = name;
            Value = value ?? string.Empty;
        }

        public string Name { get; }

        public string Value { get; }
    }

    /// <summary>
    /// One request, fully addressed and ready to send.
    /// </summary>
    /// <remarks>
    /// A transport is handed this and nothing else: no base address, no session,
    /// no knowledge of which headers mean what. That is what lets the tests swap
    /// a fake in and read exactly what the layer above decided to send.
    /// </remarks>
    public readonly struct HttpCall
    {
        public HttpCall(
            HttpMethod method,
            string url,
            string jsonBody,
            IReadOnlyList<HttpHeader> headers,
            int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Url is required.", nameof(url));
            }

            Method = method;
            Url = url;
            JsonBody = jsonBody;
            Headers = headers ?? Array.Empty<HttpHeader>();
            TimeoutSeconds = timeoutSeconds;
        }

        public HttpMethod Method { get; }

        public string Url { get; }

        /// <summary>The request body, or null when there is none.</summary>
        public string JsonBody { get; }

        public IReadOnlyList<HttpHeader> Headers { get; }

        public int TimeoutSeconds { get; }
    }

    /// <summary>What came back, unread.</summary>
    public readonly struct HttpCallResult
    {
        public readonly HttpOutcome Outcome;

        public readonly long StatusCode;

        /// <summary>The response body, empty when there was none.</summary>
        public readonly string Body;

        private HttpCallResult(HttpOutcome outcome, long statusCode, string body)
        {
            Outcome = outcome;
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public static HttpCallResult Completed(long statusCode, string body) =>
            new HttpCallResult(HttpOutcome.Completed, statusCode, body);

        public static HttpCallResult Failed(HttpOutcome outcome) =>
            new HttpCallResult(outcome, 0, string.Empty);
    }
}
