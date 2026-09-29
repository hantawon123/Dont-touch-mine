namespace Game.Client.Lobby
{
    /// <summary>
    /// Which lobby shortcut a key-down should open, and when that press is
    /// ignored. Kept as a pure function so the presenter can stay a thin
    /// owner of cursor and movement.
    /// </summary>
    public static class LobbyShortcutBindings
    {
        public static LobbyShortcutKind ReadPressed(
            bool characterKey,
            bool playersKey,
            bool settingsKey)
        {
            if (characterKey)
            {
                return LobbyShortcutKind.Character;
            }

            if (playersKey)
            {
                return LobbyShortcutKind.Players;
            }

            return settingsKey ? LobbyShortcutKind.Settings : LobbyShortcutKind.None;
        }

        /// <summary>
        /// 1 / 2 / Esc only open a shortcut while the player is in the room.
        /// Chat owns the keyboard while it is focused; the pause menu and play
        /// settings already own Esc, so a second overlay on top of them would
        /// leave two things claiming the same key.
        /// </summary>
        public static bool CanHandle(
            bool inputBlocked,
            bool menuOpen,
            bool foreignScreenOpen)
        {
            return !inputBlocked && !menuOpen && !foreignScreenOpen;
        }
    }
}
