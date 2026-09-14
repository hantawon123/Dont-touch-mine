using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// Colours and measurements for the play settings modal.
    /// </summary>
    public static class PlaySettingsStyle
    {
        public static readonly Vector2 ModalSize = new Vector2(1000f, 800f);

        public const int PanelRadius = 30;
        public const float BorderWidth = 1f;

        public const float HeaderHeight = 90f;
        public const float TitleTopPadding = 24f;
        public const float FooterHeight = 96f;
        public const float SidePadding = 40f;
        public const float RowHeight = 88f;
        public const float RowSpacing = 8f;
        public static float MapSectionHeight =>
            Layout.SectionTitleHeight
            + Layout.MapColumnSpacing
            + Layout.SelectionRowHeight
            + Layout.MapSectionBottomSpacing;
        public const float ApplyPaddingHorizontal = 80f;
        public const float ApplyPaddingVertical = 11f;
        public const int ApplyButtonRadius = 32;
        public const int LayoutVersion = 41;

        public const string RegularFontResource = "Fonts/Paperlogy-4Regular";
        public const string MediumFontResource = "Fonts/Paperlogy-5Medium";
        public const string HeaderFontResource = "Fonts/Paperlogy-9Black";
        public const string GameStartFontResource = "Fonts/Paperlogy-8ExtraBold";
        public const string CopyIconResource = "UI/ic_copy";
        public const string CopyCheckIconResource = "UI/ic_copy_check";
        public const string ArrowLeftIconResource = "UI/Icon_Left";
        public const string ArrowRightIconResource = "UI/Icon_Right";

        public static class Palette
        {
            public static readonly Color PanelFill = FromHex(0x0B1018, 0.8f);
            public static readonly Color Border = Color.white;
            public static readonly Color Text = Color.white;
            public static readonly Color MapPreview = FromHex(0x8E8E8E, 0.8f);
            public static readonly Color Underline = FromHex(0xF5F3F1, 0.5f);
            public static readonly Color ApplyFill = Color.white;
            public static readonly Color ApplyOffFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color ApplyOnLabel = Color.black;
            public static readonly Color ApplyOffLabel = FromHex(0xA8ADB3);
            public static readonly Color ApplyWarning = FromHex(0xFF3B30);
            public static readonly Color ResetFill = Color.black;
            public static readonly Color ResetHoverFill = Color.white;
            public static readonly Color ResetLabel = Color.white;
            public static readonly Color ResetHoverLabel = Color.black;
            public static readonly Color ResetStroke = Color.white;
            public static readonly Color ResetHoverStroke = Color.black;
            public static readonly Color ResetOffLabel = ApplyOffLabel;
            public static readonly Color ResetOffStroke = ApplyOffLabel;
            public static readonly Color TextHover = FromHex(0xFF9A6A);
            public static readonly Color Divider = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color DefaultMark = FromHex(0xF5F3F1, 0.85f);
            public static readonly Color ScrollbarHandle = FromHex(0xF5F3F1, 0.5f);
        }

        public static class FontSize
        {
            public const int Header = 36;
            public const int SectionTitle = 36;
            public const int Body = 28;
            public const int Counter = 24;
            public const int MapName = 18;
            public const int Apply = 32;
            public const int ApplyWarning = 20;
            public const int GameStart = 32;
        }

        public static class Overlay
        {
            public const int SortingOrder = 90;
            public static readonly Color Scrim = new Color(0f, 0f, 0f, 200f / 255f);
            public static readonly Vector2 GameStartPosition = new Vector2(-120f, 40f);
            public static readonly Vector2 GameStartSize = new Vector2(275f, 60f);
            public const int GameStartRadius = 30;
            public const float GameStartHoverScale = 1.06f;
            public const float GameStartHoverSeconds = 0.08f;
            public static readonly Vector2 CloseOffset = new Vector2(20f, 20f);
            public const float CloseSize = 24f;
        }

        public static class Layout
        {
            public static readonly Vector2 MapPreviewSize = new Vector2(200f, 150f);
            public const float MapSlotSize = 90f;
            public const float MapSlotSpacing = 12f;
            public const float ArrowSize = 24f;
            public const float CopyIconSize = 32f;
            public const float CopyFeedbackDuration = 5f;
            public const float CopiedFeedbackWidth = 220f;
            public const float CopiedFeedbackShift = 40f;
            public const float LabelAreaRatio = 0.42f;
            public const float TitleCounterWidth = 80f;
            public const float TitleCounterHeight = 28f;
            public const float TitleCounterRightInset = 4f;
            public const float TitleCounterGap = 8f;
            public static float TitleInputRightPadding =>
                TitleCounterWidth + TitleCounterGap + TitleCounterRightInset;
            public const float RoomCodeValueWidth = 160f;
            public const float RoomCodeControlSpacing = 8f;
            public const float ControlValueWidth = 132f;
            public const float DurationValueWidth = 88f;
            public const float DefaultMarkWidth = 2f;
            public const float DefaultMarkHeight = 16f;
            public const int SliderTrackRadius = 4;
            public const float ControlSpacing = 24f;
            public const float PickerSpacing = 16f;
            public const float MapColumnSpacing = 8f;
            public const float MapNameSpacing = 8f;
            public const float MapNameHeight = 52f;
            public const float MapSectionBottomSpacing = 20f;
            public const float SectionTitleHeight = 36f;
            public const float CategoryValueMinWidth = 200f;
            public const float ActionSpacing = 24f;
            public const string ResetLabel = "초기화";
            public const float ScrollbarWidth = 8f;
            public const float ScrollbarRightInset = 16f;
            public const float ScrollbarVerticalInset = 12f;
            public const int ScrollbarRadius = 4;

            public static float SelectionRowHeight =>
                MapPreviewSize.y + MapNameSpacing + MapNameHeight;
        }

        public static class MapSlotPalette
        {
            public static readonly Color Normal = new Color(0.45f, 0.45f, 0.5f, 1f);
            public static readonly Color Selected = new Color(0.92f, 0.92f, 0.95f, 1f);
            public static readonly Color SelectionOutline = new Color(0.95f, 0.95f, 1f, 1f);
        }

        private static Color FromHex(uint rgb, float alpha = 1f) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
    }
}
