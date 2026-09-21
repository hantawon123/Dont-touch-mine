using System;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Network.Session
{
    /// <summary>
    /// Fusion reports a refused room entry as an error even though it is a
    /// normal answer to a join request: the code named no room, the room is
    /// full, or the password was wrong. Downgrade only those answers so
    /// Unity's Error Pause does not stop Play Mode before the UI can show them.
    /// </summary>
    /// <remarks>
    /// The message is matched after its rich-text tags are stripped. Fusion
    /// wraps its <c>[Fusion]</c> prefix in a colour tag, so matching the raw
    /// text against <c>[Fusion] StartGame Failed:</c> never fired and the
    /// editor kept pausing on a mistyped room code.
    /// </remarks>
    internal sealed class ExpectedFusionEntryLogHandler : ILogHandler
    {
        private const string StartFailure = "StartGame Failed:";
        private const string ReasonPrefix = "ShutdownReason: ";

        private static readonly string[] ExpectedReasons =
        {
            nameof(Fusion.ShutdownReason.GameNotFound),
            nameof(Fusion.ShutdownReason.GameIsFull),
            nameof(Fusion.ShutdownReason.GameIdAlreadyExists),
            nameof(Fusion.ShutdownReason.ConnectionRefused),
            nameof(Fusion.ShutdownReason.CustomAuthenticationFailed),
        };

        private static readonly Regex RichTextTag =
            new Regex("</?[a-zA-Z]+(=[^>]*)?>", RegexOptions.Compiled);

        private readonly ILogHandler inner;

        private ExpectedFusionEntryLogHandler(ILogHandler inner)
        {
            this.inner = inner;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var logger = Debug.unityLogger;
            if (logger.logHandler is ExpectedFusionEntryLogHandler)
            {
                return;
            }

            logger.logHandler = new ExpectedFusionEntryLogHandler(logger.logHandler);
        }

        public void LogFormat(
            LogType logType,
            Object context,
            string format,
            params object[] args)
        {
            if (logType == LogType.Error && IsExpectedEntryFailure(format, args))
            {
                logType = LogType.Warning;
            }

            inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context) =>
            inner.LogException(exception, context);

        internal static bool IsExpectedEntryFailure(string format, object[] args)
        {
            var message = RichTextTag.Replace(Format(format, args), string.Empty);
            if (!message.Contains(StartFailure, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var reason in ExpectedReasons)
            {
                if (message.Contains(ReasonPrefix + reason, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Format(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format) || args == null || args.Length == 0)
            {
                return format ?? string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
