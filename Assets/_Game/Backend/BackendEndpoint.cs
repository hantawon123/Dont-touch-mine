using System;

namespace Game.Backend
{
    /// <summary>
    /// Which backend to call and how long to wait for it.
    /// </summary>
    public sealed class BackendEndpoint
    {
        /// <summary>The deployed backend.</summary>
        /// <remarks>
        /// A constant rather than a required setting so that a build which
        /// shipped without the field filled in still reaches a server. Point the
        /// serialized field at http://localhost:8080 to work against a local one.
        /// </remarks>
        public const string Deployed = "https://j15d205.p.ssafy.io";

        /// <summary>
        /// Long enough that a slow phone network still completes, short enough
        /// that a player who tapped refresh is not left watching a spinner.
        /// </summary>
        private const int DefaultTimeoutSeconds = 10;

        public BackendEndpoint(string baseUrl = null, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            var url = string.IsNullOrWhiteSpace(baseUrl) ? Deployed : baseUrl.Trim();

            // Trimmed here so that callers can write paths that start with a
            // slash without every one of them producing a double slash.
            BaseUrl = url.TrimEnd('/');
            TimeoutSeconds = timeoutSeconds;
        }

        public string BaseUrl { get; }

        public int TimeoutSeconds { get; }

        public string Url(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            return path[0] == '/' ? BaseUrl + path : BaseUrl + "/" + path;
        }
    }
}
