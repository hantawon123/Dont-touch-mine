namespace Game.Client.Lobby
{
    /// <summary>
    /// Opens the play settings screen from somewhere other than the Esc menu,
    /// such as an object the player aims at in the room.
    /// </summary>
    /// <remarks>
    /// Owned by the pause menu presenter because opening the screen means
    /// releasing the cursor and locking movement, and only the menu knows how
    /// to put those back when the screen closes again.
    /// </remarks>
    public interface IPlaySettingsOpener
    {
        /// <summary>
        /// Opens the play settings as if the player pressed Esc and chose it,
        /// except that closing the screen returns straight to the room instead
        /// of to the menu. Does nothing while any menu screen is already up.
        /// </summary>
        void OpenPlaySettingsFromWorld();
    }
}
