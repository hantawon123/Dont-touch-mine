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

            public const string Fullscreen = "settings.choice.fullscreen";
            public const string Windowed = "settings.choice.windowed";
            public const string Low = "settings.choice.low";
            public const string Medium = "settings.choice.medium";
            public const string High = "settings.choice.high";
            public const string On = "settings.choice.on";
            public const string Off = "settings.choice.off";
            public const string Small = "settings.choice.small";
            public const string Large = "settings.choice.large";
            public const string ChatOff = "settings.choice.chatOff";
            public const string ChatOn = "settings.choice.chatOn";
            public const string PushToTalk = "settings.choice.pushToTalk";
            public const string OpenMic = "settings.choice.openMic";
            public const string DefaultDevice = "settings.choice.defaultDevice";
            public const string Unbound = "settings.choice.unbound";
            public const string MouseLeft = "settings.choice.mouseLeft";
            public const string MouseRight = "settings.choice.mouseRight";
            public const string MouseMiddle = "settings.choice.mouseMiddle";
            public const string ScrollUp = "settings.choice.scrollUp";
            public const string ScrollDown = "settings.choice.scrollDown";

            public const string DisplayMode = "settings.row.displayMode";
            public const string Resolution = "settings.row.resolution";
            public const string FpsLimit = "settings.row.fpsLimit";
            public const string TextureQuality = "settings.row.textureQuality";

            public const string UiScale = "settings.row.uiScale";
            public const string FontScale = "settings.row.fontScale";
            public const string InGameUi = "settings.row.inGameUi";
            public const string FpsCounter = "settings.row.fpsCounter";
            public const string PingCounter = "settings.row.pingCounter";
            public const string PlayerNames = "settings.row.playerNames";
            public const string StreamerMode = "settings.row.streamerMode";
            public const string BeginnerGuide = "settings.row.beginnerGuide";
            public const string ChatScope = "settings.row.chatScope";

            public const string GameInvite = "settings.row.gameInvite";

            public const string SpeakerHeading = "settings.heading.speakers";
            public const string MicrophoneHeading = "settings.heading.microphone";
            public const string KeyboardMoveHeading = "settings.heading.keyboardMove";
            public const string KeyboardActionHeading = "settings.heading.keyboardAction";
            public const string FirstPersonHeading = "settings.heading.firstPerson";
            public const string ThirdPersonHeading = "settings.heading.thirdPerson";

            public const string Device = "settings.row.device";
            public const string InputMode = "settings.row.inputMode";
            public const string MicrophoneTest = "settings.row.microphoneTest";
            public const string VolumeMaster = "settings.row.volumeMaster";
            public const string VolumeMusic = "settings.row.volumeMusic";
            public const string VolumeEffects = "settings.row.volumeEffects";
            public const string VolumeMicrophone = "settings.row.volumeMicrophone";

            public const string ActionVoiceToggle = "settings.row.actionVoiceToggle";
            public const string ActionSpeakerToggle = "settings.row.actionSpeakerToggle";
            public const string ActionMoveForward = "settings.row.actionMoveForward";
            public const string ActionMoveLeft = "settings.row.actionMoveLeft";
            public const string ActionMoveBackward = "settings.row.actionMoveBackward";
            public const string ActionMoveRight = "settings.row.actionMoveRight";
            public const string ActionSprint = "settings.row.actionSprint";
            public const string ActionJump = "settings.row.actionJump";
            public const string ActionCrouch = "settings.row.actionCrouch";
            public const string ActionProne = "settings.row.actionProne";
            public const string ActionToggleView = "settings.row.actionToggleView";
            public const string ActionPrimary = "settings.row.actionPrimary";
            public const string ActionInteract = "settings.row.actionInteract";
            public const string ActionPlacement = "settings.row.actionPlacement";
            public const string ActionRotateLeft = "settings.row.actionRotateLeft";
            public const string ActionRotateRight = "settings.row.actionRotateRight";
            public const string ActionRaise = "settings.row.actionRaise";
            public const string ActionLower = "settings.row.actionLower";
            public const string ActionKeyGuide = "settings.row.actionKeyGuide";

            public const string MouseSensitivity = "settings.row.mouseSensitivity";
            public const string CameraSensitivity = "settings.row.cameraSensitivity";
            public const string InvertX = "settings.row.invertX";
            public const string InvertY = "settings.row.invertY";

            public const string Back = "settings.back";
            public const string ResetAll = "settings.resetAll";

            public const string ResetAllTitle = "settings.modal.resetAllTitle";
            public const string ResetAllSubtitle = "settings.modal.resetAllSubtitle";
            public const string ResetTabTitle = "settings.modal.resetTabTitle";
            public const string ResetTabSubtitle = "settings.modal.resetTabSubtitle";
            public const string Cancel = "settings.modal.cancel";
            public const string DiscardTitle = "settings.modal.discardTitle";
            public const string DiscardSubtitle = "settings.modal.discardSubtitle";
            public const string LeaveWithoutSaving = "settings.modal.leaveWithoutSaving";
            public const string SaveAndLeave = "settings.modal.saveAndLeave";
            public const string LeaveGameTitle = "settings.modal.leaveGameTitle";
            public const string Leave = "settings.modal.leave";

            public const string Feedback = "settings.feedback";
            public const string FeedbackSend = "settings.feedback.send";
            public const string FeedbackSubtitle = "settings.feedback.subtitle";
            public const string FeedbackPlaceholder = "settings.feedback.placeholder";
            public const string FeedbackSubmit = "settings.feedback.submit";
            public const string FeedbackSent = "settings.feedback.sent";
            public const string FeedbackKept = "settings.feedback.kept";
            public const string FeedbackNotSignedIn = "settings.feedback.notSignedIn";
            public const string FeedbackOffline = "settings.feedback.offline";
            public const string FeedbackTooLong = "settings.feedback.tooLong";
            public const string FeedbackFailed = "settings.feedback.failed";

            public const string MicTestIdle = "settings.micTest.idle";
            public const string MicTestRunning = "settings.micTest.running";
            public const string MicTestUnavailable = "settings.micTest.unavailable";

            public const string KeyInUseTitle = "settings.keyInUse.title";
            public const string KeyInUseMessage = "settings.keyInUse.message";
        }
    }
}
