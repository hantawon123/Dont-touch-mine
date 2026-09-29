using Game.Core.Match;
using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Lobby enter/leave cover timing. The black overlay itself is the lobby's
    /// <c>HighlightTransitionView</c>; this only maps elapsed time to opacity.
    /// </summary>
    public static class LobbySceneFade
    {
        public const float DurationSeconds = (float)HighlightPresentationTiming.FadeSeconds;

        public static float FadeInOpacity(float elapsed) =>
            1f - Mathf.Clamp01(elapsed / DurationSeconds);

        public static float FadeOutOpacity(float elapsed) =>
            Mathf.Clamp01(elapsed / DurationSeconds);

        public static float Lerp(float from, float to, float elapsed)
        {
            if (DurationSeconds <= 0f)
            {
                return to;
            }

            return Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / DurationSeconds));
        }

        public static bool IsComplete(float elapsed) => elapsed >= DurationSeconds;
    }
}
