namespace Game.Core.Settings
{
    /// <summary>
    /// Keys for words the interface draws, grouped by the screen that reads
    /// them. A key lands in the same change that starts looking it up.
    /// </summary>
    public static class UiText
    {
        public static class Settings
        {
            public const string Language = "settings.language";
            public const string Apply = "settings.apply";
            public const string Reset = "settings.reset";
            public const string LeaveGame = "settings.leaveGame";

            public const string TabGeneral = "settings.tab.general";
            public const string TabGraphics = "settings.tab.graphics";
            public const string TabInterface = "settings.tab.interface";
            public const string TabSound = "settings.tab.sound";
            public const string TabControls = "settings.tab.controls";
            public const string TabNotifications = "settings.tab.notifications";
        }
    }
}
