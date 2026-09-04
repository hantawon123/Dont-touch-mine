using UnityEngine;

namespace Game.Client.Rooms
{
    /// <summary>
    /// Every colour and measurement the room browser screen draws with, in one
    /// place.
    /// </summary>
    /// <remarks>
    /// The screen is built in code rather than authored in the scene, so these
    /// numbers are the only record of the design. Keeping them here means a
    /// revised mock-up is one file to edit, and it lets the list row, the code
    /// panel and the header agree on a colour without copying literals between
    /// them.
    /// <para>
    /// Measurements are in the 1920x1080 the mock-up was drawn at, which is also
    /// the canvas reference resolution, so they are literal pixels there and
    /// scale from it everywhere else.
    /// </para>
    /// </remarks>
    public static class RoomBrowserStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public static class Palette
        {
            public static readonly Color TextPrimary = FromHex(0xF5F3F1);
            public static readonly Color TextMuted = FromHex(0xA8ADB3);
            public static readonly Color SearchText = Color.white;
            public static readonly Color Accent = FromHex(0xFF7032);

            /// <summary>
            /// The wire-frame calls for 80%, and this is 95%.
            /// </summary>
            /// <remarks>
            /// Figma composites in sRGB and this project renders in linear
            /// colour, where the same 80% lets far more of the background
            /// through: a bright pixel behind the panel survives the blend and
            /// then gets lifted again on the way back to sRGB. Matching the
            /// mock-up over this desk photograph takes about 95%.
            /// </remarks>
            public static readonly Color PanelFill = FromHex(0x0B1018, 0.95f);

            public static readonly Color StatusWaiting = FromHex(0x48C981);
            public static readonly Color StatusPlaying = FromHex(0xFF9A6A);

            public static readonly Color RowStroke = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color RowHoverFill = FromHex(0xF5F3F1, 0.16f);

            /// <summary>
            /// A room that cannot be entered — being played, or full — greys out
            /// whole: status, title, map, host and count alike. Only the stroke
            /// stays, so the row still reads as a row.
            /// </summary>
            public static readonly Color RowDisabledText = FromHex(0x8E8E8E);

            public static readonly Color DisabledFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color DisabledLabel = FromHex(0xA8ADB3);

            public static readonly Color RefreshFill = FromHex(0xF5F3F1);
            public static readonly Color RefreshLabel = FromHex(0x0B1018);

            public static readonly Color CodeCellFill = FromHex(0xF5F3F1);
            public static readonly Color CodeCellText = Color.black;

            public static readonly Color ScrollbarHandle = FromHex(0xF5F3F1);

            public static readonly Color ToastFill = FromHex(0xFF9A6A, 0.2f);
            public static readonly Color ToastTitle = FromHex(0xFF7032);
            public static readonly Color ToastBody = Color.white;
        }

        /// <summary>
        /// Corner radii in pixels, one per rounded shape the screen draws.
        /// </summary>
        public static class Radius
        {
            public const int CodePanel = 32;
            public const int ListPanel = 20;
            public const int Row = 32;
            public const int RefreshButton = 15;
            public const int EnterButton = 10;
            public const int CodeCell = 15;
            public const int Scrollbar = 18;
            public const int Toast = 20;
        }

        public static class Layout
        {
            public static readonly Vector2 BackButtonPosition = new Vector2(64f, -62f);
            public static readonly Vector2 BackButtonSize = new Vector2(140f, 44f);

            /// <summary>
            /// Anchored on the left edge at mid-height, which is where the
            /// mock-up sits it, so a taller window keeps it beside the desk lamp
            /// instead of drifting toward the list.
            /// </summary>
            public static readonly Vector2 CodePanelPosition = new Vector2(205f, -5f);
            public static readonly Vector2 CodePanelSize = new Vector2(395f, 240f);
            public const float CodeTitleOffsetY = 70f;
            public const int CodeCellCount = 6;
            public const float CodeCellSize = 40f;
            public const float CodeCellSpacing = 9f;
            public const float CodeCellsOffsetY = 10f;
            public static readonly Vector2 EnterButtonSize = new Vector2(270f, 47f);
            public const float EnterButtonOffsetY = -68f;

            /// <summary>
            /// The list keeps its width and hugs the right edge while stretching
            /// vertically, so a taller window shows more rooms rather than
            /// wider ones.
            /// </summary>
            public const float ListPanelWidth = 1113f;
            public const float ListPanelRightMargin = 65f;
            public const float ListPanelTopMargin = 95f;
            public const float ListPanelBottomMargin = 72f;

            public const float HeaderHeight = 73f;
            public const float HeaderLeftPadding = 45f;
            public const float SearchIconSize = 24f;
            public const float SearchIconGap = 12f;
            public static readonly Vector2 RefreshButtonSize = new Vector2(135f, 40f);
            public const float RefreshRightPadding = 30f;
            public const float RefreshIconSize = 22f;
            public const float RefreshIconGap = 8f;

            public const float ListSidePadding = 27f;
            public const float ListTopOffset = 85f;
            public const float ListBottomPadding = 27f;

            public const float RowHeight = 103f;
            public const float RowSpacing = 12f;
            public const float RowPaddingLeft = 30f;
            public const float RowPaddingRight = 37f;
            public const float StatusDotDiameter = 10f;
            public const float StatusDotGap = 6f;

            /// <summary>
            /// Roomier than the word it holds. The label never wraps and starts
            /// the title at a fixed x either way, so the box only has to be big
            /// enough not to clip.
            /// </summary>
            public const float StatusLabelWidth = 100f;

            public const float RowStatusToTitle = 46f;
            public const float RowTitleWidth = 470f;
            public const float RowTitleToMap = 130f;
            public const float RowMapWidth = 200f;

            /// <summary>
            /// The right-hand block, holding the player count over the host
            /// name. Both are right-aligned to the row's padding.
            /// </summary>
            public const float RowCountWidth = 300f;
            public const float RowLineOffsetY = 16f;

            /// <summary>
            /// Measured from the row's left edge rather than added up from the
            /// gaps, because that is how the wire-frame places them: the title
            /// and the map name start at the same x in every row.
            /// </summary>
            public const float RowTitleX = 143f;

            /// <summary>
            /// Where the wire-frame puts the map name, and far enough left of the
            /// host name for the longest of those to clear it.
            /// </summary>
            /// <remarks>
            /// Not derived from the title column any more. A title column wide
            /// enough to show twenty characters whole pushes the map name into
            /// the host name, and the host name is the one with nowhere to go:
            /// it is right-aligned against the row's edge.
            /// </remarks>
            public const float RowMapX = 653f;

            public const float RowStrokeThickness = 1.5f;

            /// <summary>
            /// The failure notice, centred under the top edge of the screen.
            /// </summary>
            public static readonly Vector2 ToastSize = new Vector2(590f, 136f);
            public const float ToastTopMargin = 48f;
            public const float ToastTitleOffsetY = 26f;
            public const float ToastBodyOffsetY = -22f;

            /// <summary>
            /// Long enough to read two lines and short enough not to sit over
            /// the list while the player tries the next room.
            /// </summary>
            public const float ToastSeconds = 3f;

            public const float ScrollbarWidth = 10f;
            public const float ScrollbarRightPadding = 10f;
        }

        public static class FontSize
        {
            public const float Back = 30f;
            public const float CodeTitle = 24f;
            public const float EnterLabel = 24f;
            public const float Search = 24f;
            public const float CodeCell = 24f;
            public const float Refresh = 18f;
            public const float ToastTitle = 30f;
            public const float ToastBody = 20f;
            public const float RoomStatus = 20f;
            public const float RoomTitle = 24f;
            public const float MapName = 20f;
            public const float HostName = 18f;
            public const float PlayerCount = 18f;
        }

        /// <summary>
        /// Reads a design hex such as 0xF5F3F1 as a colour. The palette is
        /// written in sRGB the way the mock-up reports it, and Unity's UI shader
        /// expects exactly that, so no gamma conversion belongs here.
        /// </summary>
        public static Color FromHex(int rgb, float alpha = 1f)
        {
            const float scale = 1f / 255f;
            return new Color(
                ((rgb >> 16) & 0xFF) * scale,
                ((rgb >> 8) & 0xFF) * scale,
                (rgb & 0xFF) * scale,
                alpha);
        }
    }
}
