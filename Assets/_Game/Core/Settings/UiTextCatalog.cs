using System;
using System.Collections.Generic;

namespace Game.Core.Settings
{
    /// <summary>
    /// One line the interface can draw, in the languages it has words for.
    /// Korean is required; any other language is optional and falls back to
    /// Korean until a translation lands.
    /// </summary>
    public readonly struct UiTextLine
    {
        private readonly Dictionary<string, string> byLanguage;

        /// <summary>A short, stable identifier that screens ask for.</summary>
        public string Key { get; }

        /// <summary>The default words, and what a missing translation shows.</summary>
        public string Korean { get; }

        /// <param name="korean">Always required. The catalogue's default.</param>
        /// <param name="english">
        /// Optional. Null or blank leaves the line in Korean when English is
        /// asked for, so a screen can be moved across in pieces.
        /// </param>
        public UiTextLine(string key, string korean, string english = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A line needs a key.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(korean))
            {
                throw new ArgumentException("A line needs Korean words.", nameof(korean));
            }

            Key = key;
            Korean = korean;
            byLanguage = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ko"] = korean
            };

            if (!string.IsNullOrWhiteSpace(english))
            {
                byLanguage["en"] = english;
            }
        }

        public bool TryGet(string languageCode, out string text)
        {
            if (!string.IsNullOrWhiteSpace(languageCode)
                && byLanguage.TryGetValue(languageCode, out text))
            {
                return true;
            }

            text = null;
            return false;
        }
    }

    /// <summary>
    /// The words the interface can draw, looked up by key and language.
    /// </summary>
    /// <remarks>
    /// Data rather than a fixed list, so a test can hand a couple of lines
    /// and watch the lookup; the game itself uses <see cref="Shipped"/>.
    /// Screens add their keys here in the same change that starts reading
    /// them, grouped as <see cref="UiText"/> constants.
    /// </remarks>
    public sealed class UiTextCatalog
    {
        private readonly Dictionary<string, UiTextLine> lines;

        /// <summary>
        /// What the game ships with. The settings chrome is here; other
        /// screens add their keys when they start reading them. A missing
        /// key returns the key itself so a forgotten line is visible
        /// rather than blank.
        /// </summary>
        public static UiTextCatalog Shipped { get; } = new UiTextCatalog(
            new UiTextLine(UiText.Settings.Language, "언어", "Language"),
            new UiTextLine(UiText.Settings.Apply, "적용하기", "Apply"),
            new UiTextLine(UiText.Settings.Reset, "변경 취소", "Discard"),
            new UiTextLine(UiText.Settings.LeaveGame, "게임 나가기", "Leave Game"),
            new UiTextLine(UiText.Settings.TabGeneral, "일반", "General"),
            new UiTextLine(UiText.Settings.TabGraphics, "그래픽", "Graphics"),
            new UiTextLine(UiText.Settings.TabInterface, "인터페이스", "Interface"),
            new UiTextLine(UiText.Settings.TabSound, "사운드", "Sound"),
            new UiTextLine(UiText.Settings.TabControls, "컨트롤", "Controls"),
            new UiTextLine(UiText.Settings.TabNotifications, "알림", "Notifications"),
            new UiTextLine(UiText.Settings.Fullscreen, "전체화면", "Fullscreen"),
            new UiTextLine(UiText.Settings.Windowed, "창모드", "Windowed"),
            new UiTextLine(UiText.Settings.Low, "낮음", "Low"),
            new UiTextLine(UiText.Settings.Medium, "중간", "Medium"),
            new UiTextLine(UiText.Settings.High, "높음", "High"),
            new UiTextLine(UiText.Settings.On, "켜기", "On"),
            new UiTextLine(UiText.Settings.Off, "끄기", "Off"),
            new UiTextLine(UiText.Settings.Small, "작게", "Small"),
            new UiTextLine(UiText.Settings.Large, "크게", "Large"),
            new UiTextLine(UiText.Settings.ChatOff, "끄기(모두)", "Off (Everyone)"),
            new UiTextLine(UiText.Settings.ChatOn, "켜기(친구만)", "On (Friends only)"),
            new UiTextLine(UiText.Settings.PushToTalk, "눌러서 말하기", "Push to Talk"),
            new UiTextLine(UiText.Settings.OpenMic, "오픈 마이크", "Open Mic"),
            new UiTextLine(UiText.Settings.DefaultDevice, "기본 장치", "Default Device"),
            new UiTextLine(UiText.Settings.Unbound, "없음", "None"),
            new UiTextLine(UiText.Settings.MouseLeft, "좌클릭", "Left Click"),
            new UiTextLine(UiText.Settings.MouseRight, "우클릭", "Right Click"),
            new UiTextLine(UiText.Settings.MouseMiddle, "휠클릭", "Middle Click"),
            new UiTextLine(UiText.Settings.ScrollUp, "스크롤 ↑", "Scroll Up"),
            new UiTextLine(UiText.Settings.ScrollDown, "스크롤 ↓", "Scroll Down"),
            new UiTextLine(UiText.Settings.DisplayMode, "디스플레이 모드", "Display Mode"),
            new UiTextLine(UiText.Settings.Resolution, "해상도", "Resolution"),
            new UiTextLine(UiText.Settings.FpsLimit, "FPS 제한", "FPS Limit"),
            new UiTextLine(UiText.Settings.TextureQuality, "텍스처 품질", "Texture Quality"),
            new UiTextLine(UiText.Settings.UiScale, "UI 크기", "UI Scale"),
            new UiTextLine(UiText.Settings.FontScale, "글자 크기", "Font Size"),
            new UiTextLine(UiText.Settings.InGameUi, "게임 내 UI", "In-Game UI"),
            new UiTextLine(UiText.Settings.FpsCounter, "FPS 표시", "Show FPS"),
            new UiTextLine(UiText.Settings.PingCounter, "핑 표시", "Show Ping"),
            new UiTextLine(UiText.Settings.PlayerNames, "다른 플레이어 이름 표시", "Show Player Names"),
            new UiTextLine(UiText.Settings.StreamerMode, "스트리머 모드", "Streamer Mode"),
            new UiTextLine(UiText.Settings.BeginnerGuide, "초심자 가이드 항상 표시", "Always Show Beginner Guide"),
            new UiTextLine(UiText.Settings.ChatScope, "채팅 메시지 범위 제한", "Limit Chat to Friends"),
            new UiTextLine(UiText.Settings.GameInvite, "게임 초대 알림", "Game Invite Notifications"),
            new UiTextLine(UiText.Settings.SpeakerHeading, "스피커", "Speakers"),
            new UiTextLine(UiText.Settings.MicrophoneHeading, "마이크", "Microphone"),
            new UiTextLine(UiText.Settings.KeyboardMoveHeading, "키보드(이동)", "Keyboard (Movement)"),
            new UiTextLine(UiText.Settings.KeyboardActionHeading, "키보드(행동)", "Keyboard (Actions)"),
            new UiTextLine(UiText.Settings.FirstPersonHeading, "1인칭", "First Person"),
            new UiTextLine(UiText.Settings.ThirdPersonHeading, "3인칭", "Third Person"),
            new UiTextLine(UiText.Settings.Device, "마이크 장치", "Microphone Device"),
            new UiTextLine(UiText.Settings.InputMode, "입력 모드", "Input Mode"),
            new UiTextLine(UiText.Settings.MicrophoneTest, "마이크 테스트", "Microphone Test"),
            new UiTextLine(UiText.Settings.VolumeMaster, "마스터 볼륨", "Master Volume"),
            new UiTextLine(UiText.Settings.VolumeMusic, "배경음악 볼륨", "Music Volume"),
            new UiTextLine(UiText.Settings.VolumeEffects, "효과음 볼륨", "Effects Volume"),
            new UiTextLine(UiText.Settings.VolumeMicrophone, "마이크 볼륨", "Microphone Volume"),
            new UiTextLine(UiText.Settings.ActionVoiceToggle, "마이크 켜기/끄기", "Mute Microphone"),
            new UiTextLine(UiText.Settings.ActionSpeakerToggle, "스피커 켜기/끄기", "Mute Speakers"),
            new UiTextLine(UiText.Settings.ActionMoveForward, "앞으로 이동", "Move Forward"),
            new UiTextLine(UiText.Settings.ActionMoveLeft, "왼쪽으로 이동", "Move Left"),
            new UiTextLine(UiText.Settings.ActionMoveBackward, "뒤로 이동", "Move Backward"),
            new UiTextLine(UiText.Settings.ActionMoveRight, "오른쪽으로 이동", "Move Right"),
            new UiTextLine(UiText.Settings.ActionSprint, "달리기", "Sprint"),
            new UiTextLine(UiText.Settings.ActionJump, "점프", "Jump"),
            new UiTextLine(UiText.Settings.ActionCrouch, "앉기", "Crouch"),
            new UiTextLine(UiText.Settings.ActionProne, "엎드리기", "Prone"),
            new UiTextLine(UiText.Settings.ActionToggleView, "시점 변경(1인칭/3인칭)", "Toggle View"),
            new UiTextLine(UiText.Settings.ActionPrimary, "공격/던지기/배치", "Attack / Throw / Place"),
            new UiTextLine(UiText.Settings.ActionInteract, "물건 상호작용", "Interact"),
            new UiTextLine(UiText.Settings.ActionPlacement, "배치모드 활성화", "Placement Mode"),
            new UiTextLine(UiText.Settings.ActionRotateLeft, "가로축 회전(좌방향)", "Rotate Left"),
            new UiTextLine(UiText.Settings.ActionRotateRight, "가로축 회전(우방향)", "Rotate Right"),
            new UiTextLine(UiText.Settings.ActionRaise, "세로축 회전(상향)", "Raise Object"),
            new UiTextLine(UiText.Settings.ActionLower, "세로축 회전(하향)", "Lower Object"),
            new UiTextLine(UiText.Settings.ActionKeyGuide, "키 가이드 on/off", "Key Guide"),
            new UiTextLine(UiText.Settings.MouseSensitivity, "마우스 감도", "Mouse Sensitivity"),
            new UiTextLine(UiText.Settings.CameraSensitivity, "카메라 감도", "Camera Sensitivity"),
            new UiTextLine(UiText.Settings.InvertX, "X축 반전", "Invert X"),
            new UiTextLine(UiText.Settings.InvertY, "Y축 반전", "Invert Y"),
            new UiTextLine(UiText.Settings.Back, "← 이전", "← Back"),
            new UiTextLine(UiText.Settings.ResetAll, "전체 변경 취소", "Discard All Changes"),
            new UiTextLine(UiText.Settings.ResetAllTitle, "전체 설정의 변경을 취소하시겠습니까?", "Discard all settings changes?"),
            new UiTextLine(UiText.Settings.ResetAllSubtitle, "모든 설정을 마지막으로 저장한 값으로 되돌립니다.", "Every setting goes back to the last saved value."),
            new UiTextLine(UiText.Settings.ResetTabTitle, "{0} 설정의 변경을 취소하시겠습니까?", "Discard changes to {0} settings?"),
            new UiTextLine(UiText.Settings.ResetTabSubtitle, "현재 탭의 설정을 마지막으로 저장한 값으로 되돌립니다.", "This tab goes back to the last saved values."),
            new UiTextLine(UiText.Settings.Cancel, "취소", "Cancel"),
            new UiTextLine(UiText.Settings.DiscardTitle, "저장하고 나가시겠습니까?", "Save before leaving?"),
            new UiTextLine(UiText.Settings.DiscardSubtitle, "저장하지 않으면 변경사항이 사라집니다.", "Unsaved changes will be lost."),
            new UiTextLine(UiText.Settings.LeaveWithoutSaving, "바로 나가기", "Leave"),
            new UiTextLine(UiText.Settings.SaveAndLeave, "저장하고 나가기", "Save and Leave"),
            new UiTextLine(UiText.Settings.LeaveGameTitle, "게임을 진짜 나가시겠습니까?", "Leave the game?"),
            new UiTextLine(UiText.Settings.Leave, "나가기", "Leave"),
            new UiTextLine(UiText.Settings.Feedback, "피드백", "Feedback"),
            new UiTextLine(UiText.Settings.FeedbackSend, "피드백 보내기", "Send Feedback"),
            new UiTextLine(UiText.Settings.FeedbackSubtitle, "불편한 점이나 바라는 점을 남겨주세요.", "Tell us what is wrong or what you would like."),
            new UiTextLine(UiText.Settings.FeedbackPlaceholder, "내용을 입력해주세요", "Write your message"),
            new UiTextLine(UiText.Settings.FeedbackSubmit, "보내기", "Send"),
            new UiTextLine(UiText.Settings.FeedbackSent, "보냈습니다. 고맙습니다", "Sent. Thank you."),
            new UiTextLine(UiText.Settings.FeedbackKept, "작성한 내용은 그대로 있어요", "What you wrote is still here"),
            new UiTextLine(UiText.Settings.FeedbackNotSignedIn, "서버에 연결되어 있지 않습니다", "You are not connected to the server"),
            new UiTextLine(UiText.Settings.FeedbackOffline, "서버에 연결할 수 없습니다", "Could not reach the server"),
            new UiTextLine(UiText.Settings.FeedbackTooLong, "글이 너무 길어 보내지 못했습니다", "The message is too long to send"),
            new UiTextLine(UiText.Settings.FeedbackFailed, "보내지 못했습니다", "Could not send"),
            new UiTextLine(UiText.Settings.MicTestIdle, "테스트 해보기", "Try Test"),
            new UiTextLine(UiText.Settings.MicTestRunning, "테스트 중...", "Testing..."),
            new UiTextLine(UiText.Settings.MicTestUnavailable, "이 컴퓨터에서 마이크를 열 수 없습니다.", "This computer cannot open the microphone."),
            new UiTextLine(UiText.Settings.KeyInUseTitle, "사용 중인 키", "Key Already In Use"),
            new UiTextLine(UiText.Settings.KeyInUseMessage, "{0} 키는 이미 {1}에 사용 중입니다", "{0} is already used by {1}"),
            new UiTextLine(UiText.Home.CreateRoom, "방 만들기", "Create Room"),
            new UiTextLine(UiText.Home.FindRoom, "게임 찾기", "Find Game"),
            new UiTextLine(UiText.Home.Character, "캐릭터", "Character"),
            new UiTextLine(UiText.Home.Settings, "환경 설정", "Settings"),
            new UiTextLine(UiText.Home.Quit, "게임 종료", "Quit"),
            new UiTextLine(UiText.Home.ConnectionError, "게임 접속 오류", "Connection Error"),
            new UiTextLine(UiText.Home.HostDisconnected, "호스트의 연결이 끊어졌습니다", "The host disconnected"),
            new UiTextLine(UiText.Home.ServerDisconnected, "서버와의 연결이 끊어졌습니다", "Lost connection to the server"),
            new UiTextLine(UiText.Home.SearchAllow, "닉네임 검색 허용", "Allow nickname search"),
            new UiTextLine(UiText.Home.ConfirmChange, "변경", "Change"),
            new UiTextLine(UiText.Home.ConfirmPrompt, "\"{0}\"로 정할까요? 되돌릴 수 없어요", "Set it to \"{0}\"? This cannot be undone"),
            new UiTextLine(UiText.Home.SearchAllowFailed, "바꾸지 못했어요", "Could not change that"),
            new UiTextLine(UiText.Home.TooLong, "최대 12글자 작성가능합니다", "Up to 12 characters"),
            new UiTextLine(UiText.Home.BadCharacter, "한글/영어/숫자만 작성가능합니다", "Use Korean, English, or numbers only"),
            new UiTextLine(UiText.Home.Taken, "이미 존재하는 닉네임입니다", "That nickname is taken"),
            new UiTextLine(UiText.Home.Available, "사용 가능한 닉네임입니다", "That nickname is available"),
            new UiTextLine(UiText.Home.OneChange, "닉네임은 한 번만 변경할 수 있어요", "You can change your nickname once"),
            new UiTextLine(UiText.Home.AlreadySet, "이미 닉네임을 변경했어요", "You already changed your nickname"),
            new UiTextLine(UiText.Home.Unreachable, "확인하지 못했어요. 잠시 후 다시 시도해주세요", "Could not check. Try again shortly"),
            new UiTextLine(UiText.Home.NicknameTaken, "이미 사용 중인 이름입니다", "That name is already in use"),
            new UiTextLine(UiText.Home.NicknameForbidden, "쓸 수 없는 이름입니다", "That name cannot be used"),
            new UiTextLine(UiText.Home.NicknameInvalid, "한글, 영문, 숫자로 2~12글자여야 합니다", "Use 2–12 Korean, English, or number characters"),
            new UiTextLine(UiText.Home.AccountNotFound, "계정을 찾을 수 없습니다", "Account not found"),
            new UiTextLine(UiText.Home.RenameFailed, "이름을 바꾸지 못했습니다", "Could not change the name"),
            new UiTextLine(UiText.Home.Unfriend, "친구 끊기", "Unfriend"),
            new UiTextLine(UiText.Home.Empty, "친구가 없어요", "No friends yet"),
            new UiTextLine(UiText.Home.ListTab, "친구 목록", "Friends"),
            new UiTextLine(UiText.Home.RequestTab, "친구 요청", "Requests"),
            new UiTextLine(UiText.Home.SearchPlaceholder, "닉네임 검색", "Search nickname"),
            new UiTextLine(UiText.Home.Refresh, "새로고침", "Refresh"),
            new UiTextLine(UiText.Home.Online, "온라인", "Online"),
            new UiTextLine(UiText.Home.Offline, "오프라인", "Offline"),
            new UiTextLine(UiText.Home.SearchResults, "검색된 친구", "Search results"),
            new UiTextLine(UiText.Home.SearchEmpty, "플레이어를 찾을 수 없습니다.", "No player found."),
            new UiTextLine(UiText.Home.IncomingRequests, "요청이 온 친구 ({0})", "Incoming requests ({0})"),
            new UiTextLine(UiText.Home.TargetNotFound, "그 사용자를 찾을 수 없습니다", "That player could not be found"),
            new UiTextLine(UiText.Home.AlreadyFriends, "이미 친구입니다", "Already friends"),
            new UiTextLine(UiText.Home.RequestAlreadySent, "이미 보낸 요청입니다", "Request already sent"),
            new UiTextLine(UiText.Home.RequestNotFound, "그 요청이 이미 없습니다", "That request is already gone"),
            new UiTextLine(UiText.Home.NotFriends, "친구가 아닙니다", "Not friends"),
            new UiTextLine(UiText.Home.TargetInGame, "게임 중인 친구입니다", "That friend is in a game"),
            new UiTextLine(UiText.Home.SelfRequest, "자기 자신에게는 보낼 수 없습니다", "You cannot send that to yourself"),
            new UiTextLine(UiText.Home.Conflict, "잠시 후 다시 시도해 주세요", "Try again shortly"),
            new UiTextLine(UiText.Home.FriendFailed, "처리하지 못했습니다", "Could not complete that"),
            new UiTextLine(UiText.Home.RoomScope, "방 범위", "Type"),
            new UiTextLine(UiText.Home.RoomTitle, "방 이름", "Title"),
            new UiTextLine(UiText.Home.RoomPlayers, "인원", "Players"),
            new UiTextLine(UiText.Home.RoomSubmit, "방 생성하기", "Create Room"),
            new UiTextLine(UiText.Home.RoomTitlePlaceholder, "방 이름 입력", "Enter a room name"),
            new UiTextLine(UiText.Home.ServerTitle, "서버 설정", "Server Settings"),
            new UiTextLine(UiText.Home.SuspendedTitle, "이용이 제한된 계정입니다", "This account is suspended"),
            new UiTextLine(UiText.Home.SuspendedBody, "운영자가 이 계정의 이용을 중지했습니다.", "An operator has stopped this account."),
            new UiTextLine(UiText.Home.InviteBody, "{0}님이\n함께 플레이하자고 합니다!", "{0} invited you\nto play!"),
            new UiTextLine(UiText.Play.Title, "게임 설정", "Game Settings"),
            new UiTextLine(UiText.Play.RoomSection, "방 설정", "Room Settings"),
            new UiTextLine(UiText.Play.RoomTitle, "방 제목", "Room Title"),
            new UiTextLine(UiText.Play.RoomCode, "방 코드", "Room Code"),
            new UiTextLine(UiText.Play.MaxPlayers, "인원 설정", "Player Count"),
            new UiTextLine(UiText.Play.DestructionLimit, "파괴 기능 횟수", "Break Limit"),
            new UiTextLine(UiText.Play.HidingDuration, "숨기는 시간", "Hiding Time"),
            new UiTextLine(UiText.Play.SearchingDuration, "찾는 시간", "Seeking Time"),
            new UiTextLine(UiText.Play.SprintSpeed, "달리는 속도", "Sprint Speed"),
            new UiTextLine(UiText.Play.StunHits, "기절 펀치 횟수", "Stun Hits"),
            new UiTextLine(UiText.Play.MapSelect, "맵 선택", "Map"),
            new UiTextLine(UiText.Play.CategorySelect, "카테고리 선택", "Category"),
            new UiTextLine(UiText.Play.Reset, "초기화", "Reset"),
            new UiTextLine(UiText.Play.Unapplied, "적용되지 않은 변경사항이 있습니다!", "You have unsaved changes!"),
            new UiTextLine(UiText.Play.GameStart, "게임 시작", "Start Game"),
            new UiTextLine(UiText.Play.Copied, "복사되었습니다!", "Copied!"),
            new UiTextLine(UiText.Play.Random, "랜덤", "Random"),
            new UiTextLine(UiText.Play.Unlimited, "무한", "Unlimited"),
            new UiTextLine(UiText.Play.Seconds, "{0}초", "{0}s"),
            new UiTextLine(UiText.Play.Minutes, "{0}분", "{0}m"),
            new UiTextLine(UiText.Play.MinutesSeconds, "{0}분 {1}초", "{0}m {1}s"),
            new UiTextLine(UiText.Play.Players, "{0}명", "{0}"),
            new UiTextLine(UiText.Play.Count, "{0}회", "{0}x"),
            new UiTextLine(UiText.Play.Multiplier, "{0}배", "{0}x"),
            new UiTextLine(UiText.Play.Invite, "방 초대\n방제목: {0}\n방코드: {1}", "Room invite\nTitle: {0}\nCode: {1}"),
            new UiTextLine(UiText.Lobby.Participants, "게임 참가자 목록", "Players in Game"),
            new UiTextLine(UiText.Lobby.Friends, "친구 목록", "Friends"),
            new UiTextLine(UiText.Lobby.Online, "게임 접속 중", "Online"),
            new UiTextLine(UiText.Lobby.Waiting, "대기중", "Waiting"),
            new UiTextLine(UiText.Lobby.InGame, "게임중", "In Game"),
            new UiTextLine(UiText.Lobby.NoParticipants, "참가자가 없습니다.", "No players yet."),
            new UiTextLine(UiText.Lobby.Kick, "강퇴", "Kick"),
            new UiTextLine(UiText.Lobby.KickAction, "강퇴하기", "Kick"),
            new UiTextLine(UiText.Lobby.KickTitle, "{0} 님을\n강퇴하시겠습니까?", "Kick {0}?"),
            new UiTextLine(UiText.Lobby.Report, "신고하기", "Report"),
            new UiTextLine(UiText.Lobby.Confirm, "확인", "OK"),
            new UiTextLine(UiText.Lobby.Category, "카테고리", "Category"),
            new UiTextLine(UiText.Lobby.PlayerCount, "참여 플레이어", "Players"),
            new UiTextLine(UiText.Lobby.Closet, "캐릭터 설정", "Character"),
            new UiTextLine(UiText.Lobby.PlayersShortcut, "플레이어", "Players"),
            new UiTextLine(UiText.Lobby.Settings, "환경설정", "Settings"),
            new UiTextLine(UiText.Lobby.HostPrompt, "방 설정", "Room Settings"),
            new UiTextLine(UiText.Lobby.GuestPrompt, "방 설정 보기", "View Room Settings"),
            new UiTextLine(UiText.Lobby.ReasonAbuse, "욕설/비하", "Abuse"),
            new UiTextLine(UiText.Lobby.ReasonCheating, "치팅", "Cheating"),
            new UiTextLine(UiText.Lobby.ReasonSpam, "도배/광고", "Spam"),
            new UiTextLine(UiText.Lobby.ReasonName, "부적절한 닉네임", "Inappropriate name"),
            new UiTextLine(UiText.Lobby.ReasonOther, "기타", "Other"),
            new UiTextLine(UiText.Lobby.ReportTitle, "{0}님을 신고하시겠습니까?", "Report {0}?"),
            new UiTextLine(UiText.Rooms.NoRooms, "열려 있는 방이 없어요", "No rooms are open"),
            new UiTextLine(UiText.Rooms.NoSearchResults, "검색 결과가 없어요", "No matching rooms"),
            new UiTextLine(UiText.Rooms.Kicked, "방장에 의해 강퇴되었습니다", "The host kicked you"),
            new UiTextLine(UiText.Rooms.Waiting, "대기중", "Waiting"),
            new UiTextLine(UiText.Rooms.Playing, "게임중", "In Game"),
            new UiTextLine(UiText.Rooms.Generic, "잠시 문제가 발생했어요. 다시 접속 시도 해주세요.", "Something went wrong. Try again."),
            new UiTextLine(UiText.Rooms.CreateFailed, "방을 만들지 못했어요. 다시 시도해 주세요.", "Could not create the room. Try again."),
            new UiTextLine(UiText.Rooms.InviteGone, "초대받은 방이 사라졌어요.", "That invite's room is gone."),
            new UiTextLine(UiText.Rooms.CodeMissing, "그런 방이 없어요. 코드를 다시 확인해 주세요.", "No room with that code. Check it and try again."),
            new UiTextLine(UiText.Rooms.ListGone, "사라진 방이에요. 목록을 새로고침 해주세요.", "That room is gone. Refresh the list."),
            new UiTextLine(UiText.Rooms.InviteExpired, "초대가 만료됐어요.", "That invite expired."),
            new UiTextLine(UiText.Rooms.Full, "방이 가득 찼어요.", "That room is full."),
            new UiTextLine(UiText.Rooms.FullPickAnother, "방이 가득 찼어요. 다른 방을 골라주세요.", "That room is full. Pick another."),
            new UiTextLine(UiText.Rooms.AlreadyStarted, "이미 게임이 시작된 방이에요.", "That game has already started."),
            new UiTextLine(UiText.Rooms.CheckSettings, "방 설정을 확인해 주세요.", "Check the room settings."),
            new UiTextLine(UiText.Rooms.CheckCode, "방 코드를 다시 확인해 주세요.", "Check the room code again."),
            new UiTextLine(UiText.Rooms.AlreadyInRoom, "이미 다른 방에 들어가 있어요.", "You are already in a room."),
            new UiTextLine(UiText.Rooms.WrongPassword, "비밀번호가 일치하지 않습니다.", "The password does not match."),
            new UiTextLine(UiText.Rooms.FullModal, "방이 가득 찼습니다.", "The room is full."),
            new UiTextLine(UiText.Rooms.ClosedModal, "입장할 수 없는 방입니다.", "You cannot join that room."),
            new UiTextLine(UiText.Rooms.NotFoundModal, "방을 찾을 수 없습니다.", "Room not found."),
            new UiTextLine(UiText.Rooms.AlreadyInModal, "이미 다른 방에 있습니다.", "You are already in a room."),
            new UiTextLine(UiText.Rooms.ConnectFailed, "연결에 실패했습니다. 잠시 후 다시 시도해 주세요.", "Could not connect. Try again shortly."),
            new UiTextLine(UiText.Rooms.JoinFailed, "입장하지 못했습니다.", "Could not join."),
            new UiTextLine(UiText.Rooms.InvalidCodeModal, "존재하지 않는 방코드입니다.", "That room code does not exist."),
            new UiTextLine(UiText.Rooms.HostRoom, "{0}의 방", "{0}'s room"),
            new UiTextLine(UiText.Rooms.Back, "← 이전", "← Back"),
            new UiTextLine(UiText.Rooms.EnterByCode, "방 코드로 입장", "Join by Code"),
            new UiTextLine(UiText.Rooms.Enter, "→ 입장", "→ Join"),
            new UiTextLine(UiText.Match.ChatPlaceholder, "[Enter]로 채팅 시작하기", "Press [Enter] to chat"),
            new UiTextLine(UiText.Match.TimerHint, "서둘러 자신의 물건을 확보하세요 !", "Secure your item now!"),
            new UiTextLine(UiText.Match.WinHeadline, "YOU WIN!", "YOU WIN!"),
            new UiTextLine(UiText.Match.LoseHeadline, "YOU LOSE..", "YOU LOSE.."),
            new UiTextLine(UiText.Match.WinSubtitle, "숨겼던 물건을 끝까지 지켜냈어요!", "You kept your item to the end!"),
            new UiTextLine(UiText.Match.LoseSubtitle, "아쉽게도 물건을 지키지 못했어요!", "You couldn't keep your item!"),
            new UiTextLine(UiText.Match.NoResult, "표시할 경기 결과가 없습니다.", "There is no match result to show."),
            new UiTextLine(
                UiText.Match.ResultMissing,
                "경기 결과 데이터를 받지 못했습니다.\n\n로비로 돌아갑니다.",
                "The match result did not arrive.\n\nReturning to the lobby."),
            new UiTextLine(UiText.Match.ResultHeading, "게임 결과", "Match Result"),
            new UiTextLine(UiText.Match.Winners, "승자: {0}", "Winner: {0}"),
            new UiTextLine(UiText.Match.EndReason, "종료 사유: {0}", "Ended: {0}"),
            new UiTextLine(UiText.Match.NoWinner, "승자 없음", "No winner"),
            new UiTextLine(UiText.Match.MatchOver, "경기 종료", "Match Over"),
            new UiTextLine(UiText.Match.Victory, "승리", "Victory"),
            new UiTextLine(UiText.Match.Defeat, "패배", "Defeat"),
            new UiTextLine(UiText.Match.None, "없음", "None"),
            new UiTextLine(UiText.Match.PlayerN, "플레이어 {0}", "Player {0}"),
            new UiTextLine(UiText.Match.ReturnLobby, "로비로 돌아갑니다.", "Returning to the lobby."),
            new UiTextLine(UiText.Match.TimeExpired, "제한 시간 종료", "Time expired"),
            new UiTextLine(UiText.Match.AllItemsDestroyed, "모든 플레이어 물건 파괴", "Every player's item was destroyed"),
            new UiTextLine(UiText.Match.LastStanding, "마지막 플레이어 생존", "Last player standing"),
            new UiTextLine(UiText.Match.PhaseWaiting, "대기 중", "Waiting"),
            new UiTextLine(UiText.Match.PhaseHighlight, "하이라이트", "Highlight"),
            new UiTextLine(UiText.Match.PhaseResult, "결과", "Results"),
            new UiTextLine(UiText.Match.PhaseHiding, "숨기는 중", "Hiding"),
            new UiTextLine(UiText.Match.PhaseHidingNamed, "{0}{1} 숨기는 중", "{0} is hiding"),
            new UiTextLine(UiText.Match.HighlightTitle, "HIGHLIGHT", "HIGHLIGHT"),
            new UiTextLine(UiText.Match.HighlightSkip, "건너뛰기", "Skip"),
            new UiTextLine(UiText.Match.HighlightSkipAll, "전체 건너뛰기", "Skip All"),
            new UiTextLine(UiText.Match.HideHint, "제한 시간 안에 물건을 숨겨주세요!", "Hide your item before time runs out!"),
            new UiTextLine(
                UiText.Match.HideWarning,
                "시간 초과 시 마지막 위치에 물건이 배치됩니다",
                "Time out places the item at the last position"),
            new UiTextLine(UiText.Match.HideComplete, "숨기기 완료", "Done Hiding"),
            new UiTextLine(UiText.Match.HideBanner, "제한 시간 안에 물건을 숨겨주세요!", "Hide your item before time runs out!"),
            new UiTextLine(UiText.Match.HideFinalBanner, "서둘러 자신의 물건을 확보하세요!", "Secure your item now!"),
            new UiTextLine(UiText.Match.HideNextTurn, "다음 숨길 차례입니다", "You're hiding next"),
            new UiTextLine(UiText.Match.HideStatus, "물건을 숨기는 중", "Hiding an item"),
            new UiTextLine(UiText.Match.HideStatusNamed, "{0}님이 물건을 숨기는 중", "{0} is hiding an item"),
            new UiTextLine(
                UiText.Match.IntroHint,
                "다른 도둑들에게 빼앗기지 않도록 비밀 장소에 잘 챙겨두세요.",
                "Hide it somewhere secret so the other thieves cannot take it."),
            new UiTextLine(UiText.Match.StolenItem, "당신이 훔친 물건은 {0}입니다.", "The item you stole is {0}."),
            new UiTextLine(UiText.Match.SearchingTitle, "숨기기 시간이 끝났습니다.", "Hiding time is over."),
            new UiTextLine(
                UiText.Match.SearchingBody,
                "이제부터 서로의 물건을 노리는 진짜 탐색전이 시작됩니다.",
                "Now the real search begins — go after each other's items."),
            new UiTextLine(
                UiText.Match.SearchingHint,
                "마지막 순간에 {0}{1} 꼭 손에 쥐고 계세요!",
                "Keep the {0} in your hands at the last moment!"),
            new UiTextLine(UiText.Match.ItemFallback, "물건", "item"),
            new UiTextLine(UiText.Match.Shredder, "파쇄기", "Shredder"),
            new UiTextLine(UiText.Match.ShredderUnlimited, "파쇄기 (무한)", "Shredder (Unlimited)"),
            new UiTextLine(UiText.Match.ShredderUses, "파쇄기 ({0}/{1})", "Shredder ({0}/{1})"),
            new UiTextLine(UiText.Match.Destroyed, "{0}님이 물건을 파괴했습니다!", "{0} destroyed an item!"),
            new UiTextLine(UiText.Closet.Back, "← 이전", "← Back"),
            new UiTextLine(UiText.Closet.Reset, "초기화", "Reset"),
            new UiTextLine(UiText.Closet.Apply, "적용", "Apply"),
            new UiTextLine(UiText.Closet.SaveError, "외형을 저장하지 못했습니다", "Could not save the look"),
            new UiTextLine(UiText.Closet.ResetTitle, "초기화하시겠습니까?", "Reset your changes?"),
            new UiTextLine(UiText.Closet.DiscardTitle, "적용하지 않고 나가시겠습니까?", "Leave without applying?"),
            new UiTextLine(UiText.Closet.Subtitle, "지금까지의 변경 내용은 모두 사라집니다.", "Every change so far will be lost."),
            new UiTextLine(UiText.Closet.Decline, "아니오", "No"),
            new UiTextLine(UiText.Closet.Accept, "예", "Yes"),
            new UiTextLine(UiText.Closet.PortraitHint, "좌우 드래그로 회전 · 두 번 클릭해 정면", "Drag to turn · Double-click to face forward"),
            new UiTextLine(UiText.Closet.Hood, "후드", "Hood"),
            new UiTextLine(UiText.Closet.HoodShape, "후드 모양", "Hood Shape"),
            new UiTextLine(UiText.Closet.HoodColor, "후드 색상", "Hood Color"),
            new UiTextLine(UiText.Closet.Body, "몸 색상", "Body"),
            new UiTextLine(UiText.Closet.Shoes, "신발", "Shoes"),
            new UiTextLine(UiText.Closet.Face, "표정", "Face"),
            new UiTextLine(UiText.Guide.Click, "클릭", "Click"),
            new UiTextLine(UiText.Guide.RightClick, "우클릭", "Right-click"),
            new UiTextLine(UiText.Guide.Scroll, "스크롤", "Scroll"),
            new UiTextLine(UiText.Guide.Toggle, "키 가이드 on/off", "Key guide on/off"),
            new UiTextLine(UiText.Guide.Attack, "공격", "Attack"),
            new UiTextLine(UiText.Guide.Crouch, "앉기", "Crouch"),
            new UiTextLine(UiText.Guide.Prone, "엎드리기", "Prone"),
            new UiTextLine(UiText.Guide.ToggleView, "시점 변경", "Change View"),
            new UiTextLine(UiText.Guide.Sprint, "달리기", "Sprint"),
            new UiTextLine(UiText.Guide.Jump, "점프", "Jump"),
            new UiTextLine(UiText.Guide.Placement, "배치 모드", "Placement"),
            new UiTextLine(UiText.Guide.Throw, "던지기", "Throw"),
            new UiTextLine(UiText.Guide.Drop, "놓기", "Drop"),
            new UiTextLine(UiText.Guide.Place, "배치하기", "Place"),
            new UiTextLine(UiText.Guide.Rotate, "회전", "Rotate"),
            new UiTextLine(UiText.Guide.Twist, "좌우 회전", "Twist"),
            new UiTextLine(UiText.Guide.InteractPickup, "상호작용 · 들기", "Interact · Pick up"),
            new UiTextLine(UiText.Interact.PickUp, "물건 잡기", "Pick up"),
            new UiTextLine(UiText.Interact.Destroy, "파괴하기", "Destroy"),
            new UiTextLine(UiText.Interact.Place, "배치", "Place"),
            new UiTextLine(UiText.Tutorial.ExitTitle, "튜토리얼을 종료하겠습니까?", "Leave the tutorial?"),
            new UiTextLine(UiText.Tutorial.ExitDoor, "튜토리얼 종료", "End Tutorial"),
            new UiTextLine(UiText.Tutorial.ChecklistTitle, "도둑의 기본 훈련", "Thief Basics"),
            new UiTextLine(UiText.Tutorial.StepMove, "이동 · 주변 살피기", "Move · Look around"),
            new UiTextLine(UiText.Tutorial.StepSprint, "달리기", "Sprint"),
            new UiTextLine(UiText.Tutorial.StepJump, "점프", "Jump"),
            new UiTextLine(UiText.Tutorial.StepCrouch, "앉아서 통과", "Crouch through"),
            new UiTextLine(UiText.Tutorial.StepProne, "기어서 통과", "Crawl through"),
            new UiTextLine(UiText.Tutorial.StepPickUp, "물건 들기", "Pick up"),
            new UiTextLine(UiText.Tutorial.StepDrop, "지정 구역에 내려놓기", "Set it down"),
            new UiTextLine(UiText.Tutorial.StepThrow, "표적에 던지기", "Throw at the target"),
            new UiTextLine(UiText.Tutorial.StepPlace, "책상에 배치하기", "Place on the desk"),
            new UiTextLine(UiText.Tutorial.StepShredder, "파쇄기 사용", "Use the shredder"),
            new UiTextLine(UiText.Tutorial.StepExit, "출구 문 열기", "Open the exit"),
            new UiTextLine(
                UiText.Tutorial.Intro,
                "신입, 들리나. 여긴 우리 아지트의 훈련 구역이다. 지금부터 내 지시에 따라 움직여.",
                "You there, rookie. This is our hideout's training ground. Follow my lead."),
            new UiTextLine(
                UiText.Tutorial.InstructMove,
                "주변을 살피면서 앞으로 이동해. 좋은 도둑은 발보다 눈이 먼저 움직이는 법이지.",
                "Move forward while you look around. A good thief's eyes move first."),
            new UiTextLine(
                UiText.Tutorial.InstructSprint,
                "일이 틀어지면 망설일 시간이 없다. 건너편까지 전력으로 달려.",
                "When a job goes wrong there is no time to hesitate. Sprint to the other side."),
            new UiTextLine(
                UiText.Tutorial.InstructJump,
                "앞이 끊겨 있군. 달려가서 뛰어넘어. 떨어지면 다시 올라오게 해주지. 한 번만.",
                "The path breaks ahead. Sprint and jump it. Fall and I'll put you back up. Once."),
            new UiTextLine(
                UiText.Tutorial.InstructCrouch,
                "C를 눌러 앉은 채 통로 끝까지 지나가. 시야가 답답하면 V로 1인칭과 3인칭을 바꿀 수 있다.",
                "Hold C and crouch to the end of the passage. Press V if you need first- or third-person."),
            new UiTextLine(
                UiText.Tutorial.InstructProne,
                "이번엔 Z를 눌러 엎드려. 낮은 통로를 끝까지 기어서 지나가면 된다.",
                "This time press Z and go prone. Crawl the low passage all the way through."),
            new UiTextLine(
                UiText.Tutorial.InstructPickUp,
                "상자 하나가 보일 거다. 가까이 가서 들어 올려. 오늘부터 네가 지켜야 할 물건이다.",
                "You'll see a crate. Walk up and pick it up. From today, that item is yours to keep."),
            new UiTextLine(
                UiText.Tutorial.InstructDrop,
                "표시된 구역까지 운반한 다음 바닥에 내려놔. 던지지 말고 얌전히.",
                "Carry it to the marked spot and set it down. Don't throw it."),
            new UiTextLine(
                UiText.Tutorial.InstructThrow,
                "이번엔 표적을 봐. 힘을 조절해서 상자를 던져.",
                "Now look at the target. Gauge it and throw the crate."),
            new UiTextLine(
                UiText.Tutorial.InstructPlace,
                "배치 모드를 사용해 봐. Q/E와 우클릭으로 방향을 조정하고, 파란 목표 근처에 클릭해서 놓아. 모양이 똑같을 필요는 없다.",
                "Try placement mode. Use Q/E and right-click to turn it, then click near the blue mark. It does not have to match exactly."),
            new UiTextLine(
                UiText.Tutorial.InstructShredder,
                "마지막 처리다. 상자를 들고 파쇄기에 넣어. 증거를 남기지 마.",
                "Last job. Carry the crate to the shredder. Leave no evidence."),
            new UiTextLine(
                UiText.Tutorial.InstructComplete,
                "훈련은 끝났다. 앞의 문을 직접 열어. 밖으로 나가면 실전이다.",
                "Training's over. Open the door yourself. Outside is the real job."),
            new UiTextLine(UiText.Tutorial.CompleteMove, "좋아. 적어도 벽을 보고 걷지는 않겠군.", "Good. At least you won't walk into a wall."),
            new UiTextLine(UiText.Tutorial.CompleteSprint, "그 정도면 경비원 하나쯤은 따돌리겠어.", "That should shake one guard."),
            new UiTextLine(UiText.Tutorial.CompleteJump, "착지는 거칠지만 넘어오긴 했군.", "Rough landing, but you made it."),
            new UiTextLine(UiText.Tutorial.CompleteCrouch, "조용히 움직이는 법을 조금은 아는군.", "You know a little about moving quietly."),
            new UiTextLine(UiText.Tutorial.CompleteProne, "좋아. 체면보다 임무가 먼저라는 건 이해했군.", "Good. The job comes before pride."),
            new UiTextLine(UiText.Tutorial.CompletePickUp, "단단히 잡아. 우리 물건은 잃어버리는 순간 남의 물건이 된다.", "Hold it tight. Lose our item and it belongs to someone else."),
            new UiTextLine(UiText.Tutorial.CompleteDrop, "좋아. 내려놓는 것과 떨어뜨리는 것의 차이는 아는군.", "Good. You know the difference between setting it down and dropping it."),
            new UiTextLine(UiText.Tutorial.CompleteThrow, "정확하군. 필요할 때는 물건도 훌륭한 도구가 된다.", "Accurate. An item can be a tool when you need one."),
            new UiTextLine(UiText.Tutorial.CompletePlace, "좋아. 정리할 줄 아는 도둑은 오래 살아남지.", "Good. A tidy thief lasts longer."),
            new UiTextLine(UiText.Tutorial.CompleteShredder, "깨끗하군. 이제 저 물건이 있었다는 걸 아는 사람은 우리뿐이다.", "Clean. Now only we know that item was ever here."),
            new UiTextLine(UiText.Tutorial.RetryJump, "아래에 숨을 생각은 아니었겠지. 다시 뛰어.", "You weren't planning to hide down there. Jump again."),
            new UiTextLine(UiText.Tutorial.RetryGeneric, "집중해. 같은 실수는 두 번이면 습관이다.", "Focus. Twice is a habit."),
            new UiTextLine(UiText.Tutorial.HintPickUp, "먼저 물건 들기 · {0}", "Pick it up first · {0}"),
            new UiTextLine(UiText.Tutorial.HintPlacement, "배치 모드 · {0}", "Placement · {0}"),
            new UiTextLine(UiText.Tutorial.HintPlace, "회전 Q/E · 스크롤 / 클릭 배치", "Turn Q/E · Scroll / click to place"),
            new UiTextLine(UiText.Tutorial.FocusPickUp, "물건 들기", "Pick up"),
            new UiTextLine(UiText.Tutorial.FocusPlacement, "배치 모드", "Placement"),
            new UiTextLine(UiText.Tutorial.FocusMove, "이동 · 마우스로 주변 살피기", "Move · Look around with the mouse"),
            new UiTextLine(UiText.Tutorial.FocusSprint, "이동하며 달리기", "Sprint as you move"),
            new UiTextLine(UiText.Tutorial.FocusJump, "달려서 구덩이 뛰어넘기", "Sprint and jump the gap"),
            new UiTextLine(UiText.Tutorial.FocusCrouch, "1인칭으로 앉아서 통로 끝까지", "Crouch in first person to the end"),
            new UiTextLine(UiText.Tutorial.FocusProne, "Z로 기어서 통로 끝까지", "Crawl with Z to the end"),
            new UiTextLine(UiText.Tutorial.FocusLookPickUp, "상자를 바라보고 들기", "Look at the crate and pick it up"),
            new UiTextLine(UiText.Tutorial.FocusDrop, "상자 내려놓기", "Set the crate down"),
            new UiTextLine(UiText.Tutorial.FocusThrow, "상자 던지기", "Throw the crate"),
            new UiTextLine(UiText.Tutorial.FocusPlace, "배치 모드 · 회전 · 배치", "Placement · Rotate · Place"),
            new UiTextLine(UiText.Tutorial.FocusShredder, "상자를 들고 파쇄기 사용", "Carry the crate to the shredder"),
            new UiTextLine(UiText.Tutorial.FocusExit, "출구 문을 바라보고 열기", "Look at the exit and open it"));

        public UiTextCatalog(params UiTextLine[] lines)
        {
            this.lines = new Dictionary<string, UiTextLine>(StringComparer.Ordinal);
            if (lines == null)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Key))
                {
                    throw new ArgumentException("A catalogue cannot hold a line without a key.", nameof(lines));
                }

                if (this.lines.ContainsKey(line.Key))
                {
                    throw new ArgumentException(
                        $"'{line.Key}' is already in the catalogue.", nameof(lines));
                }

                this.lines[line.Key] = line;
            }
        }

        /// <summary>
        /// The words for <paramref name="key"/> in
        /// <paramref name="languageCode"/>. An unknown language, or a
        /// language this line has no words for, uses Korean. A key the
        /// catalogue does not list comes back as itself.
        /// </summary>
        public string Get(string key, string languageCode)
        {
            if (key == null)
            {
                return string.Empty;
            }

            if (!lines.TryGetValue(key, out var line))
            {
                return key;
            }

            return line.TryGet(languageCode, out var text) ? text : line.Korean;
        }
    }
}
