using UnityEngine;

namespace Game.Client.Settings
{
    /// <summary>
    /// Every colour and measurement the settings screen draws with, taken from
    /// the mock-up.
    /// </summary>
    /// <remarks>
    /// The same arrangement Home and the closet use: the screen is built in
    /// code, so this is the only record of the design, and a revised mock-up
    /// is one file to edit.
    /// <para>
    /// Measurements are pixels at the 1920x1080 the mock-up was drawn at, which
    /// is also the canvas reference resolution. Anything inside the panel is
    /// measured from the panel's own top-left corner, so moving the panel moves
    /// everything on it.
    /// </para>
    /// </remarks>
    public static class SettingsStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Same files the play-settings picker uses. The Settings scene wires
        /// these on the component; a view built in code, as the lobby overlay
        /// is, has to load them itself.
        /// </summary>
        public const string ArrowLeftIconResource = "UI/Icon_Left";
        public const string ArrowRightIconResource = "UI/Icon_Right";
        public const string CloseIconResource = "UI/Icon_Close";

        /// <summary>
        /// The X on a confirmation. The Settings / Closet scenes assign it in
        /// the inspector; a view built in code, as the lobby overlays are,
        /// loads the Resources copy.
        /// </summary>
        public static Sprite LoadCloseIcon(Sprite assigned = null)
        {
            if (assigned != null)
            {
                return assigned;
            }

            var loaded = Resources.Load<Sprite>(CloseIconResource);
#if UNITY_EDITOR
            if (loaded == null)
            {
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Game/Content/UI/Common/Icon_Close.png");
            }
#endif
            return loaded;
        }

        public static class Palette
        {
            public static readonly Color TextPrimary = FromHex(0xF5F3F1);
            public static readonly Color TextMuted = FromHex(0xA8ADB3);
            public static readonly Color Accent = FromHex(0xFF7032);

            /// <summary>Shown only while the background art is missing.</summary>
            public static readonly Color BackgroundFallback = FromHex(0x0B1018);

            public static readonly Color PanelFill = FromHex(0x231818);
            /// <summary>
            /// The accent, held well back. At full strength the halo competes
            /// with the panel it is meant to lift off the picture.
            /// </summary>
            public static readonly Color Glow = FromHex(0xFF7032, 0.45f);

            public static readonly Color BackLabel = FromHex(0xFFFDFC);
            public static readonly Color ResetAllLabel = FromHex(0xF5F3F1);

            /// <summary>
            /// Not given by the design. The lift a pointer gives the two text
            /// buttons at the top, so they answer a hover the way the menu on
            /// Home does.
            /// </summary>
            public static readonly Color TextHover = FromHex(0xFF9A6A);

            public static readonly Color TabSelectedFill = FromHex(0xF5F3F1, 0.16f);

            /// <summary>
            /// An unpicked tab and a row nobody is pointing at draw no plate at
            /// all.
            /// </summary>
            public static readonly Color IdleFill = FromHex(0xF5F3F1, 0f);

            /// <summary>
            /// The plate that appears under the pointer, on a tab and on a
            /// content row alike. Not given by the design, which draws this
            /// state and the selected tab as one grey; three quarters of the
            /// selected fill, so a hovered tab is not mistaken for the picked
            /// one — what really keeps those two apart is the lettering.
            /// </summary>
            public static readonly Color HoverFill = FromHex(0xF5F3F1, 0.12f);

            public static readonly Color TabSelectedLabel = FromHex(0xF5F3F1);

            /// <summary>
            /// Not given by the design: the muted grey the rest of the project
            /// uses, which sits between the idle tab and the picked one.
            /// </summary>
            public static readonly Color TabHoverLabel = FromHex(0xA8ADB3);
            public static readonly Color TabIdleLabel = FromHex(0x5E6670);
            public static readonly Color Divider = FromHex(0xF5F3F1);

            /// <summary>
            /// The handle down the panel's right edge. Not given by the design,
            /// which draws a pale line; the same white the divider uses, held
            /// back so it reads as furniture rather than as content.
            /// </summary>
            public static readonly Color ScrollHandle = FromHex(0xF5F3F1, 0.5f);

            public static readonly Color RowLabel = FromHex(0xF5F3F1);
            public static readonly Color Value = FromHex(0xF5F3F1);
            public static readonly Color ArrowEnabled = FromHex(0xF5F3F1);

            /// <summary>
            /// An arrow with nowhere to go: the same white, held back rather
            /// than swapped for a grey. A grey dark enough to read as
            /// unavailable disappears into the panel, and disappears entirely
            /// once the row lights up under the pointer — so the arrow keeps
            /// its colour and loses only its weight.
            /// </summary>
            public static readonly Color ArrowDisabled = FromHex(0xF5F3F1, 0.45f);

            public static readonly Color SectionLabel = FromHex(0xF5F3F1);

            /// <summary>
            /// A key plate: outlined rather than filled, because twenty-one
            /// filled plates down one page read as a wall. It fills in only
            /// while it waits for a press.
            /// </summary>
            public static readonly Color KeyIdleFill = FromHex(0xF5F3F1, 0.06f);

            public static readonly Color KeyHoverFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color KeyStroke = FromHex(0xF5F3F1, 0.35f);
            public static readonly Color KeyLabel = FromHex(0xF5F3F1);
            public static readonly Color KeyListeningFill = FromHex(0xFF7032);
            public static readonly Color KeyListeningStroke = FromHex(0xFF7032);
            public static readonly Color KeyListeningLabel = FromHex(0xF5F3F1);
            public static readonly Color SliderFill = FromHex(0xFF7032);
            public static readonly Color SliderTrack = FromHex(0xD9D9D9);
            public static readonly Color SliderHandle = FromHex(0xFF7032);

            /// <summary>
            /// 마이크 테스트 at rest wears the feedback button's clothes; running,
            /// it takes the accent so a test left going is not missed.
            /// </summary>
            public static readonly Color TestIdleFill = FromHex(0xF5F3F1);

            public static readonly Color TestIdleHoverFill = FromHex(0xFFFFFF);
            public static readonly Color TestIdleLabel = FromHex(0x231818);
            public static readonly Color TestRunningFill = FromHex(0xFF7032);

            /// <summary>Not given by the design: the accent, lightened.</summary>
            public static readonly Color TestRunningHoverFill = FromHex(0xFF8A52);

            public static readonly Color TestRunningLabel = FromHex(0xF5F3F1);

            /// <summary>The box written in, and what is written in it.</summary>
            public static readonly Color FieldFill = FromHex(0xF5F3F1, 0.16f);

            public static readonly Color FieldText = FromHex(0xF5F3F1);
            public static readonly Color FieldPlaceholder = FromHex(0xA8ADB3);

            /// <summary>
            /// How much has been typed. The muted grey the project uses for
            /// figures that are there to be glanced at.
            /// </summary>
            public static readonly Color Counter = FromHex(0xA8ADB3);

            public static readonly Color FeedbackFill = FromHex(0xF5F3F1);

            /// <summary>Not given by the design: the fill, brightened.</summary>
            public static readonly Color FeedbackHoverFill = FromHex(0xFFFFFF);

            public static readonly Color FeedbackLabel = FromHex(0x231818);

            public static readonly Color ButtonOffFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color ButtonOffLabel = FromHex(0xA8ADB3);
            public static readonly Color ResetOnFill = FromHex(0xF5F3F1);
            public static readonly Color ResetOnLabel = FromHex(0x0B1018);
            public static readonly Color ApplyOnFill = FromHex(0xFF7032);
            public static readonly Color ApplyOnLabel = FromHex(0xF5F3F1);

            /// <summary>Lobby-only 게임 나가기, left of the gradient.</summary>
            public static readonly Color LeaveGameStart = FromHex(0xFF9A6A);

            /// <summary>Lobby-only 게임 나가기, right of the gradient.</summary>
            public static readonly Color LeaveGameEnd = FromHex(0xFF7032);
        }

        /// <summary>The panel everything sits on, and the glow around it.</summary>
        public static class Frame
        {
            /// <summary>Top-left corner, measured from the top-left of the screen.</summary>
            public static readonly Vector2 Position = new Vector2(160f, -144f);

            public static readonly Vector2 Size = new Vector2(1600f, 876f);
            public const int Radius = 30;

            /// <summary>
            /// The orange around the panel, as the design tool describes it:
            /// pushed out by the spread and softened over the blur.
            /// </summary>
            /// <remarks>
            /// Tighter than the design's spread of 6 over a blur of 30, which
            /// on screen reads as a band of orange rather than a glow. The
            /// spread is the part drawn at full strength, so it is what to
            /// lower first; the blur only decides how far the fade trails off.
            /// </remarks>
            public const int GlowSpread = 1;

            public const int GlowBlur = 22;
        }

        /// <summary>The same arrow the room browser draws, in the same place.</summary>
        public static class Back
        {
            public static readonly Vector2 Position = new Vector2(64f, -62f);
            public static readonly Vector2 Size = new Vector2(140f, 44f);
            public static readonly Vector2 LeaveSize = new Vector2(220f, 44f);
            public const float FontSize = 30f;
            public const string Label = "← 이전";
        }

        /// <summary>The circling arrow and its words at the top right.</summary>
        public static class ResetAll
        {
            public const float RightMargin = 160f;

            /// <summary>
            /// Down from the top of the screen to the middle of the line. Level
            /// with the arrow opposite it rather than the 60 the design gives,
            /// which leaves the two sitting on different lines.
            /// </summary>
            public static float CentreY => -Back.Position.y + (Back.Size.y * 0.5f);

            public const float Height = 44f;
            public const float FontSize = 30f;
            public const float IconSize = 24f;
            public const float IconGap = 14f;
            public const string Label = "전체 변경 취소";
        }

        public static class Tabs
        {
            /// <summary>Top-left corner of the first tab, from the panel's top-left.</summary>
            public static readonly Vector2 Origin = new Vector2(30f, -105f);

            public static readonly Vector2 Size = new Vector2(280f, 80f);
            public const float Gap = 25f;
            public const float Pitch = 80f + Gap;
            public const float FontSize = 30f;
            public const float LabelLeft = 22f;

            /// <summary>
            /// Rounded down the left side only. The right edge is square,
            /// where it faces the divider and the rows.
            /// </summary>
            public const int Radius = 28;
        }

        /// <summary>The hairline between the tabs and their contents.</summary>
        public static class Divider
        {
            public const float X = 330f;
            public const float Top = 90f;
            public const float Length = 625f;
            public const float Thickness = 1f;
        }

        public static class Rows
        {
            /// <summary>Top-left corner of the first row, from the panel's top-left.</summary>
            public static readonly Vector2 Origin = new Vector2(350f, -105f);

            public static readonly Vector2 Size = new Vector2(1208f, 80f);
            public const float Gap = 25f;
            public const float Pitch = 80f + Gap;

            /// <summary>
            /// The tabs' corners, mirrored: rounded down the right side, square
            /// on the left where the row faces the divider.
            /// </summary>
            public const int Radius = 28;

            public const float LabelLeft = 20f;
            public const float LabelFontSize = 28f;

            /// <summary>From a row's right edge to whatever control sits in it.</summary>
            public const float RightMargin = 40f;

            /// <summary>
            /// The window the rows are seen through. As wide as a row and as
            /// tall as the divider beside it reaches, which comes to six rows:
            /// a tab with more than that scrolls.
            /// </summary>
            public static readonly Vector2 ViewportSize = new Vector2(
                Size.x, Divider.Top + Divider.Length + Origin.y);

            /// <summary>
            /// How tall a page of <paramref name="rows"/> stands. The gap falls
            /// between rows rather than after the last one, so a page is
            /// exactly its rows.
            /// </summary>
            public static float PageHeight(int rows) =>
                rows <= 0 ? 0f : (rows * Size.y) + ((rows - 1) * Gap);
        }

        /// <summary>
        /// A heading over a group of rows, as the 사운드 tab draws 스피커 and
        /// 마이크. Shorter than a row, with no plate and no hover.
        /// </summary>
        public static class Section
        {
            public const float Height = 60f;
            public const float FontSize = 36f;

            /// <summary>
            /// Above a heading that follows rows, and below every heading. The
            /// first heading on a page has nothing above it and starts flush.
            /// </summary>
            public const float Gap = 25f;

            /// <summary>
            /// Rows under a heading are indented past it, where the other tabs'
            /// rows start at <see cref="Rows.LabelLeft"/>.
            /// </summary>
            public const float RowLabelLeft = 60f;

            /// <summary>
            /// Where a heading starts, measured from the line between the tabs
            /// and the rows rather than from the rows' own left edge — that is
            /// the edge the design measures it against.
            /// </summary>
            public const float LabelFromDivider = 60f;

            /// <summary>
            /// The same place in the frame a row is laid out in. The window the
            /// rows scroll inside begins a little right of the line, so a
            /// heading sits that much less far into the window than it does
            /// from the line.
            /// </summary>
            public static float LabelLeft =>
                LabelFromDivider - (Rows.Origin.x - Divider.X);
        }

        /// <summary>A volume: the track, its handle, and the figure beside it.</summary>
        public static class Slider
        {
            public static readonly Vector2 TrackSize = new Vector2(292f, 7f);
            public const float FillHeight = 9f;
            public const float FillLeftOverhang = 2f;
            public const int FillRadius = 4;

            /// <summary>
            /// Keep the generated nine-slice border (radius + 1) close to half
            /// the seven-pixel track height. A larger radius is compressed only
            /// vertically by Image.Sliced, leaving long, flattened ends.
            /// </summary>
            public const int TrackRadius = 3;

            public const float HandleDiameter = 15f;
            public const float PercentFontSize = 24f;

            /// <summary>From the track's right end to the figure's left.</summary>
            public const float PercentGap = 13f;

            /// <summary>
            /// Not given by the design. Fixed rather than fitted to the digits,
            /// and wide enough for "100%", so the track does not creep as the
            /// figure changes width.
            /// </summary>
            public const float PercentWidth = 70f;

            /// <summary>
            /// Not given by the design. The strip that takes the pointer; the
            /// 7 point track alone cannot be grabbed.
            /// </summary>
            public const float HitHeight = 40f;

            public const string PercentFormat = "{0}%";
        }

        /// <summary>The words on the 마이크 테스트 button. Its shape is <see cref="FeedbackRow"/>'s.</summary>
        public static class MicrophoneTest
        {
            public const string IdleLabel = "테스트 해보기";
            public const string RunningLabel = "테스트 중...";
        }

        /// <summary>
        /// The plate showing which key an action is on, which is also the
        /// button that changes it. Shaped like the feedback button, drawn as an
        /// outline.
        /// </summary>
        public static class KeyButton
        {
            public static readonly Vector2 Size = new Vector2(200f, 60f);
            public const int Radius = 20;
            public const float StrokeThickness = 1.5f;
            public const float FontSize = 28f;

            /// <summary>
            /// A key's name is not always a letter — 좌클릭, BACKSPACE — so it
            /// shrinks to this before it is cut short.
            /// </summary>
            public const float MinFontSize = 18f;

            public const float TextPadding = 12f;
        }

        /// <summary>The 알림 tab's row names.</summary>
        public static class Notifications
        {
            /// <inheritdoc cref="GraphicsRowLabel"/>
            public static string RowLabel(Core.Settings.NotificationOption option)
            {
                switch (option)
                {
                    case Core.Settings.NotificationOption.GameInvite:
                        return "게임 초대 알림";
                    default:
                        return option.ToString();
                }
            }
        }

        /// <summary>The 컨트롤 tab's headings and row names.</summary>
        public static class Controls
        {
            public const string MicrophoneHeading = "마이크";

            /// <summary>
            /// The keyboard's two headings. One list of twenty rows read as a
            /// wall, and 이동 / 행동 is where it divides cleanly: the first is
            /// everything that changes the player's own position, speed,
            /// posture or point of view, and the second is everything that does
            /// something to the world.
            /// </summary>
            /// <remarks>
            /// 물건 rather than 행동 would leave 공격/던지기/배치 and 시점 변경
            /// homeless: the first is not about a thing the player is holding
            /// and the second is not about a thing at all.
            /// </remarks>
            public const string KeyboardMoveHeading = "키보드(이동)";

            public const string KeyboardActionHeading = "키보드(행동)";

            public const string FirstPersonHeading = "1인칭";
            public const string ThirdPersonHeading = "3인칭";

            public static string ActionLabel(Core.Settings.ControlAction action)
            {
                switch (action)
                {
                    case Core.Settings.ControlAction.MicrophoneTalk:
                        return "마이크 송출";
                    case Core.Settings.ControlAction.VoiceToggle:
                        return "마이크 고정";
                    case Core.Settings.ControlAction.ToggleSpeaker:
                        return "음성 듣기";
                    case Core.Settings.ControlAction.MoveForward:
                        return "앞으로 이동";
                    case Core.Settings.ControlAction.MoveLeft:
                        return "왼쪽으로 이동";
                    case Core.Settings.ControlAction.MoveBackward:
                        return "뒤로 이동";
                    case Core.Settings.ControlAction.MoveRight:
                        return "오른쪽으로 이동";
                    case Core.Settings.ControlAction.PrimaryAction:
                        return "공격/던지기/배치";
                    case Core.Settings.ControlAction.Interact:
                        return "물건 상호작용";
                    case Core.Settings.ControlAction.PlacementMode:
                        return "배치모드 활성화";
                    case Core.Settings.ControlAction.RotateLeft:
                        return "가로축 회전(좌방향)";
                    case Core.Settings.ControlAction.RotateRight:
                        return "가로축 회전(우방향)";
                    case Core.Settings.ControlAction.RaiseObject:
                        return "세로축 회전(상향)";
                    case Core.Settings.ControlAction.LowerObject:
                        return "세로축 회전(하향)";
                    case Core.Settings.ControlAction.Jump:
                        return "점프";
                    case Core.Settings.ControlAction.Sprint:
                        return "달리기";
                    case Core.Settings.ControlAction.ToggleView:
                        return "시점 변경(1인칭/3인칭)";
                    case Core.Settings.ControlAction.Crouch:
                        return "앉기";
                    case Core.Settings.ControlAction.Prone:
                        return "엎드리기";
                    case Core.Settings.ControlAction.ToggleKeyGuide:
                        return "키 가이드 on/off";
                    default:
                        return action.ToString();
                }
            }

            public static string SensitivityLabel(Core.Settings.ControlSensitivity sensitivity) =>
                sensitivity == Core.Settings.ControlSensitivity.ThirdPersonCamera
                    ? "카메라 감도"
                    : "마우스 감도";

            /// <summary>
            /// Said when a key could not be moved because something else has
            /// it. Names what has it, so the player knows what to move first.
            /// </summary>
            public const string InUseTitle = "사용 중인 키";

            public static string InUseMessage(string keyLabel, string action) =>
                $"{keyLabel} 키는 이미 {action}에 사용 중입니다";

            public static string ReversalLabel(Core.Settings.ControlToggle toggle) =>
                toggle == Core.Settings.ControlToggle.FirstPersonInvertX
                || toggle == Core.Settings.ControlToggle.ThirdPersonInvertX
                    ? "X축 반전"
                    : "Y축 반전";
        }

        /// <summary>The 사운드 tab's headings and row names.</summary>
        public static class Sound
        {
            public const string SpeakerHeading = "스피커";
            public const string MicrophoneHeading = "마이크";
            public const string DeviceLabel = "마이크 장치";
            public const string InputModeLabel = "입력 모드";
            public const string TestLabel = "마이크 테스트";

            public static string VolumeLabel(Core.Settings.SoundVolume volume)
            {
                switch (volume)
                {
                    case Core.Settings.SoundVolume.Master:
                        return "마스터 볼륨";
                    case Core.Settings.SoundVolume.Music:
                        return "배경음악 볼륨";
                    case Core.Settings.SoundVolume.Ambience:
                        return "환경소리 볼륨";
                    case Core.Settings.SoundVolume.Effects:
                        return "효과음 볼륨";
                    case Core.Settings.SoundVolume.Microphone:
                        return "마이크 볼륨";
                    default:
                        return volume.ToString();
                }
            }
        }

        /// <summary>The handle down the panel's right edge.</summary>
        public static class Scroll
        {
            public const float RightMargin = 20f;
            public const float Width = 8f;
            public const int Radius = 4;

            /// <summary>
            /// How far a notch of the wheel moves the page. Not given by the
            /// design; near a row's height, so one notch reads as one row.
            /// </summary>
            public const float Sensitivity = 90f;
        }

        public static class LanguageRow
        {
            public const string Label = "언어";
        }

        /// <summary>
        /// The picker every row but the feedback one carries: two arrows with
        /// the chosen value between them.
        /// </summary>
        public static class Stepper
        {
            /// <summary>Arrow to arrow.</summary>
            public const float Width = 360f;

            /// <summary>
            /// A value the machine names rather than we do — the microphone —
            /// is set over two lines at this size instead of one at
            /// <see cref="ValueFontSize"/>.
            /// </summary>
            /// <remarks>
            /// Every picker is the same width, because one wider than the rest
            /// puts its left arrow out of line with the column and reads as a
            /// mistake. So the row that cannot hold its value on one line takes
            /// two, and the smaller size is what pays for them: two lines of
            /// this leave about 480 points of run, and
            /// "헤드셋 마이크(Realtek(R) Audio)" needs some 350 of it.
            /// </remarks>
            public const float WrappedValueFontSize = 24f;

            /// <summary>
            /// Low enough that two lines can always be reached. A machine's
            /// name for a microphone runs to thirty characters, and shrinking
            /// is what buys the room; stopping the shrink too early leaves the
            /// text needing a third line it cannot have.
            /// </summary>
            public const float WrappedValueMinFontSize = 14f;

            /// <summary>
            /// Two, and the end is cut short if even the smallest size cannot
            /// fit in them. A third would stand taller than the row.
            /// </summary>
            public const int WrappedValueLines = 2;

            /// <summary>
            /// How tall a wrapped value's box stands: its lines at their full
            /// size.
            /// </summary>
            /// <remarks>
            /// Given to the box rather than left to stretch the whole row,
            /// which is what keeps two lines centred in it. A box the height of
            /// the row lets the text be laid out over three lines and only two
            /// of them shown, and a three-line block centred in the row puts
            /// the two that show half a line high.
            /// </remarks>
            public static float WrappedValueHeight =>
                WrappedValueLines * WrappedValueFontSize * LineSpacing;

            /// <summary>
            /// How much taller a line stands than its letters, near enough for
            /// sizing a box. The font's own figure decides what is drawn.
            /// </summary>
            public const float LineSpacing = 1.2f;

            public const float ArrowSize = 24f;

            /// <summary>
            /// Not given by the design. The arrow is drawn at 24 but a 24 point
            /// target is hard to hit, so the strip that takes the click is
            /// wider than the glyph that is painted.
            /// </summary>
            public const float ArrowHitWidth = 60f;

            public const float ValueFontSize = 28f;

            /// <summary>
            /// How small a value is allowed to shrink to fit between the
            /// arrows before it is cut short instead.
            /// </summary>
            /// <remarks>
            /// Not given by the design, whose values are all short. One row is
            /// not: a microphone is named by the machine, and those names run
            /// past what 240 points of one line will hold. Shrinking a little
            /// keeps that row the same shape as every other, which two lines
            /// would not.
            /// </remarks>
            public const float ValueMinFontSize = 20f;
        }

        public static class FeedbackRow
        {
            /// <summary>
            /// Not given by the design, which shows a placeholder here.
            /// </summary>
            public const string Label = "피드백";

            public static readonly Vector2 ButtonSize = new Vector2(200f, 60f);
            public const int ButtonRadius = 20;
            public const float ButtonFontSize = 28f;
            public const string ButtonLabel = "피드백 보내기";
        }

        /// <summary>
        /// The panel 피드백 보내기 opens: a box to write in and a way to send it.
        /// </summary>
        /// <remarks>
        /// Not drawn by the design. Built to the same 590 wide plate as the
        /// confirmations and the room-creation modal, and taller by exactly
        /// what the writing box and its counter need, so it reads as one of
        /// the family rather than as a fifth kind of window. Its two buttons
        /// are the confirmations' own — see
        /// <see cref="Character.CharacterClosetStyle.Modal"/>.
        /// <para>
        /// Every measurement below is down from the panel's top, derived from
        /// the one above it, so changing the box's height moves the counter and
        /// the buttons with it.
        /// </para>
        /// </remarks>
        public static class Feedback
        {
            public const float Width = 590f;
            public const int PanelRadius = 20;
            public const float SidePadding = 32f;

            public const float TitleTop = 41f;
            public const float TitleFontSize = 30f;
            public const float SubtitleGap = 15f;
            public const float SubtitleFontSize = 20f;

            public const float FieldGapAbove = 20f;
            public const float FieldHeight = 180f;
            public const int FieldRadius = 12;
            public const float FieldPadding = 20f;
            public const float FieldFontSize = 20f;

            public const float CounterGap = 8f;
            public const float CounterHeight = 22f;
            public const float CounterFontSize = 18f;

            public const float ButtonGapAbove = 30f;

            /// <summary>Below the buttons, matching <see cref="TitleTop"/>.</summary>
            public const float BottomPadding = 41f;

            /// <summary>
            /// Long enough to describe a problem, short enough that nobody
            /// writes a letter nobody reads. The box stops taking keys here and
            /// the counter is what says why.
            /// </summary>
            public const int MaxLength = 500;

            public const string Title = "피드백 보내기";
            public const string Subtitle = "불편한 점이나 바라는 점을 남겨주세요.";
            public const string Placeholder = "내용을 입력해주세요";
            public const string CancelLabel = "취소";
            public const string SubmitLabel = "보내기";

            private static float TitleHeight => TitleFontSize * 1.4f;

            private static float SubtitleHeight => SubtitleFontSize * 1.4f;

            public static float SubtitleTop => TitleTop + TitleHeight + SubtitleGap;

            public static float FieldTop => SubtitleTop + SubtitleHeight + FieldGapAbove;

            public static float CounterTop => FieldTop + FieldHeight + CounterGap;

            public static float ButtonTop => CounterTop + CounterHeight + ButtonGapAbove;

            public static Vector2 PanelSize => new Vector2(
                Width,
                ButtonTop + Character.CharacterClosetStyle.Modal.ButtonSize.y + BottomPadding);

            public static float FieldWidth => Width - (SidePadding * 2f);
        }

        /// <summary>초기화 and 적용하기, along the bottom of the panel.</summary>
        public static class Buttons
        {
            public static readonly Vector2 Size = new Vector2(275f, 60f);

            /// <summary>Left edges, from the panel's left. Together they centre on the screen.</summary>
            public const float ResetLeft = 491f;

            public const float ApplyLeft = 835f;

            /// <summary>Down from the panel's top to the buttons' top.</summary>
            public const float Top = 782f;

            /// <summary>
            /// The design says 32, which is more than half the height; 30 is
            /// the most a 60 point plate can be rounded, and draws the same
            /// pill.
            /// </summary>
            public const int Radius = 30;

            public const float FontSize = 32f;
            public const float IconSize = 30f;
            public const float IconGap = 14f;
            public const string ResetLabel = "변경 취소";
            public const string ApplyLabel = "적용하기";

            /// <summary>
            /// Lobby overlay only: same size as 적용하기, under the tabs at
            /// the panel's bottom left.
            /// </summary>
            public const float LeaveLeft = 30f;
            public const string LeaveLabel = "게임 나가기";
        }

        /// <summary>
        /// The words in the three confirmations. Their shape is the closet's
        /// modal, which the design draws identically; see
        /// <see cref="Character.CharacterClosetStyle.Modal"/>.
        /// </summary>
        public static class Modal
        {
            public const string ResetAllTitle = "전체 설정의 변경을 취소하시겠습니까?";
            public const string ResetAllSubtitle = "모든 설정을 마지막으로 저장한 값으로 되돌립니다.";

            /// <summary>Takes the tab's name in front.</summary>
            public const string ResetTabTitleSuffix = " 설정의 변경을 취소하시겠습니까?";

            public const string ResetTabSubtitle = "현재 탭의 설정을 마지막으로 저장한 값으로 되돌립니다.";
            public const string CancelLabel = "취소";
            public const string ResetLabel = "변경 취소";

            public const string DiscardTitle = "저장하고 나가시겠습니까?";
            public const string DiscardSubtitle = "저장하지 않으면 변경사항이 사라집니다.";
            public const string LeaveLabel = "바로 나가기";
            public const string SaveAndLeaveLabel = "저장하고 나가기";

            public const string LeaveGameTitle = "게임을 진짜 나가시겠습니까?";
            public const string LeaveGameSubtitle = "";
            public const string LeaveGameAcceptLabel = "나가기";
        }

        /// <summary>
        /// What the passing message over the screen is called, whether the send
        /// went through or not.
        /// </summary>
        public const string FeedbackNoticeTitle = "피드백 보내기";

        /// <summary>
        /// Said when the server wrote it down.
        /// </summary>
        /// <remarks>
        /// Thanks and nothing else. <b>No reply is promised</b> — there is no
        /// path to send one, so "답변을 드립니다" would be a lie the player only
        /// finds out about by waiting.
        /// </remarks>
        public const string FeedbackSentMessage = "보냈습니다. 고맙습니다";

        /// <summary>
        /// Added to every refusal.
        /// </summary>
        /// <remarks>
        /// The reassurance matters more than the reason. Somebody who just wrote
        /// five hundred characters fears they are gone, and the panel does keep
        /// them — saying so is what makes trying again feel worth it.
        /// </remarks>
        public const string FeedbackKeptMessage = "작성한 내용은 그대로 있어요";

        /// <summary>Refused because nothing is signed in yet.</summary>
        public const string FeedbackNotSignedInMessage = "서버에 연결되어 있지 않습니다";

        /// <summary>Refused because the server could not be reached.</summary>
        public const string FeedbackOfflineMessage = "서버에 연결할 수 없습니다";

        /// <summary>
        /// Refused as malformed. In practice that means too long, since the box
        /// itself will not take more than the limit and blank never gets sent.
        /// </summary>
        public const string FeedbackTooLongMessage = "글이 너무 길어 보내지 못했습니다";

        /// <summary>Refused for a reason the screen cannot explain.</summary>
        public const string FeedbackFailedMessage = "보내지 못했습니다";

        /// <summary>
        /// The name each 그래픽 row goes by. Kept beside the tab names rather
        /// than in the catalogue: the catalogue holds what a row offers, and
        /// this is what the row is called.
        /// </summary>
        public static string GraphicsRowLabel(Core.Settings.GraphicsOption option)
        {
            switch (option)
            {
                case Core.Settings.GraphicsOption.DisplayMode:
                    return "디스플레이 모드";
                case Core.Settings.GraphicsOption.Resolution:
                    return "해상도";
                case Core.Settings.GraphicsOption.FpsLimit:
                    return "FPS 제한";
                case Core.Settings.GraphicsOption.AntiAliasing:
                    return "안티앨리어싱";
                case Core.Settings.GraphicsOption.Hbao:
                    return "HBAO";
                case Core.Settings.GraphicsOption.TextureQuality:
                    return "텍스처 품질";
                case Core.Settings.GraphicsOption.ShadowQuality:
                    return "그림자 품질";
                case Core.Settings.GraphicsOption.DepthOfField:
                    return "피사계 심도";
                case Core.Settings.GraphicsOption.Volumetrics:
                    return "볼륨";
                default:
                    return option.ToString();
            }
        }

        /// <inheritdoc cref="GraphicsRowLabel"/>
        public static string InterfaceRowLabel(Core.Settings.InterfaceOption option)
        {
            switch (option)
            {
                case Core.Settings.InterfaceOption.UiScale:
                    return "UI 크기";
                case Core.Settings.InterfaceOption.FontScale:
                    return "글자 크기";
                case Core.Settings.InterfaceOption.InGameUi:
                    return "게임 내 UI";
                case Core.Settings.InterfaceOption.FpsCounter:
                    return "FPS 표시";
                case Core.Settings.InterfaceOption.PingCounter:
                    return "핑 표시";
                case Core.Settings.InterfaceOption.PlayerNames:
                    return "다른 플레이어 이름 표시";
                case Core.Settings.InterfaceOption.StreamerMode:
                    return "스트리머 모드";
                case Core.Settings.InterfaceOption.BeginnerGuide:
                    return "초심자 가이드 항상 표시";
                case Core.Settings.InterfaceOption.ChatScope:
                    return "채팅 메시지 범위 제한";
                default:
                    return option.ToString();
            }
        }

        public static string TabLabel(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.General:
                    return "일반";
                case SettingsTab.Graphics:
                    return "그래픽";
                case SettingsTab.Interface:
                    return "인터페이스";
                case SettingsTab.Sound:
                    return "사운드";
                case SettingsTab.Controls:
                    return "컨트롤";
                case SettingsTab.Notifications:
                    return "알림";
                default:
                    return tab.ToString();
            }
        }

        /// <summary>
        /// Reads a design hex such as 0xF5F3F1 as a colour. The palette is
        /// written in sRGB the way the mock-up reports it, and Unity's UI shader
        /// expects exactly that, so no gamma conversion belongs here.
        /// </summary>
        public static Color FromHex(uint rgb, float alpha = 1f) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
    }
}
