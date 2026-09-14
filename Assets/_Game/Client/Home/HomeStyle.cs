using UnityEngine;

namespace Game.Client.Home
{
    /// <summary>
    /// Every colour and measurement the Home screen draws with, in one place.
    /// </summary>
    /// <remarks>
    /// The screen is built in code rather than authored in the scene, so these
    /// numbers are the only record of the design. Keeping them here means a
    /// revised mock-up is one file to edit, and it lets the menu, the profile
    /// chip and the icon buttons agree on a colour without copying literals
    /// between them.
    /// <para>
    /// Measurements are in the 1920x1080 the mock-up was drawn at, which is also
    /// the canvas reference resolution, so they are literal pixels there and
    /// scale from it everywhere else.
    /// </para>
    /// </remarks>
    public static class HomeStyle
    {
        /// <summary>
        /// Over every connection failure this screen reports. The same heading
        /// as the room browser uses: from the player's side these are one
        /// event, which is that the game could not reach the server.
        /// </summary>
        public const string ConnectionErrorTitle = "게임 접속 오류";

        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public static class Palette
        {
            public static readonly Color TextPrimary = FromHex(0xF5F3F1);
            public static readonly Color Accent = FromHex(0xFF7032);

            /// <summary>
            /// Shown only while the background art is missing, so an unwired
            /// scene reads as a dark screen rather than a white one.
            /// </summary>
            public static readonly Color BackgroundFallback = FromHex(0x0B1018);

            /// <summary>
            /// The profile chip, which sits over the lit road and wants to be
            /// darker than the icon buttons beside it.
            /// </summary>
            public static readonly Color ChipFill = FromHex(0x0B1018, 0.6f);

            /// <summary>
            /// The friend and server buttons, which sit over the dark edges of
            /// the picture and only need to lift off them.
            /// </summary>
            public static readonly Color ButtonFill = FromHex(0x5C5C5C, 0.5f);

            /// <summary>
            /// Hovering settles every one of these on the same grey and draws a
            /// hairline around it, so the chip and the two buttons answer alike.
            /// </summary>
            public static readonly Color HoverFill = FromHex(0x5C5C5C, 0.5f);

            public static readonly Color HoverStroke = FromHex(0xF5F3F1);

            public static readonly Color PanelFill = FromHex(0x0B1018, 0.8f);

            public static readonly Color ToggleStroke = FromHex(0xD9D9D9);
            public static readonly Color ToggleOffFill = FromHex(0xFFFFFF);
            public static readonly Color ToggleOffKnob = FromHex(0x747474);
            public static readonly Color ToggleOnFill = FromHex(0xFF9A6A);
            public static readonly Color ToggleOnKnob = FromHex(0xFF7032);

            public static readonly Color InputFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color CheckFill = FromHex(0xFFFFFF);
            public static readonly Color CheckLabel = FromHex(0x0B1018);

            public static readonly Color ApplyOffFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color ApplyOffLabel = FromHex(0xA8ADB3);
            public static readonly Color ApplyOnFill = FromHex(0xFF7032);
            public static readonly Color ApplyOnLabel = FromHex(0xF5F3F1);

            public static readonly Color MessageRejected = FromHex(0xFF0000);
            public static readonly Color MessageAccepted = FromHex(0x00FF1E);

            /// <summary>
            /// The character counter beside the message. Not given by the
            /// design; the muted grey the rest of the project uses for figures
            /// that are there to be glanced at.
            /// </summary>
            public static readonly Color Counter = FromHex(0xA8ADB3);

            /// <summary>
            /// The plate a region row shows while the pointer is on it.
            /// </summary>
            public static readonly Color RowHover = FromHex(0xF5F3F1, 0.16f);

            public static readonly Color TabSelected = FromHex(0xFF7032);
            public static readonly Color TabIdle = FromHex(0xA2A2A2);
            public static readonly Color TabDivider = FromHex(0x666666);
            public static readonly Color BadgeFill = FromHex(0xFF7032);
            public static readonly Color BadgeLabel = FromHex(0xFFFFFF);

            public static readonly Color SearchText = FromHex(0xFFFFFF);
            public static readonly Color Placeholder = FromHex(0xA8ADB3);

            public static readonly Color ToastFill = FromHex(0xFFFFFF);

            /// <summary>
            /// Not given by the design, which names only the weight. The dark
            /// the check button already uses on white.
            /// </summary>
            public static readonly Color ToastText = FromHex(0x0B1018);
            public static readonly Color Refresh = FromHex(0xFF9A6A);

            public static readonly Color SectionPrimary = FromHex(0xFF9A6A);
            public static readonly Color SectionSecondary = FromHex(0xA2A2A2);
            public static readonly Color FriendOnline = FromHex(0xF5F3F1);
            public static readonly Color FriendOffline = FromHex(0xA8ADB3);

            public static readonly Color ScrollHandle = FromHex(0xF5F3F1);

            /// <summary>
            /// The only panel in this screen that dims what is behind it. The
            /// others hang off a control and leave Home readable; this one takes
            /// the screen over until it is answered.
            /// </summary>
            public static readonly Color Dim = FromHex(0x000000, 0.5f);

            public static readonly Color ModalFill = FromHex(0x0B1018, 0.95f);
            public static readonly Color SegmentOnFill = FromHex(0xFF7032);
            public static readonly Color SegmentOnLabel = FromHex(0xF5F3F1);
            public static readonly Color SegmentOffFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color SegmentOffLabel = FromHex(0xA8ADB3);
            public static readonly Color FieldFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color CounterFaint = FromHex(0xF5F3F1);
        }

        public static class Layout
        {
            /// <summary>
            /// The left edge of the menu and the top of its first line, both
            /// measured from the top-left corner of the mock-up.
            /// </summary>
            public const float MenuLeft = 212f;

            public const float MenuTop = 399f;

            /// <summary>
            /// The design gives 30 between one line's box and the next, not
            /// between their tops. A 40 point line stands 48 tall, so the lines
            /// repeat every 78 and the four of them centre on the character.
            /// </summary>
            public const float MenuLineHeight = 48f;

            public const float MenuGap = 30f;

            public const float MenuPitch = MenuLineHeight + MenuGap;

            public const float QuitLeft = 60f;
            public const float QuitBottom = 40f;

            /// <summary>
            /// How much a menu line grows under the pointer. Small on purpose:
            /// enough to feel the line answer, not so much that the column
            /// appears to shift.
            /// </summary>
            public const float MenuHoverScale = 1.06f;

            /// <summary>
            /// How long that growth takes, matched to the colour tint's own fade
            /// so the two read as one response.
            /// </summary>
            public const float MenuHoverSeconds = 0.08f;

            /// <summary>
            /// The chip grows with the name it holds, between these two. Twelve
            /// characters is the longest a nickname gets and the wider number is
            /// what that measures.
            /// </summary>
            public const float ChipMinWidth = 200f;

            public const float ChipMaxWidth = 380f;

            public const float ChipHeight = 60f;
            public const float ChipAvatarDiameter = 42f;
            public const float ChipLeftPadding = 12f;
            public const float ChipAvatarToName = 16f;

            /// <summary>
            /// Not given by the design, which fixes the two widths above
            /// instead. Enough that a name ending at the chip's edge does not
            /// read as clipped.
            /// </summary>
            public const float ChipRightPadding = 20f;

            public const float IconButtonSize = 60f;
            public const float FriendIconSize = 20f;
            public const float ServerIconSize = 24f;

            public const float ChipToFriendButton = 16f;
            public const float BottomRightMargin = 60f;
            public const float BottomMargin = 30f;
            public const float ServerRightMargin = 60f;
            public const float ServerTopMargin = 60f;
        }

        /// <summary>
        /// The panel that changes the nickname, hung above the profile chip.
        /// </summary>
        /// <remarks>
        /// Its right edge lines up with the chip's, which is why the margin
        /// here matches where the chip starts, and why its bottom-right corner
        /// is square: the two read as one stack rather than two plates.
        /// </remarks>
        public static class Profile
        {
            public static readonly Vector2 PanelSize = new Vector2(532f, 257f);
            public const float PanelRightMargin = 136f;
            public const float PanelBottomMargin = 108f;

            public const float SidePadding = 30f;

            public const float ToggleRowCentreY = -44f;
            public const float ToggleLeft = 190f;
            public static readonly Vector2 ToggleSize = new Vector2(50f, 30f);
            public const float ToggleStrokeThickness = 3f;

            /// <summary>
            /// How far the knob sits inside the pill. Not given by the design;
            /// chosen so the knob clears the 3 pixel stroke on both sides.
            /// </summary>
            public const float ToggleKnobInset = 5f;

            /// <summary>
            /// Said on the toggle's own row rather than on the nickname message
            /// line below, which sits under the name field and would read as a
            /// verdict on the name.
            /// </summary>
            public const string SearchAllowFailedMessage = "바꾸지 못했어요";

            public const float InputTop = -78f;
            public static readonly Vector2 InputSize = new Vector2(472f, 52f);
            public const float InputTextPadding = 20f;

            /// <summary>
            /// Where the message line under the field starts, and how far the
            /// counter sits in from the right: a little inside the field's
            /// edge, short of the text inside it. Flush with the edge crowded
            /// the corner; level with the text sat too far in.
            /// </summary>
            public const float TextInset = SidePadding + 10f;

            public static readonly Vector2 CheckSize = new Vector2(95f, 36f);
            public const float CheckRightInset = 8f;

            /// <remarks>
            /// Ten under the field rather than hard against it, so the line
            /// reads as a note on the field and not as part of its plate.
            /// </remarks>
            public const float MessageTop = -140f;
            public const float MessageHeight = 26f;

            public const float ApplyTop = -173f;
            public static readonly Vector2 ApplySize = new Vector2(472f, 52f);

            /// <summary>
            /// The panel once the one change is spent: the apply row is gone,
            /// so the plate ends where the message line does, with the same
            /// margin under it that the apply button had.
            /// </summary>
            public static readonly float SettledPanelHeight =
                PanelSize.y - ApplySize.y - ((MessageTop - MessageHeight) - ApplyTop);

            public const string TooLongMessage = "최대 12글자 작성가능합니다";
            public const string BadCharacterMessage = "한글/영어/숫자만 작성가능합니다";
            public const string TakenMessage = "이미 존재하는 닉네임입니다";
            public const string AvailableMessage = "사용 가능한 닉네임입니다";

            /// <summary>
            /// Shown whenever nothing has gone wrong, so the one-shot nature of
            /// the change is on screen before it is spent rather than after.
            /// </summary>
            public const string OneChangeMessage = "닉네임은 한 번만 변경할 수 있어요";

            public const string AlreadySetMessage = "이미 닉네임을 변경했어요";

            /// <summary>
            /// The gap between the two halves of the confirm row.
            /// </summary>
            public const float ConfirmGap = 12f;

            /// <summary>
            /// Not given by the design, which has no picture for a server that
            /// did not answer. Says to try again rather than to pick another
            /// name, because the name may well be fine.
            /// </summary>
            public const string UnreachableMessage = "확인하지 못했어요. 잠시 후 다시 시도해주세요";
        }

        /// <summary>
        /// The region picker, hung under the globe button it opens from.
        /// </summary>
        /// <remarks>
        /// Its top-right corner is square for the same reason the profile
        /// panel's bottom-right is: the panel and the button that opened it
        /// read as one piece.
        /// </remarks>
        public static class Server
        {
            public static readonly Vector2 PanelSize = new Vector2(240f, 307f);
            public const float PanelRightMargin = 60f;
            public const float PanelTopMargin = 128f;

            public const float SidePadding = 24f;
            public const float VerticalPadding = 20f;

            public const float TitleHeight = 28f;

            public const float RowHeight = 40f;
            public const float RowGap = 10f;
            public const int RowRadius = 12;

            /// <summary>
            /// Not given by the design. The check mark is square-ish and the
            /// row is 40 tall, so this leaves it room without crowding.
            /// </summary>
            public const float CheckSize = 22f;

            /// <summary>
            /// The gap between the title and the first region.
            /// </summary>
            public const float TitleToRows = 12f;

            /// <summary>
            /// Where the first row's top sits, measured down from the panel's
            /// own top: past the padding, the title, and the gap after it.
            /// </summary>
            public const float RowsTop = VerticalPadding + TitleHeight + TitleToRows;
        }

        /// <summary>
        /// The friend panel: two tabs over a list that scrolls.
        /// </summary>
        /// <remarks>
        /// Sits where the profile panel does, above the chip and against the
        /// right edge, and squares the same corner for the same reason.
        /// </remarks>
        public static class Friends
        {
            public static readonly Vector2 PanelSize = new Vector2(400f, 623f);
            public const float PanelRightMargin = 60f;
            public const float PanelBottomMargin = 108f;

            public const float SidePadding = 20f;
            public const float VerticalPadding = 20f;

            public const float TabHeight = 40f;
            public const float TabDividerHeight = 28f;
            public const float TabDividerThickness = 1f;
            public const float TabRuleThickness = 1f;
            public const float BadgeDiameter = 20f;

            /// <summary>
            /// The search box is given its own width rather than stretched to
            /// the padding, which would make it 360.
            /// </summary>
            public const float SearchRowWidth = 362f;

            public const float SearchRowHeight = 40f;
            public const float SearchIconSize = 20f;
            public const float SearchIconGap = 10f;

            public const float RefreshIconSize = 24f;
            public const float RefreshGap = 10f;

            public const float SectionHeaderGap = 15f;
            public const float SectionGap = 26f;

            /// <summary>
            /// One height for every row. Two lines of the 20 point name stand
            /// about 48 tall, so a wrapped nickname fits without the row having
            /// to grow and shove the rest of the list about.
            /// </summary>
            public const float RowHeight = 60f;

            public const float RowGap = 10f;
            public const int RowRadius = 20;

            public const float AvatarDiameter = 40f;
            public const float AvatarLeft = 16f;
            public const float AvatarToName = 16f;

            public const float RowIconSize = 36f;
            public const float RowIconGap = 8f;

            /// <summary>
            /// Each row of the header, measured down from the panel's top. Every
            /// one is derived from the sizes above rather than given, so moving
            /// one of them moves what follows it.
            /// </summary>
            public const float SearchTop = VerticalPadding + TabHeight + RefreshGap;

            public const float RefreshTop = SearchTop + SearchRowHeight + RefreshGap;

            public const float BodyTop = RefreshTop + RefreshIconSize + SectionHeaderGap;

            public const float ScrollbarWidth = 3f;

            /// <summary>
            /// How wide the bar is to the pointer. The design draws a 3 pixel
            /// line, which is far too thin to catch with a mouse, so the strip
            /// that takes the drag is wider than the strip that is painted.
            /// </summary>
            public const float ScrollbarHitWidth = 14f;

            /// <summary>
            /// Half the width, so the handle's ends are round rather than cut.
            /// </summary>
            public const int ScrollbarRadius = 2;
        }

        /// <summary>
        /// The room-creation modal, centred over a dimmed Home.
        /// </summary>
        public static class CreateRoom
        {
            public static readonly Vector2 ModalSize = new Vector2(590f, 397f);
            public const float SidePadding = 32f;
            public const float VerticalPadding = 41f;

            public const float CloseIconSize = 30f;

            public const float RowHeight = 37f;
            public const float RowGap = 23f;
            public const float LabelWidth = 70f;
            public const float LabelToControl = 49f;
            public const float ControlWidth = 308f;

            /// <summary>
            /// How far the lit half sits inside the track it slides in. Not
            /// given by the design; read off the mock-up, where the orange
            /// clears the grey on all four sides rather than filling its half.
            /// </summary>
            public const float SegmentInset = 4f;

            public const float FieldPadding = 23f;
            public const float StepperPadding = 23f;

            public static readonly Vector2 CreateSize = new Vector2(426f, 52f);

            /// <summary>
            /// Where the label column starts. The three rows add up to the same
            /// width as the create button, and the design centres that whole
            /// block rather than ranging it against the padding, so the button
            /// and the controls above it share their left and right edges.
            /// </summary>
            public static float RowsLeft => (ModalSize.x - CreateSize.x) * 0.5f;
            public const float CreateTopGap = 37f;

            public const int MinTitleLength = 1;
            public const int MaxTitleLength = 20;
            public const int MinPlayers = 2;
            public const int MaxPlayers = 6;
            public const int DefaultPlayers = 6;

            public const string TitlePlaceholder = "방 이름 입력";
        }

        /// <summary>
        /// The room-invite cards stacked at the top left. Measured off the
        /// design at 1920x1080.
        /// </summary>
        public static class Toast
        {
            /// <summary>
            /// Above the home canvas's 100, so a card is never behind a panel
            /// that opened after it.
            /// </summary>
            public const int SortingOrder = 200;

            public const float Width = 474f;
            public const float Height = 85f;
            public const float Left = 60f;
            public const float Top = 63f;
            public const float Gap = 11f;

            public const float IconWidth = 50f;
            public const float IconHeight = 40f;
            public const float IconLeft = 12f;

            /// <summary>
            /// Not given by the design. Read off the screen: wider than the
            /// icon's own left margin, so the name starts clear of the envelope
            /// rather than crowding it.
            /// </summary>
            public const float IconToText = 15f;

            /// <summary>
            /// The design gives the two buttons as one span of 84 with 4
            /// between them, which is two of these. Read that way because two
            /// buttons the height of the card would leave no room for the
            /// longest name on its own line.
            /// </summary>
            public const float ButtonDiameter = 40f;

            public const float ButtonGap = 4f;

            /// <summary>
            /// Wider than the 11 the design first gave. On screen the pair sat
            /// tight against the card's edge, and the extra breath settles them
            /// against the rounded corner rather than in it.
            /// </summary>
            public const float ButtonRight = 15f;

            /// <summary>
            /// Not given by the design. Keeps a long name from touching the
            /// decline button.
            /// </summary>
            public const float TextToButtons = 12f;
        }

        public static class Radius
        {
            public const int Chip = 20;
            public const int IconButton = 20;
            public const int Panel = 20;
            public const int Toast = 20;

            /// <summary>
            /// Not given by the design. Read off the mock-up, where the field
            /// and the apply button are rounded about a quarter of their height.
            /// </summary>
            public const int Input = 12;

            public const int Check = 8;
            public const int Modal = 20;
            public const int Control = 10;
        }

        public static class FontSize
        {
            public const float Menu = 40f;
            public const float Quit = 30f;
            public const float Nickname = 24f;
            public const float ToggleLabel = 20f;

            /// <summary>
            /// Not given by the design. Matched to the check button beside it,
            /// which is the only text on that row the design does size.
            /// </summary>
            public const float NicknameInput = 20f;

            public const float Check = 20f;
            public const float Apply = 24f;
            public const float Message = 18f;
            public const float Toast = 20f;
            public const float Counter = 18f;
            public const float ServerTitle = 20f;
            public const float Region = 24f;
            public const float Tab = 20f;
            public const float Badge = 12f;
            public const float FriendSearch = 20f;
            public const float RefreshLabel = 18f;
            public const float Section = 20f;
            public const float FriendName = 20f;
            public const float ModalTitle = 30f;
            public const float RowLabel = 24f;
            public const float Segment = 18f;
            public const float RoomName = 20f;
            public const float RoomNameCounter = 11f;
            /// <summary>
            /// Larger than the design's 20: at that size the two smallest
            /// targets on the screen are also the hardest to see.
            /// </summary>
            public const float Stepper = 28f;
            public const float PlayerCount = 18f;
            public const float Create = 24f;
        }

        /// <summary>
        /// The hairline a hovered chip or button draws around itself. Matches
        /// the room browser's list rows, which the design draws the same way.
        /// </summary>
        public const float HoverStrokeThickness = 1.5f;

        /// <summary>
        /// Reads a design hex such as 0xF5F3F1 as a colour. The palette is
        /// written in sRGB the way the mock-up reports it, and Unity's UI shader
        /// expects exactly that, so no gamma conversion belongs here.
        /// </summary>
        public static Color FromHex(int rgb, float alpha = 1f)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
        }
    }
}
