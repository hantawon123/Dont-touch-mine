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
            public const string ActionEmoteWheel = "settings.row.actionEmoteWheel";

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
            public const string Tutorial = "home.menu.tutorial";
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

            /// <summary>
            /// The key a Photon region code is named by, so adding a region
            /// only takes a catalogue line.
            /// </summary>
            public static string Region(string code) => "home.server.region." + code;

            public const string SuspendedTitle = "home.suspended.title";
            public const string SuspendedBody = "home.suspended.body";
            public const string UpdateTitle = "home.update.title";
            public const string UpdateBody = "home.update.body";
            public const string UpdateDownload = "home.update.download";
            public const string InviteBody = "home.invite.body";
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
            public const string ReportTitle = "lobby.reportTitle";
            public const string StartCountdown = "lobby.startCountdown";
        }

        public static class Map
        {
            public const string Supermarket = "map.supermarket";
            public const string Mansion = "map.mansion";
        }

        public static class Category
        {
            public const string Food = "category.food";
            public const string Household = "category.household";
            public const string Bathroom = "category.bathroom";
            public const string Plants = "category.plants";
            public const string Tools = "category.tools";
            public const string Modern = "category.modern";
            public const string Fantasy = "category.fantasy";
            public const string Beach = "category.beach";
            public const string Casino = "category.casino";
            public const string Halloween = "category.halloween";
            public const string Toys = "category.toys";
            public const string Reserve = "category.reserve";
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
            public const string Back = "rooms.back";
            public const string EnterByCode = "rooms.enterByCode";
            public const string Enter = "rooms.enter";
        }

        public static class Match
        {
            public const string ChatPlaceholder = "match.chat.placeholder";

            public const string TimerHint = "match.timer.hint";
            public const string WinHeadline = "match.result.winHeadline";
            public const string LoseHeadline = "match.result.loseHeadline";
            public const string WinSubtitle = "match.result.winSubtitle";
            public const string LoseSubtitle = "match.result.loseSubtitle";
            public const string NoResult = "match.result.none";
            public const string ResultMissing = "match.result.missing";
            public const string ResultHeading = "match.result.heading";
            public const string Winners = "match.result.winners";
            public const string EndReason = "match.result.endReason";
            public const string NoWinner = "match.result.noWinner";
            public const string MatchOver = "match.result.matchOver";
            public const string Victory = "match.result.victory";
            public const string Defeat = "match.result.defeat";
            public const string None = "match.result.noneLabel";
            public const string PlayerN = "match.result.playerN";
            public const string ReturnLobby = "match.result.returnLobby";
            public const string TimeExpired = "match.result.timeExpired";
            public const string AllItemsDestroyed = "match.result.allItemsDestroyed";
            public const string LastStanding = "match.result.lastStanding";

            public const string PhaseWaiting = "match.phase.waiting";
            public const string PhaseHighlight = "match.phase.highlight";
            public const string PhaseResult = "match.phase.result";
            public const string PhaseHiding = "match.phase.hiding";
            public const string PhaseHidingNamed = "match.phase.hidingNamed";

            public const string HighlightTitle = "match.highlight.title";
            public const string HighlightSkip = "match.highlight.skip";
            public const string HighlightSkipAll = "match.highlight.skipAll";

            public const string HideHint = "match.hide.hint";
            public const string HideWarning = "match.hide.warning";
            public const string HideComplete = "match.hide.complete";
            public const string HeldItem = "match.heldItem";
            public const string HideBanner = "match.hide.banner";
            public const string HideFinalBanner = "match.hide.finalBanner";
            public const string HideNextTurn = "match.hide.nextTurn";
            public const string HideStatus = "match.hide.status";
            public const string HideStatusNamed = "match.hide.statusNamed";

            public const string IntroHint = "match.intro.hint";
            public const string StolenItem = "match.intro.stolen";
            public const string SearchingTitle = "match.searching.title";
            public const string SearchingBody = "match.searching.body";
            public const string SearchingHint = "match.searching.hint";
            public const string ItemFallback = "match.item.fallback";

            public const string Shredder = "match.shredder";
            public const string ShredderUnlimited = "match.shredder.unlimited";
            public const string ShredderUses = "match.shredder.uses";
            public const string Destroyed = "match.destroyed";
        }

        public static class Closet
        {
            public const string Back = "closet.back";
            public const string Reset = "closet.reset";
            public const string Apply = "closet.apply";
            public const string SaveError = "closet.saveError";
            public const string ResetTitle = "closet.modal.resetTitle";
            public const string DiscardTitle = "closet.modal.discardTitle";
            public const string Subtitle = "closet.modal.subtitle";
            public const string Decline = "closet.modal.decline";
            public const string Accept = "closet.modal.accept";
            public const string PortraitHint = "closet.portraitHint";
            public const string Hood = "closet.hood";
            public const string HoodShape = "closet.hoodShape";
            public const string HoodColor = "closet.hoodColor";
            public const string Body = "closet.body";
            public const string Shoes = "closet.shoes";
            public const string Face = "closet.face";
        }

        public static class Guide
        {
            public const string Click = "guide.click";
            public const string RightClick = "guide.rightClick";
            public const string Scroll = "guide.scroll";
            public const string Toggle = "guide.toggle";
            public const string Attack = "guide.attack";
            public const string Crouch = "guide.crouch";
            public const string Prone = "guide.prone";
            public const string ToggleView = "guide.toggleView";
            public const string Sprint = "guide.sprint";
            public const string Jump = "guide.jump";
            public const string Placement = "guide.placement";
            public const string Throw = "guide.throw";
            public const string Drop = "guide.drop";
            public const string Place = "guide.place";
            public const string Rotate = "guide.rotate";
            public const string Twist = "guide.twist";
            public const string InteractPickup = "guide.interactPickup";
            public const string Emote = "guide.emote";
        }

        public static class Emote
        {
            public const string Wave = "emote.wave";
            public const string Taunt = "emote.taunt";
            public const string Insult = "emote.insult";
            public const string Chicken = "emote.chicken";
            public const string HipHop = "emote.hipHop";
            public const string Spin = "emote.spin";
            public const string Hint = "emote.hint";
        }

        public static class Interact
        {
            public const string PickUp = "interact.pickUp";
            public const string Destroy = "interact.destroy";
            public const string Place = "interact.place";
        }

        public static class Tutorial
        {
            public const string ExitTitle = "tutorial.exitTitle";
            public const string ExitDoor = "tutorial.exitDoor";
            public const string ChecklistTitle = "tutorial.checklist.title";
            public const string StepMove = "tutorial.step.move";
            public const string StepSprint = "tutorial.step.sprint";
            public const string StepJump = "tutorial.step.jump";
            public const string StepCrouch = "tutorial.step.crouch";
            public const string StepProne = "tutorial.step.prone";
            public const string StepPickUp = "tutorial.step.pickUp";
            public const string StepDrop = "tutorial.step.drop";
            public const string StepThrow = "tutorial.step.throw";
            public const string StepPlace = "tutorial.step.place";
            public const string StepShredder = "tutorial.step.shredder";
            public const string StepExit = "tutorial.step.exit";
            public const string Intro = "tutorial.radio.intro";
            public const string InstructMove = "tutorial.instruct.move";
            public const string InstructSprint = "tutorial.instruct.sprint";
            public const string InstructJump = "tutorial.instruct.jump";
            public const string InstructCrouch = "tutorial.instruct.crouch";
            public const string InstructProne = "tutorial.instruct.prone";
            public const string InstructPickUp = "tutorial.instruct.pickUp";
            public const string InstructDrop = "tutorial.instruct.drop";
            public const string InstructThrow = "tutorial.instruct.throw";
            public const string InstructPlace = "tutorial.instruct.place";
            public const string InstructShredder = "tutorial.instruct.shredder";
            public const string InstructComplete = "tutorial.instruct.complete";
            public const string CompleteMove = "tutorial.complete.move";
            public const string CompleteSprint = "tutorial.complete.sprint";
            public const string CompleteJump = "tutorial.complete.jump";
            public const string CompleteCrouch = "tutorial.complete.crouch";
            public const string CompleteProne = "tutorial.complete.prone";
            public const string CompletePickUp = "tutorial.complete.pickUp";
            public const string CompleteDrop = "tutorial.complete.drop";
            public const string CompleteThrow = "tutorial.complete.throw";
            public const string CompletePlace = "tutorial.complete.place";
            public const string CompleteShredder = "tutorial.complete.shredder";
            public const string RetryJump = "tutorial.retry.jump";
            public const string RetryGeneric = "tutorial.retry.generic";
            public const string TargetPickUp = "tutorial.target.pickUp";
            public const string TargetDrop = "tutorial.target.drop";
            public const string TargetThrow = "tutorial.target.throw";
            public const string TargetPlace = "tutorial.target.place";
            public const string HintPickUp = "tutorial.hint.pickUp";
            public const string HintPlacement = "tutorial.hint.placement";
            public const string HintPlace = "tutorial.hint.place";
            public const string FocusPickUp = "tutorial.focus.pickUp";
            public const string FocusPlacement = "tutorial.focus.placement";
            public const string FocusMove = "tutorial.focus.move";
            public const string FocusSprint = "tutorial.focus.sprint";
            public const string FocusJump = "tutorial.focus.jump";
            public const string FocusCrouch = "tutorial.focus.crouch";
            public const string FocusProne = "tutorial.focus.prone";
            public const string FocusLookPickUp = "tutorial.focus.lookPickUp";
            public const string FocusDrop = "tutorial.focus.drop";
            public const string FocusThrow = "tutorial.focus.throw";
            public const string FocusPlace = "tutorial.focus.place";
            public const string FocusShredder = "tutorial.focus.shredder";
            public const string FocusExit = "tutorial.focus.exit";
        }
    }
}
