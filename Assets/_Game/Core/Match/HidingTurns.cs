using System;

namespace Game.Core.Match
{
    /// <summary>
    /// Whose hiding turn a given moment falls in, and how much of it is left.
    /// </summary>
    /// <remarks>
    /// Turns are not replicated, and they do not need to be. A turn is a
    /// function of the phase, the moment that phase ends and how many are
    /// playing — and every peer already has all three. Keeping the rule here
    /// lets the authority, which acts on turns, and the screens, which only
    /// report them, answer from one implementation instead of two that drift.
    /// </remarks>
    public static class HidingTurns
    {
        /// <summary>No turn is running, which is every phase but hiding.</summary>
        public const int NoTurn = -1;

        /// <summary>
        /// The zero-based player index whose turn it is, or <see cref="NoTurn"/>.
        /// </summary>
        /// <remarks>
        /// Clamped to the last player because the phase is allowed to outlast
        /// its turns: rounding and a late final tick would otherwise name a
        /// player who is not in the match.
        /// </remarks>
        public static int IndexAt(
            MatchPhase phase,
            double phaseEndsAt,
            double now,
            int playerCount,
            double turnDurationSeconds)
        {
            if (phase != MatchPhase.Hiding ||
                playerCount <= 0 ||
                turnDurationSeconds <= 0d)
            {
                return NoTurn;
            }

            var elapsedSeconds = Math.Max(
                0d, now - StartedAt(phaseEndsAt, playerCount, turnDurationSeconds));

            return Math.Min(
                (int)(elapsedSeconds / turnDurationSeconds),
                playerCount - 1);
        }

        /// <summary>
        /// Seconds left in the running turn, or zero when none is running.
        /// </summary>
        public static double RemainingSecondsAt(
            MatchPhase phase,
            double phaseEndsAt,
            double now,
            int playerCount,
            double turnDurationSeconds)
        {
            var turnIndex = IndexAt(
                phase, phaseEndsAt, now, playerCount, turnDurationSeconds);

            if (turnIndex == NoTurn)
            {
                return 0d;
            }

            var turnEndsAt = StartedAt(phaseEndsAt, playerCount, turnDurationSeconds) +
                             ((turnIndex + 1) * turnDurationSeconds);

            // Capped at a full turn so the clamp above cannot report a turn
            // longer than one, once the phase has run past its last.
            return Math.Min(
                turnDurationSeconds,
                Math.Max(0d, turnEndsAt - now));
        }

        /// <summary>
        /// Derived rather than carried: the phase reports when it ends, and the
        /// turns fill it exactly, so its start is the only moment it could be.
        /// </summary>
        private static double StartedAt(
            double phaseEndsAt, int playerCount, double turnDurationSeconds) =>
            phaseEndsAt - (turnDurationSeconds * playerCount);
    }
}
