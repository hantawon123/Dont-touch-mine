using Game.Core.Settings;
using UnityEngine;
using UnityEngine.UI;

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
        public const int GameplayOverlaySortingOrder = 10000;
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Same files the play-settings picker uses. The Settings scene wires
        /// these on the component; a view built in code, as the lobby overlay
        /// is, has to load them itself.
        /// </summary>
        public const string ArrowLeftIconResource = "UI/Icon_Left";
        public const string ArrowRightIconResource = "UI/Icon_Right";
        public const string CloseIconResource = "UI/Icon_Close";
        public const string CloseIconAssetPath = "Assets/_Game/Content/Resources/UI/Icon_Close.png";

        /// <summary>
        /// The X on a confirmation. The Settings / Closet scenes assign it in
        /// the inspector; a view built in code, as the lobby overlays are,
        /// loads the Resources copy so player builds can find it.
        /// </summary>
        public static Sprite LoadCloseIcon(Sprite assigned = null)
        {
            if (assigned != null)
            {
                return assigned;
            }

            var loaded = Resources.Load<Sprite>(CloseIconResource);
            if (loaded != null)
            {
                return loaded;
            }

            var texture = Resources.Load<Texture2D>(CloseIconResource);
            if (texture != null)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(CloseIconAssetPath);
#else
            return null;
#endif
        }

        public static Sprite ApplyCloseIcon(Image image, Sprite assigned = null)
        {
            var sprite = LoadCloseIcon(assigned);
            if (image == null)
            {
                return sprite;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.enabled = sprite != null;
            return sprite;
        }

        public static Sprite ApplyCloseButton(Selectable button, Sprite assigned = null)
        {
            if (button == null)
            {
                return LoadCloseIcon(assigned);
            }

            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            var sprite = ApplyCloseIcon(image, assigned);
            if (sprite == null)
            {
                return null;
            }

            var label = button.transform.Find("Text");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            return sprite;
        }

        public static class Palette
        {
            public static readonly Color TextPrimary = FromHex(0xF5F3F1);
            public static readonly Color TextMuted = FromHex(0xA8ADB3);
            public static readonly Color Accent = FromHex(0xFF7032);

            /// <summary>Shown only while the background art is missing.</summary>
            public static readonly Color BackgroundFallback = FromHex(0x0B1018);

            /// <summary>
            /// Lobby and in-match overlay: 80% black over the room, instead
            /// of the Home picture the standalone screen uses.
            /// </summary>
            public static readonly Color OverlayDim = FromHex(0x000000, 0.8f);

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

            /// <summary>
            /// The plate under the two words at the top of the screen
            /// (S15P21D205-1086).
            /// </summary>
            /// <remarks>
            /// 글자만 두면 뒤에 오는 것에 따라 읽히기도 하고 묻히기도 합니다. 로비에서는
            /// 그 자리에 맵 미리보기 카드가 오는데 맵마다 밝기가 달라서, 마트처럼 밝은
            /// 썸네일 위에서는 흰 글자가 사라집니다. 판을 깔면 배경이 무엇이든 대비가
            /// 같습니다.
            /// </remarks>
            public static readonly Color ChromePlateFill = FromHex(0x0B1018, 0.62f);
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

            /// <summary>
            /// Lobby overlays keep the 1600×876 layout and shrink the drawn
            /// frame so type, icons and gaps stay in proportion.
            /// </summary>
            public const float LobbyScale = 0.8f;

            /// <summary>
            /// Shrinks <paramref name="rect"/> about its centre. Pivot and
            /// anchored position move so the frame stays where it was; children
            /// keep their pixel layout.
            /// </summary>
            public static void ApplyLobbyScale(RectTransform rect)
            {
                if (rect == null)
                {
                    return;
                }

                var size = rect.rect.size;
                if (size.x <= 0f || size.y <= 0f)
                {
                    size = rect.sizeDelta;
                }

                var toCenter = new Vector2(
                    (0.5f - rect.pivot.x) * size.x,
                    (0.5f - rect.pivot.y) * size.y);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition += toCenter;
                rect.localScale = Vector3.one * LobbyScale;
            }
        }

        /// <summary>The same arrow the room browser draws, in the same place.</summary>
        public static class Back
        {
            public static readonly Vector2 Position = new Vector2(64f, -62f);
            public static readonly Vector2 Size = new Vector2(140f, 44f);
            public static readonly Vector2 LeaveSize = new Vector2(220f, 44f);
            public const float FontSize = 30f;
            public static string Label =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Back, "ko");
        }

        /// <summary>
        /// 화면 맨 위 양 끝의 두 글자가 깔고 앉는 판 (S15P21D205-1086). 왼쪽이 로비의
        /// 게임 나가기, 오른쪽이 전체 변경 취소입니다.
        /// </summary>
        /// <remarks>
        /// 둘의 높이를 여기서 한 번만 정합니다. 따로 적으면 같은 선 위에 앉아야 하는 둘이
        /// 언젠가 몇 픽셀씩 어긋납니다.
        /// <para>
        /// <b>테두리는 게임 나가기에만 있습니다.</b> 둘 다 테두리를 두르면 서로 강조를
        /// 빼앗습니다. 방을 나가는 쪽이 되돌릴 수 없는 행동이라 그쪽만 두릅니다 - 대신
        /// 주황으로 가득 채우지는 않습니다. 화면에서 제일 밝은 것이 나가기 버튼일 이유는
        /// 없고, 패널을 두른 주황 글로우와도 경쟁합니다.
        /// </para>
        /// </remarks>
        public static class Chrome
        {
            /// <summary>
            /// 30 짜리 글자에 위아래로 15 씩 남습니다. 48 로 처음 잡았더니 글자가 테두리에
            /// 붙어 빽빽해 보였습니다.
            /// </summary>
            public const float PlateHeight = 60f;

            /// <summary>절반. 알약 모양이 되는 가장 큰 값입니다.</summary>
            public const int PlateRadius = 30;

            /// <summary>글자와 판 사이. 아이콘이 있는 쪽을 조금 좁게 잡습니다.</summary>
            public const int PadLeft = 26;

            public const int PadRight = 32;

            /// <summary>게임 나가기에만: 테두리 두께와 아이콘.</summary>
            public const float LeaveStroke = 2f;

            public const float LeaveIconSize = 28f;
            public const float LeaveIconGap = 14f;

            /// <summary>
            /// 화면 맨 위 줄의 중심 높이(위에서 아래로). 왼쪽 게임 나가기가 이 줄에 앉습니다.
            ///
            /// <para>
            /// 전에는 오른쪽 전체 변경 취소도 같은 줄이었습니다. 2026-09-21 에 그쪽만 한 줄
            /// 내렸으므로(<see cref="ResetAll.DropFromTopRow"/>) 이 값은 왼쪽 것만 씁니다.
            /// </para>
            /// </summary>
            public static float TopRowCentreY => -Back.Position.y + (Back.Size.y * 0.5f);
        }

        /// <summary>The circling arrow and its words at the top right.</summary>
        public static class ResetAll
        {
            /// <summary>
            /// 오른쪽 끝을 <b>설정 판의 오른쪽 끝</b>에 맞춥니다.
            ///
            /// <para>
            /// 판의 값에서 구합니다. 숫자를 따로 적으면 판 크기를 고칠 때 한쪽만 바뀌어 어긋납니다.
            /// 단추는 피벗이 오른쪽이고 글자 길이만큼 늘어나므로(ContentSizeFitter), 글자가 긴
            /// 영어에서도 왼쪽으로만 늘어나 이 끝은 그대로입니다.
            /// </para>
            /// </summary>
            public static float RightMargin =>
                ReferenceResolution.x - (Frame.Position.x + Frame.Size.x);

            /// <summary>
            /// 로비에서 쓰는 오른쪽 여백.
            ///
            /// <para>
            /// 로비는 판을 가운데 기준으로 0.8 배로 줄이므로(<see cref="Frame.LobbyScale"/>) 판의
            /// 오른쪽 끝이 그만큼 안으로 들어옵니다. 단추도 같이 들어와야 끝이 맞습니다.
            /// </para>
            /// </summary>
            public static float LobbyRightMargin
            {
                get
                {
                    var centreX = Frame.Position.x + (Frame.Size.x * 0.5f);
                    var rightEdge = centreX + (Frame.Size.x * Frame.LobbyScale * 0.5f);
                    return ReferenceResolution.x - rightEdge;
                }
            }

            /// <summary>
            /// <b>로비에서만</b> 맨 위 줄에서 이만큼 내려앉습니다 (2026-09-21 사용자 지정).
            ///
            /// <para>
            /// 로비에서는 방 제목(ROOM SETTING) 오른쪽 줄에 두기로 했습니다. 판 높이(60)보다 큰
            /// 값이라 두 줄이 겹치지 않습니다. <b>시작화면(설정 화면)은 내리지 않습니다</b> - 거기서는
            /// 판이 화면을 거의 다 채워서, 내리면 단추가 판 안으로 들어가 겹칩니다.
            /// </para>
            /// </summary>
            public const float LobbyDropFromTopRow = 84f;

            /// <summary>화면 위에서 이 줄의 중심까지. 시작화면은 맨 위 줄입니다.</summary>
            public static float CentreY => Chrome.TopRowCentreY;

            /// <summary>로비에서 쓰는 값.</summary>
            public static float LobbyCentreY => Chrome.TopRowCentreY + LobbyDropFromTopRow;

            public const float Height = 44f;
            public const float FontSize = 30f;
            public const float IconSize = 24f;
            public const float IconGap = 14f;
            public static string Label =>
                UiTextCatalog.Shipped.Get(UiText.Settings.ResetAll, "ko");
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
            public static string IdleLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicTestIdle, "ko");

            public static string RunningLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicTestRunning, "ko");

            public static string UnavailableTitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicrophoneTest, "ko");

            public static string UnavailableMessage =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicTestUnavailable, "ko");
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
            public static string RowKey(NotificationOption option)
            {
                switch (option)
                {
                    case NotificationOption.GameInvite:
                        return UiText.Settings.GameInvite;
                    default:
                        return option.ToString();
                }
            }

            public static string RowLabel(NotificationOption option, string languageCode = "ko") =>
                UiTextCatalog.Shipped.Get(RowKey(option), languageCode);
        }

        /// <summary>The 컨트롤 tab's headings and row names.</summary>
        public static class Controls
        {
            public static string MicrophoneHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicrophoneHeading, "ko");

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
            public static string KeyboardMoveHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.KeyboardMoveHeading, "ko");

            public static string KeyboardActionHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.KeyboardActionHeading, "ko");

            public static string FirstPersonHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FirstPersonHeading, "ko");

            public static string ThirdPersonHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.ThirdPersonHeading, "ko");

            public static string ActionKey(ControlAction action)
            {
                switch (action)
                {
                    case ControlAction.MicrophoneTalk:
                        return UiText.Settings.PushToTalk;
                    case ControlAction.VoiceToggle:
                        return UiText.Settings.ActionVoiceToggle;
                    case ControlAction.ToggleSpeaker:
                        return UiText.Settings.ActionSpeakerToggle;
                    case ControlAction.MoveForward:
                        return UiText.Settings.ActionMoveForward;
                    case ControlAction.MoveLeft:
                        return UiText.Settings.ActionMoveLeft;
                    case ControlAction.MoveBackward:
                        return UiText.Settings.ActionMoveBackward;
                    case ControlAction.MoveRight:
                        return UiText.Settings.ActionMoveRight;
                    case ControlAction.PrimaryAction:
                        return UiText.Settings.ActionPrimary;
                    case ControlAction.Interact:
                        return UiText.Settings.ActionInteract;
                    case ControlAction.PlacementMode:
                        return UiText.Settings.ActionPlacement;
                    case ControlAction.RotateLeft:
                        return UiText.Settings.ActionRotateLeft;
                    case ControlAction.RotateRight:
                        return UiText.Settings.ActionRotateRight;
                    case ControlAction.RaiseObject:
                        return UiText.Settings.ActionRaise;
                    case ControlAction.LowerObject:
                        return UiText.Settings.ActionLower;
                    case ControlAction.Jump:
                        return UiText.Settings.ActionJump;
                    case ControlAction.Sprint:
                        return UiText.Settings.ActionSprint;
                    case ControlAction.ToggleView:
                        return UiText.Settings.ActionToggleView;
                    case ControlAction.Crouch:
                        return UiText.Settings.ActionCrouch;
                    case ControlAction.Prone:
                        return UiText.Settings.ActionProne;
                    case ControlAction.ToggleKeyGuide:
                        return UiText.Settings.ActionKeyGuide;
                    case ControlAction.EmoteWheel:
                        return UiText.Settings.ActionEmoteWheel;
                    default:
                        return action.ToString();
                }
            }

            public static string ActionLabel(ControlAction action, string languageCode = "ko") =>
                UiTextCatalog.Shipped.Get(ActionKey(action), languageCode);

            public static string SensitivityKey(ControlSensitivity sensitivity) =>
                sensitivity == ControlSensitivity.ThirdPersonCamera
                    ? UiText.Settings.CameraSensitivity
                    : UiText.Settings.MouseSensitivity;

            public static string SensitivityLabel(ControlSensitivity sensitivity, string languageCode = "ko") =>
                UiTextCatalog.Shipped.Get(SensitivityKey(sensitivity), languageCode);

            /// <summary>
            /// Said when a key could not be moved because something else has
            /// it. Names what has it, so the player knows what to move first.
            /// </summary>
            public static string InUseTitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.KeyInUseTitle, "ko");

            public static string InUseMessage(string keyLabel, string action, string languageCode = "ko") =>
                string.Format(
                    UiTextCatalog.Shipped.Get(UiText.Settings.KeyInUseMessage, languageCode),
                    keyLabel,
                    action);

            public static string ReversalKey(ControlToggle toggle) =>
                toggle == ControlToggle.FirstPersonInvertX
                || toggle == ControlToggle.ThirdPersonInvertX
                    ? UiText.Settings.InvertX
                    : UiText.Settings.InvertY;

            public static string ReversalLabel(ControlToggle toggle, string languageCode = "ko") =>
                UiTextCatalog.Shipped.Get(ReversalKey(toggle), languageCode);
        }

        /// <summary>The 사운드 tab's headings and row names.</summary>
        public static class Sound
        {
            public static string SpeakerHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.SpeakerHeading, "ko");

            public static string MicrophoneHeading =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicrophoneHeading, "ko");

            public static string DeviceLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Device, "ko");

            public static string InputModeLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.InputMode, "ko");

            public static string TestLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.MicrophoneTest, "ko");

            public static string VolumeKey(SoundVolume volume)
            {
                switch (volume)
                {
                    case SoundVolume.Master:
                        return UiText.Settings.VolumeMaster;
                    case SoundVolume.Music:
                        return UiText.Settings.VolumeMusic;
                    case SoundVolume.Effects:
                        return UiText.Settings.VolumeEffects;
                    case SoundVolume.Microphone:
                        return UiText.Settings.VolumeMicrophone;
                    default:
                        return volume.ToString();
                }
            }

            public static string VolumeLabel(SoundVolume volume, string languageCode = "ko") =>
                UiTextCatalog.Shipped.Get(VolumeKey(volume), languageCode);
        }

        /// <summary>The handle down the panel's right edge.</summary>
        public static class Scroll
        {
            public const float RightMargin = 20f;
            public const float Width = 8f;
            public const int Radius = 4;

            /// <summary>
            /// Wheel movement scale, kept below a row height so scrolling
            /// through settings allows smaller adjustments.
            /// </summary>
            public const float Sensitivity = 30f;

            // About 95% of the wheel distance is covered in 0.15 seconds.
            public const float SmoothingTime = 0.05f;
        }

        public static class LanguageRow
        {
            public static string Label =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Language, "ko");
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
            public static string Label =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Feedback, "ko");

            public static readonly Vector2 ButtonSize = new Vector2(200f, 60f);
            public const float ButtonPaddingX = 28f;
            public const int ButtonRadius = 20;
            public const float ButtonFontSize = 28f;
            public static string ButtonLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSend, "ko");
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

            public static string Title =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSend, "ko");

            public static string Subtitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSubtitle, "ko");

            public static string Placeholder =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackPlaceholder, "ko");

            public static string CancelLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Cancel, "ko");

            public static string SubmitLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSubmit, "ko");

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
            public static string ResetLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Reset, "ko");

            public static string ApplyLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Apply, "ko");

            /// <summary>
            /// Lobby overlay only: same size as 적용하기, under the tabs at
            /// the panel's bottom left.
            /// </summary>
            public const float LeaveLeft = 30f;
            public static string LeaveLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.LeaveGame, "ko");
        }

        /// <summary>
        /// The words in the three confirmations. Their shape is the closet's
        /// modal, which the design draws identically; see
        /// <see cref="Character.CharacterClosetStyle.Modal"/>.
        /// </summary>
        public static class Modal
        {
            public static string ResetAllTitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.ResetAllTitle, "ko");

            public static string ResetAllSubtitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.ResetAllSubtitle, "ko");

            public static string ResetTabTitle(SettingsTab tab, string languageCode = "ko") =>
                string.Format(
                    UiTextCatalog.Shipped.Get(UiText.Settings.ResetTabTitle, languageCode),
                    TabLabel(tab, languageCode));

            public static string ResetTabSubtitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.ResetTabSubtitle, "ko");

            public static string CancelLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Cancel, "ko");

            public static string ResetLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Reset, "ko");

            public static string DiscardTitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.DiscardTitle, "ko");

            public static string DiscardSubtitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.DiscardSubtitle, "ko");

            public static string LeaveLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.LeaveWithoutSaving, "ko");

            public static string SaveAndLeaveLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.SaveAndLeave, "ko");

            public static string LeaveGameTitle =>
                UiTextCatalog.Shipped.Get(UiText.Settings.LeaveGameTitle, "ko");

            public const string LeaveGameSubtitle = "";

            public static string LeaveGameAcceptLabel =>
                UiTextCatalog.Shipped.Get(UiText.Settings.Leave, "ko");
        }

        /// <summary>
        /// What the passing message over the screen is called, whether the send
        /// went through or not.
        /// </summary>
        public static string FeedbackNoticeTitle =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSend, "ko");

        /// <summary>
        /// Said when the server wrote it down.
        /// </summary>
        /// <remarks>
        /// Thanks and nothing else. <b>No reply is promised</b> — there is no
        /// path to send one, so "답변을 드립니다" would be a lie the player only
        /// finds out about by waiting.
        /// </remarks>
        public static string FeedbackSentMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackSent, "ko");

        /// <summary>
        /// Added to every refusal.
        /// </summary>
        /// <remarks>
        /// The reassurance matters more than the reason. Somebody who just wrote
        /// five hundred characters fears they are gone, and the panel does keep
        /// them — saying so is what makes trying again feel worth it.
        /// </remarks>
        public static string FeedbackKeptMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackKept, "ko");

        /// <summary>Refused because nothing is signed in yet.</summary>
        public static string FeedbackNotSignedInMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackNotSignedIn, "ko");

        /// <summary>Refused because the server could not be reached.</summary>
        public static string FeedbackOfflineMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackOffline, "ko");

        /// <summary>
        /// Refused as malformed. In practice that means too long, since the box
        /// itself will not take more than the limit and blank never gets sent.
        /// </summary>
        public static string FeedbackTooLongMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackTooLong, "ko");

        /// <summary>Refused for a reason the screen cannot explain.</summary>
        public static string FeedbackFailedMessage =>
            UiTextCatalog.Shipped.Get(UiText.Settings.FeedbackFailed, "ko");

        /// <summary>
        /// The name each 그래픽 row goes by. Kept beside the tab names rather
        /// than in the catalogue: the catalogue holds what a row offers, and
        /// this is what the row is called.
        /// </summary>
        public static string GraphicsRowKey(GraphicsOption option)
        {
            switch (option)
            {
                case GraphicsOption.DisplayMode:
                    return UiText.Settings.DisplayMode;
                case GraphicsOption.Resolution:
                    return UiText.Settings.Resolution;
                case GraphicsOption.FpsLimit:
                    return UiText.Settings.FpsLimit;
                case GraphicsOption.TextureQuality:
                    return UiText.Settings.TextureQuality;
                default:
                    return option.ToString();
            }
        }

        public static string GraphicsRowLabel(GraphicsOption option, string languageCode = "ko") =>
            UiTextCatalog.Shipped.Get(GraphicsRowKey(option), languageCode);

        /// <inheritdoc cref="GraphicsRowLabel"/>
        public static string InterfaceRowKey(InterfaceOption option)
        {
            switch (option)
            {
                case InterfaceOption.UiScale:
                    return UiText.Settings.UiScale;
                case InterfaceOption.FontScale:
                    return UiText.Settings.FontScale;
                case InterfaceOption.InGameUi:
                    return UiText.Settings.InGameUi;
                case InterfaceOption.FpsCounter:
                    return UiText.Settings.FpsCounter;
                case InterfaceOption.PingCounter:
                    return UiText.Settings.PingCounter;
                case InterfaceOption.PlayerNames:
                    return UiText.Settings.PlayerNames;
                case InterfaceOption.StreamerMode:
                    return UiText.Settings.StreamerMode;
                case InterfaceOption.BeginnerGuide:
                    return UiText.Settings.BeginnerGuide;
                case InterfaceOption.ChatScope:
                    return UiText.Settings.ChatScope;
                default:
                    return option.ToString();
            }
        }

        public static string InterfaceRowLabel(InterfaceOption option, string languageCode = "ko") =>
            UiTextCatalog.Shipped.Get(InterfaceRowKey(option), languageCode);

        public static string TabKey(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.General:
                    return UiText.Settings.TabGeneral;
                case SettingsTab.Graphics:
                    return UiText.Settings.TabGraphics;
                case SettingsTab.Interface:
                    return UiText.Settings.TabInterface;
                case SettingsTab.Sound:
                    return UiText.Settings.TabSound;
                case SettingsTab.Controls:
                    return UiText.Settings.TabControls;
                case SettingsTab.Notifications:
                    return UiText.Settings.TabNotifications;
                default:
                    return tab.ToString();
            }
        }

        public static string TabLabel(SettingsTab tab, string languageCode = "ko") =>
            UiTextCatalog.Shipped.Get(TabKey(tab), languageCode);

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
