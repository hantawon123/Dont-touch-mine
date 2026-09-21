using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Network.Session
{
    /// <summary>
    /// Fusion reports a refused room lookup as an error even though it is a
    /// normal answer to a room-code request. Downgrade only that exact answer
    /// so Unity's Error Pause does not stop Play Mode before the UI can show it.
    /// </summary>
    internal sealed class ExpectedFusionEntryLogHandler : ILogHandler
    {
        private const string StartFailure = "[Fusion] StartGame Failed:";
        private const string MissingGame = "ShutdownReason: GameNotFound";

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
            if (logType == LogType.Error && IsMissingRoom(format, args))
            {
                logType = LogType.Warning;
            }

            inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context) =>
            inner.LogException(exception, context);

        internal static bool IsMissingRoom(string format, object[] args)
        {
            var message = Format(format, args);
            return message.Contains(StartFailure, StringComparison.Ordinal) &&
                   message.Contains(MissingGame, StringComparison.Ordinal);
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
