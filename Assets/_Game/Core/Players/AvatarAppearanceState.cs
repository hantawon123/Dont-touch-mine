using System;

namespace Game.Core.Players
{
    /// <summary>
    /// The appearance this player has settled on, as against the one being
    /// tried on in the closet.
    /// </summary>
    /// <remarks>
    /// One place holds the applied value because more than one screen shows it:
    /// the closet opens on it, the lobby dresses the player in it, and the
    /// account will one day be told about it. The closet's own draft is not
    /// here — it belongs to the screen and dies with it.
    /// <para>
    /// Nothing here reaches a disk or a server yet. The store arrives with the
    /// story that persists appearances; until then this keeps the value for as
    /// long as the game is running, which is what the screen needs to open on
    /// what was last applied.
    /// </para>
    /// </remarks>
    public sealed class AvatarAppearanceState
    {
        /// <remarks>
        /// No constructor parameter, not even an optional one. A container
        /// building this reads the signature and not the default, so an
        /// optional <see cref="AvatarAppearance"/> here is a dependency it
        /// cannot find. Whoever knows what was last applied calls
        /// <see cref="Apply"/>.
        /// </remarks>
        public AvatarAppearanceState()
        {
        }

        /// <summary>Raised after <see cref="Apply"/> settles on a new value.</summary>
        public event Action<AvatarAppearance> Changed;

        public AvatarAppearance Current { get; private set; }

        /// <summary>
        /// Settles on an appearance. Applying the one already worn tells
        /// nobody, so a screen that re-applies without changing anything does
        /// not make everyone else redraw.
        /// </summary>
        public void Apply(AvatarAppearance appearance)
        {
            if (Current == appearance)
            {
                return;
            }

            Current = appearance;
            Changed?.Invoke(appearance);
        }
    }
}
