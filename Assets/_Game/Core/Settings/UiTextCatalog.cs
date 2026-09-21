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
            new UiTextLine(UiText.Settings.Reset, "변경 취소", "Discard Changes"),
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
            new UiTextLine(UiText.Home.RoomScope, "방 범위", "Room Scope"),
            new UiTextLine(UiText.Home.RoomTitle, "방 이름", "Room Name"),
            new UiTextLine(UiText.Home.RoomPlayers, "인원", "Players"),
            new UiTextLine(UiText.Home.RoomSubmit, "방 생성하기", "Create Room"),
            new UiTextLine(UiText.Home.RoomTitlePlaceholder, "방 이름 입력", "Enter a room name"),
            new UiTextLine(UiText.Home.ServerTitle, "서버 설정", "Server Settings"),
            new UiTextLine(UiText.Home.SuspendedTitle, "이용이 제한된 계정입니다", "This account is suspended"),
            new UiTextLine(UiText.Home.SuspendedBody, "운영자가 이 계정의 이용을 중지했습니다.", "An operator has stopped this account."));

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
