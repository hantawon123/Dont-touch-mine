using Game.Client.Settings;
using Game.Core.Players;
using Game.Core.Settings;
using UnityEngine;

namespace Game.Client.Character
{
    /// <summary>
    /// Every colour and measurement the closet draws with, taken from the
    /// mock-up.
    /// </summary>
    /// <remarks>
    /// The same arrangement Home uses: the screen is built in code, so this is
    /// the only record of the design, and a revised mock-up is one file to
    /// edit.
    /// <para>
    /// Measurements are pixels at the 1920x1080 the mock-up was drawn at, which
    /// is also the canvas reference resolution, so they are literal pixels
    /// there and scale from it everywhere else.
    /// </para>
    /// </remarks>
    public static class CharacterClosetStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// Over a failure to store what was applied. The same heading Home and
        /// the room browser use: from the player's side these are one event,
        /// which is that the game could not reach the server.
        /// </summary>
        public static string SaveErrorTitle =>
            UiTextCatalog.Shipped.Get(UiText.Home.ConnectionError, "ko");

        public static string SaveErrorMessage =>
            UiTextCatalog.Shipped.Get(UiText.Closet.SaveError, "ko");

        public static class Palette
        {
            public static readonly Color TextPrimary = FromHex(0xFFFDFC);
            public static readonly Color TextMuted = FromHex(0xA8ADB3);
            public static readonly Color Accent = FromHex(0xFF7032);

            /// <summary>Shown only while the background art is missing.</summary>
            public static readonly Color BackgroundFallback = FromHex(0x0B1018);

            public static readonly Color TabFill = FromHex(0x0B1018, 0.8f);
            public static readonly Color TabHoverFill = FromHex(0xF5F3F1, 0.16f);
            public static readonly Color TabSelectedStroke = FromHex(0xFF7032);

            /// <summary>
            /// The selected tab says so twice: an orange ring and orange
            /// lettering. One of the two alone reads as a hover.
            /// </summary>
            public static readonly Color TabSelectedLabel = FromHex(0xFF7032);

            public static readonly Color LockerFill = FromHex(0x0B1018, 0.8f);
            public static readonly Color CellFill = FromHex(0x343845, 0.7f);
            public static readonly Color CellHoverFill = FromHex(0x343845);
            public static readonly Color CellSelectedStroke = FromHex(0xFF7032);
            public static readonly Color ScrollbarHandle = FromHex(0xF5F3F1, 0.8f);

            /// <summary>
            /// The two buttons under the character. What turns them on is the
            /// story after this one; the off colours are what the screen draws
            /// today.
            /// </summary>
            public static readonly Color ResetFill = FromHex(0xF5F3F1);
            public static readonly Color ResetLabel = FromHex(0x231818);
            public static readonly Color ApplyOnFill = FromHex(0xFF7032);
            public static readonly Color ApplyOnLabel = FromHex(0xF5F3F1);
            public static readonly Color ApplyOffFill = FromHex(0x0B1018, 0.5f);
            public static readonly Color ApplyOffLabel = FromHex(0xA8ADB3);

            public static readonly Color BackLabel = FromHex(0xFFFDFC);

            public static readonly Color Dim = FromHex(0x0B1018, 0.1f);
            public static readonly Color ModalFill = FromHex(0x0B1018, 0.8f);
            public static readonly Color ModalTitle = FromHex(0xF5F3F1);
            public static readonly Color ModalSubtitle = FromHex(0xA8ADB3);
            public static readonly Color DeclineFill = FromHex(0xF5F3F1, 0.16f);

            /// <summary>
            /// Not given by the design. The same fill lifted, which is how the
            /// tabs and the cells answer a pointer.
            /// </summary>
            public static readonly Color DeclineHoverFill = FromHex(0xF5F3F1, 0.26f);

            public static readonly Color DeclineLabel = FromHex(0xF5F3F1);
            public static readonly Color AcceptFill = FromHex(0xFF7032);

            /// <summary>Not given by the design: the accent, lightened.</summary>
            public static readonly Color AcceptHoverFill = FromHex(0xFF8A52);

            public static readonly Color AcceptLabel = FromHex(0xF5F3F1);
            public static readonly Color CloseIcon = FromHex(0xF5F3F1);
        }

        public static class Radius
        {
            public const int Tab = 32;
            public const int Locker = 32;
            public const int Cell = 20;
            public const int Button = 32;
        }

        public static class Back
        {
            public static readonly Vector2 Position = new Vector2(56f, -48f);
            public static readonly Vector2 Size = new Vector2(200f, 48f);
            public const float FontSize = 30f;
            public static string Label =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Back, "ko");
        }

        public static class Tabs
        {
            public static readonly Vector2 Size = new Vector2(324f, 128f);

            /// <summary>Top left corner of the first tab.</summary>
            public static readonly Vector2 Origin = new Vector2(60f, -281f);

            public const float Gap = 15f;
            public const float FontSize = 30f;

            /// <summary>Between the icon and the word beside it.</summary>
            public const float LabelGap = 60f;

            public const float SelectedStroke = 4f;

            /// <summary>
            /// Each icon at the size the mock-up gives it rather than all four
            /// in one box: they are different shapes, and a shoe fitted to the
            /// square a face wants comes out either tiny or stretched.
            /// </summary>
            public static Vector2 IconSize(AvatarPartCategory category)
            {
                switch (category)
                {
                    case AvatarPartCategory.Hood:
                        return new Vector2(63f, 60f);
                    case AvatarPartCategory.Shoes:
                        return new Vector2(69f, 41f);
                    case AvatarPartCategory.Face:
                        return new Vector2(62f, 62f);
                    default:
                        return new Vector2(62f, 56f);
                }
            }

            public static float IconLeft(AvatarPartCategory category) =>
                category == AvatarPartCategory.Shoes ? 49f : 52f;
        }

        public static class Locker
        {
            public static readonly Vector2 Size = new Vector2(445f, 575f);

            /// <summary>Inset from the right edge and down from the top.</summary>
            public static readonly Vector2 Margin = new Vector2(60f, 272f);

            public const float PaddingVertical = 66f;
            public const float PaddingHorizontal = 43f;
            public const int Columns = 3;
            public const float CellSize = 100f;
            public const float CellGap = 30f;
            public const float ThumbnailInset = 5f;

            /// <summary>
            /// The inset for a cell drawn as the category's own icon rather
            /// than as a picture of the part. Wider, because the icon is a
            /// silhouette with no margin of its own and fills the cell edge to
            /// edge without one.
            /// </summary>
            public const float IconInset = 18f;
            public const float SelectedStroke = 4f;

            /// <summary>
            /// The handle down the inside of the right edge, flush with the
            /// padding rather than inset from it.
            /// </summary>
            public const float ScrollbarWidth = 8f;
            public const float ScrollbarInset = 0f;
        }

        public static class HoodTabs
        {
            public const float Top = 28f;
            public const float Height = 64f;
            public const float Inset = 5f;
            public const float GridGap = 24f;
            public const float Bottom = 36f;
            public const float FontSize = 24f;
            public const int Radius = 18;
            public const float Stroke = 2f;
        }

        public static class Buttons
        {
            public static readonly Vector2 Size = new Vector2(281f, 80f);
            public const float Gap = 45f;
            public const float BottomMargin = 60f;
            public const float FontSize = 40f;

            /// <summary>The circling arrow left of the reset label.</summary>
            public const float IconSize = 30f;

            /// <summary>
            /// From the word's left edge to the arrow's right edge. The word
            /// stays in the middle of the plate, so widening this moves only
            /// the arrow, further left.
            /// </summary>
            public const float IconGap = 14f;

            /// <summary>
            /// The word and the arrow together, nudged right. Zero leaves the
            /// word in the middle of the plate, which puts the pair as a whole
            /// left of centre; raising this walks both across together.
            /// </summary>
            public const float IconRowShift = 16f;

            public static string ResetLabel =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Reset, "ko");
            public static string ApplyLabel =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Apply, "ko");
        }

        /// <summary>
        /// The two confirmations, which differ only in their heading.
        /// </summary>
        public static class Modal
        {
            public static readonly Vector2 PanelSize = new Vector2(590f, 306f);
            public const int PanelRadius = 20;

            public const float TitleTop = 87f;
            public const float TitleFontSize = 30f;
            public const float SubtitleGap = 15f;
            public const float SubtitleFontSize = 20f;
            public const float ButtonGapAbove = 41f;
            public const float BottomPadding = 41f;

            public static readonly Vector2 ButtonSize = new Vector2(223f, 52f);
            public const float ButtonGap = 44f;
            public const int ButtonRadius = 10;
            public const float ButtonFontSize = 24f;

            public static readonly Vector2 CloseOffset = new Vector2(20f, 20f);
            public const float CloseSize = 24f;

            /// <summary>
            /// How much the picture behind is shrunk before it is softened.
            /// One step: enough to make the mip chain cheap, not enough to
            /// show as blocks.
            /// </summary>
            public const int BackdropHalvings = 1;

            /// <summary>
            /// How far the softening is pushed. A continuous number, so this is
            /// the one to turn if the blur is too strong or too weak: 1 is
            /// barely there, 2 is soft, 3 starts to lose the room.
            /// </summary>
            public const float BackdropBlur = 1.5f;

            public static string ResetTitle =>
                UiTextCatalog.Shipped.Get(UiText.Closet.ResetTitle, "ko");
            public static string DiscardTitle =>
                UiTextCatalog.Shipped.Get(UiText.Closet.DiscardTitle, "ko");
            public static string Subtitle =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Subtitle, "ko");
            public static string DeclineLabel =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Decline, "ko");
            public static string AcceptLabel =>
                UiTextCatalog.Shipped.Get(UiText.Closet.Accept, "ko");
        }

        /// <summary>
        /// Lobby overlay: the Home closet chrome inside the settings panel.
        /// Insets are tighter than the full-screen page so the same rail,
        /// locker and buttons fit the 1600×876 frame.
        /// </summary>
        public static class Overlay
        {
            public static readonly Vector2 FramePosition = SettingsStyle.Frame.Position;

            public static readonly Vector2 FrameSize = SettingsStyle.Frame.Size;

            public const int FrameRadius = SettingsStyle.Frame.Radius;

            public const float BorderWidth = 1f;

            public static readonly Color PanelFill = FromHex(0x0B1018, 0.8f);

            public static readonly Color Border = Color.white;

            /// <summary>Left/right inset from the panel edge. Full screen uses 60.</summary>
            public const float InsetX = 48f;

            /// <summary>
            /// Top inset. Full screen starts the rail at 281 because the
            /// character owns the middle of a 1080 page; the panel is 876.
            /// </summary>
            public const float InsetY = 72f;

            public static readonly Vector2 TabsOrigin = new Vector2(InsetX, -InsetY);

            public static readonly Vector2 LockerMargin = new Vector2(InsetX, InsetY);

            /// <summary>Full screen uses 60; the panel is shorter.</summary>
            public const float ButtonsBottom = 40f;

            /// <summary>Keeps the pair flush with the locker right inset.</summary>
            public const float ButtonsRightInset = InsetX;

            /// <summary>초기화 and 적용 as one block.</summary>
            public static Vector2 ActionBarSize =>
                new Vector2(
                    (Buttons.Size.x * 2f) + Buttons.Gap,
                    Buttons.Size.y);

            /// <summary>Same dim the other lobby overlays sit on.</summary>
            public static readonly Color Scrim = new Color(0f, 0f, 0f, 200f / 255f);
        }

        private static Color FromHex(uint rgb, float alpha = 1f) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                alpha);
    }
}
