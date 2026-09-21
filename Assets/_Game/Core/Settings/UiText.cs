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

        public static class Home
        {
            public const string CreateRoom = "home.menu.createRoom";
            public const string FindRoom = "home.menu.findRoom";
            public const string Character = "home.menu.character";
            public const string Settings = "home.menu.settings";
            public const string Quit = "home.menu.quit";

            public const string ConnectionError = "home.connectionError";
            public const string HostDisconnected = "home.hostDisconnected";
            public const string ServerDisconnected = "home.serverDisconnected";

            public const string SearchAllow = "home.profile.searchAllow";
            public const string ConfirmChange = "home.profile.confirmChange";
            public const string ConfirmPrompt = "home.profile.confirmPrompt";
            public const string SearchAllowFailed = "home.profile.searchAllowFailed";
            public const string TooLong = "home.profile.tooLong";
            public const string BadCharacter = "home.profile.badCharacter";
            public const string Taken = "home.profile.taken";
            public const string Available = "home.profile.available";
            public const string OneChange = "home.profile.oneChange";
            public const string AlreadySet = "home.profile.alreadySet";
            public const string Unreachable = "home.profile.unreachable";
            public const string NicknameTaken = "home.profile.nicknameTaken";
            public const string NicknameForbidden = "home.profile.nicknameForbidden";
            public const string NicknameInvalid = "home.profile.nicknameInvalid";
            public const string AccountNotFound = "home.profile.accountNotFound";
            public const string RenameFailed = "home.profile.renameFailed";

            public const string Unfriend = "home.friends.unfriend";
            public const string Empty = "home.friends.empty";
            public const string ListTab = "home.friends.listTab";
            public const string RequestTab = "home.friends.requestTab";
            public const string SearchPlaceholder = "home.friends.searchPlaceholder";
            public const string Refresh = "home.friends.refresh";
            public const string Online = "home.friends.online";
            public const string Offline = "home.friends.offline";
            public const string SearchResults = "home.friends.searchResults";
            public const string SearchEmpty = "home.friends.searchEmpty";
            public const string IncomingRequests = "home.friends.incomingRequests";
            public const string TargetNotFound = "home.friends.targetNotFound";
            public const string AlreadyFriends = "home.friends.alreadyFriends";
            public const string RequestAlreadySent = "home.friends.requestAlreadySent";
            public const string RequestNotFound = "home.friends.requestNotFound";
            public const string NotFriends = "home.friends.notFriends";
            public const string TargetInGame = "home.friends.targetInGame";
            public const string SelfRequest = "home.friends.selfRequest";
            public const string Conflict = "home.friends.conflict";
            public const string FriendFailed = "home.friends.failed";

            public const string RoomScope = "home.createRoom.scope";
            public const string RoomTitle = "home.createRoom.title";
            public const string RoomPlayers = "home.createRoom.players";
            public const string RoomSubmit = "home.createRoom.submit";
            public const string RoomTitlePlaceholder = "home.createRoom.placeholder";

            public const string ServerTitle = "home.server.title";

            public const string SuspendedTitle = "home.suspended.title";
            public const string SuspendedBody = "home.suspended.body";
        }

        public static class Play
        {
            public const string Title = "play.title";
            public const string RoomSection = "play.roomSection";
            public const string RoomTitle = "play.roomTitle";
            public const string RoomCode = "play.roomCode";
            public const string MaxPlayers = "play.maxPlayers";
            public const string DestructionLimit = "play.destructionLimit";
            public const string HidingDuration = "play.hidingDuration";
            public const string SearchingDuration = "play.searchingDuration";
            public const string SprintSpeed = "play.sprintSpeed";
            public const string StunHits = "play.stunHits";
            public const string MapSelect = "play.mapSelect";
            public const string CategorySelect = "play.categorySelect";
            public const string Reset = "play.reset";
            public const string Unapplied = "play.unapplied";
            public const string GameStart = "play.gameStart";
            public const string Copied = "play.copied";
            public const string Random = "play.random";
            public const string Unlimited = "play.unlimited";
            public const string Seconds = "play.seconds";
            public const string Minutes = "play.minutes";
            public const string MinutesSeconds = "play.minutesSeconds";
            public const string Players = "play.players";
            public const string Count = "play.count";
            public const string Multiplier = "play.multiplier";
            public const string Invite = "play.invite";
        }

        public static class Lobby
        {
            public const string Participants = "lobby.participants";
            public const string Friends = "lobby.friends";
            public const string Online = "lobby.online";
            public const string Waiting = "lobby.waiting";
            public const string InGame = "lobby.inGame";
            public const string NoParticipants = "lobby.noParticipants";
            public const string Kick = "lobby.kick";
            public const string KickAction = "lobby.kickAction";
            public const string KickTitle = "lobby.kickTitle";
            public const string Report = "lobby.report";
            public const string Confirm = "lobby.confirm";
            public const string Category = "lobby.category";
            public const string PlayerCount = "lobby.playerCount";
            public const string Closet = "lobby.shortcut.closet";
            public const string PlayersShortcut = "lobby.shortcut.players";
            public const string Settings = "lobby.shortcut.settings";
            public const string HostPrompt = "lobby.plan.host";
            public const string GuestPrompt = "lobby.plan.guest";
            public const string ReasonAbuse = "lobby.reason.abuse";
            public const string ReasonCheating = "lobby.reason.cheating";
            public const string ReasonSpam = "lobby.reason.spam";
            public const string ReasonName = "lobby.reason.name";
            public const string ReasonOther = "lobby.reason.other";
        }

        public static class Rooms
        {
            public const string NoRooms = "rooms.noRooms";
            public const string NoSearchResults = "rooms.noSearch";
            public const string Kicked = "rooms.kicked";
            public const string Waiting = "rooms.waiting";
            public const string Playing = "rooms.playing";
            public const string Generic = "rooms.entry.generic";
            public const string CreateFailed = "rooms.entry.createFailed";
            public const string InviteGone = "rooms.entry.inviteGone";
            public const string CodeMissing = "rooms.entry.codeMissing";
            public const string ListGone = "rooms.entry.listGone";
            public const string InviteExpired = "rooms.entry.inviteExpired";
            public const string Full = "rooms.entry.full";
            public const string FullPickAnother = "rooms.entry.fullPickAnother";
            public const string AlreadyStarted = "rooms.entry.alreadyStarted";
            public const string CheckSettings = "rooms.entry.checkSettings";
            public const string CheckCode = "rooms.entry.checkCode";
            public const string AlreadyInRoom = "rooms.entry.alreadyInRoom";
            public const string WrongPassword = "rooms.modal.wrongPassword";
            public const string FullModal = "rooms.modal.full";
            public const string ClosedModal = "rooms.modal.closed";
            public const string NotFoundModal = "rooms.modal.notFound";
            public const string AlreadyInModal = "rooms.modal.alreadyIn";
            public const string ConnectFailed = "rooms.modal.connectFailed";
            public const string JoinFailed = "rooms.modal.joinFailed";
            public const string InvalidCodeModal = "rooms.modal.invalidCode";
            public const string HostRoom = "rooms.hostRoom";
        }
    }
}
