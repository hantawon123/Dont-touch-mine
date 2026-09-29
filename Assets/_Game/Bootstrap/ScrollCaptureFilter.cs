using Game.Core.Settings;
using Math = System.Math;

namespace Game.Bootstrap
{
    /// <summary>
    /// Decides when a roll of the mouse wheel means the player wants the wheel
    /// on an action.
    /// </summary>
    /// <remarks>
    /// A wheel is not a button, so nothing presses it: it reports a nudge and
    /// is back at rest the next frame. One nudge is too easy to give by
    /// accident — the settings page itself scrolls — so a deliberate roll is
    /// asked for: <see cref="RequiredNotches"/> nudges the same way, with a
    /// change of direction starting the count again.
    /// <para>
    /// Kept apart from the input layer, and free of Unity, so that the rule can
    /// be tested without a mouse.
    /// </para>
    /// </remarks>
    public sealed class ScrollCaptureFilter
    {
        /// <summary>
        /// How many nudges the same way count as meaning it. Two rather than
        /// one, and not so many that the player wonders whether it is working.
        /// </summary>
        public const int RequiredNotches = 2;

        /// <summary>
        /// Below this a report is the wheel sitting still. Platforms disagree
        /// about how much a notch is worth — a hundred and twenty on Windows,
        /// one elsewhere — so the size is not looked at, only the sign.
        /// </summary>
        private const float AtRest = 0.01f;

        private int notches;
        private int direction;

        /// <summary>
        /// Takes this frame's wheel reading. Returns the code to bind once the
        /// roll has been deliberate enough, and null until then.
        /// </summary>
        public string Feed(float delta)
        {
            if (Math.Abs(delta) < AtRest)
            {
                return null;
            }

            var sign = delta > 0f ? 1 : -1;
            if (sign != direction)
            {
                direction = sign;
                notches = 0;
            }

            notches++;
            if (notches < RequiredNotches)
            {
                return null;
            }

            Reset();
            return sign > 0 ? ControlCatalog.ScrollUp : ControlCatalog.ScrollDown;
        }

        public void Reset()
        {
            notches = 0;
            direction = 0;
        }
    }
}
