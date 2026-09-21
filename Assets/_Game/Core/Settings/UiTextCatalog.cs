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
            new UiTextLine(UiText.Settings.InvertY, "Y축 반전", "Invert Y"));

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
