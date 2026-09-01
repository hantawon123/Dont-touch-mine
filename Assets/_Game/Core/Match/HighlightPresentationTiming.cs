using System;

namespace Game.Core.Match
{
    public static class HighlightPresentationTiming
    {
        public const double PostRollSeconds = 3d;
        public const double DeliveryGraceSeconds = 2d;
        public const double ReadyLeadSeconds = 0.5d;
        public const double TitleSeconds = 0.4d;
        public const double FadeSeconds = 0.3d;
        public const double EndHoldSeconds = 0.4d;
        public const double BlackSeconds = 0.2d;
        public const double OverheadSeconds = TitleSeconds + FadeSeconds * 2d + EndHoldSeconds + BlackSeconds;

        public static double BodyTime(double elapsed, double duration) =>
            Math.Max(0d, Math.Min(duration, elapsed - TitleSeconds - FadeSeconds));

        public static float CountdownExitOpacity(double remaining) =>
            (float)(1d - Math.Clamp(remaining / FadeSeconds, 0d, 1d));

        public static float Opacity(double elapsed, double duration)
        {
            if (elapsed < TitleSeconds) return 1f;
            if (elapsed < TitleSeconds + FadeSeconds)
                return (float)(1d - (elapsed - TitleSeconds) / FadeSeconds);
            var fadeOutAt = TitleSeconds + FadeSeconds + duration + EndHoldSeconds;
            if (elapsed < fadeOutAt) return 0f;
            return (float)Math.Min(1d, (elapsed - fadeOutAt) / FadeSeconds);
        }
    }
}
